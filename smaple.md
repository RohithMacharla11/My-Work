Good catch — those were referenced but not built. Here's the rest.

## 1. Workflow visibility service — `core/services/workflow-visibility.service.ts`

This is the brain of the "who sees what, when" logic — Rules A, B, D from the spec, in one place so no component re-implements it.

```typescript
import { Injectable } from '@angular/core';
import { Candidate, InterviewRound } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';
import { WorkflowStage, WORKFLOW_STAGE_ORDER } from '../models/workflow-stage.enum';
import { CurrentUserService } from './current-user.service';

export type RoundKey = 'techRound1' | 'techRound2Mgmt' | 'onShoreRound' | 'hrRound';

export interface RoundVisibility {
  visible: boolean;      // false = round doesn't render at all for this viewer
  editable: boolean;     // true only if it's this user's round AND it's unlocked
  readOnly: boolean;     // visible, but locked to read history
  locked: boolean;       // visible as a placeholder, e.g. "Waiting for Tech Round 1 to complete"
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

  constructor(private currentUser: CurrentUserService) {}

  /** Rule A: a round is editable only if the previous round is Selected. */
  isRoundUnlocked(candidate: Candidate, round: RoundKey): boolean {
    const index = ROUND_SEQUENCE.indexOf(round);
    if (index === 0) {
      return candidate.prescreeningSelected === RoundStatus.Selected;
    }
    const previousRound = ROUND_SEQUENCE[index - 1];
    return this.getRound(candidate, previousRound).selection === RoundStatus.Selected;
  }

  /** Rule A: rejection anywhere upstream terminates everything downstream. */
  isPipelineTerminated(candidate: Candidate, upToRound: RoundKey): boolean {
    const index = ROUND_SEQUENCE.indexOf(upToRound);
    if (candidate.prescreeningSelected === RoundStatus.Rejected) return true;
    for (let i = 0; i < index; i++) {
      if (this.getRound(candidate, ROUND_SEQUENCE[i]).selection === RoundStatus.Rejected) {
        return true;
      }
    }
    return false;
  }

  /**
   * Rule B/D combined: given the logged-in user's assigned round, decide what
   * they see for every round on this candidate.
   */
  getVisibilityFor(candidate: Candidate, round: RoundKey): RoundVisibility {
    const user = this.currentUser.get();

    if (user.role === 'HrAdmin' || user.role === 'Recruiter') {
      // Full visibility, read-only for recruiters, editable for HR admin only on HR round
      return {
        visible: true,
        editable: user.role === 'HrAdmin' && round === 'hrRound' && this.isRoundUnlocked(candidate, round),
        readOnly: user.role === 'Recruiter',
        locked: false
      };
    }

    const assignedRoundIndex = ROUND_SEQUENCE.indexOf(user.assignedRound as RoundKey);
    const thisRoundIndex = ROUND_SEQUENCE.indexOf(round);

    // Round after the interviewer's own assignment: doesn't exist for them
    if (thisRoundIndex > assignedRoundIndex) {
      return { visible: false, editable: false, readOnly: false, locked: false };
    }

    // Rounds before their own: read-only history, IF they've been completed
    if (thisRoundIndex < assignedRoundIndex) {
      return { visible: true, editable: false, readOnly: true, locked: false };
    }

    // Their own round
    if (this.isPipelineTerminated(candidate, round)) {
      return { visible: true, editable: false, readOnly: true, locked: false };
    }

    if (!this.isRoundUnlocked(candidate, round)) {
      const prevLabel = thisRoundIndex === 0 ? 'Pre-Screen' : ROUND_LABEL[ROUND_SEQUENCE[thisRoundIndex - 1]];
      return {
        visible: true,
        editable: false,
        readOnly: false,
        locked: true,
        lockedMessage: `Waiting for ${prevLabel} to complete.`
      };
    }

    const alreadySubmitted = this.getRound(candidate, round).selection !== RoundStatus.Pending;
    return {
      visible: true,
      editable: !alreadySubmitted,
      readOnly: alreadySubmitted,
      locked: false
    };
  }

  private getRound(candidate: Candidate, key: RoundKey): InterviewRound {
    return candidate[key];
  }
}
```

**`core/services/current-user.service.ts`** — stub for now, wire to SharePoint's `_api/web/currentuser` later.

```typescript
import { Injectable } from '@angular/core';
import { RoundKey } from './workflow-visibility.service';

export type UserRole = 'HrAdmin' | 'Recruiter' | 'Interviewer';

export interface CurrentUser {
  id: number;
  title: string;
  email: string;
  role: UserRole;
  assignedRound?: RoundKey;
}

@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private user: CurrentUser = {
    id: 0,
    title: 'Loading...',
    email: '',
    role: 'Interviewer',
    assignedRound: 'techRound1'
  };

  get(): CurrentUser {
    return this.user;
  }

  set(user: CurrentUser): void {
    this.user = user;
  }
}
```

## 2. Workflow pipeline cell — `shared/components/workflow-pipeline-cell/`

The dot-sequence renderer used in the AG Grid "Workflow" column.

```typescript
import { Component } from '@angular/core';
import { ICellRendererAngularComp } from 'ag-grid-angular';
import { ICellRendererParams } from 'ag-grid-community';
import { Candidate } from '../../../core/models/candidate.model';
import { WorkflowStage, WORKFLOW_STAGE_ORDER, WORKFLOW_STAGE_LABEL } from '../../../core/models/workflow-stage.enum';
import { CandidateService } from '../../../core/services/candidate.service';

@Component({
  selector: 'app-workflow-pipeline-cell',
  template: `
    <div class="pipeline-cell">
      <div class="dots">
        <span
          *ngFor="let stage of stages"
          class="dot"
          [class.dot--done]="isDone(stage)"
          [class.dot--current]="isCurrent(stage)"
          [class.dot--rejected]="isRejected"
          [class.dot--todo]="isTodo(stage)">
        </span>
      </div>
      <div class="pipeline-label" [class.pipeline-label--rejected]="isRejected">
        {{ statusLabel }}
      </div>
    </div>
  `,
  styleUrls: ['./workflow-pipeline-cell.component.scss']
})
export class WorkflowPipelineCellComponent implements ICellRendererAngularComp {
  candidate!: Candidate;
  stages = WORKFLOW_STAGE_ORDER;
  currentStage!: WorkflowStage;
  isRejected = false;
  statusLabel = '';

  constructor(private candidateService: CandidateService) {}

  agInit(params: ICellRendererParams): void {
    this.candidate = params.data;
    this.currentStage = this.candidateService.getCurrentStage(this.candidate);
    this.isRejected = this.currentStage === WorkflowStage.Rejected;
    this.statusLabel = this.isRejected
      ? 'Rejected'
      : WORKFLOW_STAGE_LABEL[this.currentStage];
  }

  refresh(): boolean {
    return false;
  }

  isDone(stage: WorkflowStage): boolean {
    const currentIdx = this.stages.indexOf(this.currentStage);
    const stageIdx = this.stages.indexOf(stage);
    return currentIdx > stageIdx && !this.isRejected;
  }

  isCurrent(stage: WorkflowStage): boolean {
    return stage === this.currentStage;
  }

  isTodo(stage: WorkflowStage): boolean {
    const currentIdx = this.stages.indexOf(this.currentStage);
    const stageIdx = this.stages.indexOf(stage);
    return stageIdx > currentIdx || this.isRejected;
  }
}
```

```scss
.pipeline-cell {
  .dots {
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--cohort-border);

    &--done { background: var(--cohort-primary); }
    &--current { background: var(--cohort-primary); box-shadow: 0 0 0 3px var(--status-pass-bg); }
    &--rejected { background: var(--status-reject-text) !important; }
    &--todo { background: var(--cohort-border); }
  }

  .pipeline-label {
    font-size: 12px;
    color: var(--cohort-muted);
    margin-top: 4px;

    &--rejected { color: var(--status-reject-text); }
  }
}
```

Register it in whichever module declares shared components (`SharedModule`), and note it needs to also be in `entryComponents` if you're on an older Angular version using ViewEngine — for Ivy (Angular 9+) that's not required.

## 3. App routing — `app-routing.module.ts`

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
  {
    path: 'assign/:source/:id',
    loadChildren: () => import('./features/panel-assignment/panel-assignment.module')
      .then(m => m.PanelAssignmentModule)
  },
  { path: '**', redirectTo: 'dashboard' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
```

`candidate-detail` and `panel-assignment` are stubbed as lazy modules — we'll build them next.

## 4. App module — `app.module.ts`

```typescript
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { SharedModule } from './shared/shared.module';

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
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {}
```

## 5. App component — nav bar shell

**`app.component.ts`**

```typescript
import { Component, OnInit } from '@angular/core';
import { CurrentUserService, CurrentUser } from './core/services/current-user.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  appTitle = 'Cohort';
  appSubtitle = 'Interview Management';
  currentUser!: CurrentUser;
  navOpen = true;

  constructor(private currentUserService: CurrentUserService) {}

  ngOnInit(): void {
    this.currentUser = this.currentUserService.get();
  }

  toggleNav(): void {
    this.navOpen = !this.navOpen;
  }

  get userInitials(): string {
    return this.currentUser.title
      .split(' ')
      .map(n => n[0])
      .join('')
      .substring(0, 2)
      .toUpperCase();
  }
}
```

**`app.component.html`**

```html
<div class="app-shell">
  <nav class="app-navbar" [class.app-navbar--collapsed]="!navOpen">
    <div class="navbar-top">
      <button class="collapse-btn" (click)="toggleNav()">
        <span *ngIf="navOpen">‹</span>
        <span *ngIf="!navOpen">›</span>
      </button>
      <div class="brand" *ngIf="navOpen">
        <div class="brand-mark">C</div>
        <div class="brand-text">
          <div class="brand-title">{{ appTitle }}</div>
          <div class="brand-subtitle">{{ appSubtitle }}</div>
        </div>
      </div>
    </div>

    <div class="nav-section" *ngIf="navOpen">
      <div class="nav-section-title">Dashboard</div>
      <a routerLink="/dashboard" routerLinkActive="nav-link--active" class="nav-link">
        All Candidates
      </a>
    </div>

    <div class="nav-section" *ngIf="navOpen">
      <div class="nav-section-title">Candidate Record — by stage</div>
      <a routerLink="/dashboard" [queryParams]="{stage: 'techRound1'}" routerLinkActive="nav-link--active" class="nav-link">
        Stage 1 · Tech Round 1
      </a>
      <a routerLink="/dashboard" [queryParams]="{stage: 'techRound2Mgmt'}" routerLinkActive="nav-link--active" class="nav-link">
        Stage 2 · Tech 2 / Mgmt
      </a>
      <a routerLink="/dashboard" [queryParams]="{stage: 'onShoreRound'}" routerLinkActive="nav-link--active" class="nav-link">
        Stage 3 · On-Shore
      </a>
      <a routerLink="/dashboard" [queryParams]="{stage: 'hrRound'}" routerLinkActive="nav-link--active" class="nav-link">
        Stage 4 · HR Final
      </a>
      <a routerLink="/dashboard" [queryParams]="{stage: 'offer'}" routerLinkActive="nav-link--active" class="nav-link">
        Offer Stage
      </a>
      <a routerLink="/dashboard" [queryParams]="{stage: 'rejected'}" routerLinkActive="nav-link--active" class="nav-link">
        Rejected
      </a>
    </div>

    <div class="nav-section" *ngIf="navOpen && currentUser.role === 'HrAdmin'">
      <div class="nav-section-title">HR — Assign</div>
      <a routerLink="/assign/new" class="nav-link">Assign · New Candidate</a>
    </div>
  </nav>

  <div class="app-main">
    <header class="app-topbar">
      <div class="topbar-crumb">Cohort <span class="crumb-sep">›</span> Interview Management</div>
      <div class="topbar-user">
        <div class="user-avatar">{{ userInitials }}</div>
        <div class="user-meta">
          <div class="user-name">{{ currentUser.title }}</div>
          <div class="user-role">{{ currentUser.role }}</div>
        </div>
      </div>
    </header>

    <main class="app-content">
      <router-outlet></router-outlet>
    </main>
  </div>
</div>
```

**`app.component.scss`**

```scss
@import './styles/tokens';

* { box-sizing: border-box; }

.app-shell {
  display: flex;
  height: 100vh;
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.app-navbar {
  width: 260px;
  background: var(--cohort-primary-deep);
  color: white;
  display: flex;
  flex-direction: column;
  padding: 16px 12px;
  transition: width 0.2s ease;
  overflow-y: auto;

  &--collapsed {
    width: 56px;
    padding: 16px 8px;
  }
}

.navbar-top {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 24px;
}

.collapse-btn {
  background: rgba(255,255,255,0.1);
  border: none;
  color: white;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  cursor: pointer;
  flex-shrink: 0;
}

.brand-mark {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: var(--cohort-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.brand-title { font-weight: 700; font-size: 15px; }
.brand-subtitle { font-size: 11px; opacity: 0.7; }

.nav-section {
  margin-bottom: 20px;
}

.nav-section-title {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  opacity: 0.6;
  padding: 0 10px;
  margin-bottom: 6px;
}

.nav-link {
  display: block;
  padding: 8px 10px;
  border-radius: 6px;
  color: rgba(255,255,255,0.85);
  text-decoration: none;
  font-size: 13px;
  margin-bottom: 2px;

  &:hover { background: rgba(255,255,255,0.08); }

  &--active {
    background: var(--cohort-primary);
    color: white;
    font-weight: 600;
  }
}

.app-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.app-topbar {
  height: 56px;
  background: white;
  border-bottom: 1px solid var(--cohort-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  flex-shrink: 0;
}

.topbar-crumb {
  font-size: 13px;
  color: var(--cohort-muted);

  .crumb-sep { margin: 0 6px; }
}

.topbar-user {
  display: flex;
  align-items: center;
  gap: 10px;
}

.user-avatar {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: var(--cohort-primary);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
}

.user-name { font-size: 13px; font-weight: 600; color: var(--cohort-text); }
.user-role { font-size: 11px; color: var(--cohort-muted); }

.app-content {
  flex: 1;
  overflow-y: auto;
  background: var(--cohort-canvas);
}
```

## One thing to flag from your screenshots

Your `status-chip.component.ts` references `'./status-chip.component.html'` and `'./status-chip.component.scss'`, but I'd originally given you an inline template. Since you've already split it to a separate HTML file (which is fine, cleaner actually), just make sure `status-chip.component.html` contains exactly:

```html
<span class="chip" [class]="statusClass">{{ label }}</span>
```

— which your second screenshot shows is already correct. Good.

Next step: want me to build `candidate-detail` (the full single-candidate page, wired to `WorkflowVisibilityService`) or `panel-assignment` (HR's "assign interviewer + date" screen)?




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