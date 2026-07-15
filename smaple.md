Full rebuild below — models, services, dashboard, and the single-scroll detail page with the Management Round split, self-assign locking, and viewer-scoped editable fields.

## Updated folder structure

```
src/app/core/models/
  round-status.enum.ts        (updated)
  workflow-stage.enum.ts      (updated)
  candidate.model.ts          (updated)
src/app/core/services/
  current-user.service.ts     (updated — switchable mock)
  workflow-visibility.service.ts (rewritten — assign-to-me + role scoping)
  candidate.service.ts        (updated mapping)
src/app/features/candidate-dashboard/
  candidate-dashboard.component.ts   (updated)
  candidate-dashboard.component.html (updated)
src/app/features/candidate-detail/
  candidate-detail.component.ts/html/scss (updated)
  components/
    prescreen-panel/           (updated)
    tech-round-panel/          (rewritten — combined TR1/TR2, capped)
    mgmt-panel/                 (NEW)
    onshore-panel/              (updated)
    hr-panel/                   (updated)
  candidate-detail.module.ts   (updated)
```

---

## 1. `core/models/round-status.enum.ts`

```typescript
export enum RoundStatus {
  Pending = 'Pending',
  Selected = 'Selected',
  Rejected = 'Rejected',
  NotApplicable = 'N/A'
}
```

## 2. `core/models/workflow-stage.enum.ts`

```typescript
export enum WorkflowStage {
  Unassigned = 'Unassigned',
  PreScreen = 'PreScreen',
  TechRound1 = 'TechRound1',
  TechRound2 = 'TechRound2',
  ManagementRound = 'ManagementRound',
  OnShore = 'OnShore',
  HrFinal = 'HrFinal',
  OfferStage = 'OfferStage',
  Rejected = 'Rejected'
}

export const WORKFLOW_STAGE_LABEL: Record<WorkflowStage, string> = {
  [WorkflowStage.Unassigned]: 'Not assigned',
  [WorkflowStage.PreScreen]: 'Pre-Screen',
  [WorkflowStage.TechRound1]: 'Tech Round 1',
  [WorkflowStage.TechRound2]: 'Tech Round 2',
  [WorkflowStage.ManagementRound]: 'Management Round',
  [WorkflowStage.OnShore]: 'On-Shore Round',
  [WorkflowStage.HrFinal]: 'HR Round — final',
  [WorkflowStage.OfferStage]: 'Offer Stage',
  [WorkflowStage.Rejected]: 'Rejected'
};

export const WORKFLOW_STAGE_ORDER: WorkflowStage[] = [
  WorkflowStage.PreScreen,
  WorkflowStage.TechRound1,
  WorkflowStage.TechRound2,
  WorkflowStage.ManagementRound,
  WorkflowStage.OnShore,
  WorkflowStage.HrFinal
];
```

## 3. `core/models/candidate.model.ts`

```typescript
import { RoundStatus } from './round-status.enum';

export interface SkillAssessment {
  skillName: string;
  screeningScore: string;
  techRound1Comment: string;
  techRound2Comment: string; // 'N/A' when Tech Round 2 is skipped
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

export interface ManagementRound extends InterviewRound {
  communication: string | null;
  attitude: string | null;
  culturalFit: string | null;
  leadership: string | null;
  otherTopics: string | null;
}

export interface Candidate {
  id: number;
  listSource: 'Chennai' | 'Mumbai';

  candidateId: string;
  candidateName: string;
  candidateEmailID: string;
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
  techRound2: InterviewRound;      // selection may be RoundStatus.NotApplicable
  mgmtRound: ManagementRound;
  onShoreRound: InterviewRound;
  hrRound: InterviewRound;

  offerSent: boolean;
  offerAccepted: boolean;

  cvUpload: string;
  hireproResultsUpload: string;
}
```

## 4. `core/services/current-user.service.ts` (rewritten — switch role here to test)

```typescript
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

export type UserRole = 'HrAdmin' | 'Recruiter' | 'TechPanel' | 'MgmtPanel' | 'OnShorePanel' | 'HrPanel';

export interface CurrentUser {
  id: number;
  title: string;
  email: string;
  role: UserRole;
}

/**
 * ===== SWITCH THIS to test different roles/permissions while real
 * SharePoint group → role mapping isn't wired up yet. =====
 *
 * Try changing `role` to any of: 'HrAdmin' | 'Recruiter' | 'TechPanel'
 * | 'MgmtPanel' | 'OnShorePanel' | 'HrPanel' and reload to see the
 * dashboard/detail page behave differently for each.
 */
const MOCK_USER: CurrentUser = {
  id: 101,
  title: 'Arun Kumar',
  email: 'arun.kumar@xyzxyz.com',
  role: 'TechPanel'
};

@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private user: CurrentUser = MOCK_USER;

  get(): CurrentUser {
    return this.user;
  }

  /** Kept for later — swap this to a real /_api/web/currentuser + group lookup. */
  loadCurrentUser(): Observable<CurrentUser> {
    return of(this.user);
  }
}
```

## 5. `core/services/workflow-visibility.service.ts` (full rewrite)

```typescript
import { Injectable } from '@angular/core';
import { Candidate, InterviewRound, SharePointUser } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';
import { CurrentUserService, UserRole } from './current-user.service';

export type RoundKey = 'techRound1' | 'techRound2' | 'mgmtRound' | 'onShoreRound' | 'hrRound';

export interface RoundVisibility {
  visible: boolean;
  editable: boolean;
  readOnly: boolean;
  notApplicable: boolean;
  canAssignToMe: boolean;
  assignedToOther: boolean;
}

const ROUND_SEQUENCE: RoundKey[] = ['techRound1', 'techRound2', 'mgmtRound', 'onShoreRound', 'hrRound'];

/** Which rounds each role is allowed to act on. Tech panel covers both tech rounds. */
const ROLE_ACTIONABLE_ROUNDS: Record<UserRole, RoundKey[]> = {
  HrAdmin: ROUND_SEQUENCE,
  Recruiter: [],
  TechPanel: ['techRound1', 'techRound2'],
  MgmtPanel: ['mgmtRound'],
  OnShorePanel: ['onShoreRound'],
  HrPanel: ['hrRound']
};

/** Self-service rounds: the panel member claims it themselves.
 *  Admin-assign rounds: HR Admin assigns a specific person via the Assign Panel screen. */
const SELF_ASSIGN_ROUNDS: RoundKey[] = ['techRound1', 'techRound2', 'mgmtRound'];

@Injectable({ providedIn: 'root' })
export class WorkflowVisibilityService {

  constructor(private currentUser: CurrentUserService) {}

  // ---------- Progress / gating ----------

  hasCandidateReached(candidate: Candidate, round: RoundKey): boolean {
    switch (round) {
      case 'techRound1':
        return candidate.prescreeningSelected === RoundStatus.Selected;
      case 'techRound2':
        return candidate.techRound1.selection === RoundStatus.Selected;
      case 'mgmtRound': {
        const tr2 = candidate.techRound2.selection;
        if (tr2 === RoundStatus.Selected) return true;
        if (tr2 === RoundStatus.NotApplicable) return candidate.techRound1.selection === RoundStatus.Selected;
        return false;
      }
      case 'onShoreRound':
        return candidate.mgmtRound.selection === RoundStatus.Selected;
      case 'hrRound':
        return candidate.onShoreRound.selection === RoundStatus.Selected;
    }
  }

  isPipelineTerminated(candidate: Candidate, upToRound: RoundKey): boolean {
    if (candidate.prescreeningSelected === RoundStatus.Rejected) return true;
    const index = ROUND_SEQUENCE.indexOf(upToRound);
    for (let i = 0; i < index; i++) {
      const r = this.getRound(candidate, ROUND_SEQUENCE[i]);
      if (r.selection === RoundStatus.Rejected) return true;
    }
    return false;
  }

  /** The single round the candidate is currently sitting at (first reached-but-not-finalized round). Null if fully done or fully rejected. */
  getCurrentActiveRound(candidate: Candidate): RoundKey | null {
    if (this.isPipelineTerminated(candidate, 'hrRound')) return null;
    for (const round of ROUND_SEQUENCE) {
      if (round === 'techRound2' && candidate.techRound2.selection === RoundStatus.NotApplicable) continue;
      if (this.hasCandidateReached(candidate, round)) {
        const r = this.getRound(candidate, round);
        if (r.selection === RoundStatus.Pending) return round;
      }
    }
    return null;
  }

  // ---------- Viewer access scope ----------

  getMaxVisibleRoundIndex(): number {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return ROUND_SEQUENCE.length - 1;
    const rounds = ROLE_ACTIONABLE_ROUNDS[user.role];
    if (!rounds.length) return -1;
    return ROUND_SEQUENCE.indexOf(rounds[rounds.length - 1]);
  }

  isRoundInViewerAccess(round: RoundKey): boolean {
    return ROUND_SEQUENCE.indexOf(round) <= this.getMaxVisibleRoundIndex();
  }

  // ---------- Per-round visibility for rendering a section ----------

  getVisibilityFor(candidate: Candidate, round: RoundKey): RoundVisibility {
    const user = this.currentUser.get();

    if (!this.isRoundInViewerAccess(round)) {
      return { visible: false, editable: false, readOnly: false, notApplicable: false, canAssignToMe: false, assignedToOther: false };
    }

    if (round === 'techRound2' && candidate.techRound2.selection === RoundStatus.NotApplicable) {
      return { visible: true, editable: false, readOnly: true, notApplicable: true, canAssignToMe: false, assignedToOther: false };
    }

    const roundData = this.getRound(candidate, round);
    const finalized = roundData.selection === RoundStatus.Selected || roundData.selection === RoundStatus.Rejected;
    const assignedToMe = roundData.interviewedBy?.id === user.id;
    const assignedToOther = !!roundData.interviewedBy && !assignedToMe;

    if (user.role === 'HrAdmin') {
      return { visible: true, editable: !finalized, readOnly: finalized, notApplicable: false, canAssignToMe: false, assignedToOther: false };
    }
    if (user.role === 'Recruiter') {
      return { visible: true, editable: false, readOnly: true, notApplicable: false, canAssignToMe: false, assignedToOther: false };
    }

    const canActOnRound = ROLE_ACTIONABLE_ROUNDS[user.role].includes(round);
    if (!canActOnRound) {
      // within their max-access range but not a round they personally act on (e.g. TechPanel viewing... n/a here since actionable==access, but kept for future roles)
      return { visible: true, editable: false, readOnly: true, notApplicable: false, canAssignToMe: false, assignedToOther };
    }

    if (this.isPipelineTerminated(candidate, round)) {
      return { visible: true, editable: false, readOnly: true, notApplicable: false, canAssignToMe: false, assignedToOther: false };
    }

    if (!this.hasCandidateReached(candidate, round)) {
      return { visible: false, editable: false, readOnly: false, notApplicable: false, canAssignToMe: false, assignedToOther: false };
    }

    if (assignedToOther) {
      return { visible: true, editable: false, readOnly: false, notApplicable: false, canAssignToMe: false, assignedToOther: true };
    }

    if (!roundData.interviewedBy) {
      const isCurrentTurn = this.getCurrentActiveRound(candidate) === round;
      return { visible: true, editable: false, readOnly: false, notApplicable: false, canAssignToMe: isCurrentTurn, assignedToOther: false };
    }

    // assigned to me
    return { visible: true, editable: !finalized, readOnly: finalized, notApplicable: false, canAssignToMe: false, assignedToOther: false };
  }

  // ---------- Dashboard-level: can this viewer open the candidate at all ----------

  canOpenCandidate(candidate: Candidate): boolean {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return true;

    const myRounds = ROLE_ACTIONABLE_ROUNDS[user.role];
    const active = this.getCurrentActiveRound(candidate);

    for (const r of myRounds) {
      const round = this.getRound(candidate, r);
      if (round.interviewedBy?.id === user.id) return true;       // their own round, any state
      if (active === r && !round.interviewedBy) return true;      // open + it's this candidate's turn
    }
    return false;
  }

  /** Small helper for the dashboard row: what to show in the Action column. */
  getDashboardActionState(candidate: Candidate): 'open' | 'assign' | 'locked' {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return 'open';

    const myRounds = ROLE_ACTIONABLE_ROUNDS[user.role];
    const active = this.getCurrentActiveRound(candidate);

    for (const r of myRounds) {
      const round = this.getRound(candidate, r);
      if (round.interviewedBy?.id === user.id) return 'open';
      if (active === r && !round.interviewedBy) return 'assign';
    }
    return 'locked';
  }

  // ---------- Mutations (mock — replace bodies with SharePoint PATCH calls later) ----------

  assignToMe(candidate: Candidate, round: RoundKey): void {
    const user = this.currentUser.get();
    const spUser: SharePointUser = { id: user.id, title: user.title, email: user.email };
    this.getRound(candidate, round).interviewedBy = spUser;
    // TODO: SharePoint PATCH — set {Round}InterviewedBy on the list item.
  }

  submitRound(
    candidate: Candidate,
    round: RoundKey,
    feedback: string,
    decision: RoundStatus.Selected | RoundStatus.Rejected,
    mgmtExtras?: Partial<Pick<import('../models/candidate.model').ManagementRound,
      'communication' | 'attitude' | 'culturalFit' | 'leadership' | 'otherTopics'>>
  ): void {
    const r = this.getRound(candidate, round);
    r.interviewFeedback = feedback;
    r.selection = decision;
    r.interviewDate = new Date().toISOString().split('T')[0];
    if (round === 'mgmtRound' && mgmtExtras) {
      Object.assign(candidate.mgmtRound, mgmtExtras);
    }
    // TODO: SharePoint PATCH — set {Round}InterviewFeedback/Selection/Date on the list item.
  }

  private getRound(candidate: Candidate, key: RoundKey): InterviewRound {
    return candidate[key];
  }
}
```

## 6. `core/services/candidate.service.ts` (updated mapping)

```typescript
import { Injectable } from '@angular/core';
import { Observable, forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import { SharePointService } from './sharepoint.service';
import { Candidate, SkillAssessment } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';
import { WorkflowStage } from '../models/workflow-stage.enum';

const SKILL_KEYS = ['One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten'];

const SELECT_FIELDS = [
  'Id', 'CandidateId', 'CandidateName', 'CandidateEmailId', 'CandidatePhoneNumber',
  'Location', 'RoleDesignation', 'Profile', 'AllocatedBizLine',
  'PrescreeningTestLink', 'PrescreeningTestDate', 'PrescreeningScore',
  'PrescreeningSelected', 'PrescreeningComments',
  ...SKILL_KEYS.flatMap(k => [`Skill${k}`, `Skill${k}Score`, `Skill${k}TROne`, `Skill${k}TRTwo`]),
  'TechRound1InterviewDate', 'TechRound1InterviewFeedback', 'TechRound1InterviewSelection',
  'TechRound2InterviewDate', 'TechRound2InterviewFeedback', 'TechRound2InterviewSelection',
  'MgmtRoundInterviewDate', 'MgmtRoundCommunication', 'MgmtRoundAttitude', 'MgmtRoundCulturalFit',
  'MgmtRoundLeadership', 'MgmtRoundOtherTopics', 'MgmtRoundInterviewSelection',
  'OnShoreRoundInterviewDate', 'OnShoreRoundInterviewFeedback', 'OnShoreRoundInterviewSelection',
  'HrRoundInterviewDate', 'HrRoundInterviewFeedback', 'HrRoundInterviewSelection',
  'AllocatedOfferSent', 'AllocatedOfferAccepted',
  'CvUpload', 'HireproResultsUpload',
  'TechRound1InterviewedBy/Id', 'TechRound1InterviewedBy/Title', 'TechRound1InterviewedBy/EMail',
  'TechRound2InterviewedBy/Id', 'TechRound2InterviewedBy/Title', 'TechRound2InterviewedBy/EMail',
  'MgmtRoundInterviewedBy/Id', 'MgmtRoundInterviewedBy/Title', 'MgmtRoundInterviewedBy/EMail',
  'OnShoreRoundInterviewedBy/Id', 'OnShoreRoundInterviewedBy/Title', 'OnShoreRoundInterviewedBy/EMail',
  'HrRoundInterviewedBy/Id', 'HrRoundInterviewedBy/Title', 'HrRoundInterviewedBy/EMail'
];

const EXPAND_FIELDS = [
  'TechRound1InterviewedBy', 'TechRound2InterviewedBy', 'MgmtRoundInterviewedBy',
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
    return forkJoin([this.getCandidates('Chennai'), this.getCandidates('Mumbai')]).pipe(
      map(([chennai, mumbai]) => [...chennai, ...mumbai])
    );
  }

  private mapToCandidate(item: any, source: 'Chennai' | 'Mumbai'): Candidate {
    return {
      id: item.Id,
      listSource: source,
      candidateId: item.CandidateId,
      candidateName: item.CandidateName,
      candidateEmailID: item.CandidateEmailId,
      candidatePhoneNumber: item.CandidatePhoneNumber,
      location: item.Location,
      roleDesignation: item.RoleDesignation,
      profile: item.Profile,
      allocatedBizLine: item.AllocatedBizLine,

      prescreeningTestLink: item.PrescreeningTestLink,
      prescreeningTestDate: item.PrescreeningTestDate,
      prescreeningScore: item.PrescreeningScore,
      prescreeningSelected: this.toStatus(item.PrescreeningSelected),
      prescreeningComments: item.PrescreeningComments,

      skills: this.mapSkills(item),

      techRound1: {
        interviewedBy: this.mapUser(item.TechRound1InterviewedBy),
        interviewDate: item.TechRound1InterviewDate,
        interviewFeedback: item.TechRound1InterviewFeedback,
        selection: this.toStatus(item.TechRound1InterviewSelection)
      },
      techRound2: {
        interviewedBy: this.mapUser(item.TechRound2InterviewedBy),
        interviewDate: item.TechRound2InterviewDate,
        interviewFeedback: item.TechRound2InterviewFeedback,
        selection: this.toStatus(item.TechRound2InterviewSelection)
      },
      mgmtRound: {
        interviewedBy: this.mapUser(item.MgmtRoundInterviewedBy),
        interviewDate: item.MgmtRoundInterviewDate,
        interviewFeedback: null,
        selection: this.toStatus(item.MgmtRoundInterviewSelection),
        communication: item.MgmtRoundCommunication,
        attitude: item.MgmtRoundAttitude,
        culturalFit: item.MgmtRoundCulturalFit,
        leadership: item.MgmtRoundLeadership,
        otherTopics: item.MgmtRoundOtherTopics
      },
      onShoreRound: {
        interviewedBy: this.mapUser(item.OnShoreRoundInterviewedBy),
        interviewDate: item.OnShoreRoundInterviewDate,
        interviewFeedback: item.OnShoreRoundInterviewFeedback,
        selection: this.toStatus(item.OnShoreRoundInterviewSelection)
      },
      hrRound: {
        interviewedBy: this.mapUser(item.HrRoundInterviewedBy),
        interviewDate: item.HrRoundInterviewDate,
        interviewFeedback: item.HrRoundInterviewFeedback,
        selection: this.toStatus(item.HrRoundInterviewSelection)
      },

      offerSent: item.AllocatedOfferSent === 'Yes',
      offerAccepted: item.AllocatedOfferAccepted === 'Yes',

      cvUpload: item.CvUpload,
      hireproResultsUpload: item.HireproResultsUpload
    };
  }

  private mapSkills(item: any): SkillAssessment[] {
    return SKILL_KEYS
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
    return 