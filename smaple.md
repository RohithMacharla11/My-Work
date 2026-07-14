Good — wiring the visibility service in now, with a temporary override so everything's editable. Here's the full build: candidate detail page, workflow stepper, same-tab navigation, and a polished grid theme.

## Updated folder structure

```
src/app/
├── core/
│   ├── models/
│   │   ├── candidate-row.model.ts        (existing)
│   │   ├── candidate.model.ts            (existing)
│   │   ├── round-status.enum.ts          (existing)
│   │   └── workflow-stage.enum.ts        (existing)
│   └── services/
│       ├── workflow-visibility.service.ts (UPDATED — god-mode flag)
│       └── ...(unchanged)
├── features/
│   ├── candidate-dashboard/
│   │   ├── candidate-dashboard.component.ts    (UPDATED — same-tab nav)
│   │   └── ...(rest unchanged)
│   └── candidate-detail/                        (NEW)
│       ├── candidate-detail.component.ts
│       ├── candidate-detail.component.html
│       ├── candidate-detail.component.scss
│       ├── candidate-detail.module.ts
│       └── components/
│           ├── workflow-stepper/
│           │   ├── workflow-stepper.component.ts
│           │   ├── workflow-stepper.component.html
│           │   └── workflow-stepper.component.scss
│           ├── general-details-panel/
│           │   ├── general-details-panel.component.ts
│           │   ├── general-details-panel.component.html
│           │   └── general-details-panel.component.scss
│           ├── prescreen-panel/
│           │   ├── prescreen-panel.component.ts
│           │   ├── prescreen-panel.component.html
│           │   └── prescreen-panel.component.scss
│           └── tech-round-panel/
│               ├── tech-round-panel.component.ts
│               ├── tech-round-panel.component.html
│               └── tech-round-panel.component.scss
├── app-routing.module.ts                 (UPDATED)
└── styles/
    ├── _tokens.scss                      (existing)
    └── _ag-grid-theme.scss               (NEW)
```

## 1. `core/services/workflow-visibility.service.ts` (updated with god-mode)

```typescript
import { Injectable } from '@angular/core';
import { Candidate, InterviewRound } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';
import { CurrentUserService } from './current-user.service';

export type RoundKey = 'techRound1' | 'techRound2Mgmt' | 'onShoreRound' | 'hrRound';

export interface RoundVisibility {
  visible: boolean;
  editable: boolean;
  readOnly: boolean;
  locked: boolean;
  lockedMessage?: string;
}

const ROUND_SEQUENCE: RoundKey[] = ['techRound1', 'techRound2Mgmt', 'onShoreRound', 'hrRound'];

const ROUND_LABEL: Record<RoundKey, string> = {
  techRound1: 'Tech Round 1',
  techRound2Mgmt: 'Tech 2 / Management',
  onShoreRound: 'On-Shore Round',
  hrRound: 'HR Round'
};

@Injectable({ providedIn: 'root' })
export class WorkflowVisibilityService {

  /**
   * TEMP: while permissions aren't finalized, everything is visible and
   * editable for everyone. Flip this to false once role-based access
   * is ready to switch on — no other code needs to change.
   */
  readonly godModeEnabled = true;

  constructor(private currentUser: CurrentUserService) {}

  isRoundUnlocked(candidate: Candidate, round: RoundKey): boolean {
    if (this.godModeEnabled) return true;

    const index = ROUND_SEQUENCE.indexOf(round);
    if (index === 0) {
      return candidate.prescreeningSelected === RoundStatus.Selected;
    }
    const previousRound = ROUND_SEQUENCE[index - 1];
    return this.getRound(candidate, previousRound).selection === RoundStatus.Selected;
  }

  isPipelineTerminated(candidate: Candidate, upToRound: RoundKey): boolean {
    if (this.godModeEnabled) return false;

    const index = ROUND_SEQUENCE.indexOf(upToRound);
    if (candidate.prescreeningSelected === RoundStatus.Rejected) return true;
    for (let i = 0; i < index; i++) {
      if (this.getRound(candidate, ROUND_SEQUENCE[i]).selection === RoundStatus.Rejected) {
        return true;
      }
    }
    return false;
  }

  getVisibilityFor(candidate: Candidate, round: RoundKey): RoundVisibility {
    if (this.godModeEnabled) {
      return { visible: true, editable: true, readOnly: false, locked: false };
    }

    const user = this.currentUser.get();

    if (user.role === 'HrAdmin' || user.role === 'Recruiter') {
      return {
        visible: true,
        editable: user.role === 'HrAdmin' && round === 'hrRound' && this.isRoundUnlocked(candidate, round),
        readOnly: user.role === 'Recruiter',
        locked: false
      };
    }

    const assignedRoundIndex = ROUND_SEQUENCE.indexOf(user.assignedRound as RoundKey);
    const thisRoundIndex = ROUND_SEQUENCE.indexOf(round);

    if (thisRoundIndex > assignedRoundIndex) {
      return { visible: false, editable: false, readOnly: false, locked: false };
    }

    if (thisRoundIndex < assignedRoundIndex) {
      return { visible: true, editable: false, readOnly: true, locked: false };
    }

    if (this.isPipelineTerminated(candidate, round)) {
      return { visible: true, editable: false, readOnly: true, locked: false };
    }

    if (!this.isRoundUnlocked(candidate, round)) {
      const prevLabel = thisRoundIndex === 0 ? 'Pre-Screen' : ROUND_LABEL[ROUND_SEQUENCE[thisRoundIndex - 1]];
      return {
        visible: true, editable: false, readOnly: false, locked: true,
        lockedMessage: `Waiting for ${prevLabel} to complete.`
      };
    }

    const alreadySubmitted = this.getRound(candidate, round).selection !== RoundStatus.Pending;
    return { visible: true, editable: !alreadySubmitted, readOnly: alreadySubmitted, locked: false };
  }

  private getRound(candidate: Candidate, key: RoundKey): InterviewRound {
    return candidate[key];
  }
}
```

## 2. `app-routing.module.ts` (updated)

```typescript
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadChildren: () => import('./features/candidate-dashboard/candidate-dashboard.module')
      .then(m => m.CandidateDashboardModule)
  },
  {
    path: 'candidates/:source/:id',
    loadChildren: () => import('./features/candidate-detail/candidate-detail.module')
      .then(m => m.CandidateDetailModule)
  },
  { path: '**', redirectTo: 'dashboard' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
```

## 3. Dashboard — fix `openCandidate` to same-tab nav

In `candidate-dashboard.component.ts`, replace the `openCandidate` method:

```typescript
openCandidate(candidate: CandidateRow): void {
  this.router.navigate(['/candidates', candidate.listSource, candidate.id]);
}
```

(Remove the `window.open` / `serializeUrl` version from before — that was wrong per your correction.)

## 4. `candidate-detail.module.ts`

```typescript
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Routes } from '@angular/router';

import { SharedModule } from '../../shared/shared.module';
import { CandidateDetailComponent } from './candidate-detail.component';
import { WorkflowStepperComponent } from './components/workflow-stepper/workflow-stepper.component';
import { GeneralDetailsPanelComponent } from './components/general-details-panel/general-details-panel.component';
import { PrescreenPanelComponent } from './components/prescreen-panel/prescreen-panel.component';
import { TechRoundPanelComponent } from './components/tech-round-panel/tech-round-panel.component';

const routes: Routes = [
  { path: '', component: CandidateDetailComponent }
];

@NgModule({
  declarations: [
    CandidateDetailComponent,
    WorkflowStepperComponent,
    GeneralDetailsPanelComponent,
    PrescreenPanelComponent,
    TechRoundPanelComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    SharedModule,
    RouterModule.forChild(routes)
  ]
})
export class CandidateDetailModule {}
```

## 5. `candidate-detail.component.ts`

```typescript
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateService } from '../../core/services/candidate.service';
import { WorkflowVisibilityService, RoundKey } from '../../core/services/workflow-visibility.service';
import { Candidate } from '../../core/models/candidate.model';
import { WorkflowStage, WORKFLOW_STAGE_LABEL } from '../../core/models/workflow-stage.enum';

export type DetailTab = 'general' | 'preScreen' | RoundKey;

@Component({
  selector: 'app-candidate-detail',
  templateUrl: './candidate-detail.component.html',
  styleUrls: ['./candidate-detail.component.scss']
})
export class CandidateDetailComponent implements OnInit {

  candidate: Candidate | null = null;
  currentStage: WorkflowStage | null = null;
  loading = true;
  errorMessage: string | null = null;

  activeTab: DetailTab = 'general';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private candidateService: CandidateService,
    public visibility: WorkflowVisibilityService
  ) {}

  ngOnInit(): void {
    const source = this.route.snapshot.paramMap.get('source') as 'Chennai' | 'Mumbai';
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadCandidate(source, id);
  }

  private loadCandidate(source: 'Chennai' | 'Mumbai', id: number): void {
    this.loading = true;
    this.errorMessage = null;

    this.candidateService.getAllCandidates().subscribe({
      next: (candidates) => {
        const found = candidates.find(c => c.listSource === source && c.id === id);
        if (!found) {
          this.errorMessage = 'Candidate not found.';
          this.loading = false;
          return;
        }
        this.candidate = found;
        this.currentStage = this.candidateService.getCurrentStage(found);
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Could not load candidate details from SharePoint.';
        this.loading = false;
      }
    });
  }

  setActiveTab(tab: DetailTab): void {
    this.activeTab = tab;
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }

  get stageLabel(): string {
    return this.currentStage ? WORKFLOW_STAGE_LABEL[this.currentStage] : '';
  }
}
```

## 6. `candidate-detail.component.html`

```html
<div class="detail-page">

  <div class="detail-topbar">
    <button class="back-link" (click)="goBack()">← Back to Candidates</button>
  </div>

  <div *ngIf="loading" class="state-panel">
    <div class="spinner"></div>
    <p>Loading candidate...</p>
  </div>

  <div *ngIf="errorMessage" class="state-panel state-panel--error">
    <p class="state-title">{{ errorMessage }}</p>
  </div>

  <ng-container *ngIf="candidate && !loading">

    <div class="candidate-hero">
      <div class="hero-avatar">{{ candidate.candidateName.split(' ').map(n => n[0]).join('').substring(0,2).toUpperCase() }}</div>
      <div class="hero-info">
        <h1>{{ candidate.candidateName }}</h1>
        <div class="hero-meta">
          <span>{{ candidate.candidateEmailId || '—' }}</span>
          <span class="dot">·</span>
          <span>{{ candidate.candidatePhoneNumber || '—' }}</span>
          <span class="dot">·</span>
          <span>{{ candidate.location || '—' }}</span>
          <span class="dot">·</span>
          <span>{{ candidate.roleDesignation }} · {{ candidate.profile }}</span>
        </div>
      </div>
      <div class="hero-id">
        <div class="id-label">ID</div>
        <div class="id-value">{{ candidate.candidateId }}</div>
      </div>
    </div>

    <app-workflow-stepper
      [candidate]="candidate"
      [activeTab]="activeTab"
      (tabSelected)="setActiveTab($event)">
    </app-workflow-stepper>

    <div class="detail-content">
      <app-general-details-panel *ngIf="activeTab === 'general'" [candidate]="candidate"></app-general-details-panel>
      <app-prescreen-panel *ngIf="activeTab === 'preScreen'" [candidate]="candidate"></app-prescreen-panel>
      <app-tech-round-panel *ngIf="activeTab === 'techRound1'" [candidate]="candidate" roundKey="techRound1"></app-tech-round-panel>
      <app-tech-round-panel *ngIf="activeTab === 'techRound2Mgmt'" [candidate]="candidate" roundKey="techRound2Mgmt"></app-tech-round-panel>
      <app-tech-round-panel *ngIf="activeTab === 'onShoreRound'" [candidate]="candidate" roundKey="onShoreRound"></app-tech-round-panel>
      <app-tech-round-panel *ngIf="activeTab === 'hrRound'" [candidate]="candidate" roundKey="hrRound"></app-tech-round-panel>
    </div>

  </ng-container>
</div>
```

## 7. `candidate-detail.component.scss`

```scss
.detail-page {
  padding: 24px 32px 48px;
  background: var(--cohort-canvas);
  min-height: 100%;
}

.detail-topbar {
  margin-bottom: 16px;
}

.back-link {
  background: none;
  border: none;
  color: var(--cohort-primary);
  font-weight: 600;
  font-size: 13.5px;
  cursor: pointer;
  padding: 0;
  transition: opacity 0.15s ease;

  &:hover { opacity: 0.7; }
}

.candidate-hero {
  display: flex;
  align-items: center;
  gap: 18px;
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 12px;
  padding: 20px 24px;
  margin-bottom: 20px;
}

.hero-avatar {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  background: var(--cohort-primary);
  color: white;
  font-size: 20px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.hero-info {
  flex: 1;

  h1 { margin: 0 0 4px; font-size: 20px; color: var(--cohort-text); }

  .hero-meta {
    font-size: 13px;
    color: var(--cohort-muted);

    .dot { margin: 0 6px; }
  }
}

.hero-id {
  text-align: right;
  .id-label { font-size: 10px; text-transform: uppercase; color: var(--cohort-muted); letter-spacing: 0.05em; }
  .id-value { font-weight: 700; color: var(--cohort-text); font-size: 14px; }
}

.detail-content {
  margin-top: 20px;
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(4px); }
  to { opacity: 1; transform: translateY(0); }
}

.state-panel {
  text-align: center;
  padding: 80px 0;
  color: var(--cohort-muted);

  &--error { color: var(--status-reject-text); }
}

.spinner {
  width: 28px; height: 28px;
  border: 3px solid var(--cohort-border);
  border-top-color: var(--cohort-primary);
  border-radius: 50%;
  margin: 0 auto 12px;
  animation: spin 0.8s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }
```

## 8. `components/workflow-stepper/workflow-stepper.component.ts`

```typescript
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';
import { WorkflowVisibilityService, RoundKey } from '../../../../core/services/workflow-visibility.service';
import { CandidateService } from '../../../../core/services/candidate.service';
import { DetailTab } from '../../candidate-detail.component';

interface StepDef {
  tab: DetailTab;
  label: string;
  roundKey?: RoundKey;
}

const STEPS: StepDef[] = [
  { tab: 'preScreen', label: 'Pre-Screen' },
  { tab: 'techRound1', label: 'Tech Round 1', roundKey: 'techRound1' },
  { tab: 'techRound2Mgmt', label: 'Tech 2 / Mgmt', roundKey: 'techRound2Mgmt' },
  { tab: 'onShoreRound', label: 'On-Shore', roundKey: 'onShoreRound' },
  { tab: 'hrRound', label: 'HR Final', roundKey: 'hrRound' }
];

@Component({
  selector: 'app-workflow-stepper',
  templateUrl: './workflow-stepper.component.html',
  styleUrls: ['./workflow-stepper.component.scss']
})
export class WorkflowStepperComponent {
  @Input() candidate!: Candidate;
  @Input() activeTab!: DetailTab;
  @Output() tabSelected = new EventEmitter<DetailTab>();

  steps = STEPS;

  constructor(
    private visibility: WorkflowVisibilityService,
    private candidateService: CandidateService
  ) {}

  select(step: StepDef): void {
    this.tabSelected.emit(step.tab);
  }

  isLocked(step: StepDef): boolean {
    if (!step.roundKey) return false;
    const v = this.visibility.getVisibilityFor(this.candidate, step.roundKey);
    return v.locked || !v.visible;
  }

  status(step: StepDef): 'done' | 'current' | 'rejected' | 'todo' {
    if (!step.roundKey) {
      return this.candidate.prescreeningSelected === 'Selected' ? 'done' : 'current';
    }
    const round = this.candidate[step.roundKey];
    if (round.selection === 'Selected') return 'done';
    if (round.selection === 'Rejected') return 'rejected';
    const v = this.visibility.getVisibilityFor(this.candidate, step.roundKey);
    return v.editable || v.readOnly ? 'current' : 'todo';
  }
}
```

## 9. `workflow-stepper.component.html`

```html
<div class="stepper">
  <div
    *ngFor="let step of steps; let last = last"
    class="step"
    [class.step--active]="activeTab === step.tab"
    [class.step--locked]="isLocked(step)"
    (click)="!isLocked(step) && select(step)">

    <div class="step-node" [ngClass]="'step-node--' + status(step)">
      <span *ngIf="status(step) === 'done'">✓</span>
      <span *ngIf="status(step) === 'rejected'">✕</span>
      <span *ngIf="isLocked(step) && status(step) === 'todo'">🔒</span>
    </div>
    <div class="step-label">{{ step.label }}</div>

    <div class="step-connector" *ngIf="!last"></div>
  </div>
</div>
```

## 10. `workflow-stepper.component.scss`

```scss
.stepper {
  display: flex;
  align-items: flex-start;
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 12px;
  padding: 20px 28px;
}

.step {
  display: flex;
  flex-direction: column;
  align-items: center;
  position: relative;
  flex: 1;
  cursor: pointer;
  transition: opacity 0.15s ease;

  &--locked {
    cursor: not-allowed;
    opacity: 0.45;
  }

  &--active .step-label {
    color: var(--cohort-primary);
    font-weight: 700;
  }
}

.step-node {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  margin-bottom: 8px;
  z-index: 1;
  transition: all 0.2s ease;

  &--done { background: var(--cohort-primary); color: white; }
  &--current { background: var(--status-wait-bg); color: var(--status-wait-text); border: 2px solid var(--cohort-primary); }
  &--rejected { background: var(--status-reject-bg); color: var(--status-reject-text); }
  &--todo { background: var(--cohort-border); color: var(--cohort-muted); }
}

.step-label {
  font-size: 12.5px;
  color: var(--cohort-muted);
  text-align: center;
}

.step-connector {
  position: absolute;
  top: 17px;
  left: 50%;
  width: 100%;
  height: 2px;
  background: var(--cohort-border);
  z-index: 0;
}
```

## 11. `components/general-details-panel/general-details-panel.component.ts`

```typescript
import { Component, Input } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';

@Component({
  selector: 'app-general-details-panel',
  templateUrl: './general-details-panel.component.html',
  styleUrls: ['./general-details-panel.component.scss']
})
export class GeneralDetailsPanelComponent {
  @Input() candidate!: Candidate;
}
```

## 12. `general-details-panel.component.html`

```html
<div class="panel">
  <h3>Candidate Details</h3>
  <div class="detail-grid">
    <div class="detail-field"><label>Candidate ID</label><span>{{ candidate.candidateId || '—' }}</span></div>
    <div class="detail-field"><label>Full Name</label><span>{{ candidate.candidateName || '—' }}</span></div>
    <div class="detail-field"><label>Email</label><span>{{ candidate.candidateEmailId || '—' }}</span></div>
    <div class="detail-field"><label>Phone</label><span>{{ candidate.candidatePhoneNumber || '—' }}</span></div>
    <div class="detail-field"><label>Location</label><span>{{ candidate.location || '—' }}</span></div>
    <div class="detail-field"><label>Role / Designation</