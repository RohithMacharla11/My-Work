Cohort-Hiring/
└── src/app/
    ├── core/
    │   ├── config/
    │   │   └── cohort.config.ts       — central constants
    │   ├── guards/
    │   │   └── access.guard.ts
    │   ├── models/
    │   │   └── candidate.model.ts
    │   └── services/
    │       ├── sharepoint-context.service.ts
    │       ├── sharepoint.service.ts   — generic REST engine
    │       ├── current-user.service.ts
    │       ├── odata-filter.service.ts
    │       ├── lookup.service.ts
    │       ├── candidate.service.ts
    │       └── workflow.service.ts     — access/state engine
    ├── features/
    │   ├── dashboard/
    │   ├── candidate-detail/
    │   │   └── panels/
    │   │       ├── tech-round-panel/
    │   │       ├── mgmt-round-panel/
    │   │       ├── onshore-round-panel/
    │   │       └── hr-round-panel/
    │   └── no-access.component.ts
    ├── shared/
    │   └── people-picker/
    ├── app-routing.module.ts, app.component.*, app.config.ts
    └── (root) index.html, main.ts, styles.scss