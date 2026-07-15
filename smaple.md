Got it. Here's the full rebuild — single scrolling page, access-capped combined tech table, strict edit rules, and a smoother, more polished UI throughout.

## Updated component list (repurposed, not renamed, to match your folder)

- `workflow-stepper` → becomes a **sticky jump-nav** (scrolls to sections, doesn't swap content)
- `tech-round-panel` → becomes the **combined Tech Assessment table**, capped at viewer's access
- `general-details-panel` → gets a **"View more" expand**
- `prescreen-panel`, plus new **on-shore-panel** and **hr-panel** sections

## 1. `core/services/workflow-visibility.service.ts` (rewritten)

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
   * TEMP: while permissions aren't finalized, viewer access is treated as
   * "full" (HrAdmin-equivalent). Flip to false once real role assignment
   * is wired up — every rule below already respects it correctly.
   */
  readonly godModeEnabled = true;

  constructor(private currentUser: CurrentUserService) {}

  /** Index (0-3) of the last round this viewer is allowed to see at all, capped by their own assignment. */
  getMaxVisibleRoundIndex(): number {
    if (this.godModeEnabled) return ROUND_SEQUENCE.length - 1;

    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') {
      return ROUND_SEQUENCE.length - 1;
    }
    const idx = ROUND_SEQUENCE.indexOf(user.assignedRound as RoundKey);
    return idx === -1 ? -1 : idx;
  }

  /** Whether this round is within the viewer's access at all (regardless of candidate progress). */
  isRoundInViewerAccess(round: RoundKey): boolean {
    return ROUND_SEQUENCE.indexOf(round) <= this.getMaxVisibleRoundIndex();
  }

  /** Whether the candidate has actually reached this round (previous round Selected). */
  hasCandidateReached(candidate: Candidate, round: RoundKey): boolean {
    const index = ROUND_SEQUENCE.indexOf(round);
    if (index === 0) {
      return candidate.prescreeningSelected === RoundStatus.Selected;
    }
    const previousRound = ROUND_SEQUENCE[index - 1];
    return this.getRound(candidate, previousRound).selection === RoundStatus.Selected;
  }

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
   * A round is editable ONLY if:
   *  - it is exactly the viewer's own assigned round (not before, not after)
   *  - the previous round is Selected (their turn has arrived)
   *  - this round hasn't already been finalized (Selected/Rejected)
   * Once finalized, it locks for everyone — including the original interviewer.
   */
  getVisibilityFor(candidate: Candidate, round: RoundKey): RoundVisibility {
    if (!this.isRoundInViewerAccess(round)) {
      return { visible: false, editable: false, readOnly: false, locked: false };
    }

    if (this.godModeEnabled) {
      const alreadyFinal = this.getRound(candidate, round).selection !== RoundStatus.Pending;
      return { visible: true, editable: !alreadyFinal, readOnly: alreadyFinal, locked: false };
    }

    const user = this.currentUser.get();

    if (user.role === 'HrAdmin' || user.role === 'Recruiter') {
      const alreadyFinal = this.getRound(candidate, round).selection !== RoundStatus.Pending;
      return {
        visible: true,
        editable: user.role === 'HrAdmin' && round === 'hrRound' && !alreadyFinal,
        readOnly: user.role === 'Recruiter' || alreadyFinal,
        locked: false
      };
    }

    const isOwnRound = user.assignedRound === round;

    if (this.isPipelineTerminated(candidate, round)) {
      return { visible: true, editable: false, readOnly: true, locked: false };
    }

    if (!this.hasCandidateReached(candidate, round)) {
      if (!isOwnRound) {
        return { visible: true, editable: false, readOnly: false, locked: true, lockedMessage: 'Not reached yet.' };
      }
      const index = ROUND_SEQUENCE.indexOf(round);
      const prevLabel = index === 0 ? 'Pre-Screen' : ROUND_LABEL[ROUND_SEQUENCE[index - 1]];
      return {
        visible: true, editable: false, readOnly: false, locked: true,
        lockedMessage: `Waiting for ${prevLabel} to complete.`
      };
    }

    if (!isOwnRound) {
      return { visible: true, editable: false, readOnly: true, locked: false };
    }

    const alreadyFinal = this.getRound(candidate, round).selection !== RoundStatus.Pending;
    return { visible: true, editable: !alreadyFinal, readOnly: alreadyFinal, locked: false };
  }

  private getRound(candidate: Candidate, key: RoundKey): InterviewRound {
    return candidate[key];
  }
}
```

## 2. `candidate-detail.component.ts` (rewritten — no tab state, just scroll)

```typescript
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateService } from '../../core/services/candidate.service';
import { WorkflowVisibilityService, RoundKey } from '../../core/services/workflow-visibility.service';
import { Candidate } from '../../core/models/candidate.model';
import { WorkflowStage, WORKFLOW_STAGE_LABEL } from '../../core/models/workflow-stage.enum';

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

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }

  scrollTo(sectionId: string): void {
    document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  get candidateInitials(): string {
    if (!this.candidate?.candidateName) return '';
    return this.candidate.candidateName
      .split(' ')
      .map(n => n[0])
      .join('')
      .substring(0, 2)
      .toUpperCase();
  }

  get stageLabel(): string {
    return this.currentStage ? WORKFLOW_STAGE_LABEL[this.currentStage] : '';
  }

  // Section visibility, computed once for the template
  get showPreScreen(): boolean {
    return !!this.candidate && this.visibility.isRoundInViewerAccess('techRound1');
    // pre-screen is visible to anyone with at least Tech Round 1 access
  }

  get showTechTable(): boolean {
    if (!this.candidate) return false;
    return this.visibility.isRoundInViewerAccess('techRound1')
      && this.visibility.hasCandidateReached(this.candidate, 'techRound1');
  }

  get showTechRound2Column(): boolean {
    if (!this.candidate) return false;
    return this.visibility.isRoundInViewerAccess('techRound2Mgmt');
  }

  get showOnShore(): boolean {
    if (!this.candidate) return false;
    return this.visibility.isRoundInViewerAccess('onShoreRound')
      && this.visibility.hasCandidateReached(this.candidate, 'onShoreRound');
  }

  get showHr(): boolean {
    if (!this.candidate) return false;
    return this.visibility.isRoundInViewerAccess('hrRound')
      && this.visibility.hasCandidateReached(this.candidate, 'hrRound');
  }
}
```

## 3. `candidate-detail.component.html` (rewritten — one scroll, sticky jump-nav)

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
          <span>{{ candidate.roleDesignation }} · {{ candidate.profile }}</span>
        </div>
      </div>
      <div class="hero-id">
        <div class="id-label">Stage</div>
        <div class="id-value">{{ stageLabel }}</div>
      </div>
    </div>

    <nav class="jump-nav">
      <button (click)="scrollTo('general')">General</button>
      <button *ngIf="showPreScreen" (click)="scrollTo('prescreen')">Pre-Screen</button>
      <button *ngIf="showTechTable" (click)="scrollTo('tech')">Technical</button>
      <button *ngIf="showOnShore" (click)="scrollTo('onshore')">On-Shore</button>
      <button *ngIf="showHr" (click)="scrollTo('hr')">HR Final</button>
    </nav>

    <div class="detail-sections">
      <section id="general">
        <app-general-details-panel [candidate]="candidate"></app-general-details-panel>
      </section>

      <section id="prescreen" *ngIf="showPreScreen">
        <app-prescreen-panel [candidate]="candidate"></app-prescreen-panel>
      </section>

      <section id="tech" *ngIf="showTechTable">
        <app-tech-round-panel [candidate]="candidate" [showRound2]="showTechRound2Column"></app-tech-round-panel>
      </section>

      <section id="onshore" *ngIf="showOnShore">
        <app-onshore-panel [candidate]="candidate"></app-onshore-panel>
      </section>

      <section id="hr" *ngIf="showHr">
        <app-hr-panel [candidate]="candidate"></app-hr-panel>
      </section>
    </div>

  </ng-container>
</div>
```

## 4. `candidate-detail.component.scss`

```scss
.detail-page {
  padding: 24px 32px 64px;
  background: var(--cohort-canvas);
  min-height: 100%;
}

.detail-topbar { margin-bottom: 16px; }

.back-link {
  background: none;
  border: none;
  color: var(--cohort-primary);
  font-weight: 600;
  font-size: 13.5px;
  cursor: pointer;
  padding: 0;
  transition: opacity 0.15s ease;
  &:hover { opacity: 0.65; }
}

.candidate-hero {
  display: flex;
  align-items: center;
  gap: 18px;
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 12px;
  padding: 20px 24px;
  margin-bottom: 16px;
  animation: fadeUp 0.25s ease;
}

.hero-avatar {
  width: 56px; height: 56px;
  border-radius: 14px;
  background: var(--cohort-primary);
  color: white;
  font-size: 20px; font-weight: 700;
  display: flex; align-items: center; justify-content: center;
  flex-shrink: 0;
}

.hero-info {
  flex: 1;
  h1 { margin: 0 0 4px; font-size: 20px; color: var(--cohort-text); }
  .hero-meta { font-size: 13px; color: var(--cohort-muted); .dot { margin: 0 6px; } }
}

.hero-id {
  text-align: right;
  .id-label { font-size: 10px; text-transform: uppercase; color: var(--cohort-muted); letter-spacing: 0.05em; }
  .id-value { font-weight: 700; color: var(--cohort-primary); font-size: 14px; }
}

.jump-nav {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  gap: 8px;
  background: var(--cohort-canvas);
  padding: 10px 0 16px;

  button {
    background: white;
    border: 1px solid var(--cohort-border);
    color: var(--cohort-muted);
    padding: 7px 16px;
    border-radius: 999px;
    font-size: 12.5px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.15s ease;

    &:hover {
      border-color: var(--cohort-primary);
      color: var(--cohort-primary);
      transform: translateY(-1px);
    }
  }
}

.detail-sections {
  display: flex;
  flex-direction: column;
  gap: 16px;

  section {
    scroll-margin-top: 60px;
    animation: fadeUp 0.3s ease;
  }
}

@keyframes fadeUp {
  from { opacity: 0; transform: translateY(6px); }
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

## 5. Shared panel shell style — `_panel-shared.scss` (new, put in `styles/`)

```scss
.panel {
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 12px;
  padding: 22px 26px;
  transition: box-shadow 0.2s ease;

  &:hover { box-shadow: 0 2px 10px rgba(0,0,0,0.04); }

  h3 { margin: 0 0 18px; font-size: 15px; color: var(--cohort-text); }
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18px 24px;
}

.detail-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--cohort-muted); }
  span, a { font-size: 14px; color: var(--cohort-text); font-weight: 500; }
  a { color: var(--cohort-primary); text-decoration: none; &:hover { text-decoration: underline; } }
}

.chip {
  display: inline-flex;
  width: fit-content;
  padding: 3px 12px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
  &--pass { background: var(--status-pass-bg); color: var(--status-pass-text); }
  &--reject { background: var(--status-reject-bg); color: var(--status-reject-text); }
  &--wait { background: var(--status-wait-bg); color: var(--status-wait-text); }
}

.lock-badge {
  font-size: 12px;
  color: var(--status-wait-text);
  background: var(--status-wait-bg);
  padding: 4px 10px;
  border-radius: 999px;
}
```

## 6. `general-details-panel.component.ts` (with view-more expand)

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
  expanded = false;

  toggle(): void { this.expanded = !this.expanded; }
}
```

## 7. `general-details-panel.component.html`

```html
<div class="panel">
  <div class="panel-header">
    <h3>Candidate Details</h3>
    <button class="expand-btn" (click)="toggle()">
      {{ expanded ? 'View less' : 'View more' }}
      <span class="chevron" [class.chevron--open]="expanded">⌄</span>
    </button>
  </div>

  <div class="detail-grid">
    <div class="detail-field"><label>Candidate ID</label><span>{{ candidate.candidateId || '—' }}</span></div>
    <div class="detail-field"><label>Full Name</label><span>{{ candidate.candidateName || '—' }}</span></div>
    <div class="detail-field"><label>Email</label><span>{{ candidate.candidateEmailId || '—' }}</span></div>
    <div class="detail-field"><label>Phone</label><span>{{ candidate.candidatePhoneNumber || '—' }}</span></div>
    <div class="detail-field"><label>Role / Designation</label><span>{{ candidate.roleDesignation || '—' }}</span></div>
    <div class="detail-field"><label>Profile</label><span>{{ candidate.profile || '—' }}</span></div>
  </div>

  <div class="expand-panel" [class.expand-panel--open]="expanded">
    <div class="detail-grid">
      <div class="detail-field"><label>Location</label><span>{{ candidate.location || '—' }}</span></div>
      <div class="detail-field"><label>Allocated Biz Line</label><span>{{ candidate.allocatedBizLine || '—' }}</span></div>
      <div class="detail-field" *ngIf="candidate.cvUpload">
        <label>CV</label>
        <a [href]="candidate.cvUpload" target="_blank">View CV ↗</a>
      </div>
      <div class="detail-field" *ngIf="candidate.hireproResultsUpload">
        <label>Hirepro Results</label>
        <a [href]="candidate.hireproResultsUpload" target="_blank">View results ↗</a>
      </div>
    </div>
  </div>
</div>
```

## 8. `general-details-panel.component.scss`

```scss
@import '../../../../../styles/panel-shared';

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
  h3 { margin: 0; }
}

.expand-btn {
  background: none;
  border: none;
  color: var(--cohort-primary);
  font-size: 12.5px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
  transition: opacity 0.15s ease;
  &:hover { opacity: 0.7; }
}

.chevron {
  transition: transform 0.2s ease;
  &--open { transform: rotate(180deg); }
}

.expand-panel {
  max-height: 0;
  overflow: hidden;
  transition: max-height 0.25s ease, opacity 0.2s ease, margin-top 0.25s ease;
  opacity: 0;

  &--open {
    max-height: 300px;
    opacity: 1;
    margin-top: 18px;
    padding-top: 18px;
    border-top: 1px solid var(--cohort-border);
  }
}
```

## 9. `prescreen-panel.component.ts` / `.html` / `.scss`

```typescript
import { Component, Input } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';

@Component({
  selector: 'app-prescreen-panel',
  templateUrl: './prescreen-panel.component.html',
  styleUrls: ['./prescreen-panel.component.scss']
})
export class PrescreenPanelComponent {
  @Input() candidate!: Candidate;
}
```

```html
<div class="panel">
  <h3>Pre-Screening</h3>
  <div class="detail-grid">
    <div class="detail-field"><label>Score</label><span>{{ candidate.prescreeningScore || '—' }} / 100</span></div>
    <div class="detail-field">
      <label>Result</label>
      <span class="chip" [ngClass]="{
        'chip--pass': candidate.prescreeningSelected === 'Selected',
        'chip--reject': candidate.prescreeningSelected === 'Rejected',
        'chip--wait': !candidate.prescreeningSelected || candidate.prescreeningSelected === 'Pending'
      }">{{ candidate.prescreeningSelected || 'Pending' }}</span>
    </div>
    <div class="detail-field"><label>Test Date</label><span>{{ candidate.prescreeningTestDate || '—' }}</span></div>
    <div class="detail-field" *ngIf="candidate.prescreeningTestL