Good catch — no, I referenced `SharedModule` in `app.module.ts` but never actually wrote it. Yes, you need it — it's what makes `StatusChipComponent` and `WorkflowPipelineCellComponent` usable across your feature modules without re-declaring them everywhere.

**`shared/shared.module.ts`**

```typescript
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AgGridModule } from 'ag-grid-angular';

import { StatusChipComponent } from './components/status-chip/status-chip.component';
import { WorkflowPipelineCellComponent } from './components/workflow-pipeline-cell/workflow-pipeline-cell.component';

@NgModule({
  declarations: [
    StatusChipComponent,
    WorkflowPipelineCellComponent
  ],
  imports: [
    CommonModule,
    AgGridModule
  ],
  exports: [
    CommonModule,
    AgGridModule,
    StatusChipComponent,
    WorkflowPipelineCellComponent
  ]
})
export class SharedModule {}
```

A few things worth flagging while I'm at it:

- **`WorkflowPipelineCellComponent` implements `ICellRendererAngularComp`**, so it's used by AG Grid as a dynamic cell renderer, not placed directly in a template with `<app-workflow-pipeline-cell>`. It still needs to be declared here so Angular knows about it, but AG Grid instantiates it internally via the `cellRenderer` you set in `columnDefs`.
- `AgGridModule` is imported and re-exported here so `candidate-dashboard.module.ts` doesn't need to import it separately — just import `SharedModule` there.
- Every future feature module (`candidate-detail`, `panel-assignment`) should import `SharedModule`, not redeclare the chip or pipeline cell.

One correction on `candidate-dashboard.module.ts` — I never gave you that one either, and it needs to import `FormsModule` for the `[(ngModel)]` on the search box I used. Want that one now too, or should I fold it into the next thing I build (candidate-detail or panel-assignment)?