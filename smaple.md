Here's the complete, corrected set — all four files, ready to drop in as-is.

## `candidate-dashboard.module.ts`

```typescript
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Routes } from '@angular/router';

import { SharedModule } from '../../shared/shared.module';
import { CandidateDashboardComponent } from './candidate-dashboard.component';

const routes: Routes = [
  { path: '', component: CandidateDashboardComponent }
];

@NgModule({
  declarations: [CandidateDashboardComponent],
  imports: [
    CommonModule,
    FormsModule,
    SharedModule,
    RouterModule.forChild(routes)
  ]
})
export class CandidateDashboardModule {}
```

## `candidate-dashboard.component.ts`

```typescript
import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ColDef, GridOptions, GridApi, GridReadyEvent } from 'ag-grid-community';

import { CandidateService } from '../../core/services/candidate.service';
import { Candidate } from '../../core/models/candidate.model';
import { WORKFLOW_STAGE_LABEL } from '../../core/models/workflow-stage.enum';
import { WorkflowPipelineCellComponent } from '../../shared/components/workflow-pipeline-cell/workflow-pipeline-cell.component';

@Component({
  selector: 'app-candidate-dashboard',
  templateUrl: './candidate-dashboard.component.html',
  styleUrls: ['./candidate-dashboard.component.scss']
})
export class CandidateDashboardComponent implements OnInit {

  rowData: Candidate[] = [];
  quickFilterText = '';
  loading = true;
  errorMessage: string | null = null;

  gridApi!: GridApi;

  columnDefs: ColDef[] = [
    {
      headerName: 'Candidate',
      field: 'candidateName',
      flex: 2,
      cellRenderer: (params: any) => `
        <div class="candidate-cell">
          <div class="avatar">${this.initials(params.data.candidateName)}</div>
          <div>
            <div class="name">${params.data.candidateName}</div>
            <div class="email">${params.data.candidateEmailId}</div>
          </div>
        </div>`
    },
    {
      headerName: 'Role / Profile',
      field: 'roleDesignation',
      flex: 1.2,
      valueGetter: (p) => `${p.data.roleDesignation} — ${p.data.profile}`
    },
    {
      headerName: 'Pre-Screen',
      field: 'prescreeningScore',
      flex: 1,
      valueGetter: (p) => `${p.data.prescreeningScore} · ${p.data.prescreeningSelected}`
    },
    {
      headerName: 'Workflow',
      field: 'currentStage',
      flex: 1.5,
      cellRenderer: WorkflowPipelineCellComponent
    },
    {
      headerName: 'Action',
      flex: 0.8,
      cellRenderer: () => `<button class="open-btn">Open</button>`,
      onCellClicked: (params) => this.openCandidate(params.data)
    }
  ];

  gridOptions: GridOptions = {
    rowHeight: 64,
    headerHeight: 44,
    suppressCellFocus: true,
    domLayout: 'autoHeight'
  };

  constructor(
    private candidateService: CandidateService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadCandidates();
  }

  loadCandidates(): void {
    this.loading = true;
    this.errorMessage = null;

    this.candidateService.getAllCandidates().subscribe({
      next: (candidates) => {
        this.rowData = candidates.map(c => ({
          ...c,
          currentStage: this.candidateService.getCurrentStage(c),
          currentStageLabel: WORKFLOW_STAGE_LABEL[this.candidateService.getCurrentStage(c)]
        })) as any;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'This query is too large for SharePoint to run. A column used in the filter needs to be indexed.';
        this.loading = false;
      }
    });
  }

  onGridReady(params: GridReadyEvent): void {
    this.gridApi = params.api;
  }

  onSearchChange(value: string): void {
    this.gridApi?.setQuickFilter(value);
  }

  openCandidate(candidate: Candidate): void {
    this.router.navigate(['/candidates', candidate.listSource, candidate.id]);
  }

  private initials(name: string): string {
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }
}
```

## `candidate-dashboard.component.html`

```html
<div class="dashboard-container">
  <header class="dashboard-header">
    <h4>Recruitment Pipeline</h4>
    <h1>Candidates</h1>
  </header>

  <div class="dashboard-toolbar">
    <input
      type="text"
      class="search-input"
      placeholder="Search name, email, phone, ID, role, profile or skill..."
      [(ngModel)]="quickFilterText"
      (ngModelChange)="onSearchChange($event)" />
  </div>

  <div *ngIf="loading" class="state-panel">Searching SharePoint...</div>

  <div *ngIf="errorMessage" class="state-panel state-panel--error">
    <p>Something went wrong</p>
    <p class="detail">{{ errorMessage }}</p>
    <button (click)="loadCandidates()">Try again</button>
  </div>

  <ag-grid-angular
    *ngIf="!loading && !errorMessage"
    class="ag-theme-alpine"
    [rowData]="rowData"
    [columnDefs]="columnDefs"
    [gridOptions]="gridOptions"
    (gridReady)="onGridReady($event)">
  </ag-grid-angular>
</div>
```

## `candidate-dashboard.component.scss`

```scss
@import '../../styles/tokens';

.dashboard-container {
  padding: 24px;
  background: var(--cohort-canvas);
}

.dashboard-header h4 {
  color: var(--cohort-muted);
  text-transform: uppercase;
  font-size: 11px;
  letter-spacing: 0.06em;
  margin: 0;
}

.dashboard-header h1 {
  color: var(--cohort-text);
  margin: 4px 0 20px;
}

.search-input {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid var(--cohort-border);
  border-radius: 8px;
  margin-bottom: 16px;
  font-size: 14px;

  &:focus {
    outline: none;
    border-color: var(--cohort-primary);
  }
}

::ng-deep .candidate-cell {
  display: flex;
  align-items: center;
  gap: 10px;
  height: 100%;

  .avatar {
    width: 32px;
    height: 32px;
    border-radius: 8px;
    background: var(--cohort-primary);
    color: white;
    font-size: 12px;
    font-weight: 700;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  .name { font-weight: 600; color: var(--cohort-text); }
  .email { font-size: 12px; color: var(--cohort-muted); }
}

::ng-deep .open-btn {
  background: transparent;
  color: var(--cohort-primary);
  border: 1px solid var(--cohort-primary);
  border-radius: 6px;
  padding: 4px 12px;
  cursor: pointer;
  font-weight: 600;

  &:hover { background: var(--cohort-primary); color: white; }
}

.state-panel {
  text-align: center;
  padding: 60px 0;
  color: var(--cohort-muted);

  &--error { color: var(--status-reject-text); }

  button {
    margin-top: 12px;
    padding: 8px 20px;
    background: var(--cohort-primary);
    color: white;
    border: none;
    border-radius: 6px;
    cursor: pointer;
  }
}
```

Drop these four in exactly as shown, restart `ng serve`, and it should compile clean. If any AG Grid runtime warning shows up in the browser console (version-specific API naming), paste it and I'll match it to your installed version.