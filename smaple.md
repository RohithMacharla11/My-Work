Based on the screenshots, here's the reconstructed file structure of your Angular project:
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
│   │   │   │   └── access.guard.ts
│   │   │   ├── models/
│   │   │   │   └── candidate.model.ts
│   │   │   └── services/
│   │   │       ├── candidate.service.spec.ts
│   │   │       ├── candidate.service.ts
│   │   │       ├── current-user.service.spec.ts
│   │   │       ├── current-user.service.ts
│   │   │       ├── lookup.service.spec.ts
│   │   │       ├── lookup.service.ts
│   │   │       ├── odata-filter.service.spec.ts
│   │   │       ├── odata-filter.service.ts
│   │   │       ├── sharepoint-context.service.spec.ts
│   │   │       ├── sharepoint-context.service.ts
│   │   │       ├── sharepoint.service.spec.ts
│   │   │       ├── sharepoint.service.ts
│   │   │       ├── workflow.service.spec.ts
│   │   │       └── workflow.service.ts
│   │   │
│   │   ├── features/
│   │   │   ├── candidate-detail/
│   │   │   │   ├── panels/
│   │   │   │   │   ├── hr-round-panel/
│   │   │   │   │   │   ├── hr-round-panel.component.html
│   │   │   │   │   │   ├── hr-round-panel.component.scss
│   │   │   │   │   │   ├── hr-round-panel.component.spec.ts
│   │   │   │   │   │   └── hr-round-panel.component.ts
│   │   │   │   │   │
│   │   │   │   │   ├── mgmt-round-panel/
│   │   │   │   │   │   ├── mgmt-round-panel.component.html
│   │   │   │   │   │   ├── mgmt-round-panel.component.scss
│   │   │   │   │   │   ├── mgmt-round-panel.component.spec.ts
│   │   │   │   │   │   └── mgmt-round-panel.component.ts
│   │   │   │   │   │
│   │   │   │   │   ├── onshore-round-panel/
│   │   │   │   │   │   ├── onshore-round-panel.component.html
│   │   │   │   │   │   ├── onshore-round-panel.component.scss
│   │   │   │   │   │   ├── onshore-round-panel.component.spec.ts
│   │   │   │   │   │   └── onshore-round-panel.component.ts
│   │   │   │   │   │
│   │   │   │   │   ├── tech-round-panel/
│   │   │   │   │   │   ├── tech-round-panel.component.html
│   │   │   │   │   │   ├── tech-round-panel.component.scss
│   │   │   │   │   │   ├── tech-round-panel.component.spec.ts
│   │   │   │   │   │   └── tech-round-panel.component.ts
│   │   │   │   │   │
│   │   │   │   │   └── round-panel.shared.scss
│   │   │   │   │
│   │   │   │   ├── candidate-detail.component.html
│   │   │   │   ├── candidate-detail.component.scss
│   │   │   │   ├── candidate-detail.component.spec.ts
│   │   │   │   └── candidate-detail.component.ts
│   │   │   │
│   │   │   ├── dashboard/
│   │   │   │   ├── dashboard.component.html
│   │   │   │   ├── dashboard.component.scss
│   │   │   │   ├── dashboard.component.spec.ts
│   │   │   │   └── dashboard.component.ts
│   │   │   │
│   │   │   ├── redirect/
│   │   │   │   ├── redirect.component.html
│   │   │   │   ├── redirect.component.scss
│   │   │   │   ├── redirect.component.spec.ts
│   │   │   │   ├── redirect.component.ts
│   │   │   │   └── no-access.component.ts
│   │   │   │
│   │   │   └── shared/
│   │   │       └── people-picker/
│   │   │           ├── people-picker.component.html
│   │   │           ├── people-picker.component.scss
│   │   │           ├── people-picker.component.spec.ts
│   │   │           └── people-picker.component.ts
│   │   │
│   │   ├── app-routing.module.ts
│   │   ├── app.component.html
│   │   ├── app.component.scss
│   │   ├── app.component.spec.ts
│   │   ├── app.component.ts
│   │   └── app.config.ts
│   │
│   ├── index.html
│   ├── main.ts
│   └── styles.scss
│
├── .editorconfig
├── angular.json
├── package.json
├── package-lock.json
├── README.md
├── tsconfig.app.json
├── tsconfig.json
├── tsconfig.spec.json
└── .gitignore
```
This is the complete structure visible from your screenshots. A few hidden folders/files (if any were collapsed) may not be included, but everything shown in the Explorer has been listed.