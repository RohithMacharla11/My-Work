This is the full rebuild — every file needed for the workflow exactly as you described it. Real SharePoint write calls, not mocked.

## Updated folder structure

```
src/app/core/models/
  candidate.model.ts        (updated)
  round-status.enum.ts       (unchanged)
core/services/
  sharepoint.service.ts      (updated — write support)
  candidate.service.ts       (updated — real PATCH calls)
  current-user.service.ts    (updated — role switch)
  workflow-visibility.service.ts (full rewrite)
shared/components/
  people-picker/              (NEW)
  skip-confirm-dialog/        (NEW)
features/candidate-dashboard/  (rewritten — multi-select, assign bar)
features/candidate-detail/
  candidate-detail.component (updated — hero collapse fix)
  components/
    tech-round-panel/          (rewritten)
    mgmt-panel/                 (rewritten)
    onshore-panel/              (rewritten — people picker)
    hr-panel/                   (rewritten — people picker)
```

---

## 1. `core/models/candidate.model.ts`

```typescript
import { RoundStatus } from './round-status.enum';

export interface SkillAssessment {
  skillName: string;
  screeningScore: string;
  techRound1Comment: string;
  techRound2Comment: string;
  mgmtRoundComment: string;
}

export interface SharePointUser {
  id: number;
  title: string;
  email: string;
}

export interface InterviewRound {
  interviewedBy: SharePointUser | null;
  interviewDate: string | null;
  interviewFeedback: string | null; // "Others" free text for tech/mgmt rounds
  selection: RoundStatus;
}

export interface ManagementRound extends InterviewRound {
  communication: string | null;
  attitude: string | null;
  culturalFit: string | null;
  leadership: string | null;
  otherTopics: string | null;
}

export interface OnShoreRound {
  interviewedBy: SharePointUser | null; // assigned by HR Admin
  interviewDate: string | null;
  department: string | null;
  selection: RoundStatus;
}

export interface HrRound {
  interviewedBy: SharePointUser | null; // assigned by HR Admin
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
  techRound2: InterviewRound;
  mgmtRound: ManagementRound;
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

## 2. `core/services/current-user.service.ts`

```typescript
import { Injectable } from '@angular/core';

export type UserRole = 'HrAdmin' | 'Recruiter' | 'TechPanel' | 'MgmtPanel' | 'OnShorePanel' | 'HrPanel';

export interface CurrentUser {
  id: number;
  title: string;
  email: string;
  role: UserRole;
}

/**
 * ===== SWITCH `role` HERE to test each role's dashboard/detail behavior. =====
 * 'HrAdmin' | 'Recruiter' | 'TechPanel' | 'MgmtPanel' | 'OnShorePanel' | 'HrPanel'
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
  get(): CurrentUser { return this.user; }
}
```

## 3. `core/services/sharepoint.service.ts` (updated — real write support)

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';

export interface SpListResponse<T> { d: { results: T[] }; }

@Injectable({ providedIn: 'root' })
export class SharePointService {
  private readonly siteUrl = '/sites/CohortHiring';

  constructor(private http: HttpClient) {}

  getListItems<T>(listName: string, selectFields: string[], expandFields: string[] = [], filter?: string): Observable<T[]> {
    let url = `${this.siteUrl}/_api/web/lists/getbytitle('${listName}')/items?$select=${selectFields.join(',')}`;
    if (expandFields.length) url += `&$expand=${expandFields.join(',')}`;
    if (filter) url += `&$filter=${filter}`;

    const headers = new HttpHeaders({ 'Accept': 'application/json;odata=verbose' });
    return this.http.get<SpListResponse<T>>(url, { headers }).pipe(map(res => res.d.results));
  }

  private getRequestDigest(): Observable<string> {
    const headers = new HttpHeaders({ 'Accept': 'application/json;odata=verbose' });
    return this.http.post<any>(`${this.siteUrl}/_api/contextinfo`, {}, { headers }).pipe(
      map(res => res.d.GetContextWebInformation.FormDigestValue)
    );
  }

  updateListItem(listName: string, itemId: number, fields: Record<string, any>): Observable<any> {
    return this.getRequestDigest().pipe(
      switchMap(digest => {
        const headers = new HttpHeaders({
          'Accept': 'application/json;odata=verbose',
          'Content-Type': 'application/json;odata=verbose',
          'X-RequestDigest': digest,
          'X-HTTP-Method': 'MERGE',
          'If-Match': '*'
        });
        const body = { __metadata: { type: `SP.Data.${listName}ListItem` }, ...fields };
        const url = `${this.siteUrl}/_api/web/lists/getbytitle('${listName}')/items(${itemId})`;
        return this.http.post(url, body, { headers });
      })
    );
  }

  searchUsers(query: string): Observable<{ id: number; title: string; email: string }[]> {
    const headers = new HttpHeaders({ 'Accept': 'application/json;odata=verbose' });
    const url = `${this.siteUrl}/_api/web/siteusers?$filter=substringof('${query}',Title)`;
    return this.http.get<any>(url, { headers }).pipe(
      map(res => (res.d.results || []).map((u: any) => ({ id: u.Id, title: u.Title, email: u.Email })))
    );
  }
}
```

## 4. `core/services/candidate.service.ts` (updated — write methods added, department/offer fields)

```typescript
import { Injectable } from '@angular/core';
import { Observable, forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import { SharePointService } from './sharepoint.service';
import { Candidate, SkillAssessment, SharePointUser } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';

const SKILL_KEYS = ['One','Two','Three','Four','Five','Six','Seven','Eight','Nine','Ten'];

const SELECT_FIELDS = [
  'Id','CandidateId','CandidateName','CandidateEmailId','CandidatePhoneNumber',
  'Location','RoleDesignation','Profile','AllocatedBizLine',
  'PrescreeningTestLink','PrescreeningTestDate','PrescreeningScore','PrescreeningSelected','PrescreeningComments',
  ...SKILL_KEYS.flatMap(k => [`Skill${k}`,`Skill${k}Score`,`Skill${k}TROne`,`Skill${k}TRTwo`,`Skill${k}Mgmt`]),
  'TechRound1InterviewDate','TechRound1InterviewFeedback','TechRound1InterviewSelection',
  'TechRound2InterviewDate','TechRound2InterviewFeedback','TechRound2InterviewSelection',
  'MgmtRoundInterviewDate','MgmtRoundCommunication','MgmtRoundAttitude','MgmtRoundCulturalFit',
  'MgmtRoundLeadership','MgmtRoundOtherTopics','MgmtRoundInterviewSelection',
  'OnShoreRoundInterviewDate','OnShoreRoundDepartment','OnShoreRoundInterviewSelection',
  'HrRoundInterviewDate','HrRoundInterviewSelection','HrRoundOfferSent','HrRoundOfferAccepted',
  'CvUpload','HireproResultsUpload',
  'TechRound1InterviewedBy/Id','TechRound1InterviewedBy/Title','TechRound1InterviewedBy/EMail',
  'TechRound2InterviewedBy/Id','TechRound2InterviewedBy/Title','TechRound2InterviewedBy/EMail',
  'MgmtRoundInterviewedBy/Id','MgmtRoundInterviewedBy/Title','MgmtRoundInterviewedBy/EMail',
  'OnShoreRoundInterviewedBy/Id','OnShoreRoundInterviewedBy/Title','OnShoreRoundInterviewedBy/EMail',
  'HrRoundInterviewedBy/Id','HrRoundInterviewedBy/Title','HrRoundInterviewedBy/EMail'
];

const EXPAND_FIELDS = [
  'TechRound1InterviewedBy','TechRound2InterviewedBy','MgmtRoundInterviewedBy',
  'OnShoreRoundInterviewedBy','HrRoundInterviewedBy'
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
      map(([c, m]) => [...c, ...m])
    );
  }

  // ---------- Writes ----------

  private listNameFor(c: Candidate): string {
    return c.listSource === 'Chennai' ? 'ChennaiInterviewList' : 'MumbaiInterviewList';
  }

  assignUserToRound(candidate: Candidate, roundField: string, user: SharePointUser): Observable<any> {
    return this.sp.updateListItem(this.listNameFor(candidate), candidate.id, {
      [`${roundField}InterviewedById`]: user.id
    });
  }

  submitRoundDecision(candidate: Candidate, fields: Record<string, any>): Observable<any> {
    return this.sp.updateListItem(this.listNameFor(candidate), candidate.id, fields);
  }

  searchUsers(query: string) {
    return this.sp.searchUsers(query);
  }

  // ---------- Mapping ----------

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
        department: item.OnShoreRoundDepartment,
        selection: this.toStatus(item.OnShoreRoundInterviewSelection)
      },
      hrRound: {
        interviewedBy: this.mapUser(item.HrRoundInterviewedBy),
        interviewDate: item.HrRoundInterviewDate,
        selection: this.toStatus(item.HrRoundInterviewSelection),
        offerSent: item.HrRoundOfferSent === 'Yes',
        offerAccepted: item.HrRoundOfferAccepted === 'Yes'
      },

      cvUpload: item.CvUpload,
      hireproResultsUpload: item.HireproResultsUpload
    };
  }

  private mapSkills(item: any): SkillAssessment[] {
    return SKILL_KEYS.map(k => ({
      skillName: item[`Skill${k}`],
      screeningScore: item[`Skill${k}Score`],
      techRound1Comment: item[`Skill${k}TROne`],
      techRound2Comment: item[`Skill${k}TRTwo`],
      mgmtRoundComment: item[`Skill${k}Mgmt`]
    })).filter(s => !!s.skillName);
  }

  private mapUser(f: any): SharePointUser | null {
    if (!f) return null;
    return {
      id: f.Id ?? f.results?.[0]?.Id,
      title: f.Title ?? f.results?.[0]?.Title,
      email: f.EMail ?? f.results?.[0]?.EMail
    };
  }

  private toStatus(value: string | null): RoundStatus {
    if (value === 'Selected') return RoundStatus.Selected;
    if (value === 'Rejected') return RoundStatus.Rejected;
    if (value === 'N/A') return RoundStatus.NotApplicable;
    return RoundStatus.Pending;
  }
}
```

## 5. `core/services/workflow-visibility.service.ts` (full rewrite — the real state machine)

```typescript
import { Injectable } from '@angular/core';
import { Candidate } from '../models/candidate.model';
import { RoundStatus } from '../models/round-status.enum';
import { CurrentUserService, UserRole } from './current-user.service';

export type RoundKey = 'techRound1' | 'techRound2' | 'mgmtRound' | 'onShoreRound' | 'hrRound';

export interface RoundAccess {
  state: 'not-reached' | 'assignable' | 'locked-other' | 'mine-editable' | 'mine-locked' | 'no-access';
  message?: string;
}

const SELF_ASSIGN_ROUNDS: RoundKey[] = ['techRound1', 'techRound2', 'mgmtRound'];
const ADMIN_ASSIGN_ROUNDS: RoundKey[] = ['onShoreRound', 'hrRound'];

const ROLE_ROUNDS: Record<UserRole, RoundKey[]> = {
  HrAdmin: ['techRound1', 'techRound2', 'mgmtRound', 'onShoreRound', 'hrRound'],
  Recruiter: [],
  TechPanel: ['techRound1', 'techRound2'],
  MgmtPanel: ['mgmtRound'],
  OnShorePanel: ['onShoreRound'],
  HrPanel: ['hrRound']
};

@Injectable({ providedIn: 'root' })
export class WorkflowVisibilityService {
  constructor(private currentUser: CurrentUserService) {}

  // ---------- Stage readiness ----------

  techStageDecision(candidate: Candidate): RoundStatus {
    const r1 = candidate.techRound1.selection;
    const r2 = candidate.techRound2.selection;
    if (r1 === RoundStatus.Rejected) return RoundStatus.Rejected;
    if (r1 !== RoundStatus.Selected) return RoundStatus.Pending;
    if (r2 === RoundStatus.NotApplicable) return RoundStatus.Selected;
    if (r2 === RoundStatus.Rejected) return RoundStatus.Rejected;
    if (r2 === RoundStatus.Selected) return RoundStatus.Selected;
    return RoundStatus.Pending; // TR1 selected, TR2 not yet decided
  }

  isTechRound2Reachable(candidate: Candidate): boolean {
    return candidate.techRound1.selection === RoundStatus.Selected
      && candidate.techRound2.selection !== RoundStatus.NotApplicable;
  }

  isMgmtReachable(candidate: Candidate): boolean {
    return this.techStageDecision(candidate) === RoundStatus.Selected;
  }

  isOnShoreReachable(candidate: Candidate): boolean {
    return candidate.mgmtRound.selection === RoundStatus.Selected;
  }

  isHrReachable(candidate: Candidate): boolean {
    return candidate.onShoreRound.selection === RoundStatus.Selected;
  }

  hasCandidateReached(candidate: Candidate, round: RoundKey): boolean {
    switch (round) {
      case 'techRound1': return candidate.prescreeningSelected === RoundStatus.Selected;
      case 'techRound2': return this.isTechRound2Reachable(candidate);
      case 'mgmtRound': return this.isMgmtReachable(candidate);
      case 'onShoreRound': return this.isOnShoreReachable(candidate);
      case 'hrRound': return this.isHrReachable(candidate);
    }
  }

  /** Needed to decide whether Tech Round 2 should be offered to skip via popup for a Mgmt panel user. */
  techRound2NotYetDecided(candidate: Candidate): boolean {
    return candidate.techRound1.selection === RoundStatus.Selected
      && candidate.techRound2.selection === RoundStatus.Pending
      && !candidate.techRound2.interviewedBy;
  }

  // ---------- Per-round access for the currently logged-in user ----------

  getAccess(candidate: Candidate, round: RoundKey): RoundAccess {
    const user = this.currentUser.get();

    if (user.role === 'HrAdmin') {
      if (!this.hasCandidateReached(candidate, round)) return { state: 'not-reached', message: 'Not reached yet.' };
      const r = this.getRoundStatus(candidate, round);
      return r === RoundStatus.Pending
        ? { state: 'mine-editable' }
        : { state: 'mine-locked' };
    }

    if (user.role === 'Recruiter') {
      return this.hasCandidateReached(candidate, round)
        ? { state: 'mine-locked' } // read-only viewer
        : { state: 'not-reached', message: 'Not reached yet.' };
    }

    const myRounds = ROLE_ROUNDS[user.role];
    if (!myRounds.includes(round)) {
      return { state: 'no-access' };
    }

    if (!this.hasCandidateReached(candidate, round)) {
      return { state: 'not-reached', message: 'This candidate has not reached your round yet.' };
    }

    const assignedTo = this.getInterviewedBy(candidate, round);
    const finalized = this.getRoundStatus(candidate, round) !== RoundStatus.Pending;

    if (!assignedTo) {
      return SELF_ASSIGN_ROUNDS.includes(round)
        ? { state: 'assignable' }
        : { state: 'not-reached', message: 'Waiting for HR Admin to assign an interviewer.' };
    }

    if (assignedTo.id !== user.id) {
      return { state: 'locked-other', message: `Assigned to ${assignedTo.title}.` };
    }

    return finalized ? { state: 'mine-locked' } : { state: 'mine-editable' };
  }

  /** Whether this viewer can open the candidate's detail page at all. */
  canOpenCandidate(candidate: Candidate): boolean {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return true;

    const myRounds = ROLE_ROUNDS[user.role];
    for (const r of myRounds) {
      const access = this.getAccess(candidate, r);
      if (access.state === 'mine-editable' || access.state === 'mine-locked') return true;
    }
    return false;
  }

  /** Dashboard action state per candidate for the current viewer. */
  getDashboardAction(candidate: Candidate): 'open' | 'assignable' | 'locked' | 'not-in-workflow' {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return 'open';

    const myRounds = ROLE_ROUNDS[user.role];
    let sawNotReached = 0;
    for (const r of myRounds) {
      const access = this.getAccess(candidate, r);
      if (access.state === 'mine-editable' || access.state === 'mine-locked') return 'open';
      if (access.state === 'assignable') return 'assignable';
      if (access.state === 'not-reached') sawNotReached++;
    }
    return sawNotReached === myRounds.length ? 'not-in-workflow' : 'locked';
  }

  // ---------- Assignment / submission (real SharePoint writes handled by caller via CandidateService) ----------

  private getRoundStatus(candidate: Candidate, round: RoundKey): RoundStatus {
    return candidate[round].selection;
  }

  private getInterviewedBy(candidate: Candidate, round: RoundKey) {
    return candidate[round].interviewedBy;
  }

  roundFieldPrefix(round: RoundKey): string {
    const map: Record<RoundKey, string> = {
      techRound1: 'TechRound1', techRound2: 'TechRound2', mgmtRound: 'MgmtRound',
      onShoreRound: 'OnShoreRound', hrRound: 'HrRound'
    };
    return map[round];
  }
}
```

---

## 6. Shared: `people-picker` component (for On-Shore/HR admin assignment)

**`shared/components/people-picker/people-picker.component.ts`**
```typescript
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { SharePointUs