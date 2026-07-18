Here's the project structure reconstructed from the three screenshots:

```
Cohort-Hiring/
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
│   │   │   ├── models/
│   │   │   │   └── candidate.model.ts
│   │   │   └── services/
│   │   │       ├── candidate.service.ts (+ .spec.ts)
│   │   │       ├── cohort.service.spec.ts
│   │   │       ├── current-user.service.ts (+ .spec.ts)
│   │   │       ├── lookup.service.ts (+ .spec.ts)
│   │   │       ├── odata-filter.service.ts (+ .spec.ts)
│   │   │       ├── sharepoint-context.service.ts (+ .spec.ts)
│   │   │       ├── sharepoint.service.ts (+ .spec.ts)
│   │   │       └── workflow.service.ts (+ .spec.ts)
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
│   │   │   ├── create-cohort/
│   │   │   │   └── create-cohort.component.{html,scss,ts,spec.ts}
│   │   │   ├── dashboard/
│   │   │   │   └── dashboard.component.{html,scss,ts,spec.ts}
│   │   │   ├── redirect/
│   │   │   │   └── redirect.component.{html,scss,ts,spec.ts}
│   │   │   └── no-access.component.ts
│   │   ├── shared/
│   │   │   └── people-picker/
│   │   │       └── people-picker.component.{html,scss,ts,spec.ts}
│   │   ├── app-routing.module.ts
│   │   ├── app.component.{html,scss,ts,spec.ts}
│   │   └── app.config.ts
│   ├── index.html
│   ├── main.ts
│   └── styles.scss
├── .editorconfig
├── .gitignore
├── angular.json
├── package.json
├── package-lock.json
├── README.md
├── tsconfig.app.json
├── tsconfig.json
└── tsconfig.spec.json
```

A few notes: several files show `M` (modified) or `U` (untracked) badges — `cohort.service.spec.ts`, `create-cohort/*`, `dashboard.component.*`, `tech-round-panel.component.html/scss`, `tsconfig.json`, `app-routing.module.ts`, `people-picker.component.ts` — meaning there are uncommitted changes across those files. Let me know if you want the git status broken out separately.