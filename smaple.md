Good catch — this matters a lot. The real SharePoint schema has **no `SkillXMgmt` columns at all**, and Management Round only has `MgmtRoundInterviewedBy/Date/Feedback/Selection` — the same shape as Tech Round 1/2, not the Communication/Attitude/Cultural Fit/Leadership fields I built earlier. Let me correct every file that assumed those.

## 1. `core/models/candidate.model.ts` (corrected)

```typescript
import { RoundStatus } from './round-status.enum';

export interface SkillAssessment {
  skillName: string;
  screeningScore: string;
  techRound1Comment: string;
  techRound2Comment: string;
  // No mgmtRoundComment — SharePoint has no SkillXMgmt columns.
}

export interface SharePointUser {
  id: number;
  title: string;
  email: string;
}

export interface InterviewRound {
  interviewedBy: SharePointUser | null;
  interviewDate: string | null;
  interviewFeedback: string | null;
  selection: RoundStatus;
}

export interface OnShoreRound {
  interviewedBy: SharePointUser | null;
  interviewDate: string | null;
  department: string | null;
  selection: RoundStatus;
}

export interface HrRound {
  interviewedBy: SharePointUser | null;
  interviewDate: string | null;
  selection: RoundStatus;
  offerSent: boolean;
  offerAccepted: boolean;
}

export interface Candidate {
  id: number;
  listSource: 'Chennai' | 'Mumbai';

  candidateId: string;
  candidateName: string;
  candidateEmailId: string;
  candidatePhoneNumber: string;
  location: string;
  roleDesignation: string;
  profile: string;
  allocatedBizLine: string;

  prescreeningTestLink: string;
  prescreeningTestDate: string | null;
  prescreeningScore: string;
  prescreeningSelected: RoundStatus;
  prescreeningComments: string;

  skills: SkillAssessment[];

  techRound1: InterviewRound;
  techRound2: InterviewRound;
  mgmtRound: InterviewRound;      // same shape as tech rounds now — no separate sub-fields
  onShoreRound: OnShoreRound;
  hrRound: HrRound;

  cvUpload: string;
  hireproResultsUpload: string;
}

export const DEPARTMENT_OPTIONS = [
  'CIB — Markets Tech',
  'CIB — Digital',
  'CIB — Risk Technology',
  'CIB — Trade Finance',
  'Global Markets Ops',
  'Corporate Banking Tech'
];
```

## 2. `core/services/candidate.service.ts` — corrected `SELECT_FIELDS` and mapping

Replace the Management Round section of `SELECT_FIELDS`:

```typescript
const SELECT_FIELDS = [
  'Id','CandidateId','CandidateName','CandidateEmailId','CandidatePhoneNumber',
  'Location','RoleDesignation','Profile','AllocatedBizLine',
  'PrescreeningTestLink','PrescreeningTestDate','PrescreeningScore','PrescreeningSelected','PrescreeningComments',
  ...SKILL_KEYS.flatMap(k => [`Skill${k}`,`Skill${k}Score`,`Skill${k}TROne`,`Skill${k}TRTwo`]), // no Mgmt variant
  'TechRound1InterviewDate','TechRound1InterviewFeedback','TechRound1InterviewSelection',
  'TechRound2InterviewDate','TechRound2InterviewFeedback','TechRound2InterviewSelection',
  'MgmtRoundInterviewDate','MgmtRoundInterviewFeedback','MgmtRoundInterviewSelection',
  'OnshoreRoundInterviewDate','OnshoreRoundDepartment','OnshoreRoundInterviewSelection',
  'HrRoundInterviewDate','HrRoundInterviewSelection','HrRoundOfferSent','HrRoundOfferAccepted',
  'CvUpload','HireproResultsUpload',
  'TechRound1InterviewedBy/Id','TechRound1InterviewedBy/Title','TechRound1InterviewedBy/EMail',
  'TechRound2InterviewedBy/Id','TechRound2InterviewedBy/Title','TechRound2InterviewedBy/EMail',
  'MgmtRoundInterviewedBy/Id','MgmtRoundInterviewedBy/Title','MgmtRoundInterviewedBy/EMail',
  'OnshoreRoundInterviewedBy/Id','OnshoreRoundInterviewedBy/Title','OnshoreRoundInterviewedBy/EMail',
  'HrRoundInterviewedBy/Id','HrRoundInterviewedBy/Title','HrRoundInterviewedBy/EMail'
];
```

Note: your schema uses **`OnshoreRound`** (lowercase `s`), not `OnShoreRound` — I had the casing wrong. Confirm that against your sheet before running; SharePoint internal names are case-sensitive in REST calls.

Replace the `mgmtRound` mapping inside `mapToCandidate`:

```typescript
mgmtRound: {
  interviewedBy: this.mapUser(item.MgmtRoundInterviewedBy),
  interviewDate: item.MgmtRoundInterviewDate,
  interviewFeedback: item.MgmtRoundInterviewFeedback,
  selection: this.toStatus(item.MgmtRoundInterviewSelection)
},
```

And drop `mgmtRoundComment` out of `mapSkills`:

```typescript
private mapSkills(item: any): SkillAssessment[] {
  return SKILL_KEYS.map(k => ({
    skillName: item[`Skill${k}`],
    screeningScore: item[`Skill${k}Score`],
    techRound1Comment: item[`Skill${k}TROne`],
    techRound2Comment: item[`Skill${k}TRTwo`]
  })).filter(s => !!s.skillName);
}
```

## 3. `mgmt-panel.component.ts` (simplified — matches Tech Round pattern exactly, no per-skill editing)

```typescript
import { Component, Input, OnChanges } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';
import { WorkflowVisibilityService, RoundAccess } from '../../../../core/services/workflow-visibility.service';
import { CandidateService } from '../../../../core/services/candidate.service';
import { CurrentUserService } from '../../../../core/services/current-user.service';

@Component({
  selector: 'app-mgmt-panel',
  templateUrl: './mgmt-panel.component.html',
  styleUrls: ['./mgmt-panel.component.scss']
})
export class MgmtPanelComponent implements OnChanges {
  @Input() candidate!: Candidate;

  access!: RoundAccess;
  feedback = '';
  decision: 'Selected' | 'Rejected' | null = null;
  showSkipDialog = false;

  constructor(
    private visibility: WorkflowVisibilityService,
    private candidateService: CandidateService,
    private currentUser: CurrentUserService
  ) {}

  ngOnChanges(): void {
    this.access = this.visibility.getAccess(this.candidate, 'mgmtRound');
    this.feedback = this.candidate.mgmtRound.interviewFeedback || '';
  }

  get needsSkipConfirm(): boolean {
    return this.access.state === 'assignable' && this.visibility.techRound2NotYetDecided(this.candidate);
  }

  onAssignClick(): void {
    if (this.needsSkipConfirm) { this.showSkipDialog = true; return; }
    this.doAssign();
  }

  confirmSkipAndAssign(): void {
    this.candidate.techRound2.selection = 'N/A' as any;
    this.showSkipDialog = false;
    this.doAssign();
  }

  cancelSkip(): void { this.showSkipDialog = false; }

  private doAssign(): void {
    const user = this.currentUser.get();
    const spUser = { id: user.id, title: user.title, email: user.email };
    this.candidateService.assignUserToRound(this.candidate, 'MgmtRound', spUser).subscribe(() => {
      this.candidate.mgmtRound.interviewedBy = spUser;
      this.ngOnChanges();
    });
  }

  submit(): void {
    if (!this.decision) return;
    const fields = {
      MgmtRoundInterviewFeedback: this.feedback,
      MgmtRoundInterviewSelection: this.decision,
      MgmtRoundInterviewDate: new Date().toISOString().split('T')[0]
    };
    this.candidateService.submitRoundDecision(this.candidate, fields).subscribe(() => {
      this.candidate.mgmtRound.interviewFeedback = this.feedback;
      this.candidate.mgmtRound.selection = this.decision as any;
      this.ngOnChanges();
    });
  }
}
```

## 4. `mgmt-panel.component.html` (simplified — read-only combined skill table + single feedback field, exactly like Tech rounds)

```html
<div class="panel">
  <h3>Management Round</h3>

  <div class="assign-cta" *ngIf="access.state === 'assignable'">
    <span>Waiting for a Management Round interviewer.</span>
    <button (click)="onAssignClick()">Assign to Me</button>
  </div>
  <p class="locked-note" *ngIf="access.state === 'locked-other' || access.state === 'not-reached'">{{ access.message }}</p>

  <table class="skill-table" *ngIf="access.state === 'mine-editable' || access.state === 'mine-locked'">
    <thead>
      <tr><th>Skill</th><th>Score</th><th>Tech Round 1</th><th *ngIf="candidate.techRound2.selection !== 'N/A'">Tech Round 2</th></tr>
    </thead>
    <tbody>
      <tr *ngFor="let skill of candidate.skills">
        <td class="skill-name">{{ skill.skillName }}</td>
        <td>{{ skill.screeningScore || '—' }}</td>
        <td>{{ skill.techRound1Comment || '—' }}</td>
        <td *ngIf="candidate.techRound2.selection !== 'N/A'">{{ skill.techRound2Comment || '—' }}</td>
      </tr>
    </tbody>
  </table>

  <ng-container *ngIf="access.state === 'mine-editable'">
    <label class="field-label">Management Round Feedback</label>
    <textarea class="feedback-input" [(ngModel)]="feedback" placeholder="Communication, attitude, cultural fit, leadership, overall notes..."></textarea>
    <div class="decision-row">
      <button class="decision-btn decision-btn--select" [class.active]="decision==='Selected'" (click)="decision='Selected'">✓ Selected</button>
      <button class="decision-btn decision-btn--reject" [class.active]="decision==='Rejected'" (click)="decision='Rejected'">✕ Rejected</button>
    </div>
    <button class="submit-btn" [disabled]="!decision" (click)="submit()">Submit Management Round</button>
  </ng-container>

  <div class="round-footer" *ngIf="access.state === 'mine-locked'">
    <div class="detail-field"><label>Interviewed By</label><span>{{ candidate.mgmtRound.interviewedBy?.title || '—' }}</span></div>
    <div class="detail-field"><label>Feedback</label><span>{{ candidate.mgmtRound.interviewFeedback || '—' }}</span></div>
    <div class="detail-field"><label>Decision</label>
      <span class="chip" [ngClass]="{'chip--pass': candidate.mgmtRound.selection==='Selected','chip--reject': candidate.mgmtRound.selection==='Rejected'}">{{ candidate.mgmtRound.selection }}</span>
    </div>
  </div>
</div>

<app-skip-confirm-dialog
  [visible]="showSkipDialog"
  [candidateNames]="[candidate.candidateName]"
  (confirmed)="confirmSkipAndAssign()" (cancelled)="cancelSkip()">
</app-skip-confirm-dialog>
```

## 5. `mgmt-panel.component.scss` (drop the `.mgmt-grid` styles, reuse the tech-round-panel look)

```scss
@import '../../../../styles/panel-shared';
.field-label { font-size: 11.5px; text-transform: uppercase; color: var(--cohort-muted); font-weight: 600; display: block; margin-bottom: 8px; }
.skill-table { width: 100%; border-collapse: collapse; margin-bottom: 24px;
  th { text-align: left; font-size: 12px; text-transform: uppercase; color: var(--cohort-primary-deep); font-weight: 700; padding: 10px 12px; border-bottom: 2px solid var(--cohort-border); }
  td { padding: 12px; font-size: 14px; border-bottom: 1px solid var(--cohort-border); }
  .skill-name { font-weight: 700; }
  tr:hover td { background: var(--cohort-canvas); } }
.round-footer { display: flex; flex-wrap: wrap; gap: 26px; }
```

## Also double-check: `Onshore` vs `OnShore` casing

Your screenshots show `OnshoreRoundInterviewedBy` (lowercase `s`), but I've been writing `OnShoreRound...` in several places (component TS, service field strings). Before running again, grep your codebase for `OnShoreRound` and `OnshoreRound` and pick **one** consistent casing that matches your actual SharePoint internal names exactly — REST calls will silently fail or 400 if the casing doesn't match the real column. Given your screenshot is the source of truth, standardize everything to `OnshoreRound...`.

Want me to do a full pass renaming every `OnShoreRound` reference to `OnshoreRound` across all the files I've given you so far, so it's guaranteed consistent?