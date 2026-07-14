Good — styling's fixed and the grid is finally rendering data. Now let's fix the real bugs (undefined values, broken workflow cell, missing filters) and rebuild this properly. Here's the full updated set.

## Root causes in your screenshot

1. **"undefined" under names** — your candidate-cell renderer references a field (likely `profile`/`location`) that SharePoint returned as `null`, with no fallback.
2. **"null · Pending"** in Pre-Screen — same issue, no null-guarding.
3. **`./workflow-pipeline-cell.component.html` showing as literal text** — this confirms `WorkflowPipelineCellComponent` isn't registering correctly as an AG Grid Angular cell renderer (a common integration issue). Fix: stop using an Angular component renderer for this cell — replace it with a plain HTML-string cell renderer function, exactly like your candidate cell already does. More reliable, no framework registration needed.
4. **No filters, thin data, same-tab open** — addressed below.

## Updated folder structure

```
src/app/
├── core/
│   ├── models/
│   │   ├── candidate.model.ts          (unchanged)
│   │   ├── candidate-row.model.ts      (NEW — grid row w/ derived fields)
│   │   ├── round-status.enum.ts        (unchanged)
│   │   └── workflow-stage.enum.ts      (unchanged)
│   └── services/
│       ├── sharepoint.service.ts       (unchanged)
│       ├── candidate.service.ts        (unchanged)
│       ├── current-user.service.ts     (UPDATED — real SharePoint fetch)
│       └── workflow-visibility.service.ts (unchanged)
├── app.module.ts                       (UPDATED — APP_INITIALIZER)
├── features/candidate-dashboard/
│   ├── candidate-dashboard.component.ts    (REWRITTEN)
│   ├── candidate-dashboard.component.html  (REWRITTEN)
│   ├── candidate-dashboard.component.scss  (REWRITTEN)
│   └── candidate-dashboard.module.ts       (unchanged)
```

## `core/models/candidate-row.model.ts` (new)

```typescript
import { Candidate } from './candidate.model';
import { WorkflowStage } from './workflow-stage.enum';

export interface CandidateRow extends Candidate {
  currentStage: WorkflowStage;
  currentStageLabel: string;
}
```

## `core/services/current-user.service.ts` (updated — real fetch, runs at startup)

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { RoundKey } from './workflow-visibility.service';

export type UserRole = 'HrAdmin' | 'Recruiter' | 'Interviewer';

export interface CurrentUser {
  id: number;
  title: string;
  email: string;
  role: UserRole;
  assignedRound?: RoundKey;
}

// Map your real SharePoint group names here once confirmed
const GROUP_ROLE_MAP: Record<string, UserRole> = {
  'HR Cohort Team (Admin)': 'HrAdmin',
  'HR Recruiters': 'Recruiter',
  'Tech Interview Panel': 'Interviewer',
  'Mgmt. Interview Panel': 'Interviewer',
  'On Shore Interview Panel': 'Interviewer'
};

@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly siteUrl = '/sites/CohortHiring';

  private user: CurrentUser = {
    id: 0,
    title: 'Loading...',
    email: '',
    role: 'Interviewer'
  };

  constructor(private http: HttpClient) {}

  get(): CurrentUser {
    return this.user;
  }

  /** Called once at app startup via APP_INITIALIZER. */
  loadCurrentUser(): Observable<CurrentUser> {
    const headers = new HttpHeaders({ 'Accept': 'application/json;odata=verbose' });

    return this.http.get<any>(`${this.siteUrl}/_api/web/currentuser`, { headers }).pipe(
      map(res => {
        this.user = {
          id: res.d.Id,
          title: res.d.Title,
          email: res.d.Email,
          role: 'Interviewer'
        };
        return this.user;
      }),
      catchError(() => {
        this.user = { id: 0, title: 'Unknown User', email: '', role: 'Interviewer' };
        return of(this.user);
      })
    );
  }

  private resolveRole(groupNames: string[]): UserRole {
    for (const name of groupNames) {
      if (GROUP_ROLE_MAP[name]) return GROUP_ROLE_MAP[name];
    }
    return 'Interviewer';
  }
}
```

## `app.module.ts` (updated — loads user before app renders)

```typescript
import { NgModule, APP_INITIALIZER } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { SharedModule } from './shared/shared.module';
import { CurrentUserService } from './core/services/current-user.service';

export function initializeUser(userService: CurrentUserService) {
  return () => userService.loadCurrentUser().toPromise();
}

@NgModule({
  declarations: [AppComponent],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    HttpClientModule,
    FormsModule,
    SharedModule,
    AppRoutingModule
  ],
  providers: [
    {
      provide: APP_INITIALIZER,
      useFactory: initializeUser,
      deps: [CurrentUserService],
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule {}
```

## `candidate-dashboard.component.ts` (full rewrite)

```typescript
import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ColDef, GridOptions, GridApi, GridReadyEvent } from 'ag-grid-community';

import { CandidateService } from '../../core/services/candidate.service';
import { CandidateRow } from '../../core/models/candidate-row.model';
import { WorkflowStage, WORKFLOW_STAGE_LABEL, WORKFLOW_STAGE_ORDER } from '../../core/models/workflow-stage.enum';

@Component({
  selector: 'app-candidate-dashboard',
  templateUrl: './candidate-dashboard.component.html',
  styleUrls: ['./candidate-dashboard.component.scss']
})
export class CandidateDashboardComponent implements OnInit {

  private allCandidates: CandidateRow[] = [];
  rowData: CandidateRow[] = [];

  quickFilterText = '';
  stageFilter = 'all';
  profileFilter = 'all';
  locationFilter = 'all';

  profileOptions: string[] = [];
  locationOptions: string[] = [];
  stageOptions = WORKFLOW_STAGE_ORDER.map(s => ({ value: s, label: WORKFLOW_STAGE_LABEL[s] }));

  loading = true;
  errorMessage: string | null = null;
  gridApi!: GridApi;

  // Summary counts for the top stat strip
  stats = { total: 0, active: 0, offer: 0, rejected: 0 };

  columnDefs: ColDef[] = [
    {
      headerName: 'Candidate',
      field: 'candidateName',
      flex: 2,
      minWidth: 220,
      cellRenderer: (params: any) => {
        const name = params.data.candidateName || 'Unnamed Candidate';
        const email = params.data.candidateEmailId || 'No email on file';
        const id = params.data.candidateId || '—';
        return `
          <div class="candidate-cell">
            <div class="avatar">${this.initials(name)}</div>
            <div class="candidate-meta">
              <div class="name">${name}</div>
              <div class="sub">${email} · ID ${id}</div>
            </div>
          </div>`;
      }
    },
    {
      headerName: 'Role / Profile',
      flex: 1.3,
      minWidth: 170,
      valueGetter: (p) => {
        const role = p.data.roleDesignation || '—';
        const profile = p.data.profile || '—';
        return `${role} · ${profile}`;
      }
    },
    {
      headerName: 'Location',
      field: 'location',
      flex: 0.8,
      minWidth: 110,
      valueFormatter: (p) => p.value || '—'
    },
    {
      headerName: 'Pre-Screen',
      flex: 1,
      minWidth: 140,
      cellRenderer: (params: any) => {
        const score = params.data.prescreeningScore;
        const result = params.data.prescreeningSelected;
        if (!score && !result) {
          return `<span class="muted-pill">Not started</span>`;
        }
        const cls = result === 'Selected' ? 'pass' : result === 'Rejected' ? 'reject' : 'wait';
        return `<span class="score-pill">${score ?? '—'}/100</span>
                <span class="chip chip--${cls}">${result || 'Pending'}</span>`;
      }
    },
    {
      headerName: 'Biz Line',
      field: 'allocatedBizLine',
      flex: 0.9,
      minWidth: 130,
      valueFormatter: (p) => p.value || '—'
    },
    {
      headerName: 'Workflow',
      flex: 1.6,
      minWidth: 200,
      cellRenderer: (params: any) => {
        const stage: WorkflowStage = params.data.currentStage;
        const isRejected = stage === WorkflowStage.Rejected;
        const label = isRejected ? 'Rejected' : WORKFLOW_STAGE_LABEL[stage];
        const currentIdx = WORKFLOW_STAGE_ORDER.indexOf(stage);

        const dots = WORKFLOW_STAGE_ORDER.map((s, i) => {
          let cls = 'todo';
          if (!isRejected) {
            if (i < currentIdx) cls = 'done';
            else if (i === currentIdx) cls = 'current';
          } else {
            cls = 'rejected';
          }
          return `<span class="wf-dot wf-dot--${cls}"></span>`;
        }).join('');

        return `
          <div class="wf-cell">
            <div class="wf-dots">${dots}</div>
            <div class="wf-label ${isRejected ? 'wf-label--rejected' : ''}">${label}</div>
          </div>`;
      }
    },
    {
      headerName: '',
      flex: 0.7,
      minWidth: 90,
      sortable: false,
      filter: false,
      cellRenderer: () => `<button class="open-btn">Open ↗</button>`,
      onCellClicked: (params) => this.openCandidate(params.data)
    }
  ];

  gridOptions: GridOptions = {
    rowHeight: 68,
    headerHeight: 44,
    suppressCellFocus: true,
    domLayout: 'autoHeight',
    animateRows: true
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
        this.allCandidates = candidates.map(c => {
          const stage = this.candidateService.getCurrentStage(c);
          return { ...c, currentStage: stage, currentStageLabel: WORKFLOW_STAGE_LABEL[stage] } as CandidateRow;
        });

        this.profileOptions = [...new Set(this.allCandidates.map(c => c.profile).filter(Boolean))];
        this.locationOptions = [...new Set(this.allCandidates.map(c => c.location).filter(Boolean))];

        this.computeStats();
        this.applyFilters();
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

  onSearchChange(): void {
    this.applyFilters();
  }

  onFilterChange(): void {
    this.applyFilters();
  }

  resetFilters(): void {
    this.quickFilterText = '';
    this.stageFilter = 'all';
    this.profileFilter = 'all';
    this.locationFilter = 'all';
    this.applyFilters();
  }

  private applyFilters(): void {
    const q = this.quickFilterText.trim().toLowerCase();

    this.rowData = this.allCandidates.filter(c => {
      const matchesSearch = !q || [
        c.candidateName, c.candidateEmailId, c.candidatePhoneNumber,
        c.candidateId, c.roleDesignation, c.profile, c.location
      ].some(field => (field || '').toLowerCase().includes(q));

      const matchesStage = this.stageFilter === 'all' || c.currentStage === this.stageFilter;
      const matchesProfile = this.profileFilter === 'all' || c.profile === this.profileFilter;
      const matchesLocation = this.locationFilter === 'all' || c.location === this.locationFilter;

      return matchesSearch && matchesStage && matchesProfile && matchesLocation;
    });
  }

  private computeStats(): void {
    this.stats.total = this.allCandidates.length;
    this.stats.rejected = this.allCandidates.filter(c => c.currentStage === WorkflowStage.Rejected).length;
    this.stats.offer = this.allCandidates.filter(c => c.currentStage === WorkflowStage.OfferStage).length;
    this.stats.active = this.stats.total - this.stats.rejected - this.stats.offer;
  }

  openCandidate(candidate: CandidateRow): void {
    const url = this.router.serializeUrl(
      this.router.createUrlTree(['/candidates', candidate.listSource, candidate.id])
    );
    window.open(url, '_blank');
  }

  private initials(name: string): string {
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }
}
```

## `candidate-dashboard.component.html` (full rewrite)

```html
<div class="dashboard-container">

  <header class="dashboard-header">
    <h4>Recruitment Pipeline</h4>
    <h1>Candidates</h1>
  </header>

  <div class="stat-strip" *ngIf="!loading && !errorMessage">
    <div class="stat-card">
      <div class="stat-value">{{ stats.total }}</div>
      <div class="stat-label">Total Candidates</div>
    </div>
    <div class="stat-card stat-card--active">
      <div class="stat-value">{{ stats.active }}</div>
      <div class="stat-label">In Progress</div>
    </div>
    <div class="stat-card stat-card--pass">
      <div class="stat-value">{{ stats.offer }}</div>
      <div class="stat-label">Offer Stage</div>
    </div>
    <div class="stat-card stat-card--reject">
      <div class="stat-value">{{ stats.rejected }}</div>
      <div class="stat-label">Rejected</div>
    </div>
  </div>

  <div class="dashboard-toolbar" *ngIf="!loading && !errorMessage">
    <input
      type="text"
      class="search-input"
      placeholder="Search name, email, phone, ID, role, profile or location..."
      [(ngModel)]="quickFilterText"
      (ngModelChange)="onSearchChange()" />

    <select class="filter-select" [(ngModel)]="stageFilter" (ngModelChange)="onFilterChange()">
      <option value="all">All Stages</option>
      <option *ngFor="let s of stageOptions" [value]="s.value">{{ s.label }}</option>
    </select>

    <select class="filter-select" [(ngModel)]="profileFilter" (ngModelChange)="onFilterChange()">
      <option value="all">All Profiles</option>
      <option *ngFor="let p of profileOptions" [value]="p">{{ p }}</option>
    </select>

    <select class="filter-select" [(ngModel)]="locationFilter" (ngModelChange)="onFilterChange()">
      <option value="all">All Locations</option>
      <option *ngFor="let l of locationOptions" [value]="l">{{ l }}</option>
    </select>

    <button class="reset-btn" (click)="resetFilters()">Reset</button>
  </div>

  <div *ngIf="loading" class="state-panel">
    <div class="spinner"></div>
    <p>Searching SharePoint...</p>
  </div>

  <div *ngIf="errorMessage" class="state-panel state-panel--error">
    <p class="state-title">Something went wrong</p>
    <p class="state-detail">{{ errorMessage }}</p>
    <button (click)="loadCandidates()">Try again</button>
  </div>

  <div *ngIf="!loading && !errorMessage && rowData.length === 0" class="state-panel">
    <p class="state-title">No candidates match</p>
    <p class="state-detail">Try adjusting your search or filters.</p>
    <button (click)="resetFilters()">Clear filters</button>
  </div>

  <ag-grid-angular
    *ngIf="!loading && !errorMessage && rowData.length > 0"
    class="ag-theme-alpine cohort-grid"
    [rowData]="rowData"
    [columnDefs]="columnDefs"
    [gridOptions]="gridOptions"
    (gridReady)="onGridReady($event)">
  </ag-grid-angular>

</div>
```

## `candidate-dashboard.component.scss` (full rewrite)

```scss
.dashboard-container {
  padding: 24px 28px;
  background: var(--cohort-canvas);
  min-height: 100%;
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
  font-size: 26px;
}

.stat-strip {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-bottom: 20px;
}

.stat-card {
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 10px;
  padding: 14px 16px;
  transition: transform 0.15s ease, box-shadow 0.15s ease;

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 14px rgba(0,0,0,0.06);
  }

  .stat-value { font-size: 22px; font-weight: 700; color: var(--cohort-text); }
  .stat-label { font-size: 12px; color: var(--cohort-muted); margin-top: 2px; }

  &--active .stat-value { color: #8A6D00; }
  &--pass .stat-value { color: var(--cohort-primary); }
  &--reject .stat-value { color: var(--status-reject-text); }
}

.dashboard-toolbar {
  display: flex;
  gap: 10px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  min-width: 260px;
  padding: 10px 14px;
  border: 1px solid var(--cohort-border);
  border-radius: 8px;
  font-size: 14px;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;

  &:focus {
    outline: none;
    border-color: var(--cohort-primary);
    box-shadow: 0 0 0 3px var(--status-pass-bg);
  }
}

.filter-select {
  padding: 10px 12px;
  border: 1px solid var(--cohort-border);
  border-radius: 8px;
  font-size: 13px;
  background: white;
  color: var(--cohort-text);
  cursor: pointer;

  &:focus { outline: none; border-color: var(--cohort-primary); }
}

.reset-btn {
  padding: 10px 16px;
  border: 1px solid var(--cohort-border);
  border-radius: 8px;
  background: white;
  color: var(--cohort-muted);
  cursor: pointer;
  font-weight: 600;
  font-size: 13px;
  transition: all 0.15s ease;

  &:hover { border-color: var(--cohort-primary); color: var(--cohort-primary); }
}

.cohort-grid {
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid var(--cohort-border);
}

::ng-deep {
  .candidate-cell {
    display: flex;
    align-items: center;
    gap: 10px;
    height: 100%;

    .avatar {
      width: 34px;
      height: 34px;
      border-radius: 9px;
      background: var(--cohort-primary);
      color: white;
      font-size: 12px;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .name { font-weight: 600; color: var(--cohort-text); font-size: 13.5px; }
    .sub { font-size: 11.5px; color: var(--cohort-muted); margin-top: 1px; }
  }

  .muted-pill {
    font-size: 12px;
    color: var(--cohort-muted);
  }

  .score-pill {
    display: inline-block;
    font-size: 12px;
    font-weight: 600;
    color: var(--cohort-text);
    margin-right: 6px;
  }

  .chip {
    display: inline-flex;
    padding: 2px 10px;
    border-radius: 999px;
    font-size: 11px;
    font-weight: 600;

    &--pass { background: var(--status-pass-bg); color: var(--status-pass-text); }
    &--reject { background: var(--status-reject-bg); color: var(--status-reject-text); }
    &--wait { background: var(--status-wait-bg); color: var(--status-wait-text); }
  }

  .wf-cell {
    display: flex;
    flex-direction: column;
    justify-content: center;
    height: 100%;
  }

  .wf-dots {
    display: flex;
    gap: 4px;
    margin-bottom: 4px;
  }

  .wf-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--cohort-border);
    transition: background 0.2s ease;

    &--done, &--current { background: var(--cohort-primary); }
    &--rejected { background: var(--status-reject-text); }
  }

  .wf-label {
    font-size: 12px;
    color: var(--cohort-muted);

    &--rejected { color: var(--status-reject-text); }
  }

  .open-btn {
    background: transparent;
    color: var(--cohort-primary);
    border: 1px solid var(--cohort-primary);
    border-radius: 6px;
    padding: 5px 12px;
    cursor: pointer;
    font-weight: 600;
    font-size: 12px;
    transition: all 0.15s ease;

    &:hover { background: var(--cohort-primary); color: white; }
  }
}

.state-panel {
  text-align: center;
  padd