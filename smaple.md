Here's the project structure compiled from your screenshots:

```
UI/
└── Cohort-Hiring/
    ├── .angular/
    ├── dist/
    ├── node_modules/
    ├── public/
    ├── src/
    │   ├── app/
    │   │   ├── core/
    │   │   │   ├── config/
    │   │   │   │   └── cohort.config.ts
    │   │   │   ├── guards/
    │   │   │   │   └── access.guard.ts
    │   │   │   ├── models/
    │   │   │   │   └── candidate.model.ts
    │   │   │   └── services/
    │   │   │       ├── candidate.service.ts (+ .spec.ts)
    │   │   │       ├── current-user.service.ts (+ .spec.ts)
    │   │   │       ├── lookup.service.ts (+ .spec.ts)
    │   │   │       ├── odata-filter.service.ts (+ .spec.ts)
    │   │   │       ├── sharepoint-context.service.ts (+ .spec.ts)
    │   │   │       ├── sharepoint.service.ts (+ .spec.ts)
    │   │   │       └── workflow.service.ts (+ .spec.ts)
    │   │   │
    │   │   ├── features/
    │   │   │   ├── candidate-detail/
    │   │   │   │   ├── panels/
    │   │   │   │   │   ├── hr-round-panel/
    │   │   │   │   │   │   └── hr-round-panel.component.{html,scss,ts,spec.ts}
    │   │   │   │   │   ├── mgmt-round-panel/
    │   │   │   │   │   │   └── mgmt-round-panel.component.{html,scss,ts,spec.ts}
    │   │   │   │   │   ├── onshore-round-panel/
    │   │   │   │   │   │   └── onshore-round-panel.component.{html,scss,ts,spec.ts}
    │   │   │   │   │   ├── tech-round-panel/
    │   │   │   │   │   │   └── tech-round-panel.component.{html,scss,ts,spec.ts}
    │   │   │   │   │   └── round-panel.shared.scss
    │   │   │   │   └── candidate-detail.component.{html,scss,ts,spec.ts}
    │   │   │   │
    │   │   │   ├── create-cohort/
    │   │   │   │   └── create-cohort.component.{html,scss,ts,spec.ts}
    │   │   │   │
    │   │   │   ├── dashboard/
    │   │   │   │   └── dashboard.component.{html,scss,ts,spec.ts}
    │   │   │   │
    │   │   │   ├── redirect/
    │   │   │   │   └── redirect.component.{html,scss,ts,spec.ts}
    │   │   │   │
    │   │   │   ├── no-access.component.ts
    │   │   │   │
    │   │   │   └── shared/
    │   │   │       └── people-picker/
    │   │   │           └── people-picker.component.{html,scss,ts,spec.ts}
    │   │   │
    │   │   ├── app-routing.module.ts
    │   │   ├── app.component.{html,scss,ts,spec.ts}
    │   │   └── app.config.ts
    │   │
    │   ├── index.html
    │   ├── main.ts
    │   └── styles.scss
    │
    ├── .editorconfig
    ├── .gitignore
    ├── angular.json
    ├── package.json
    ├── package-lock.json
    ├── tsconfig.json
    ├── tsconfig.app.json
    ├── tsconfig.spec.json
    └── README.md
```

A few notes:
- This is an **Angular** app (standalone-style config with `app.config.ts` + `app-routing.module.ts`).
- Each component follows the standard 4-file pattern: `.html`, `.scss`, `.ts`, `.spec.ts`.
- `core/` holds cross-cutting concerns (SharePoint integration, workflow, lookups, guards, models) while `features/` holds the actual pages/panels (dashboard, cohort creation, candidate detail with round panels, redirect/no-access, and a shared people-picker component).
- Branch shown at the bottom is `Feature/Version1.1`.

Let me know if you want this exported as an actual file (e.g. a markdown tree or a script to scaffold these folders).