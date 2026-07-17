Here's the complete, exact structure as shown across your screenshots:

```
UI/
└── Cohort-Hiring/
    ├── .angular/
    ├── node_modules/
    ├── public/
    ├── src/
    │   └── app/
    │       ├── core/
    │       │   ├── config/
    │       │   │   └── cohort.config.ts
    │       │   ├── guards/
    │       │   │   └── access.guard.ts
    │       │   ├── models/
    │       │   │   └── candidate.model.ts
    │       │   └── services/
    │       │       ├── candidate.service.spec.ts
    │       │       ├── candidate.service.ts
    │       │       ├── current-user.service.spec.ts
    │       │       ├── current-user.service.ts
    │       │       ├── lookup.service.spec.ts
    │       │       ├── lookup.service.ts
    │       │       ├── odata-filter.service.spec.ts
    │       │       ├── odata-filter.service.ts
    │       │       ├── sharepoint-context.service.spec.ts
    │       │       ├── sharepoint-context.service.ts
    │       │       ├── sharepoint.service.spec.ts
    │       │       ├── sharepoint.service.ts
    │       │       ├── workflow.service.spec.ts
    │       │       └── workflow.service.ts
    │       │
    │       ├── features/
    │       │   ├── candidate-detail/
    │       │   │   ├── panels/
    │       │   │   │   ├── hr-round-panel/
    │       │   │   │   │   ├── hr-round-panel.component.html
    │       │   │   │   │   ├── hr-round-panel.component.scss
    │       │   │   │   │   ├── hr-round-panel.component.spec.ts
    │       │   │   │   │   └── hr-round-panel.component.ts
    │       │   │   │   ├── mgmt-round-panel/
    │       │   │   │   │   ├── mgmt-round-panel.component.html
    │       │   │   │   │   ├── mgmt-round-panel.component.scss
    │       │   │   │   │   ├── mgmt-round-panel.component.spec.ts
    │       │   │   │   │   └── mgmt-round-panel.component.ts
    │       │   │   │   ├── onshore-round-panel/
    │       │   │   │   │   ├── onshore-round-panel.component.html
    │       │   │   │   │   ├── onshore-round-panel.component.scss
    │       │   │   │   │   ├── onshore-round-panel.component.spec.ts
    │       │   │   │   │   └── onshore-round-panel.component.ts
    │       │   │   │   ├── tech-round-panel/
    │       │   │   │   │   ├── tech-round-panel.component.html
    │       │   │   │   │   ├── tech-round-panel.component.scss
    │       │   │   │   │   ├── tech-round-panel.component.spec.ts
    │       │   │   │   │   └── tech-round-panel.component.ts
    │       │   │   │   └── round-panel.shared.scss
    │       │   │   ├── candidate-detail.component.html
    │       │   │   ├── candidate-detail.component.scss
    │       │   │   ├── candidate-detail.component.spec.ts
    │       │   │   └── candidate-detail.component.ts
    │       │   ├── dashboard/
    │       │   │   ├── dashboard.component.html
    │       │   │   ├── dashboard.component.scss
    │       │   │   ├── dashboard.component.spec.ts
    │       │   │   └── dashboard.component.ts
    │       │   └── no-access.component.ts
    │       │
    │       ├── shared/
    │       │   └── people-picker/
    │       │       ├── people-picker.component.html
    │       │       ├── people-picker.component.scss
    │       │       ├── people-picker.component.spec.ts
    │       │       └── people-picker.component.ts
    │       │
    │       ├── app-routing.module.ts
    │       ├── app.component.html
    │       ├── app.component.scss
    │       ├── app.component.spec.ts
    │       ├── app.component.ts
    │       └── app.config.ts
    │
    ├── index.html
    ├── main.ts
    ├── styles.scss
    ├── .editorconfig
    ├── angular.json
    ├── package-lock.json
    ├── package.json
    ├── README.md
    ├── tsconfig.app.json
    ├── tsconfig.json
    ├── tsconfig.spec.json
    └── .gitignore
```

This is a clean, conventional Angular NgModule layout:
- **`core/`** — app-wide singletons (config, guards, models, services) — loaded once, never re-imported into feature modules.
- **`features/`** — routed, self-contained screens (dashboard, candidate-detail with its 4 round panels, no-access).
- **`shared/`** — reusable, dumb UI components used across features (people-picker).

Every component consistently ships with its own `.html`/`.scss`/`.ts`/`.spec.ts` quartet — good discipline, and it matches the structure you kept in Novus. Want me to check whether anything's *missing* against what the workflow/candidate services actually expect (e.g. any panel component not yet wired up, or a service with no spec file)?