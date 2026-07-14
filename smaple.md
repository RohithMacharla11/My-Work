Two real bugs here — no way back to General Details, and status color vs active-tab color are conflated. Plus the full navbar rebuild. Let's fix all three properly.

## 1. `app.component.html` (full rewrite — no sidebar)

```html
<div class="app-shell">
  <header class="app-topbar">
    <div class="topbar-brand">
      <div class="brand-mark">C</div>
      <div class="brand-text">Cohort</div>
    </div>

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
```

## 2. `app.component.ts` — remove nav toggle logic

```typescript
import { Component, OnInit } from '@angular/core';
import { CurrentUserService, CurrentUser } from './core/services/current-user.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  currentUser!: CurrentUser;

  constructor(private currentUserService: CurrentUserService) {}

  ngOnInit(): void {
    this.currentUser = this.currentUserService.get();
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

## 3. `app.component.scss` (full rewrite)

```scss
@import './styles/tokens';

* { box-sizing: border-box; }

.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  font-family: 'Segoe UI', system-ui, sans-serif;
}

.app-topbar {
  height: 56px;
  background: var(--cohort-primary-deep);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  flex-shrink: 0;
}

.topbar-brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.brand-mark {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: var(--cohort-primary);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 14px;
}

.brand-text {
  color: white;
  font-weight: 700;
  font-size: 16px;
  letter-spacing: 0.02em;
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

.user-name { font-size: 13px; font-weight: 600; color: white; }
.user-role { font-size: 11px; color: rgba(255,255,255,0.65); }

.app-content {
  flex: 1;
  overflow-y: auto;
  background: var(--cohort-canvas);
}
```

## 4. `workflow-stepper.component.ts` — add General Details as a real, clickable step

```typescript
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';
import { WorkflowVisibilityService, RoundKey } from '../../../../core/services/workflow-visibility.service';
import { DetailTab } from '../../candidate-detail.component';

interface StepDef {
  tab: DetailTab;
  label: string;
  roundKey?: RoundKey;
}

const STEPS: StepDef[] = [
  { tab: 'general', label: 'General Details' },
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

  constructor(private visibility: WorkflowVisibilityService) {}

  select(step: StepDef): void {
    if (this.isLocked(step)) return;
    this.tabSelected.emit(step.tab);
  }

  isLocked(step: StepDef): boolean {
    if (!step.roundKey) return false; // 'general' and 'preScreen' are never locked
    const v = this.visibility.getVisibilityFor(this.candidate, step.roundKey);
    return v.locked || !v.visible;
  }

  status(step: StepDef): 'done' | 'current' | 'rejected' | 'todo' | 'neutral' {
    if (step.tab === 'general') return 'neutral';

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

## 5. `workflow-stepper.component.html`

```html
<div class="stepper">
  <div
    *ngFor="let step of steps; let last = last"
    class="step"
    [class.step--active]="activeTab === step.tab"
    [class.step--locked]="isLocked(step)"
    (click)="select(step)">

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

## 6. `workflow-stepper.component.scss` — separate "active tab" ring from "pass/done" color

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
  padding: 6px 4px;
  border-radius: 10px;
  transition: background 0.15s ease, opacity 0.15s ease;

  &:hover:not(.step--locked) { background: var(--cohort-canvas); }

  &--locked {
    cursor: not-allowed;
    opacity: 0.4;
  }

  // Active tab gets its own visual language — a ring + tinted background —
  // completely independent of the round's pass/fail color.
  &--active {
    background: var(--status-pass-bg);

    .step-label {
      color: var(--cohort-primary);
      font-weight: 700;
    }

    .step-node {
      box-shadow: 0 0 0 3px white, 0 0 0 5px var(--cohort-primary);
    }
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
  &--neutral { background: var(--cohort-canvas); color: var(--cohort-text); border: 2px solid var(--cohort-border); }
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

## 7. `candidate-detail.component.html` — remove the standalone unclickable hero, since it's now the "General Details" tab content

The hero card stays as a persistent summary banner above the stepper (that's good UX — always know whose page you're on), but it's no longer trying to double as a tab. Just confirm this structure is what you have:

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
      <div class="hero-avatar">{{ candidateInitials }}</div>
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

Everything else (`candidate-detail.component.ts` with `activeTab: DetailTab = 'general'` as default) stays as-is — that default already handles opening on General Details first. The fix was purely that **General Details wasn't a step in the stepper**, so once you clicked away there was no button to click back to it. Now it's step one, always reachable, with its own distinct "active" visual (tinted background + ring) instead of borrowing the green "passed" color.

Test: land on detail page → General Details active and highlighted → click Pre-Screen → its content shows, its tab highlights → click General Details again → works, no back-and-reload needed.