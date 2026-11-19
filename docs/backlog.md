# UnionFlight — backlog d'exécution

Board kanban : `unionflight` (racine `P` = `t_fd43c65c`).

Ce document est le backlog de référence ; le board porte les statuts, les dépendances et les workers. Les corps de cartes contiennent les livrables et les critères d'acceptation.

## Definition of done du programme

- Les 5 hosts SimConnect (MSFS 2020/2024, P3D v4/v5/v6) sont packagés, compilés sur Windows et validés sur simulateur réel.

- Le package `UnionFlight` / `UnionFlight.Windows` est publié en `0.1.0-alpha` sur NuGet.

- CI Linux + Windows verte ; tests end-to-end X-Plane et IPC exécutés à chaque push.

- Les checklists live X-Plane, MSFS et P3D sont documentées et exécutées.

- Le périmètre reste **lecture seule** ; aucune écriture dataref/SimVar/commande.

## Chemin critique

`A1 → A2 → A3 → A4 → D1 → D2 → D3 → D5`

Le plus court chemin vers un résultat MSFS/P3D visible : `A1 → A2 → A3 → A4 → B1 → B2` (MSFS 2024) ou `A1 → A2 → A3 → A4 → C1 → C2` (P3D v6).

## Cartes

### A — Socle SimConnect (Linux, testable sans Windows)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| A1 | `t_fd2e52f2` | Contrat IPC host SimConnect | — | running |
| A2 | `t_ed0b866b` | Runner host + session factice | A1 | todo |
| A3 | `t_a81cfca0` | Client SimConnectHostProvider + détection | A1, A2 | todo |
| A4 | `t_af34417b` | Tests IPC end-to-end + faux host exe | A3 | todo |

### B — MSFS 2020/2024 (Windows + SDK)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| B1 | `t_e9f22597` | Session MSFS partagée (2020/2024) | A1, W1 | blocked |
| B2 | `t_ed424913` | Host MSFS 2024 + validation live | B1, A4 | blocked |
| B3 | `t_2a9c8859` | Host MSFS 2020 + validation live | B1, A4 | blocked |
| B4 | `t_94dcdbb9` | Checklist et tests structures MSFS | B2, B3 | blocked |

### C — Prepar3D v4/v5/v6 (Windows + SDK)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| C1 | `t_9bf7018e` | Session P3D partagée (v4/v5/v6) | A1, W1 | blocked |
| C2 | `t_dd6e0fee` | Host P3D v6 + validation live | C1, A4 | blocked |
| C3 | `t_b606f893` | Host P3D v5 + validation live | C1, A4 | blocked |
| C4 | `t_7b820205` | Host P3D v4 + validation live | C1, A4 | blocked |
| C5 | `t_4095dbe7` | Checklist et tests structures P3D | C2, C3, C4 | blocked |

### D — Packaging et release

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| D1 | `t_c7ef50d1` | Métadonnées NuGet et documentation | A4 | todo |
| D2 | `t_7d4f4396` | Meta-package UnionFlight.Windows + 5 hosts | D1, B2, B3, C2, C3, C4 | blocked |
| D3 | `t_ce04b9b1` | Pack local + installation scratch | D2 | todo |
| D4 | `t_d8b55031` | CI Windows + release workflow | D2 | todo |
| D5 | `t_afd1efb7` | Publier 0.1.0-alpha sur NuGet | D3, D4 | blocked |

### E — Durcissement et documentation

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| E1 | `t_ac27fafb` | LoggerMessage + docs XML + analyzers | A4 | todo |
| E2 | `t_a6f15910` | Fuzzing/property tests parseurs | A4 | todo |
| E3 | `t_3f9e9bc5` | Profiling allocations | A4 | todo |
| E4 | `t_84363a64` | Multi-sim simultané (optionnel) | A4, B4, C5 | blocked |
| E5 | `t_977de96d` | Docs versions/capabilities + troubleshooting | B4, C5 | blocked |

### F — Validation live (simulateurs réels)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| F1 | `t_4ae080bd` | Checklist live X-Plane 11/12 | — | blocked |
| F2 | `t_f47541cd` | Checklist live MSFS 2020/2024 | B2, B3, D3 | blocked |
| F3 | `t_79b4d60d` | Checklist live P3D v4/v5/v6 | C2, C3, C4, D3 | blocked |
| F4 | `t_9fc6b30d` | Scénarios LAN/remote (optionnel) | F1, F2, F3 | blocked |

### W — Infrastructure Windows (prérequis de la lane Windows)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| W1 | `t_9fe780b6` | Runner Windows + SDKs | — | blocked |

## Commandes utiles

```bash
hermes kanban --board unionflight ls
hermes kanban --board unionflight show t_fd2e52f2
hermes kanban --board unionflight dispatch
hermes kanban --board unionflight watch
```

La carte `P` (`t_fd43c65c`) est la carte parapluie ; elle reste ouverte jusqu'à la fin du programme.

## Notes d'exécution

- `blocked` = prérequis humain ou matériel manquant ; `todo` = en attente de dépendances ; `ready` = dispatchable. `unblock <id>` sur une carte dont les parents sont ouverts la met en `todo` ; quand tous les parents sont `done`, le dispatcher la passe en `ready` et lance un worker.

- **Deux lanes :** la lane Linux (`A1 → A2 → A3 → A4 → D1/E1/E2/E3`) avance seule sur le VPS. La lane Windows (`W1 → B1 → B2/B3` et `W1 → C1 → C2/C3/C4`) est bloquée tant que `W1` n'est pas terminée sur une machine Windows avec les SDK et les simulateurs.

- `W1` est la porte d'entrée Windows : ne pas la terminer avant d'avoir compilé les cinq hosts et lancé chaque simulateur. `B1` et `C1` ont `W1` comme parent, donc ils ne peuvent pas être dispatchés par erreur sur le VPS.

- `D5` (publication NuGet) reste bloquée : elle nécessite une clé API NuGet et une décision humaine sur le numéro de version.

- Une carte ne se termine pas tant qu'un enfant est bloqué ; résoudre l'enfant avant le parent.

- Chaque carte doit inclure `[projet=unionflight]` dans son titre (convention de suivi).
