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
| A1 | `t_fd2e52f2` | Contrat IPC host SimConnect | — | ready |
| A2 | `t_ed0b866b` | Runner host + session factice | A1 | blocked |
| A3 | `t_a81cfca0` | Client SimConnectHostProvider + détection | A1, A2 | blocked |
| A4 | `t_af34417b` | Tests IPC end-to-end + faux host exe | A3 | blocked |

### B — MSFS 2020/2024 (Windows + SDK)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| B1 | `t_e9f22597` | Session MSFS partagée (2020/2024) | A1 | blocked |
| B2 | `t_ed424913` | Host MSFS 2024 + validation live | B1, A4 | blocked |
| B3 | `t_2a9c8859` | Host MSFS 2020 + validation live | B1, A4 | blocked |
| B4 | `t_94dcdbb9` | Checklist et tests structures MSFS | B2, B3 | blocked |

### C — Prepar3D v4/v5/v6 (Windows + SDK)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| C1 | `t_9bf7018e` | Session P3D partagée (v4/v5/v6) | A1 | blocked |
| C2 | `t_dd6e0fee` | Host P3D v6 + validation live | C1, A4 | blocked |
| C3 | `t_b606f893` | Host P3D v5 + validation live | C1, A4 | blocked |
| C4 | `t_7b820205` | Host P3D v4 + validation live | C1, A4 | blocked |
| C5 | `t_4095dbe7` | Checklist et tests structures P3D | C2, C3, C4 | blocked |

### D — Packaging et release

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| D1 | `t_c7ef50d1` | Métadonnées NuGet et documentation | A4 | blocked |
| D2 | `t_7d4f4396` | Meta-package UnionFlight.Windows + 5 hosts | D1, B2, B3, C2, C3, C4 | blocked |
| D3 | `t_ce04b9b1` | Pack local + installation scratch | D2 | blocked |
| D4 | `t_d8b55031` | CI Windows + release workflow | D2 | blocked |
| D5 | `t_afd1efb7` | Publier 0.1.0-alpha sur NuGet | D3, D4 | blocked |

### E — Durcissement et documentation

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| E1 | `t_ac27fafb` | LoggerMessage + docs XML + analyzers | A4 | blocked |
| E2 | `t_a6f15910` | Fuzzing/property tests parseurs | A4 | blocked |
| E3 | `t_3f9e9bc5` | Profiling allocations | A4 | blocked |
| E4 | `t_84363a64` | Multi-sim simultané (optionnel) | A4, B4, C5 | blocked |
| E5 | `t_977de96d` | Docs versions/capabilities + troubleshooting | B4, C5 | blocked |

### F — Validation live (simulateurs réels)

| Carte | Kanban | Tâche | Dépend de | Statut |
|---|---|---|---|---|
| F1 | `t_4ae080bd` | Checklist live X-Plane 11/12 | — | blocked |
| F2 | `t_f47541cd` | Checklist live MSFS 2020/2024 | B2, B3, D3 | blocked |
| F3 | `t_79b4d60d` | Checklist live P3D v4/v5/v6 | C2, C3, C4, D3 | blocked |
| F4 | `t_9fc6b30d` | Scénarios LAN/remote (optionnel) | F1, F2, F3 | blocked |

## Notes d'exécution

- Statut `blocked` = backlog, pas encore dispatchable. `unblock <id>` passe la carte en `ready` ; le dispatcher de la gateway la prend alors automatiquement.

- Les cartes B/C/F2/F3 nécessitent **Windows + SDK MSFS/P3D + simulateur**. Elles ne peuvent pas être exécutées sur le VPS Linux ; il faut un runner Windows ou une machine locale.

- Les cartes A/D1/D3/E1/E2/E3 sont exécutables sur le VPS (Linux, .NET 10).

- Une carte ne se termine pas tant qu'un enfant est bloqué ; résoudre l'enfant avant le parent.

- Chaque carte doit inclure `[projet=unionflight]` dans son titre (convention de suivi).
