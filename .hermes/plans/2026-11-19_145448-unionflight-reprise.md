# UnionFlightReader — audit d'architecture et plan de reprise

> **Pour Hermes :** utiliser `subagent-driven-development` pour exécuter ce plan tâche par tâche.
> Plan généré le 2026-11-19. Aucun fichier de code n'a été modifié.
> **Décisions du 2026-11-19 :** toutes les versions (X-Plane 11/12, MSFS 2020/2024, P3D v4/v5/v6), licence MIT, lecture seule en v1.

**Goal :** transformer le POC X-Plane en une bibliothèque .NET fiable, testée et packagée, exposant une API unique pour X-Plane 11/12, MSFS 2020/2024 et P3D v4/v5/v6.

**Architecture :** un contrat commun (`FlightSnapshot` + `IFlightSimProvider`), un orchestrateur (`FlightSimMonitor`) qui détecte le simulateur actif et gère le failover, un provider X-Plane, et un provider SimConnect qui lance un **process hôte par version** (MSFS 2020/2024, P3D v4/v5/v6). Le transport reste interne à chaque provider.

**Tech Stack :** .NET 10 (LTS), C# 14, xUnit, `Microsoft.Extensions.Logging.Abstractions`, `System.Text.Json`, `ClientWebSocket`, `UdpClient`, SimConnect (MSFS/P3D, Windows uniquement), GitHub Actions, NuGet.

---

## 0. Statut d'exécution (2026-11-19)

| Phase | État | Détail |
|---|---|---|
| 0 — Socle | ✅ Fait | SDK 10.0.404, solution `src/tests/samples`, MIT, `Directory.Build.props`, `global.json`, `.editorconfig` ; anciens projets POC supprimés. |
| 1 — Contrat/Core | ✅ Fait | `FlightSnapshot`, `IFlightSimProvider`, `FlightSimProviderBase`, `FlightSimMonitor`, `UnionFlightClient` ; 9 tests. |
| 2 — X-Plane Web API | ✅ Fait (hors test réel) | REST + WebSocket, IDs `long`, strings statiques via REST ; 16 tests X-Plane. |
| 3 — X-Plane UDP | ✅ Fait (hors test réel) | RREF batché, annulation propre, `TryGetValue`, reconnexion ; tests golden. |
| 4 — Hosts MSFS | ⏳ À faire | Nécessite Windows + SDK MSFS 2020/2024. |
| 5 — Hosts P3D | ⏳ À faire | Nécessite Windows + SDK P3D v4/v5/v6. |
| 6 — Packaging/CI | 🟡 Partiel | README, CHANGELOG, CI Linux ; métadonnées NuGet et pack à faire. |
| 7 — Durcissement | ⏳ À faire | `LoggerMessage`, docs XML, fuzzing, profiling. |

**Vérifié le 2026-11-19 :** `dotnet build UnionFlight.sln` -> 0 warning / 0 error ; `dotnet test UnionFlight.sln` -> 25/25 ; sample sans X-Plane -> « No simulator detected » sans exception.

---

## 1. Verdict

### 1.1 Ce qui est bon

- **Le découpage par simulateur est la bonne intuition.** Un projet par sim + une couche d'interfaces, c'est exactement la forme que doit prendre ce produit.
- **Le POC X-Plane fonctionne sur le fond.** Le protocole RREF est correct : header `RREF`, id, float, padding à 413 octets. Les datarefs choisis (position, vitesses, `sim/time/paused`) sont les bons.
- **C#/.NET est un bon choix.** Le wrapper managé SimConnect existe, X-Plane parle UDP/WebSocket, et NuGet est le bon canal de distribution.
- **Le README-MVP est une bonne base de spec.** La checklist par simulateur donne déjà les critères d'acceptation.
- **Le repo est petit et propre.** ~800 lignes de C#, 13 commits, pas de dette de dépendances. La reprise est peu coûteuse.

### 1.2 Ce qui ne tient pas (par ordre d'impact)

1. **Ce n'est pas encore une bibliothèque, c'est un programme global.**
   - Singletons statiques partout (`XPReader.Instance`, `Aircraft.Instance`, `Flight.Instance`, `Simulator.Instance`, `UnionFlight.Instance`).
   - État mutable global (`DataRefLinks.UsedDataRefLinks`, `XPReader.dataRefLinksDictionary`).
   - Impossible d'instancier deux readers, de tester sans simulateur, ou de reconnecter proprement.
   - `dynamic` pour toutes les valeurs : pas de typage, pas d'IntelliSense, coût runtime, erreurs découvertes à l'exécution.

2. **Le contrat de données est ambigu.**
   - Aucune unité documentée : `Altitude` est en pieds, `HeightAgl` en pieds, `Groundspeed` en nœuds, `VerticalSpeed` en ft/min, `Heading` magnétique. Rien de tout ça n'est dans l'interface.
   - Mélange `float?` et `double?` sans raison.
   - `TailNumber` est `string` non nullable alors que la valeur peut être absente.
   - `IsSimPaused` et `IsFlightLaunched` sont `bool?` mais la sémantique « inconnu » n'est pas définie.
   - `ISim` est dans le namespace `FlightDataClass`, les autres interfaces dans `UnionFlight.FlightData` : incohérence.

3. **Le reader X-Plane a des bugs de robustesse.**
   - `cancelationTokenSource.Cancel()` n'est jamais appelé ; `stop()` dispose le CTS et ferme les sockets, mais les deux `Task.Factory.StartNew(async ...)` continuent et fautent en silence (`Task<Task>` non observé).
   - `dataRefLinksDictionary[id]` est **hors** du `try` : un id inconnu (réponse tardive d'une session précédente) tue la boucle de réception.
   - `catch (Exception) { }` avale tout, y compris les erreurs de parsing.
   - Deux `UdpClient` bindés sur le même port local (`client` puis `server = new UdpClient((IPEndPoint)client.Client.LocalEndPoint)`) : dépend du système, fragile.
   - `requestDataRefs()` envoie **un datagramme de 413 octets par dataref**, et un par caractère pour les chaînes : ~305 datagrammes toutes les 5 s, dont 290 pour `TailNumber` (40 chars) + `Name` (250 chars) à 5 Hz. C'est 1 500+ paquets/s pour deux chaînes statiques.
   - `StringDataReader.UpdateValue` reconstruit la chaîne complète à chaque caractère (`PadRight`, `Remove`, `Insert`) : O(n²) et allocations.
   - `IsSimLaunched()` via `GetActiveUdpListeners()` dépend de la configuration réseau de X-Plane ; si l'utilisateur a désactivé l'UDP, le sim tourne mais est déclaré absent.

4. **La sémantique « flight launched » est fausse.**
   - `IsFlightLaunched = sim_speed_actual != 0`. Or `sim_speed_actual` vaut 0 **quand le sim est en pause**, et peut être < 1 quand le FPS limite la simulation. Un vol en pause est donc signalé comme « pas lancé ».
   - Il faut séparer : `IsPaused` (`sim/time/paused`), `IsFlightLoaded` (données avion reçues / `total_flight_time_sec`), et `IsConnected`.

5. **MSFS et P3D sont des coquilles vides.**
   - `Class1.cs` et trois classes vides. Aucune stratégie pour la vraie difficulté : **les versions de `SimConnect.dll`**.
   - Le wrapper managé MSFS 2020 et 2024 ne sont pas interchangeables sur les structures qui ont changé (idents d'aéroport). Le wrapper P3D v6 ne se connecte ni à FSX ni aux P3D précédents.
   - Un seul binaire ne peut pas couvrir MSFS 2020 + 2024 + P3D v4/v5/v6 sans chargement dynamique de DLL ou providers séparés.

6. **Zéro test, zéro CI, zéro métadonnée NuGet.**
   - Aucun projet de test, aucun workflow, aucun `Directory.Build.props`, aucun analyzer, `Nullable` activé seulement dans certains projets.
   - Le README promet un package NuGet, mais rien n'est prêt pour `dotnet pack`.

7. **.NET 8 est EOL depuis le 10 novembre 2026.**
   - .NET 8 et .NET 9 ne reçoivent plus de correctifs de sécurité depuis 9 jours. La cible doit être **.NET 10 LTS** (support jusqu'au 14 novembre 2028).
   - .NET 11 est sorti le 10 novembre 2026 mais c'est une STS ; pour une bibliothèque, .NET 10 est le bon choix.

8. **La licence GPL-3.0 est un frein à l'adoption — décision : MIT.**
   - Une bibliothèque GPL-3.0 contamine les applications qui la référencent. Pour un package NuGet destiné à des add-ons flight sim (souvent propriétaires ou MIT), c'est un blocage.
   - **Décision : passer en MIT.** Remplacer le fichier `LICENSE`, mettre `PackageLicenseExpression=MIT`, vérifier qu'aucun fichier ne porte d'en-tête GPL incompatible.

### 1.3 Les décisions structurantes

| # | Décision | Recommandation |
|---|---|---|
| 1 | Périmètre de versions | **Toutes** : X-Plane 11/12, MSFS 2020/2024, P3D v4/v5/v6. P3D v1-v3 et FSX (32-bit) hors périmètre sauf demande explicite. |
| 2 | « Un seul module » | Une seule **API** et un package méta, mais **providers séparés** et **un process hôte par version SimConnect**. On ne peut pas avoir un seul binaire à cause des DLL version-locked et du Windows-only. |
| 3 | Transport X-Plane | Web API (REST + WebSocket) en priorité pour XP 12.1.1+, UDP RREF en fallback XP11 et LAN. |
| 4 | Licence | **MIT** (décidé). Remplacer `LICENSE`, `PackageLicenseExpression`, en-têtes. |
| 5 | Périmètre fonctionnel | **Lecture seule en v1.** Pas d'écriture de datarefs/SimVars, pas de commandes, pas d'events sortants. |
| 6 | Cible .NET | `net10.0` pour le core, X-Plane et le client SimConnect ; `net10.0-windows` pour les hosts SimConnect (fallback `net48` si le wrapper ne se charge pas). |

---

## 2. État des lieux

### 2.1 Le repo

- **URL :** https://github.com/Asanio06/UnionFlightReader
- **Créé :** 2023-04-10
- **Dernier commit :** 2024-08-24 (`feat(XplaneReader): know when the simulator is paused`)
- **Commits :** 13
- **Stars/forks :** 0 / 0
- **Description GitHub :** vide
- **Licence :** GPL-3.0 (décision : passer en MIT)
- **Taille :** ~1 330 lignes tous fichiers confondus, dont ~800 lignes de C#.

### 2.2 Structure actuelle

```
UnionFlightReader/
├── ConsoleExemple/                 # sample console (85 lignes)
├── FlightDataClass/                # interfaces communes (77 lignes)
│   ├── IAircraft.cs
│   ├── IFlight.cs
│   ├── IFlightReader.cs
│   └── ISim.cs
├── UnionFlight/                    # façade + singleton (109 lignes)
│   ├── IUnionFlight.cs
│   └── UnionFlight.cs
├── UnionFlightXplaneReader/        # implémentation X-Plane (612 lignes)
│   ├── XPReader.cs
│   ├── DataReader/
│   ├── DataRefLink/
│   ├── DataUtils/
│   └── FlightData/
├── UnionFlightMsfsReader/          # coquille vide (Class1.cs)
├── UnionFlightP3dReader/           # coquille vide (Class1.cs + 3 classes vides)
└── UnionFlight.sln
```

### 2.3 Ce qui fonctionne réellement

- X-Plane 11/12 en UDP local : nom, immatriculation, IAS, GS, TAS, heading, VS, position, pause, « flight launched ».
- Détection du sim par écoute UDP sur le port 49000.
- Sample console qui affiche le simulateur et l'avion.

### 2.4 Ce qui est vide ou cassé

- MSFS : rien.
- P3D : rien.
- Tests : rien.
- CI/CD : rien.
- Package NuGet : rien.
- Reconnexion après fermeture du sim : cassée (le CTS est disposé, pas annulé).
- Reprise après `stop()` : impossible sans recréer le singleton.
- Gestion des erreurs : silencieuse.

### 2.5 Build

`dotnet` n'est pas installé sur le VPS. Le code n'a donc pas été compilé ici. La Phase 0 installe le SDK .NET 10 sans sudo (`~/.dotnet`).

---

## 3. Recherche : les choix sont-ils les bons ?

### 3.1 X-Plane

#### UDP RREF vs Web API

| Critère | UDP RREF | Web API (REST + WebSocket) |
|---|---|---|
| Disponibilité | X-Plane 9/10/11/12 | X-Plane 12.1.1+ (v1), 12.1.4 (v2), 12.4.0 (v3) |
| Format | binaire, paquets de 413 octets | JSON |
| Fréquence | choisie par dataref, valeurs renvoyées même inchangées | 10 Hz, **uniquement les valeurs changées** |
| Chaînes | uniquement en tableaux de chars (`dataref[i]`) | type `data`, base64 |
| Réseau | LAN possible | **localhost uniquement** (pas de LAN pour l'instant) |
| Sécurité | aucune | 403 si « Disable Incoming Traffic », `--no_web_server` coupe tout |
| Coût CPU/réseau | élevé si beaucoup de datarefs | faible (delta only) |
| Debug | Wireshark | curl / JSON lisible |

**Verdict :** la Web API est objectivement meilleure pour un usage local sur XP12. UDP reste nécessaire pour XP11 et pour les setups réseau (sim sur une autre machine). Le code actuel a donc une valeur de fallback, mais il doit être refactoré.

Source : [X-Plane local Web API](https://developer.x-plane.com/article/x-plane-web-api/), mis à jour le 2026-10-20.

#### Détails de la Web API

- REST : `http://localhost:8086/api/v3/...`
- WebSocket : `ws://localhost:8086/api/v3`
- Port configurable via `--web_server_port=`.
- `GET /api/capabilities` (non versionné) renvoie les versions d'API supportées et la version de X-Plane :
  ```json
  {"api":{"versions":["v1","v2","v3"]},"x-plane":{"version":"12.4.0"}}
  ```
- Résolution d'un dataref : `GET /api/v3/datarefs?filter[name]=sim/flightmodel/position/latitude`
  → `{"data":[{"id":9952311,"name":"...","value_type":"float"}]}`
- Lecture d'une valeur : `GET /api/v3/datarefs/{id}/value` → `{"data": ...}` (attention au pluriel `datarefs` et au suffixe `/value` ; le singulier `dataref` renvoie 404).
- Abonnement WebSocket :
  ```json
  {"req_id":1,"type":"dataref_subscribe_values","params":{"datarefs":[{"id":9952311}]}}
  ```
- Mise à jour :
  ```json
  {"type":"dataref_update_values","data":{"9952311":48.123}}
  ```
- `value_type` : `float`, `double`, `int`, `int_array`, `float_array`, `data` (base64).
- Les IDs de datarefs sont **stables pendant une session**, mais pas entre deux lancements. Il faut les re-résoudre à chaque connexion.
- Si un nom de dataref n'existe pas, le filtre renvoie 404 `invalid_dataref_name` : résoudre les noms un par un (ou gérer le 404 global) et marquer le champ comme non supporté.

#### Pièges découverts

1. **Les datarefs statiques ne sont pas toujours poussés.** `sim/aircraft/view/acf_ui_name` n'est envoyé qu'une fois, au chargement du vol. Si l'outil se connecte après le chargement, il ne reçoit rien. Il faut lire les chaînes via REST (`GET /api/v3/datarefs/{id}/value`) à la connexion, puis s'abonner aux valeurs dynamiques.
   - Source : [forum X-Plane, déc. 2025](https://forums.x-plane.org/forums/topic/340871-dataref-simaircraftviewacf_ui_name-mostly-unavailable/)
2. **`acf_ui_name` est X-Plane 12 uniquement** (`byte[250]`). Sur XP11, utiliser `sim/aircraft/view/acf_descrip` (`byte[260]`) comme fallback.
3. **`acf_tailnum` est `byte[40]`**, pas une chaîne native.
4. **La Web API est localhost-only.** Pour un sim distant, il faut UDP.
5. **« Disable Incoming Traffic »** renvoie 403 sur toutes les méthodes. Le diagnostic doit être explicite.

#### Datarefs recommandés

| Champ snapshot | Dataref | Unité native | Conversion |
|---|---|---|---|
| `LatitudeDeg` | `sim/flightmodel/position/latitude` | degrés | — |
| `LongitudeDeg` | `sim/flightmodel/position/longitude` | degrés | — |
| `AltitudeFt` | `sim/flightmodel/position/elevation` | mètres | × 3.280839895 |
| `HeightAglFt` | `sim/flightmodel/position/y_agl` | mètres | × 3.280839895 |
| `IndicatedAirspeedKt` | `sim/flightmodel/position/indicated_airspeed` | nœuds | — |
| `TrueAirspeedKt` | `sim/flightmodel/position/true_airspeed` | m/s | × 1.943844 |
| `GroundspeedKt` | `sim/flightmodel/position/groundspeed` | m/s | × 1.943844 |
| `VerticalSpeedFpm` | `sim/flightmodel/position/vh_ind_fpm` | ft/min | — |
| `HeadingMagDeg` | `sim/flightmodel/position/mag_psi` | degrés | — |
| `AircraftTitle` | `sim/aircraft/view/acf_ui_name` (XP12) / `acf_descrip` (XP11) | byte[250] / byte[260] | décoder, trim |
| `AircraftTailNumber` | `sim/aircraft/view/acf_tailnum` | byte[40] | décoder, trim |
| `IsPaused` | `sim/time/paused` | int 0/1 | — |
| `IsFlightLoaded` | données avion reçues + `sim/time/total_flight_time_sec` | secondes | > 0 ou reset |
| `SimulatorVersion` | `/api/capabilities` | string | — |

**Ne pas utiliser `sim/time/sim_speed_actual` pour « flight launched ».** Il vaut 0 en pause et reflète le FPS. Utiliser `sim/time/paused` pour la pause, et la réception de données avion pour « flight loaded ».

#### Faut-il dépendre de XPlaneConnector ?

- `XPlaneConnector` 1.3.0 date de **2021**, cible `netstandard2.0`, et ne fait que de l'UDP.
- Licence à vérifier dans le package (`license.txt`), mais le projet est stale.
- **Recommandation :** ne pas en dépendre. Garder le code UDP maison (il est petit et déjà écrit), ou réutiliser son design comme référence. La Web API n'est pas couverte par ce package.

### 3.2 MSFS 2020 / 2024

#### Le bon transport : SimConnect

- SimConnect est la seule API externe supportée pour MSFS 2020 et 2024.
- Le SDK fournit un wrapper managé `Microsoft.FlightSimulator.SimConnect.dll` (`$(MSFS SDK)\SimConnect SDK\lib\managed`).
- Les docs officielles parlent de .NET Framework 4.7, mais des applications .NET 8 l'utilisent en production (ex. thread DevSupport de déc. 2024). Le wrapper est une assembly .NET Framework qui P/Invoke `SimConnect.dll` ; elle se charge dans .NET 8/10.
- **SimConnect n'est pas thread-safe.** Un seul thread doit posséder l'objet et appeler `ReceiveMessage()`. Pour une console, on peut passer `IntPtr.Zero` et pomper `ReceiveMessage()` dans une boucle dédiée (le sample P3D `ManagedChangeVehicle` le fait).
- Les callbacks arrivent via `OnRecvOpen`, `OnRecvQuit`, `OnRecvException`, `OnRecvSimobjectData`, `OnRecvEvent`.

Sources : [SimConnect SDK MSFS 2024](https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/simconnect-sdk/), [Managed code](https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/programming-simconnect-clients-using-managed-code/).

#### Le point dur : 2020 vs 2024

- Il n'y a **pas de breaking change majeur** pour les add-ons legacy : un module compilé avec le SDK 2020 est détecté comme module 2020 et fonctionne dans 2024, sans les nouvelles fonctionnalités.
- Mais les structures qui ont changé (`SIMCONNECT_DATA_FACILITY_AIRPORT`, idents à 8 caractères) produisent des données incorrectes si on mélange les DLL. Un client compilé 2024 ne donne pas les bons résultats sur 2020, et inversement.
- La solution utilisée par les outils réels : détecter le processus (`FlightSimulator.exe` vs `FlightSimulator2024.exe`), puis charger la bonne paire `SimConnect.dll` + `Microsoft.FlightSimulator.SimConnect.dll` **avant le premier appel SimConnect** (les DLL sont lazy-loadées), ou compiler deux assemblies providers et charger la bonne via `AssemblyLoadContext`.
- `bm98/FSimAdapter20_24` fait exactement ça : deux dossiers de déploiement V2020/V2024, chargement dynamique, interface 2024 exposée. Licence GPL-3.0, .NET Framework 4.8, beta. Utile comme référence, pas comme dépendance.

Source : [DevSupport MSFS, déc. 2024](https://devsupport.flightsimulator.com/t/2020-2024-compatible-microsoft-flightsimulator-simconnect-dll/11844).

**Recommandation (décision : toutes versions) :** deux hosts SimConnect, `Msfs2020` et `Msfs2024`, chacun compilé contre son SDK et sa paire de DLL. Le client détecte le processus et lance le bon host. Ne jamais charger les deux paires dans le même process : la DLL native porte le même nom et la résolution est imprévisible.

#### Pause et état de vol

Ne pas deviner avec des simvars. Utiliser les **system events** :

- `Sim` : 1 = simulation en cours, 0 = menu/chargement.
- `Pause_EX1` : flags de pause (`0` off, `1` full pause, `4` active pause, `8` sim pause).
- `FlightLoaded` / `AircraftLoaded` : nom du fichier de vol / de l'avion. Attention : `FlightLoaded` est émis avant que SimConnect ne soit connecté ; utiliser `RequestSystemState("FlightLoaded")` à la connexion pour l'état initial.
- `OnRecvQuit` : le sim se ferme → déclencher la reconnexion.

Sources : [SimConnect_SubscribeToSystemEvent](https://docs.flightsimulator.com/html/Programming_Tools/SimConnect/API_Reference/Events_And_Data/SimConnect_SubscribeToSystemEvent.htm), [FSDeveloper, détection de pause](https://www.fsdeveloper.com/forum/threads/with-simconnect-detect-when-im-not-in-flight.456748/).

#### SimVars recommandés

| Champ snapshot | SimVar | Unité |
|---|---|---|
| `LatitudeDeg` | `PLANE LATITUDE` | degrees |
| `LongitudeDeg` | `PLANE LONGITUDE` | degrees |
| `AltitudeFt` | `PLANE ALTITUDE` | feet |
| `HeightAglFt` | `PLANE ALT ABOVE GROUND` | feet |
| `IndicatedAirspeedKt` | `AIRSPEED INDICATED` | knots |
| `TrueAirspeedKt` | `AIRSPEED TRUE` | knots |
| `GroundspeedKt` | `GROUND VELOCITY` | knots |
| `VerticalSpeedFpm` | `VERTICAL SPEED` | feet per minute |
| `HeadingMagDeg` | `PLANE HEADING DEGREES MAGNETIC` | degrees |
| `AircraftTitle` | `TITLE` | string256 |
| `AircraftTailNumber` | `ATC ID` | string32 |
| `OnGround` | `SIM ON GROUND` | bool |

#### Plateforme

- Windows uniquement : `net10.0-windows`.
- Le wrapper managé et `SimConnect.dll` ne doivent **pas** être commités dans le repo. Les référencer via une propriété MSBuild (`$(MsfsSimConnectDir)`) et documenter que le runtime SimConnect est installé par MSFS.
- Vérifier la licence de redistribution avant de mettre les DLL dans un package NuGet. Par défaut : ne pas les ship, exiger le SDK/runtime côté consommateur.

### 3.3 Prepar3D

- P3D utilise SimConnect (lignée ESP), avec `LockheedMartin.Prepar3D.SimConnect.dll`.
- **Le SDK v6 ne peut pas se connecter à FSX, ESP ou aux P3D précédents.** Chaque version majeure a sa DLL managée. Un client compilé v6 ne marche pas en v5, et inversement.
- P3D v5 et v6 sont 64-bit ; les DLL 32-bit de P3D v3/FSX ne peuvent pas être chargées par une app 64-bit. Le serveur SimConnect de P3D v6 est compatible avec les clients 32-bit legacy, mais ça ne concerne pas une app .NET 64-bit.
- Les docs du wrapper managé P3D demandent .NET Framework 4.8. Il faut tester la référence depuis `net10.0-windows` ; si ça échoue, cibler `net48` pour le provider et l'héberger dans un process séparé.
- Les SimVars sont les mêmes que MSFS (ESP), avec les mêmes unités.
- Pause : events `Pause`, `Paused`, `Unpaused`, `Sim`, `SimStart`, `SimStop`. Vérifier si `Pause_EX1` existe dans le SDK v6 ; sinon utiliser `Pause` (1/0).

Source : [Prepar3D SDK](https://www.prepar3d.com/sdk/), [Managed SimConnect Projects v6](https://prepar3d.com/SDKv6/sdk/simconnect_api/managed_simconnect_projects.html).

**Recommandation (décision : toutes versions) :** trois hosts, P3D **v4**, **v5** et **v6**, chacun avec son SDK. Détection par version de fichier de `Prepar3D.exe`. P3D v1-v3 et FSX sont 32-bit : hors périmètre sauf demande explicite (il faudrait un host x86 séparé).

### 3.4 Transverse

#### .NET 8 est mort

- .NET 8 : EOL **2026-11-10**.
- .NET 9 : EOL **2026-11-10**.
- .NET 10 : LTS, support jusqu'au **2028-11-14**.
- .NET 11 : STS, sorti le 2026-11-10, support jusqu'au 2028-11-14.

**Cible : `net10.0`.** Ne pas partir sur .NET 11 STS pour une bibliothèque.

Sources : [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [blog .NET 8/9 EOL](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).

#### Licence

- **Décision : MIT.** Le repo est actuellement en GPL-3.0 ; c'est un frein pour les add-ons propriétaires/MIT et pour NuGet.
- Actions : remplacer `LICENSE` par le texte MIT, mettre `PackageLicenseExpression=MIT`, vérifier qu'aucun fichier ne porte d'en-tête GPL incompatible.
- Le changement est possible car le repo a un seul auteur (à vérifier : 13 commits, probablement tous de toi).

#### NuGet

- Les IDs `UnionFlight`, `UnionFlight.Core`, `UnionFlight.XPlane`, `UnionFlight.Reader` sont **libres** sur nuget.org (vérifié le 2026-11-19).
- Il faut réserver tôt : publier un `0.1.0-alpha` même incomplet.
- Métadonnées minimales : `PackageId`, `Authors`, `Description`, `PackageLicenseExpression`, `RepositoryUrl`, `PackageReadmeFile`, `GenerateDocumentationFile`, `IncludeSymbols`, `SymbolPackageFormat`, `ContinuousIntegrationBuild`.

#### Bibliothèques existantes

| Bibliothèque | Licence | Cible | Périmètre | Verdict |
|---|---|---|---|---|
| `SimConnect.NET` 0.2.2 | MIT | net8.0 / net9.0 | MSFS 2020/2024, async, bundle `SimConnect.dll` | Beta (0.2.2, 14 stars). Utilisable comme raccourci MSFS, mais ne couvre pas P3D. |
| `XPlaneConnector` 1.3.0 | à vérifier | netstandard2.0 | X-Plane UDP uniquement | Stale (2021), pas de Web API. Ne pas dépendre. |
| `bm98/FSimAdapter20_24` | GPL-3.0 | .NET Framework 4.8 | MSFS 2020+2024, chargement dynamique | Référence d'architecture, pas une dépendance. |

**Aucune bibliothèque unifiée mature n'existe.** L'abstraction est donc justifiée : ce n'est pas un doublon.

#### « Un seul module » : oui, mais…

- Une seule **API publique** et un seul package méta : oui.
- Un seul **binaire** pour les trois sims : non.
  - X-Plane est cross-platform.
  - MSFS/P3D sont Windows-only.
  - Les DLL SimConnect sont version-locked.
- La bonne unité de composition est le **provider**. Le package `UnionFlight` référence les providers ; l'utilisateur peut aussi installer seulement `UnionFlight.XPlane` sur Linux.

---

## 4. Architecture cible

### 4.1 Vue d'ensemble

```
                        ┌────────────────────────────────┐
                        │  UnionFlight (méta-package)    │
                        └───────────────┬────────────────┘
                                        │
                        ┌───────────────▼────────────────┐
                        │  UnionFlight.Core              │
                        │  FlightSimMonitor              │
                        │  probe / failover / events     │
                        └───────┬────────────────┬───────┘
                                │                │
             ┌──────────────────▼───┐      ┌─────▼──────────────────────┐
             │  UnionFlight.XPlane  │      │  UnionFlight.SimConnect    │
             │  Web API + UDP       │      │  client + hosts (Windows)  │
             │  net10.0             │      │  MSFS 2020/2024            │
             └──────────────────────┘      │  P3D v4/v5/v6              │
                                           │  net10.0 + net10.0-windows │
                                           └────────────────────────────┘
```

### 4.2 Projets

| Projet | TFM | Rôle | Dépendances |
|---|---|---|---|
| `src/UnionFlight.Abstractions` | `net10.0` | Contrats, snapshot, erreurs, options | aucune |
| `src/UnionFlight.Core` | `net10.0` | `FlightSimMonitor`, probe, failover, logging | Abstractions, `Microsoft.Extensions.Logging.Abstractions` |
| `src/UnionFlight.XPlane` | `net10.0` | Provider X-Plane (Web API + UDP) | Abstractions, `System.Text.Json` |
| `src/UnionFlight.SimConnect` | `net10.0` | Client provider : détection du sim, lancement du host, IPC NDJSON | Abstractions |
| `src/UnionFlight.SimConnect.Host.Abstractions` | `net10.0` | Contrat host, `AircraftData`, messages IPC | aucune |
| `src/UnionFlight.SimConnect.Host` | `net10.0-windows` | Runner host : pipe, JSON, cycle de vie, session de base | Host.Abstractions |
| `src/UnionFlight.SimConnect.Host.Msfs/` | source partagée | `MsfsSession` + data definition (pas de csproj) | — |
| `src/UnionFlight.SimConnect.Host.P3D/` | source partagée | `P3DSession` + data definition (pas de csproj) | — |
| `src/hosts/UF.SimConnectHost.Msfs2020` | `net10.0-windows` | Exe host MSFS 2020 (compile la source Msfs + SDK 2020) | Host + SDK 2020 |
| `src/hosts/UF.SimConnectHost.Msfs2024` | `net10.0-windows` | Exe host MSFS 2024 (compile la source Msfs + SDK 2024) | Host + SDK 2024 |
| `src/hosts/UF.SimConnectHost.P3Dv4` | `net10.0-windows` | Exe host P3D v4 (compile la source P3D + SDK v4) | Host + SDK v4 |
| `src/hosts/UF.SimConnectHost.P3Dv5` | `net10.0-windows` | Exe host P3D v5 (compile la source P3D + SDK v5) | Host + SDK v5 |
| `src/hosts/UF.SimConnectHost.P3Dv6` | `net10.0-windows` | Exe host P3D v6 (compile la source P3D + SDK v6) | Host + SDK v6 |
| `src/UnionFlight` | `net10.0` | Méta-package + façade `UnionFlightClient` (X-Plane + Core) | Core + XPlane |
| `src/UnionFlight.Windows` | `net10.0-windows` | Méta-package Windows ; les 5 hosts sont inclus comme contenu de package, pas comme ProjectReference | UnionFlight + SimConnect |
| `samples/UnionFlight.Sample.Console` | `net10.0` | Sample console X-Plane | `UnionFlight` |
| `samples/UnionFlight.Sample.Windows` | `net10.0-windows` | Sample console MSFS/P3D | `UnionFlight.Windows` |
| `tests/UnionFlight.Abstractions.Tests` | `net10.0` | Tests contrat | xUnit |
| `tests/UnionFlight.XPlane.Tests` | `net10.0` | Tests parsers/fixtures | xUnit |
| `tests/UnionFlight.Core.Tests` | `net10.0` | Tests monitor/failover | xUnit |
| `tests/UnionFlight.SimConnect.Tests` | `net10.0` | Tests client IPC + fake host | xUnit |
| `tests/UnionFlight.TestKit` | `net10.0` | Fakes, fixtures, replay | xUnit |
| `tests/UnionFlight.IntegrationTests.XPlane` | `net10.0` | Tests réels X-Plane | xUnit, `[Trait]` |
| `tests/UnionFlight.IntegrationTests.SimConnect` | `net10.0-windows` | Tests réels MSFS/P3D | xUnit, `[Trait]` |

### 4.3 Contrat public

```csharp
// src/UnionFlight.Abstractions/SimulatorKind.cs
namespace UnionFlight;

public enum SimulatorKind
{
    Unknown = 0,
    XPlane,
    Msfs2020,
    Msfs2024,
    Prepar3Dv4,
    Prepar3Dv5,
    Prepar3Dv6
}
```

```csharp
// src/UnionFlight.Abstractions/FlightSnapshot.cs
namespace UnionFlight;

/// <summary>
/// Immutable snapshot of the user aircraft at a point in time.
/// All units are explicit in the property names; no implicit conversions.
/// </summary>
public sealed record FlightSnapshot
{
    public required SimulatorKind Simulator { get; init; }
    public string? SimulatorVersion { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>True when a flight is loaded (not the main menu / loading screen).</summary>
    public bool IsFlightLoaded { get; init; }

    /// <summary>Null when the simulator does not expose pause state.</summary>
    public bool? IsPaused { get; init; }

    public string? AircraftTitle { get; init; }
    public string? AircraftTailNumber { get; init; }

    public double? LatitudeDeg { get; init; }
    public double? LongitudeDeg { get; init; }
    public double? AltitudeFt { get; init; }
    public double? HeightAglFt { get; init; }
    public double? IndicatedAirspeedKt { get; init; }
    public double? TrueAirspeedKt { get; init; }
    public double? GroundspeedKt { get; init; }
    public double? VerticalSpeedFpm { get; init; }
    public double? HeadingMagDeg { get; init; }
    public bool? OnGround { get; init; }
}
```

```csharp
// src/UnionFlight.Abstractions/IFlightSimProvider.cs
namespace UnionFlight;

public interface IFlightSimProvider : IAsyncDisposable
{
    SimulatorKind Kind { get; }
    string DisplayName { get; }
    bool IsSupportedOnThisPlatform { get; }

    /// <summary>Cheap, side-effect-free availability probe (HTTP, UDP, process).</summary>
    Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken);

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);

    FlightSnapshot? LatestSnapshot { get; }

    event EventHandler<FlightSnapshot>? SnapshotReceived;
    event EventHandler<ProviderFaultedEventArgs>? Faulted;
}

public sealed record SimulatorProbeResult(
    bool IsAvailable,
    string? Version,
    string? Detail);

public sealed class ProviderFaultedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
```

```csharp
// src/UnionFlight.Core/FlightSimMonitor.cs
namespace UnionFlight;

public sealed class FlightSimMonitor : IAsyncDisposable
{
    private readonly IReadOnlyList<IFlightSimProvider> _providers;
    private readonly TimeProvider _time;
    private readonly ILogger<FlightSimMonitor> _logger;
    private IFlightSimProvider? _active;
    private FlightSnapshot? _latest;

    public FlightSimMonitor(
        IEnumerable<IFlightSimProvider> providers,
        TimeProvider? time = null,
        ILogger<FlightSimMonitor>? logger = null)
    {
        _providers = providers.Where(p => p.IsSupportedOnThisPlatform).ToArray();
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<FlightSimMonitor>.Instance;
    }

    public IFlightSimProvider? ActiveProvider => _active;
    public FlightSnapshot? Latest => Volatile.Read(ref _latest);

    public event EventHandler<FlightSnapshot>? SnapshotReceived;
    public event EventHandler<IFlightSimProvider>? ProviderChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var available = await ProbeAllAsync(cancellationToken).ConfigureAwait(false);
        _active = available.FirstOrDefault();
        if (_active is null) return;

        await _active.StartAsync(cancellationToken).ConfigureAwait(false);
        _active.SnapshotReceived += OnSnapshot;
        _active.Faulted += OnFaulted;
        ProviderChanged?.Invoke(this, _active);
    }

    // StopAsync, DisposeAsync, OnSnapshot, OnFaulted, ProbeAllAsync...
}
```

**Règles de conception :**

- Aucun singleton. Tous les providers sont injectés.
- Aucun `dynamic`. Les valeurs sont typées `double?` / `string?` / `bool?`.
- Toute la vie est async et annulable (`StartAsync`, `StopAsync`, `IAsyncDisposable`).
- `LatestSnapshot` est un champ `volatile` ou lu via `Volatile.Read` ; les snapshots sont immuables.
- Les events sont optionnels ; le polling de `LatestSnapshot` reste possible.
- Les erreurs sont typées (`ProviderFaultedEventArgs`) et loguées, jamais avalées.
- Chaque provider est responsable de sa reconnexion (backoff exponentiel) et de son propre thread de transport.

### 4.4 Façade de commodité

```csharp
// src/UnionFlight/UnionFlightClient.cs
namespace UnionFlight;

public sealed class UnionFlightClient : IAsyncDisposable
{
    private readonly FlightSimMonitor _monitor;

    public UnionFlightClient(FlightSimMonitor? monitor = null)
    {
        _monitor = monitor ?? new FlightSimMonitor(new IFlightSimProvider[]
        {
            new XPlaneProvider()
        });
    }

    public FlightSnapshot? Latest => _monitor.Latest;
    public event EventHandler<FlightSnapshot>? SnapshotReceived
    {
        add => _monitor.SnapshotReceived += value;
        remove => _monitor.SnapshotReceived -= value;
    }

    public Task StartAsync(CancellationToken ct = default) => _monitor.StartAsync(ct);
    public Task StopAsync(CancellationToken ct = default) => _monitor.StopAsync(ct);
    public ValueTask DisposeAsync() => _monitor.DisposeAsync();
}
```

Le package `UnionFlight.Windows` ajoute `UnionFlightClient.CreateWindowsDefault()` (ou une extension `ServiceCollection`) qui enregistre `SimConnectHostProvider` — un seul provider qui détecte le sim et lance le host de la bonne version. Le package `UnionFlight` de base ne référence que X-Plane, pour rester installable sur Linux/macOS.

Le sample console devient :

```csharp
await using var client = new UnionFlightClient();
await client.StartAsync();

client.SnapshotReceived += (_, s) =>
    Console.WriteLine($"{s.Simulator} {s.AircraftTitle} {s.AltitudeFt:F0} ft {s.GroundspeedKt:F0} kt paused={s.IsPaused}");

await Task.Delay(Timeout.Infinite);
```

### 4.5 Matrice des responsabilités

| Responsabilité | Abstractions | Core | XPlane | SimConnect (client) | Hosts SimConnect |
|---|---|---|---|---|---|
| Contrat public | ✅ | | | | |
| Détection du sim | | ✅ | ✅ | ✅ | |
| Lancement du host | | | | ✅ | |
| Connexion / reconnexion | | | ✅ | ✅ | ✅ |
| Parsing protocole | | | ✅ | ✅ (NDJSON) | ✅ (SimConnect) |
| Mapping unités | | | ✅ | ✅ | ✅ |
| Snapshot immuable | ✅ | | ✅ | ✅ | ✅ |
| Failover | | ✅ | | | |
| Logging | | ✅ | ✅ | ✅ | ✅ |
| Tests unitaires | ✅ | ✅ | ✅ | ✅ | ✅ (mapping/offsets) |

### 4.6 Non-goals de la v1 (lecture seule)

- Écriture de datarefs / SimVars (altitude, heading, vitesse, autopilote).
- Commandes X-Plane (`/api/v3/commands/...`) et events SimConnect (`TransmitClientEvent`).
- Météo, trafic, aéroports, plans de vol, caméras.
- P3D v1-v3 et FSX (32-bit) : nécessiteraient un host x86 séparé.
- Multi-sim simultané : un seul provider actif en v1 (le monitor choisit le premier disponible). Le multi-sim est un sujet Phase 7.
- Interface graphique : la livraison est une bibliothèque + un sample console.

---

## 5. Plan par phases

**Durée estimée :** ~19-23 jours de travail focalisé (hors tests manuels sur les 7 versions de sims).

> Chaque tâche = 30 min à 2 h. Les étapes TDD sont explicites : test rouge → implémentation → test vert → commit.

### Phase 0 — Remise à niveau du socle (0,5 j)

#### Task 0.1 : Installer .NET 10 SDK sur le VPS

**Objectif :** pouvoir compiler et tester sans sudo.

**Fichiers :** aucun (outillage).

**Étapes :**

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
dotnet --info
```

Ajouter `export PATH="$HOME/.dotnet:$PATH"` à `~/.bashrc`.

**Vérification :** `dotnet --version` affiche `10.0.x`.

#### Task 0.2 : Ajouter `global.json`, `Directory.Build.props`, `.editorconfig`

**Fichiers :**
- Créer : `global.json`
- Créer : `Directory.Build.props`
- Créer : `.editorconfig`

**`global.json` :**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

**`Directory.Build.props` :**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors> <!-- passer à true en Phase 6 -->
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile> <!-- activer en Phase 6 -->
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

**Vérification :** `dotnet build` ne casse pas sur les projets existants (les warnings deviendront des erreurs ; corriger au fur et à mesure).

#### Task 0.3 : Réorganiser la solution

**Fichiers :**
- Déplacer : `FlightDataClass/` → `src/UnionFlight.Abstractions/`
- Déplacer : `UnionFlight/` → `src/UnionFlight.Core/`
- Déplacer : `UnionFlightXplaneReader/` → `src/UnionFlight.XPlane/`
- Supprimer : `UnionFlightMsfsReader/` (remplacé par `src/UnionFlight.SimConnect.Host.Msfs/` en Phase 4)
- Supprimer : `UnionFlightP3dReader/` (remplacé par `src/UnionFlight.SimConnect.Host.P3D/` en Phase 5)
- Déplacer : `ConsoleExemple/` → `samples/UnionFlight.Sample.Console/`
- Modifier : `UnionFlight.sln`

**Étapes :**

1. Créer les dossiers `src/`, `samples/`, `tests/`.
2. `git mv` chaque projet.
3. Renommer les `.csproj` et les `AssemblyName`.
4. Supprimer `Class1.cs` et les classes vides.
5. Corriger les `namespace` (`UnionFlight.FlightData` → `UnionFlight`).
6. Mettre à jour les `ProjectReference`.

**Vérification :** `dotnet build UnionFlight.sln` sur Linux (les projets Windows seront ajoutés en Phase 4/5).

#### Task 0.4 : Nettoyer les interfaces existantes

**Fichiers :**
- Remplacer : `src/UnionFlight.Abstractions/IAircraft.cs`, `IFlight.cs`, `IFlightReader.cs`, `ISim.cs`
- Créer : `src/UnionFlight.Abstractions/SimulatorKind.cs`, `FlightSnapshot.cs`, `IFlightSimProvider.cs`

**Étapes :**

1. Supprimer `IAircraft`, `IFlight`, `IFlightReader`, `ISim` (remplacés par `FlightSnapshot` + `IFlightSimProvider`).
2. Ajouter les nouveaux contrats (§4.3).
3. Corriger le sample pour compiler avec un `FlightSnapshot` factice.

**Vérification :** `dotnet build` passe ; le sample affiche un snapshot en dur.

#### Task 0.5 : Passer la licence en MIT

**Objectif :** aligner le repo sur la décision du 2026-11-19.

**Fichiers :**
- Remplacer : `LICENSE`
- Modifier : `README.md` (section Licence)

**Étapes :**

1. Remplacer le contenu de `LICENSE` par le texte MIT standard, avec `Copyright (c) 2026 Asanio06`.
2. Vérifier qu'aucun fichier source ne contient d'en-tête GPL :
   ```bash
   grep -ri "GNU General Public\|GPL" --include="*.cs" --include="*.csproj" --include="*.md" .
   ```
3. Mettre à jour le README.

**Vérification :** `LICENSE` contient `MIT License` ; aucun en-tête GPL dans les sources.

#### Task 0.6 : Premier commit de structure

```bash
git add -A
git commit -m "refactor: split solution into src/samples/tests, target net10.0; switch to MIT"
```

---

### Phase 1 — Contrat et orchestrateur (1,5 j)

#### Task 1.1 : Tests du snapshot et du provider

**Fichiers :**
- Créer : `tests/UnionFlight.Abstractions.Tests/UnionFlight.Abstractions.Tests.csproj`
- Créer : `tests/UnionFlight.Abstractions.Tests/FlightSnapshotTests.cs`
- Créer : `tests/UnionFlight.Abstractions.Tests/FakeProvider.cs`

**Étapes :**

1. Ajouter xUnit :
   ```bash
   dotnet new xunit -o tests/UnionFlight.Abstractions.Tests
   dotnet add tests/UnionFlight.Abstractions.Tests reference src/UnionFlight.Abstractions
   ```
2. Écrire un test qui vérifie que `FlightSnapshot` est immuable et que les unités sont explicites :
   ```csharp
   [Fact]
   public void Snapshot_Preserves_Explicit_Units()
   {
       var s = new FlightSnapshot
       {
           Simulator = SimulatorKind.XPlane,
           TimestampUtc = DateTimeOffset.UnixEpoch,
           AltitudeFt = 35000,
           GroundspeedKt = 450,
           VerticalSpeedFpm = -1200
       };

       Assert.Equal(35000, s.AltitudeFt);
       Assert.Equal(450, s.GroundspeedKt);
       Assert.Equal(-1200, s.VerticalSpeedFpm);
   }
   ```
3. Lancer : `dotnet test tests/UnionFlight.Abstractions.Tests` → doit échouer si le contrat n'existe pas encore, puis passer.

#### Task 1.2 : Implémenter `FlightSimMonitor`

**Fichiers :**
- Créer : `src/UnionFlight.Core/FlightSimMonitor.cs`
- Créer : `src/UnionFlight.Core/ProbeAllAsync.cs` (ou méthode privée)
- Créer : `src/UnionFlight.Core/UnionFlight.Core.csproj`

**Étapes :**

1. Implémenter `StartAsync` : probe en parallèle avec un timeout de 2 s, choisir le premier disponible selon l'ordre de la liste.
2. Implémenter `StopAsync` : désabonner les events, `StopAsync` du provider actif, remettre `_active` à null.
3. Implémenter `DisposeAsync`.
4. Implémenter `OnSnapshot` : mettre à jour `_latest` via `Volatile.Write`, puis `SnapshotReceived?.Invoke`.
5. Implémenter `OnFaulted` : logger, arrêter le provider, relancer un probe (avec backoff).

**Vérification :**
```bash
dotnet test tests/UnionFlight.Core.Tests
```
Tests :
- probe retourne le premier provider disponible ;
- probe ignore les providers indisponibles ;
- `SnapshotReceived` est déclenché ;
- `Faulted` déclenche un nouveau probe ;
- `StopAsync` est idempotent.

#### Task 1.3 : Tests du monitor avec un fake provider

**Fichiers :**
- Créer : `tests/UnionFlight.Core.Tests/FlightSimMonitorTests.cs`
- Créer : `tests/UnionFlight.TestKit/FakeFlightSimProvider.cs`

**Étapes :**

1. `FakeFlightSimProvider` expose `ProbeAsync` configurable, `StartAsync` qui émet un snapshot, `StopAsync` qui compte les appels.
2. Tester le failover : provider A indisponible, provider B disponible → B actif.
3. Tester la cancellation : `StartAsync` avec un token annulé → `OperationCanceledException`.

**Vérification :** `dotnet test` → tous verts.

#### Task 1.4 : Façade `UnionFlightClient` + sample

**Fichiers :**
- Créer : `src/UnionFlight/UnionFlightClient.cs`
- Modifier : `samples/UnionFlight.Sample.Console/Program.cs`

**Étapes :**

1. Implémenter la façade (§4.4).
2. Remplacer le sample par la version `await using`.
3. Lancer le sample avec un `FakeFlightSimProvider` pour vérifier l'affichage.

**Vérification :** `dotnet run --project samples/UnionFlight.Sample.Console` affiche un snapshot.

#### Task 1.5 : Commit

```bash
git add -A
git commit -m "feat: add immutable snapshot contract, monitor, and client facade"
```

---

### Phase 2 — Provider X-Plane Web API (2-3 j)

#### Task 2.1 : Options et détection

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/XPlaneOptions.cs`
- Créer : `src/UnionFlight.XPlane/XPlaneDetector.cs`
- Créer : `src/UnionFlight.XPlane/XPlaneApiException.cs`

**`XPlaneOptions` :**

```csharp
public sealed class XPlaneOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int WebApiPort { get; init; } = 8086;
    public int UdpPort { get; init; } = 49000;
    public bool PreferWebApi { get; init; } = true;
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(2);
}
```

**`XPlaneDetector` :**

```csharp
public async Task<SimulatorProbeResult> ProbeAsync(CancellationToken ct)
{
    using var http = new HttpClient { BaseAddress = new Uri($"http://{_options.Host}:{_options.WebApiPort}") };
    using var response = await http.GetAsync("/api/capabilities", ct).ConfigureAwait(false);
    if (response.StatusCode == HttpStatusCode.Forbidden)
        return new(false, null, "X-Plane web server is running but 'Disable Incoming Traffic' is enabled.");
    if (!response.IsSuccessStatusCode)
        return new(false, null, $"HTTP {(int)response.StatusCode}");

    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    var versions = doc.RootElement.GetProperty("api").GetProperty("versions")
        .EnumerateArray().Select(v => v.GetString()).ToArray();
    var xplaneVersion = doc.RootElement.GetProperty("x-plane").GetProperty("version").GetString();

    return new(true, xplaneVersion, $"Web API {string.Join(",", versions)}");
}
```

**Vérification :**
- Test unitaire avec un `HttpMessageHandler` fake qui renvoie le JSON `capabilities`.
- Test avec 403 → message explicite.
- Test avec connection refused → `IsAvailable = false`.

#### Task 2.2 : Résolution des datarefs

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/XPlaneDatarefResolver.cs`
- Créer : `tests/UnionFlight.XPlane.Tests/XPlaneDatarefResolverTests.cs`

**Étapes :**

1. Résoudre chaque nom individuellement :
   ```csharp
   public async Task<long?> ResolveAsync(string name, CancellationToken ct)
   {
       var url = $"/api/{_apiVersion}/datarefs?filter[name]={Uri.EscapeDataString(name)}";
       using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
       if (response.StatusCode == HttpStatusCode.NotFound) return null;
       response.EnsureSuccessStatusCode();

       using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
       var data = doc.RootElement.GetProperty("data");
       return data.GetArrayLength() == 0 ? null : data[0].GetProperty("id").GetInt64();
   }
   ```
2. Mettre en cache les IDs (`Dictionary<string, long>`) pour la session. Les IDs de la Web API peuvent dépasser `int` (ex. `1253033683792`) : utiliser `long`.
3. Limiter la concurrence à 8 avec `SemaphoreSlim`.
4. Tester : un nom trouvé, un nom absent (404 → null), un JSON malformé (exception typée).

**Vérification :** `dotnet test tests/UnionFlight.XPlane.Tests` vert.

#### Task 2.3 : Client WebSocket

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/XPlaneWebSocketClient.cs`
- Créer : `tests/UnionFlight.XPlane.Tests/XPlaneWebSocketClientTests.cs`

**Étapes :**

1. Se connecter à `ws://{host}:{port}/api/{apiVersion}`.
2. Envoyer l'abonnement :
   ```csharp
   var request = new
   {
       req_id = Interlocked.Increment(ref _requestId),
       type = "dataref_subscribe_values",
       @params = new { datarefs = ids.Select(id => new { id }).ToArray() }
   };
   await socket.SendAsync(
       JsonSerializer.SerializeToUtf8Bytes(request),
       WebSocketMessageType.Text, true, ct);
   ```
3. Boucle de réception :
   ```csharp
   var buffer = new byte[16 * 1024];
   using var ms = new MemoryStream();

   while (!ct.IsCancellationRequested)
   {
       WebSocketReceiveResult result;
       do
       {
           result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
           if (result.MessageType == WebSocketMessageType.Close) return;
           ms.Write(buffer, 0, result.Count);
       }
       while (!result.EndOfMessage);

       using var doc = JsonDocument.Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
       ms.SetLength(0);
       HandleMessage(doc.RootElement);
   }
   ```
4. `HandleMessage` :
   - `type == "result"` : si `success == false`, lever `XPlaneApiException(error_code, error_message)`.
   - `type == "dataref_update_values"` : pour chaque propriété, `long.TryParse(prop.Name.Trim(), out var id)`, puis lire `double` / `int` / `string` (base64).
5. Reconnexion : si le socket se ferme, backoff exponentiel (1 s, 2 s, 4 s, max 30 s), re-résoudre les IDs, réabonner.
6. Ne pas garder de `JsonElement` après le `using` : convertir immédiatement en `double`/`string`.

**Vérification :** tests avec des trames JSON enregistrées (fixtures) :
- `subscribe_ok.json`
- `update_values.json`
- `update_string_base64.json`
- `result_error.json`

#### Task 2.4 : Provider Web API

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/XPlaneWebApiProvider.cs`
- Créer : `src/UnionFlight.XPlane/XPlaneDatarefMap.cs`

**Étapes :**

1. Au `StartAsync` :
   - probe `/api/capabilities` ;
   - résoudre les IDs des datarefs dynamiques ;
   - lire les chaînes statiques via REST (`acf_ui_name` ou `acf_descrip`, `acf_tailnum`) ;
   - ouvrir le WebSocket et s'abonner.
2. À chaque `dataref_update_values`, construire un `FlightSnapshot` avec les valeurs disponibles et `TimestampUtc = TimeProvider.GetUtcNow()`.
3. `IsPaused = sim/time/paused == 1`.
4. `IsFlightLoaded = true` dès qu'au moins une valeur avion a été reçue.
5. Les champs non résolus restent `null` (pas de valeur inventée).
6. `Faulted` est déclenché sur erreur de transport ; le monitor décide de relancer.

**Mapping :** cf. tableau §3.1.

**Vérification :** test unitaire avec un faux client WebSocket + fixtures ; le snapshot attendu est comparé champ par champ.

#### Task 2.5 : Test manuel X-Plane 12

**Prérequis :** X-Plane 12.1.1+ lancé avec un vol chargé.

**Étapes :**

1. `dotnet run --project samples/UnionFlight.Sample.Console`.
2. Vérifier : nom d'avion, immatriculation, latitude/longitude, altitude, vitesses, heading, VS.
3. Mettre le sim en pause (`P`) → `IsPaused = true`, `IsFlightLoaded = true`.
4. Quitter le vol → le provider doit se déconnecter proprement, puis se reconnecter au vol suivant.
5. Fermer X-Plane → le monitor doit repasser à `null` sans exception non gérée.

**Vérification :** sortie console conforme, pas de fuite de tâche, pas d'exception.

#### Task 2.6 : Commit

```bash
git add -A
git commit -m "feat(xplane): add Web API provider with REST dataref resolution and WebSocket subscriptions"
```

---

### Phase 3 — Provider X-Plane UDP (fallback) (2 j)

#### Task 3.1 : Protocole RREF

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/Udp/XPlaneRrefProtocol.cs`
- Créer : `tests/UnionFlight.XPlane.Tests/XPlaneRrefProtocolTests.cs`

**Étapes :**

1. Construire des paquets **batchés** : plusieurs entrées `(freq, id, dataref\0)` dans un même paquet de 413 octets, au lieu d'un paquet par dataref.
2. Utiliser `BinaryPrimitives.WriteInt32LittleEndian` / `WriteSingleLittleEndian`.
3. Parser les réponses :
   ```csharp
   if (buffer.Length < 5) return;
   if (!buffer.AsSpan(0, 4).SequenceEqual("RREF"u8)) return;

   var pos = 5;
   while (pos + 8 <= buffer.Length)
   {
       var id = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(pos, 4));
       var value = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(pos + 4, 4));
       pos += 8;
       if (_readers.TryGetValue(id, out var reader)) reader.Update(value);
   }
   ```
4. Ignorer les IDs inconnus (`TryGetValue`, jamais d'indexeur).
5. Tester : paquet valide, paquet tronqué, header inconnu, id inconnu, plusieurs entrées.

**Vérification :** tests unitaires avec des paquets hexadécimaux golden.

#### Task 3.2 : Provider UDP

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/Udp/XPlaneUdpProvider.cs`
- Créer : `src/UnionFlight.XPlane/Udp/XPlaneUdpOptions.cs`

**Étapes :**

1. Un seul `UdpClient`, bindé à `IPEndPoint(IPAddress.Any, 0)`.
2. Envoyer les paquets RREF vers `options.Host:options.UdpPort`.
3. Boucle de réception `await socket.ReceiveAsync(ct)` (API .NET 6+ avec `CancellationToken`).
4. `CancellationTokenSource` créé dans `StartAsync`, annulé dans `StopAsync`, `Dispose` après `await` des tâches.
5. Détection : envoyer un RREF de `sim/time/paused` avec un id sentinelle et attendre la première réponse avec un timeout de 1 s. Si rien, `IsAvailable = false`.
6. Reconnexion : recréer le socket et le CTS à chaque `StartAsync`.

**Vérification :** test avec un faux socket/enregistreur ; test manuel XP11 et XP12.

#### Task 3.3 : Assembleur de chaînes

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/Udp/XPlaneStringAssembler.cs`
- Créer : `tests/UnionFlight.XPlane.Tests/XPlaneStringAssemblerTests.cs`

**Étapes :**

1. Stocker un `char[]` par dataref de chaîne (40 pour `acf_tailnum`, 250 pour `acf_ui_name`, 260 pour `acf_descrip`).
2. À l'index 0, réinitialiser le buffer.
3. À chaque caractère, écrire dans le buffer (pas de `PadRight`/`Remove`/`Insert`).
4. Exposer `ToString()` en coupant au premier `\0`, puis `TrimEnd()`.
5. Tester : écriture séquentielle, écriture dans le désordre, caractère nul, reset.

**Vérification :** tests unitaires verts.

#### Task 3.4 : Sélection Web API vs UDP

**Fichiers :**
- Créer : `src/UnionFlight.XPlane/XPlaneProvider.cs` (façade qui choisit le transport)

**Étapes :**

1. `ProbeAsync` : essayer la Web API, puis l'UDP.
2. `StartAsync` : utiliser la Web API si disponible et `PreferWebApi`, sinon l'UDP.
3. Exposer `Transport` (`WebApi` / `Udp`) dans `SimulatorProbeResult.Detail` pour le diagnostic.

**Vérification :** tests avec les deux fakes ; test manuel XP12 (Web API) et XP11 (UDP).

#### Task 3.5 : Test manuel XP11/XP12 + LAN

**Étapes :**

1. XP12 local → Web API.
2. XP11 local → UDP.
3. XP12 sur une autre machine → UDP (la Web API est localhost-only).
4. Vérifier que la charge réseau a baissé par rapport au code actuel (paquets batchés, chaînes à 1 Hz).

**Vérification :** snapshots corrects dans les trois cas.

#### Task 3.6 : Commit

```bash
git add -A
git commit -m "feat(xplane): add efficient batched UDP RREF fallback with cancellation and reconnect"
```

---

### Phase 4 — Infrastructure SimConnect + MSFS 2020/2024 (5-6 j)

**Décision :** un **process hôte par version de SimConnect**. Le client .NET ne charge jamais les DLL SimConnect dans son propre process.

Pourquoi :

- Chaque version a une paire `SimConnect.dll` native + wrapper managé version-locked.
- Deux DLL natives du même nom dans un process → résolution imprévisible.
- .NET Core n'utilise pas le GAC ; le wrapper managé doit être copié dans l'output.
- Le host isole les crashes et peut passer en `net48` si le wrapper ne se charge pas en .NET 10.
- Le client reste `net10.0` : la partie X-Plane tourne sur Linux/macOS ; le provider SimConnect et les hosts sont Windows-only. Le mode distant (client Linux ↔ host Windows) n'est pas implémenté en v1.

**Matrice des hosts :**

| Host | SDK | Wrapper managé | TFM |
|---|---|---|---|
| `UF.SimConnectHost.Msfs2020` | MSFS 2020 | `Microsoft.FlightSimulator.SimConnect.dll` 2020 | `net10.0-windows` (fallback `net48`) |
| `UF.SimConnectHost.Msfs2024` | MSFS 2024 | `Microsoft.FlightSimulator.SimConnect.dll` 2024 | idem |
| `UF.SimConnectHost.P3Dv4` | P3D v4 | `LockheedMartin.Prepar3D.SimConnect.dll` v4 | idem |
| `UF.SimConnectHost.P3Dv5` | P3D v5 | `LockheedMartin.Prepar3D.SimConnect.dll` v5 | idem |
| `UF.SimConnectHost.P3Dv6` | P3D v6 | `LockheedMartin.Prepar3D.SimConnect.dll` v6 | idem |

#### Task 4.1 : Vérifier les EULA et préparer les SDK

**Objectif :** savoir si les DLL peuvent être packagées, et où elles se trouvent.

**Fichiers :**
- Créer : `docs/simconnect-licensing.md`
- Créer : `tools/fetch-simconnect.ps1`
- Modifier : `Directory.Build.props`

**Étapes :**

1. Lire les EULA MSFS SDK et P3D SDK sur la redistribution de `SimConnect.dll` et du wrapper managé.
2. Ajouter les chemins configurables :
   ```xml
   <Msfs2020SimConnectDir Condition="'$(Msfs2020SimConnectDir)' == ''">$(MSFS2020_SDK)\SimConnect SDK\lib\managed</Msfs2020SimConnectDir>
   <Msfs2024SimConnectDir Condition="'$(Msfs2024SimConnectDir)' == ''">$(MSFS2024_SDK)\SimConnect SDK\lib\managed</Msfs2024SimConnectDir>
   <P3Dv4SimConnectDir Condition="'$(P3Dv4SimConnectDir)' == ''">$(P3D_V4_SDK)\lib\SimConnect\managed</P3Dv4SimConnectDir>
   <P3Dv5SimConnectDir Condition="'$(P3Dv5SimConnectDir)' == ''">$(P3D_V5_SDK)\lib\SimConnect\managed</P3Dv5SimConnectDir>
   <P3Dv6SimConnectDir Condition="'$(P3Dv6SimConnectDir)' == ''">$(P3D_V6_SDK)\lib\SimConnect\managed</P3Dv6SimConnectDir>
   ```
3. Écrire `tools/fetch-simconnect.ps1` : copie les paires `(wrapper managé, SimConnect.dll)` depuis chaque SDK vers `artifacts/simconnect/{version}/`.
4. Si l'EULA interdit la redistribution : le package NuGet ne contient pas les DLL. Le host les charge à l'exécution via `AssemblyLoadContext.Default.Resolving` (wrapper managé) et `NativeLibrary.SetDllImportResolver` (natif), depuis `UNIONFLIGHT_SIMCONNECT_DIR` ou le dossier du sim.

**Vérification :** `docs/simconnect-licensing.md` contient une conclusion explicite par sim (autorisé / interdit / conditionnel).

#### Task 4.2 : Contrat IPC

**Fichiers :**
- Créer : `src/UnionFlight.SimConnect.Host.Abstractions/AircraftData.cs`
- Créer : `src/UnionFlight.SimConnect.Host.Abstractions/HostMessage.cs`
- Créer : `src/UnionFlight.SimConnect.Host.Abstractions/ISimConnectSession.cs`
- Créer : `src/UnionFlight.SimConnect.Host.Abstractions/JsonContext.cs`

**`AircraftData` :**

```csharp
public readonly record struct AircraftData(
    string? Title,
    string? AtcId,
    double? LatitudeDeg,
    double? LongitudeDeg,
    double? AltitudeFt,
    double? HeightAglFt,
    double? IndicatedAirspeedKt,
    double? TrueAirspeedKt,
    double? GroundspeedKt,
    double? VerticalSpeedFpm,
    double? HeadingMagDeg,
    bool? OnGround);
```

**Messages NDJSON :**

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(SnapshotMessage), "snapshot")]
[JsonDerivedType(typeof(StateMessage), "state")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
[JsonDerivedType(typeof(StopMessage), "stop")]
public abstract record HostMessage(string Type);

public sealed record HelloMessage(string Sim, string SimVersion, string HostVersion)
    : HostMessage("hello");

public sealed record SnapshotMessage(
    DateTimeOffset TimestampUtc,
    bool IsFlightLoaded,
    bool? IsPaused,
    AircraftData Aircraft) : HostMessage("snapshot");

public sealed record StateMessage(bool IsFlightLoaded, bool? IsPaused, bool IsConnected)
    : HostMessage("state");

public sealed record ErrorMessage(string Code, string Message) : HostMessage("error");

public sealed record StopMessage() : HostMessage("stop");
```

**Session :**

```csharp
public interface ISimConnectSession : IDisposable
{
    SimulatorKind Kind { get; }
    string SimulatorVersion { get; }
    event EventHandler<AircraftData>? DataReceived;
    event EventHandler<SessionState>? StateChanged;
    event EventHandler<Exception>? Faulted;
    void Start();
}
```

`JsonContext` utilise `[JsonSerializable(typeof(HostMessage))]` et `[JsonSerializable(typeof(AircraftData))]` pour la sérialisation source-generated.

**Vérification :** tests de sérialisation/désérialisation des cinq messages.

#### Task 4.3 : Runner host

**Fichiers :**
- Créer : `src/UnionFlight.SimConnect.Host/HostRunner.cs`
- Créer : `src/UnionFlight.SimConnect.Host/Program.cs`
- Créer : `src/UnionFlight.SimConnect.Host/SimConnectSessionBase.cs`

**Étapes :**

1. Le host lit `--pipe <name>` et `--parent-pid <pid>`.
2. Il crée un `NamedPipeServerStream` (`PipeDirection.InOut`, `PipeTransmissionMode.Byte`, `PipeOptions.Asynchronous`), attend le client, et lit les commandes (`stop`) en parallèle de l'écriture des snapshots.
3. Il instancie la session (`MsfsSession` ou `P3DSession`), écrit `hello`, puis les `snapshot`/`state`/`error` en NDJSON.
4. `SimConnectSessionBase` :
   - thread dédié ;
   - `new SimConnect(...)`, boucle `ReceiveMessage()` ;
   - `AddToDataDefinition`, `RegisterDataDefineStruct`, `RequestDataOnSimObject` ;
   - events `Sim`, `Pause_EX1`, `FlightLoaded`, `OnRecvQuit`, `OnRecvException` ;
   - `Dispose` marshalé sur le même thread.
5. Watchdog : si le process parent disparaît, le host s'arrête.
6. Les logs de démarrage vont sur stderr ; le client les capture.

**Vérification :** lancer le host sans sim → `error` + code de sortie non nul, sans crash non géré.

#### Task 4.4 : Session MSFS

**Fichiers :**
- Créer : `src/UnionFlight.SimConnect.Host.Msfs/MsfsSession.cs` (source partagée, pas de csproj)
- Créer : `src/UnionFlight.SimConnect.Host.Msfs/MsfsDataDefinition.cs`

Cette source est compilée **deux fois** : une fois par `UF.SimConnectHost.Msfs2020`, une fois par `UF.SimConnectHost.Msfs2024`, chacune avec sa propre référence SDK. C'est ce qui permet d'avoir un code identique sans mélanger les DLL.

**Étapes :**

1. Implémenter `ISimConnectSession` avec `Microsoft.FlightSimulator.SimConnect`.
2. Data definition : cf. tableau §3.2.
3. `SIMCONNECT_PERIOD.SIM_FRAME` + `SIMCONNECT_DATA_REQUEST_FLAG.CHANGED`.
4. `Sim`, `Pause_EX1`, `FlightLoaded` ; `RequestSystemState("FlightLoaded")` à la connexion.
5. `OnRecvQuit` → `Faulted` + arrêt du host.
6. Le host écrit un `SnapshotMessage` à chaque `OnRecvSimobjectData` et un `StateMessage` à chaque event.

**Vérification :** test `Marshal.SizeOf`/offsets ; test de mapping ; build Windows.

#### Task 4.5 : Exécutables hosts MSFS

**Fichiers :**
- Créer : `src/hosts/UF.SimConnectHost.Msfs2020/UF.SimConnectHost.Msfs2020.csproj`
- Créer : `src/hosts/UF.SimConnectHost.Msfs2024/UF.SimConnectHost.Msfs2024.csproj`
- Créer : `src/hosts/UF.SimConnectHost.Msfs2020/Program.cs`
- Créer : `src/hosts/UF.SimConnectHost.Msfs2024/Program.cs`

**Csproj type (2024) :**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <AssemblyName>UF.SimConnectHost.Msfs2024</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UnionFlight.SimConnect.Host\UnionFlight.SimConnect.Host.csproj" />
    <Compile Include="..\..\UnionFlight.SimConnect.Host.Msfs\**\*.cs" LinkBase="Msfs" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Microsoft.FlightSimulator.SimConnect">
      <HintPath>$(Msfs2024SimConnectDir)\Microsoft.FlightSimulator.SimConnect.dll</HintPath>
      <Private>true</Private>
    </Reference>
    <None Include="$(Msfs2024SimConnectDir)\SimConnect.dll" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Le host 2020 est identique, avec `$(Msfs2020SimConnectDir)` et `UF.SimConnectHost.Msfs2020`.

**Vérification :** `dotnet build` Windows ; chaque exe sort avec la bonne paire de DLL.

#### Task 4.6 : Client provider et détection

**Fichiers :**
- Créer : `src/UnionFlight.SimConnect/SimConnectHostProvider.cs`
- Créer : `src/UnionFlight.SimConnect/SimConnectProcessDetector.cs`
- Créer : `src/UnionFlight.SimConnect/SimConnectHostOptions.cs`
- Créer : `src/UnionFlight.SimConnect/HostLocator.cs`

**Étapes :**

1. `DetectAsync` :
   - `FlightSimulator2024.exe` → `Msfs2024` ;
   - `FlightSimulator.exe` → `Msfs2020` ;
   - `Prepar3D.exe` → `FileVersionInfo.FileMajorPart` → `P3Dv4`/`P3Dv5`/`P3Dv6` ;
   - rien → indisponible.
2. `StartAsync` :
   - localiser le host dans `runtimes/win-x64/native/hosts/` ou `HostDirectory` ;
   - `Process.Start` avec `--pipe <guid> --parent-pid <pid>` ;
   - attendre `hello` avec timeout 5 s ;
   - lire les NDJSON, mapper `SnapshotMessage` → `FlightSnapshot`.
3. `StopAsync` : envoyer `{"type":"stop"}`, attendre 2 s, puis `Kill(entireProcessTree: true)`.
4. Si le host meurt : `Faulted`, le monitor reprobe.
5. Logger stderr/stdout du host.

**Vérification :** test avec un faux host (exe console qui écrit des NDJSON) ; test de kill ; test de timeout.

#### Task 4.7 : Tests et test manuel MSFS

**Tests :**
- IPC round-trip avec `FakeHost`.
- Crash du host → `Faulted`.
- Timeout de démarrage.
- Mapping `AircraftData` → `FlightSnapshot`.

**Manuel :**
- MSFS 2024 : vol, pause active, menu, quit.
- MSFS 2020 : idem.
- Vérifier que le host 2024 ne se lance pas pour 2020, et inversement.

**Vérification :** snapshots conformes, aucune DLL mixée.

#### Task 4.8 : Commit

```bash
git add -A
git commit -m "feat(simconnect): add per-version host processes for MSFS 2020/2024"
```

---

### Phase 5 — Hosts P3D v4/v5/v6 (4-5 j)

#### Task 5.1 : Session P3D

**Fichiers :**
- Créer : `src/UnionFlight.SimConnect.Host.P3D/P3DSession.cs` (source partagée, pas de csproj)
- Créer : `src/UnionFlight.SimConnect.Host.P3D/P3DDataDefinition.cs`

Cette source est compilée **trois fois** : une par host P3D v4/v5/v6, chacune avec son SDK.

**Étapes :**

1. Même modèle que `MsfsSession`, avec `LockheedMartin.Prepar3D.SimConnect`.
2. Events `Sim`, `Pause` (et `Pause_EX1` si le SDK le fournit), `FlightLoaded`.
3. Mêmes SimVars (ESP).
4. Si le wrapper .NET Framework ne se charge pas en `net10.0-windows`, passer le host en `net48` ; le protocole IPC ne change pas.

**Vérification :** build Windows ; test de mapping.

#### Task 5.2 : Exécutables hosts P3D

**Fichiers :**
- Créer : `src/hosts/UF.SimConnectHost.P3Dv4/...`
- Créer : `src/hosts/UF.SimConnectHost.P3Dv5/...`
- Créer : `src/hosts/UF.SimConnectHost.P3Dv6/...`

**Étapes :**

1. Même csproj que MSFS, avec `$(P3Dv4SimConnectDir)`, `$(P3Dv5SimConnectDir)`, `$(P3Dv6SimConnectDir)`, et l'include `<Compile Include="..\..\UnionFlight.SimConnect.Host.P3D\**\*.cs" LinkBase="P3D" />`.
2. Détection de version dans le client : `Prepar3D.exe` + `FileVersionInfo.FileMajorPart`.
3. Si v3/FSX → `SimulatorKind.Unknown` + message « 32-bit non supporté ».

**Vérification :** build ; les trois exes sortent avec leurs DLL.

#### Task 5.3 : Test manuel P3D

**Étapes :**

1. P3D v6 : vol, pause, menu, quit.
2. P3D v5 et v4 si disponibles : idem.
3. Vérifier qu'un host v6 ne se lance pas pour v5.

**Vérification :** snapshots conformes.

#### Task 5.4 : Commit

```bash
git add -A
git commit -m "feat(simconnect): add per-version host processes for Prepar3D v4/v5/v6"
```

---

### Phase 6 — Packaging et CI (1-2 j)

#### Task 6.1 : Métadonnées NuGet

**Fichiers :**
- Modifier : `Directory.Build.props`
- Créer : `README.md` (section usage)
- Créer : `CHANGELOG.md`

**`Directory.Build.props` (ajout) :**

```xml
<PropertyGroup>
  <Authors>Asanio06</Authors>
  <Company>UnionFlight</Company>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <RepositoryUrl>https://github.com/Asanio06/UnionFlightReader</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageTags>flight-simulator;xplane;msfs;prepar3d;simconnect;telemetry</PackageTags>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
</PropertyGroup>
```

**Étapes :**

1. Ajouter `README.md` en `None Include` dans le package.
2. Mettre `<IsPackable>false</IsPackable>` dans tous les projets de test et les samples.
3. Inclure les 5 hosts dans `UnionFlight.Windows` :
   ```xml
   <None Include="$(ArtifactsDir)\hosts\**" Pack="true" PackagePath="runtimes/win-x64/native/hosts/" />
   ```
   Si les EULA l'autorisent, inclure aussi les paires `SimConnect.dll` + wrapper managé dans le même dossier ; sinon, documenter `UNIONFLIGHT_SIMCONNECT_DIR`.
4. Passer `TreatWarningsAsErrors` à `true` et `GenerateDocumentationFile` à `true` dans `Directory.Build.props` ; corriger les warnings restants.
5. Vérifier `dotnet pack -c Release`.
6. Inspecter le `.nupkg` (`unzip -l`) et vérifier la présence des hosts.

**Vérification :** `dotnet pack` produit un package avec README, XML docs et symbols.

#### Task 6.2 : GitHub Actions CI

**Fichiers :**
- Créer : `.github/workflows/ci.yml`
- Créer : `.github/workflows/release.yml`

**Solution filters :** créer `UnionFlight.Linux.slnf` (Abstractions, Core, XPlane, sample console, tests) et `UnionFlight.Windows.slnf` (tous les projets). Le job Linux utilise le `.slnf` ; le job Windows utilise `UnionFlight.sln`.

**`ci.yml` :**

```yaml
name: ci

on:
  push:
    branches: [main]
  pull_request:

jobs:
  linux:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore UnionFlight.Linux.slnf
      - run: dotnet build UnionFlight.Linux.slnf -c Release --no-restore
      - run: dotnet test tests/UnionFlight.Core.Tests -c Release --no-build
      - run: dotnet test tests/UnionFlight.XPlane.Tests -c Release --no-build

  windows:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build UnionFlight.sln -c Release
      # Les providers MSFS/P3D compilent ; les tests d'intégration sont manuels.
```

**`release.yml` :**

```yaml
name: release

on:
  push:
    tags: ['v*']

jobs:
  pack:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet pack UnionFlight.sln -c Release -o artifacts
      - run: dotnet nuget push artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

**Vérification :** CI verte sur `main` ; tag `v0.1.0-alpha` → package publié.

#### Task 6.3 : Publier `0.1.0-alpha`

**Étapes :**

1. Réserver l'ID `UnionFlight` sur nuget.org.
2. Créer le tag.
3. Vérifier l'installation dans une app scratch :
   ```bash
   dotnet new console -o /tmp/uf-test
   cd /tmp/uf-test
   dotnet add package UnionFlight --version 0.1.0-alpha
   ```
4. Lancer l'app : elle doit afficher un snapshot si un sim tourne.

**Vérification :** package installable, sample fonctionnel.

---

### Phase 7 — Durcissement (2 j, optionnel)

- Reconnexion avec backoff + jitter, watchdog `TimeProvider`.
- Logging structuré (`ILogger`), event counters (`EventSource`/`Meter`).
- XML docs sur tous les membres publics ; `GenerateDocumentationFile` sans warnings.
- Fuzzing des parseurs (SharpFuzz) ou tests property-based (FsCheck).
- Profiling allocations du parser UDP ; zéro allocation par paquet si possible.
- Support multi-sims simultanés (`IReadOnlyList<FlightSnapshot>`).
- `netstandard2.0` si des consommateurs .NET Framework/Unity le demandent.
- Documentation des datarefs par version de sim.

---

## 6. Tests et CI

### 6.1 Pyramide de tests

| Niveau | Ce qui est testé | Où | Dépend d'un sim ? |
|---|---|---|---|
| Unitaire | Contrats, mapping, conversions, parsers | `tests/*.Tests` | Non |
| Fixtures | Trames UDP/JSON enregistrées, rejouées | `tests/UnionFlight.TestKit` | Non |
| IPC host | NDJSON, cycle de vie, crash, timeout | `tests/UnionFlight.SimConnect.Tests` | Non (fake host) |
| Intégration | Connexion réelle, valeurs, pause, reconnexion | `tests/UnionFlight.IntegrationTests` | Oui |
| Manuel | Checklist par version de sim | `docs/manual-testing.md` | Oui |

### 6.2 Fixtures

- **X-Plane UDP :** capturer avec Wireshark/tcpdump sur le port 49000, sauver en hex + snapshot attendu.
- **X-Plane Web API :** sauver les trames JSON (`capabilities`, `datarefs`, `dataref_update_values`, `result`).
- **MSFS/P3D :** pas de capture réseau (SimConnect est local). Tester les structures (`Marshal.SizeOf`, offsets) et le mapping avec des données synthétiques.
- **Hosts SimConnect :** un `FakeHost` (exe console) qui écrit des NDJSON `hello`/`snapshot`/`state`/`error` ; tester le client sans sim.

### 6.3 Tests d'intégration

- Marqués `[Trait("Category", "Integration")]`.
- Skip si la variable d'environnement `UNIONFLIGHT_SIM` n'est pas définie.
- Un test par version de sim (X-Plane 11/12 + 5 hosts), qui vérifie :
  - probe disponible ;
  - snapshot non nul ;
  - latitude/longitude dans une plage valide ;
  - pause détectée ;
  - reconnexion après quit.

### 6.4 CI

- **Linux :** build + tests unitaires Core/XPlane. Pas de hosts SimConnect.
- **Windows :** build complet (dont les 5 hosts) + tests unitaires. Les tests d'intégration sont manuels.
- **Release :** tag → pack → push NuGet.
- **Qualité :** `dotnet format --verify-no-changes`, `dotnet list package --vulnerable`, `TreatWarningsAsErrors`.

---

## 7. Risques et mitigations

| Risque | Impact | Mitigation |
|---|---|---|
| Versions de `SimConnect.dll` (MSFS 2020/2024, P3D v4/v5/v6) | Élevé | **Un process hôte par version**, détection du processus, jamais deux paires de DLL dans le même process. |
| Redistribution des DLL SimConnect (licences MS/Lockheed) | Élevé | Ne pas commiter/shipper les DLL sans EULA ; `HintPath` configurable ; script `fetch-simconnect.ps1` ; documenter le prérequis runtime. |
| Wrapper managé .NET Framework sur .NET 10 | Moyen | Test précoce sur Windows ; fallback `net48` pour le host ; le protocole IPC ne change pas. |
| Gestion des process hôtes (crash, orphelin, timeout) | Moyen | Named pipes, watchdog parent, `Kill(entireProcessTree)`, tests de crash/timeout. |
| Web API X-Plane localhost-only | Moyen | UDP fallback pour le LAN ; documenter la limitation. |
| XP11 sans Web API | Moyen | UDP fallback obligatoire ; `acf_descrip` au lieu de `acf_ui_name`. |
| Datarefs statiques non poussés | Moyen | Lire les chaînes via REST à la connexion ; ne pas dépendre du subscription pour les valeurs statiques. |
| Mises à jour des sims qui changent les datarefs/SimVars | Moyen | Table de mapping versionnée ; probe de capabilities ; champs non supportés à `null`. |
| Licence GPL-3.0 actuelle | Faible | **Décision : MIT.** Remplacer `LICENSE`, `PackageLicenseExpression`, en-têtes. |
| Pas de machine Windows/sims sur le VPS | Élevé pour la validation | Tests manuels sur une machine Windows ; CI Windows pour le build ; fixtures pour le unitaire. |
| VPS 3,7 Go RAM, pas de sudo | Faible | SDK dans `~/.dotnet` ; pas de build MSFS/P3D sur le VPS. |
| `dynamic` et singletons existants | Moyen | Remplacement progressif ; ne pas conserver l'API publique actuelle. |

---

## 8. Questions ouvertes

Décidés le 2026-11-19 : toutes les versions, MIT, lecture seule.

1. **Distribution :** un seul package `UnionFlight.Windows` (client + 5 hosts) ou des packages séparés `UnionFlight.Msfs` / `UnionFlight.P3D` + un méta ?
2. **Cibles :** `net10.0` seulement, ou aussi `netstandard2.0` / `net48` pour des add-ons .NET Framework ou Unity ?
3. **P3D v1-v3 / FSX :** hors périmètre (32-bit), confirmé ? Si non, il faut un host x86 séparé.
4. **Tests d'intégration :** as-tu une machine Windows avec MSFS 2020/2024, P3D v4/v5/v6 et X-Plane 11/12 pour valider les 5 hosts ?
5. **Mode distant :** le client peut-il tourner sur le VPS Linux et parler à un host Windows via TCP, ou tout doit rester sur la même machine ?

---

## 9. Sources

- [X-Plane local Web API](https://developer.x-plane.com/article/x-plane-web-api/) — doc officielle, mise à jour 2026-10-20.
- [X-Plane Web APIs](https://developer.x-plane.com/docs/web-apis/) — index.
- [X-Plane dataref `sim/time/paused`](https://flightsimcoach.com/x-plane-datarefs/sim/time/paused/) — type, accès, sémantique.
- [X-Plane dataref `sim/time/sim_speed`](https://flightsimcoach.com/x-plane-datarefs/sim/time/sim_speed/) — 0 = pause, 1 = temps réel.
- [X-Plane datarefs Views/Graphics](https://flightsimcoach.com/x-plane-datarefs/views-graphics/) — `acf_tailnum` byte[40], `acf_ui_name` byte[250] XP12.
- [Forum X-Plane — `acf_ui_name` non poussé](https://forums.x-plane.org/forums/topic/340871-dataref-simaircraftviewacf_ui_name-mostly-unavailable/) — déc. 2025.
- [MSFS 2024 SimConnect SDK](https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/simconnect-sdk/) — doc officielle.
- [MSFS 2024 — Managed code](https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/programming-simconnect-clients-using-managed-code/) — wrapper .NET.
- [MSFS — `SimConnect_SubscribeToSystemEvent`](https://docs.flightsimulator.com/html/Programming_Tools/SimConnect/API_Reference/Events_And_Data/SimConnect_SubscribeToSystemEvent.htm) — `Sim`, `Pause`, `Pause_EX1`, `FlightLoaded`.
- [DevSupport MSFS — DLL 2020+2024](https://devsupport.flightsimulator.com/t/2020-2024-compatible-microsoft-flightsimulator-simconnect-dll/11844) — déc. 2024.
- [Prepar3D SDK](https://www.prepar3d.com/sdk/) — v6 6.1, compatibilité SimConnect.
- [Prepar3D v6 — Managed SimConnect](https://prepar3d.com/SDKv6/sdk/simconnect_api/managed_simconnect_projects.html) — .NET Framework 4.8.
- [FSDeveloper — Managed SimConnect multi-P3D](https://www.fsdeveloper.com/forum/threads/managed-simconnect-support-multiple-versions-of-p3d.447537/) — DLL non interchangeables v4/v5.
- [FSDeveloper — détection pause MSFS](https://www.fsdeveloper.com/forum/threads/with-simconnect-detect-when-im-not-in-flight.456748/) — events `Pause_EX1`, `SimStart`, `SimStop`.
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) — .NET 8/9 EOL 2026-11-10, .NET 10 LTS jusqu'au 2028-11-14.
- [Blog .NET — fin de support .NET 8/9](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
- [NuGet `SimConnect.NET`](https://www.nuget.org/packages/SimConnect.NET/) — 0.2.2, MIT, net8.0/net9.0, bundle `SimConnect.dll`.
- [GitHub `stopbars/SimConnect.NET`](https://github.com/stopbars/SimConnect.NET).
- [NuGet `XPlaneConnector`](https://www.nuget.org/packages/XPlaneConnector/) — 1.3.0, netstandard2.0, 2021.
- [GitHub `bm98/FSimAdapter20_24`](https://github.com/bm98/FSimAdapter20_24) — adaptateur 2020/2024, GPL-3.0, .NET Framework 4.8.
