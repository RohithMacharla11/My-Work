Good, let's start exactly there — SharePoint fetch → AG Grid dashboard, with everything reusable properly separated. Here's the structure and the code.

## Folder structure

```
src/app/
├── core/
│   ├── models/
│   │   ├── candidate.model.ts
│   │   ├── workflow-stage.enum.ts
│   │   └── round-status.enum.ts
│   └── services/
│       ├── sharepoint.service.ts
│       ├── candidate.service.ts
│       └── workflow-visibility.service.ts
├── shared/
│   ├── components/
│   │   ├── status-chip/
│   │   │   ├── status-chip.component.ts
│   │   │   └── status-chip.component.scss
│   │   └── workflow-pipeline-cell/
│   │       └── workflow-pipeline-cell.component.ts
│   └── shared.module.ts
├── features/
│   └── candidate-dashboard/
│       ├── candidate-dashboard.component.ts
│       ├── candidate-dashboard.component.html
│       ├── candidate-dashboard.component.scss
│       └── candidate-dashboard.module.ts
└── styles/
    └── _tokens.scss
```

## 1. Models — `core/models/`

**`workflow-stage.enum.ts`**
```typescript
export enum WorkflowStage {
  Unassigned = 'Unassigned',
  PreScreen = 'PreScreen',
  TechRound1 = 'TechRound1',
  TechRound2Mgmt = 'TechRound2Mgmt',
  OnShore = 'OnShore',
  HrFinal = 'HrFinal',
  OfferStage = 'OfferStage',
  Rejected = 'Rejected'
}

export const WORKFLOW_STAGE_LABEL: Record<WorkflowStage, string> = {
  [WorkflowStage.Unassigned]: 'Not assigned',
  [WorkflowStage.PreScreen]: 'Pre-Screen',
  [WorkflowStage.TechRound1]: 'Tech Round 1',
  [WorkflowStage.TechRound2Mgmt]: 'Tech 2 / Management',
  [WorkflowStage.OnShore]: 'On-Shore Round',
  [WorkflowStage.HrFinal]: 'HR Round — final',
  [WorkflowStage.OfferStage]: 'Offer stage',
  [WorkflowStage.Rejected]: 'Rejected'
};

// Order matters — used to render the progress pipeline dots
export const WORKFLOW_STAGE_ORDER: WorkflowStage[] = [
  WorkflowStage.PreScreen,
  WorkflowStage.TechRound1,
  WorkflowStage.TechRound2Mgmt,
  WorkflowStage.OnShore,
  WorkflowStage.HrFinal
];
```

**`round-status.enum.ts`**
```typescript
export enum RoundStatus {
  Pending = 'Pending',
  Selected = 'Selected',
  Rejected = 'Rejected',
  Locked = 'Locked',       // previous round not yet Selected
  NotAssigned = 'NotAssigned'
}
```

**`candidate.model.ts`** — mirrors your `InterviewDetailsCT` columns directly, so mapping from SharePoint stays 1:1 and honest to the real schema.

```typescript
import { RoundStatus } from './round-status.enum';

export interface SkillAssessment {
  skillName: string;
  screeningScore: string;
  techRound1Comment: string;
  techRound2Comment: string;
}

export interface InterviewRound {
  interviewedBy: SharePointUser | null;
  interviewDate: string | null;      // ISO date
  interviewFeedback: string | null;
  selection: RoundStatus;
}

export interface SharePointUser {
  id: number;
  title: string;
  email: string;
}

export interface Candidate {
  // SharePoint system
  id: number;                        // SP list item ID
  listSource: 'Chennai' | 'Mumbai';

  // Candidate core fields
  candidateId: string;
  candidateName: string;
  candidateEmailId: string;
  candidatePhoneNumber: string;
  location: string;
  roleDesignation: string;
  profile: string;                   // e.g. "Java Full Stack"
  allocatedBizLine: string;

  // Pre-screening
  prescreeningTestLink: string;
  prescreeningTestDate: string | null;
  prescreeningScore: string;
  prescreeningSelected: RoundStatus;
  prescreeningComments: string;

  // Skills 1–10 (fixed grid — matches SP schema)
  skills: SkillAssessment[];

  // Rounds
  techRound1: InterviewRound;
  techRound2Mgmt: InterviewRound;
  onShoreRound: InterviewRound;
  hrRound: InterviewRound;

  // Admin/system
  cvUpload: string;
  hireproResultsUpload: string;
}
```

## 2. SharePoint fetch service — `core/services/sharepoint.service.ts`

Generic, reusable — not tied to candidates. Any other list in the future uses this too.

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface SpListResponse<T> {
  d: { results: T[] };
}

@Injectable({ providedIn: 'root' })
export class SharePointService {
  private readonly siteUrl = '/sites/ISPLCohort'; // adjust to your site path

  constructor(private http: HttpClient) {}

  getListItems<T>(listName: string, selectFields: string[], expandFields: string[] = [], filter?: string): Observable<T[]> {
    let url = `${this.siteUrl}/_api/web/lists/getbytitle('${listName}')/items`
      + `?$select=${selectFields.join(',')}`;

    if (expandFields.length) {
      url += `&$expand=${expandFields.join(',')}`;
    }
    if (filter) {
      url += `&$filter=${filter}`;
    }

    const headers = new HttpHeaders({
      'Accept': 'application/json;odata=verbose'
    });

    return this.http.get<SpListResponse<T>>(url, { headers }).pipe(
      map(res => res.d.results)
    );
  }

  getListItemById<T>(listName: string, id: number, selectFields: string[], expandFields: string[] = []): Observable<T> {
    let url = `${this.siteUrl}/_api/web/lists/getbytitle('${listName}')/items(${id})`
      + `?$select=${selectFields.join(',')}`;

    if (expandFields.length) {
      url += `&$expand=${expandFields.join(',')}`;
    }

    const headers = new HttpHeaders({ 'Accept': 'application/json;odata=verbose' });
    return this.http.get<{ d: T }>(url, { headers }).pipe(map(res => res.d));
  }
}
```

## 3. Candidate mapping service — `core/services/candidate.service.ts`

This is where raw SharePoint fields → your clean `Candidate` model, and where the **derived status** logic lives (per Rule: status is computed, never stored).

```typescript
import { Injectable } from '@angular/core';
import { Observable, forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import { SharePointService } from './sharepoint.service';
import { Candidate, SkillAssessment } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';
import { WorkflowStage } from '../models/workflow-stage.enum';

const SELECT_FIELDS = [
  'Id', 'CandidateId', 'CandidateName', 'CandidateEmailId', 'CandidatePhoneNumber',
  'Location', 'RoleDesignation', 'Profile', 'AllocatedBizLine',
  'PrescreeningTestLink', 'PrescreeningTestDate', 'PrescreeningScore',
  'PrescreeningSelected', 'PrescreeningComments',
  'SkillOne', 'SkillOneScore', 'SkillOneTROne', 'SkillOneTRTwo',
  'SkillTwo', 'SkillTwoScore', 'SkillTwoTROne', 'SkillTwoTRTwo',
  'SkillThree', 'SkillThreeScore', 'SkillThreeTROne', 'SkillThreeTRTwo',
  'SkillFour', 'SkillFourScore', 'SkillFourTROne', 'SkillFourTRTwo',
  'SkillFive', 'SkillFiveScore', 'SkillFiveTROne', 'SkillFiveTRTwo',
  'TechRound1InterviewDate', 'TechRound1InterviewFeedback', 'TechRound1InterviewSelection',
  'Tech2MgmtRoundInterviewDate', 'Tech2MgmtRoundInterviewFeedback', 'Tech2MgmtRoundInterviewSelection',
  'OnShoreRoundInterviewDate', 'OnShoreRoundInterviewFeedback', 'OnShoreRoundInterviewSelection',
  'HrRoundInterviewDate', 'HrRoundInterviewFeedback', 'HrRoundInterviewSelection',
  'CvUpload', 'HireproResultsUpload'
];

const EXPAND_FIELDS = [
  'TechRound1InterviewedBy', 'Tech2MgmtRoundInterviewedBy',
  'OnShoreRoundInterviewedBy', 'HrRoundInterviewedBy'
];

@Injectable({ providedIn: 'root' })
export class CandidateService {

  constructor(private sp: SharePointService) {}

  getCandidates(source: 'Chennai' | 'Mumbai'): Observable<Candidate[]> {
    const listName = source === 'Chennai' ? 'ChennaiInterviewList' : 'MumbaiInterviewList';

    return this.sp.getListItems<any>(listName, SELECT_FIELDS, EXPAND_FIELDS).pipe(
      map(items => items.map(item => this.mapToCandidate(item, source)))
    );
  }

  getAllCandidates(): Observable<Candidate[]> {
    return forkJoin([
      this.getCandidates('Chennai'),
      this.getCandidates('Mumbai')
    ]).pipe(map(([chennai, mumbai]) => [...chennai, ...mumbai]));
  }

  private mapToCandidate(item: any, source: 'Chennai' | 'Mumbai'): Candidate {
    return {
      id: item.Id,
      listSource: source,
      candidateId: item.CandidateId,
      candidateName: item.CandidateName,
      candidateEmailId: item.CandidateEmailId,
      candidatePhoneNumber: item.CandidatePhoneNumber,
      location: item.Location,
      roleDesignation: item.RoleDesignation,
      profile: item.Profile,
      allocatedBizLine: item.AllocatedBizLine,

      prescreeningTestLink: item.PrescreeningTestLink,
      prescreeningTestDate: item.PrescreeningTestDate,
      prescreeningScore: item.PrescreeningScore,
      prescreeningSelected: this.toRoundStatus(item.PrescreeningSelected),
      prescreeningComments: item.PrescreeningComments,

      skills: this.mapSkills(item),

      techRound1: {
        interviewedBy: this.mapUser(item.TechRound1InterviewedBy),
        interviewDate: item.TechRound1InterviewDate,
        interviewFeedback: item.TechRound1InterviewFeedback,
        selection: this.toRoundStatus(item.TechRound1InterviewSelection)
      },
      techRound2Mgmt: {
        interviewedBy: this.mapUser(item.Tech2MgmtRoundInterviewedBy),
        interviewDate: item.Tech2MgmtRoundInterviewDate,
        interviewFeedback: item.Tech2MgmtRoundInterviewFeedback,
        selection: this.toRoundStatus(item.Tech2MgmtRoundInterviewSelection)
      },
      onShoreRound: {
        interviewedBy: this.mapUser(item.OnShoreRoundInterviewedBy),
        interviewDate: item.OnShoreRoundInterviewDate,
        interviewFeedback: item.OnShoreRoundInterviewFeedback,
        selection: this.toRoundStatus(item.OnShoreRoundInterviewSelection)
      },
      hrRound: {
        interviewedBy: this.mapUser(item.HrRoundInterviewedBy),
        interviewDate: item.HrRoundInterviewDate,
        interviewFeedback: item.HrRoundInterviewFeedback,
        selection: this.toRoundStatus(item.HrRoundInterviewSelection)
      },

      cvUpload: item.CvUpload,
      hireproResultsUpload: item.HireproResultsUpload
    };
  }

  private mapSkills(item: any): SkillAssessment[] {
    const skillKeys = ['One', 'Two', 'Three', 'Four', 'Five']; // extend to Ten as needed
    return skillKeys
      .map(k => ({
        skillName: item[`Skill${k}`],
        screeningScore: item[`Skill${k}Score`],
        techRound1Comment: item[`Skill${k}TROne`],
        techRound2Comment: item[`Skill${k}TRTwo`]
      }))
      .filter(s => !!s.skillName);
  }

  private mapUser(spUserField: any): { id: number; title: string; email: string } | null {
    if (!spUserField) return null;
    return {
      id: spUserField.Id ?? spUserField.results?.[0]?.Id,
      title: spUserField.Title ?? spUserField.results?.[0]?.Title,
      email: spUserField.EMail ?? spUserField.results?.[0]?.EMail
    };
  }

  private toRoundStatus(value: string | null): RoundStatus {
    if (value === 'Selected') return RoundStatus.Selected;
    if (value === 'Rejected') return RoundStatus.Rejected;
    return RoundStatus.Pending;
  }

  /** Derived — never stored. Computes current stage from the 4 round decisions. */
  getCurrentStage(c: Candidate): WorkflowStage {
    if (c.hrRound.selection === RoundStatus.Rejected
      || c.onShoreRound.selection === RoundStatus.Rejected
      || c.techRound2Mgmt.selection === RoundStatus.Rejected
      || c.techRound1.selection === RoundStatus.Rejected
      || c.prescreeningSelected === RoundStatus.Rejected) {
      return WorkflowStage.Rejected;
    }
    if (c.hrRound.selection === RoundStatus.Selected) return WorkflowStage.OfferStage;
    if (c.onShoreRound.selection === RoundStatus.Selected) return WorkflowStage.HrFinal;
    if (c.techRound2Mgmt.selection === RoundStatus.Selected) return WorkflowStage.OnShore;
    if (c.techRound1.selection === RoundStatus.Selected) return WorkflowStage.TechRound2Mgmt;
    if (c.prescreeningSelected === RoundStatus.Selected) return WorkflowStage.TechRound1;
    return WorkflowStage.Unassigned;
  }
}
```

## 4. BNP theme tokens — `styles/_tokens.scss`

```scss
:root {
  --cohort-primary: #00785A;
  --cohort-primary-deep: #005B43;
  --cohort-canvas: #F4F6F5;
  --cohort-text: #1F2A28;
  --cohort-muted: #6B7674;
  --cohort-border: #E3E8E6;

  --status-pass-bg: #E4F4EE;
  --status-pass-text: #00785A;
  --status-reject-bg: #FBE9EA;
  --status-reject-text: #C0392B;
  --status-wait-bg: #FFF6DD;
  --status-wait-text: #8A6D00;
  --status-idle-bg: #EEF0EF;
  --status-idle-text: #6B7674;
}
```

## 5. Status chip — reusable, `shared/components/status-chip/`

```typescript
import { Component, Input } from '@angular/core';
import { RoundStatus } from '../../../core/models/round-status.enum';

@Component({
  selector: 'app-status-chip',
  template: `<span class="chip" [class]="statusClass">{{ label }}</span>`,
  styleUrls: ['./status-chip.component.scss']
})
export class StatusChipComponent {
  @Input() status: RoundStatus = RoundStatus.Pending;
  @Input() customLabel?: string;

  get label(): string {
    return this.customLabel ?? this.status;
  }

  get statusClass(): string {
    switch (this.status) {
      case RoundStatus.Selected: return 'chip--pass';
      case RoundStatus.Rejected: return 'chip--reject';
      case RoundStatus.Locked:
      case RoundStatus.Pending: return 'chip--wait';
      default: return 'chip--idle';
    }
  }
}
```

```scss
.chip {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;

  &--pass { background: var(--status-pass-bg); color: var(--status-pass-text); }
  &--reject { background: var(--status-reject-bg); color: var(--status-reject-text); }
  &--wait { background: var(--status-wait-bg); color: var(--status-wait-text); }
  &--idle { background: var(--status-idle-bg); color: var(--status-idle-text); }
}
```

## 6. Dashboard component — the actual AG Grid page

**`candidate-dashboard.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ColDef, GridOptions } from 'ag-grid-community';
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

  openCandidate(candidate: Candidate): void {
    this.router.navigate(['/candidates', candidate.listSource, candidate.id]);
  }

  private initials(name: string): string {
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }
}
```

**`candidate-dashboard.component.html`**

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
      (input)="gridApi.setQuickFilter(quickFilterText)" />
  </div>

  <div *ngIf="loading" class="state-panel">Searching SharePoint...</div>

  <div *ngIf="errorMessage" class="state-panel state-panel--error">
    <p>Something went wrong</p>
    <p class="detail">{{ errorMessage }}</p>
    <button (click)="loadCandidates()">Try again</button>
  </div>

  <ag-grid-angular
    *ngIf="!loading && !errorMessage"
    class="ag-theme-cohort"
    [rowData]="rowData"
    [columnDefs]="columnDefs"
    [gridOptions]="gridOptions"
    (gridReady)="gridApi = $event.api">
  </ag-grid-angular>
</div>
```

**`candidate-dashboard.component.scss`**

```scss
@import '../../../styles/tokens';

.dashboard-container {
  padding: 24px;
  background: var(--cohort-canvas);
}

.dashboard-header h4 {
  color: var(--cohort-muted);
  text-transform: uppercase;
  font-size: 11px;
  letter-spacing: 0.06em;
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

  &:focus {
    outline: none;
    border-color: var(--cohort-primary);
  }
}

::ng-deep .candidate-cell {
  display: flex;
  align-items: center;
  gap: 10px;

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
}
```

## What's next

Once you route `/candidates/:source/:id`, the click sends you to a **detail component** that reuses `StatusChipComponent` and a new `SkillAssessmentGridComponent` (shared, since Tech Round 1 and Tech Round 2 detail views both need it) — pulling from the *same* `CandidateService.getCandidateById()`, not a separate call.

Want me to build that detail page next, with the visibility service wired in (locked/read-only/editable per round based on logged-i