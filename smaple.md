Found it — this is a real bug, not a config issue, plus one design fix needed. Let me walk through both.

## Bug 1: The core lock bug (this is the big one)

SharePoint returns `{"__deferred": {...}}` for empty Person/Lookup fields when they're not properly expanded — not `null`. Your `mapUser()` only checks `if (!f) return null`, but `{__deferred: {...}}` is a **truthy object**, so it slips past that check and builds a fake user with `id: undefined`. That fake "assigned" user then makes `getAccess()` think every round is already claimed by someone else — hence everything shows **Locked** instead of **Assign to Me**, for every role except HR Admin (which never checks `assignedTo` at all, which is why it "works perfectly").

**Fix — `candidate.service.ts`, `mapUser()`:**
```typescript
private mapUser(f: any): SharePointUser | null {
  if (!f) return null;
  const id = f.Id ?? f.results?.[0]?.Id;
  if (!id) return null; // catches {__deferred:...} and empty results arrays
  return {
    id,
    title: f.Title ?? f.results?.[0]?.Title,
    email: f.EMail ?? f.results?.[0]?.EMail
  };
}
```

## Bug 2: HR Admin was editing, not just assigning

You're right — HR Admin should be **read-only viewer + assigner only**, never fill feedback. Currently `getAccess()` gives HR Admin `editable: true` on every round.

## Bug 3: Detail page didn't gate sections by role

Even once you can open a candidate, a Tech Panel member would see Management/On-Shore/HR sections too — they should stop at Tech Round 2.

## Bug 4: On-Shore/HR field name casing

Your real SharePoint schema uses `OnshoreRound...` (lowercase `s`), but the write calls use `OnShoreRound...`. A mismatched field name makes SharePoint reject the PATCH — and since nothing surfaced the error, it looked like nothing happened at all.

---

## `core/services/current-user.service.ts` (real fetch, switchable role for testing)

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

export type UserRole = 'HrAdmin' | 'Recruiter' | 'TechPanel' | 'MgmtPanel' | 'OnShorePanel' | 'HrPanel';

export interface CurrentUser {
  id: number;
  title: string;
  email: string;
  role: UserRole;
}

/** ===== SWITCH THIS to test each role while real group→role mapping isn't wired yet. ===== */
const TEST_ROLE: UserRole = 'TechPanel';

@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly siteUrl = '/sites/CohortHiring';
  private user: CurrentUser = { id: 0, title: 'Loading...', email: '', role: TEST_ROLE };

  constructor(private http: HttpClient) {}

  get(): CurrentUser { return this.user; }

  loadCurrentUser(): Observable<CurrentUser> {
    const headers = new HttpHeaders({ 'Accept': 'application/json;odata=verbose' });
    return this.http.get<any>(`${this.siteUrl}/_api/web/currentuser`, { headers }).pipe(
      map(res => {
        this.user = { id: res.d.Id, title: res.d.Title, email: res.d.Email, role: TEST_ROLE };
        return this.user;
      }),
      catchError(() => {
        this.user = { id: 0, title: 'Unknown User', email: '', role: TEST_ROLE };
        return of(this.user);
      })
    );
  }
}
```

This uses the real logged-in ID (matching what you're seeing in the topbar, "tec.Rohith MACHARLA") while letting you flip `TEST_ROLE` to test each role's behavior against real assigned-by-name data.

## `core/services/workflow-visibility.service.ts` (corrected `getAccess`, added section gating)

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

const SECTION_ORDER: RoundKey[] = ['techRound1', 'techRound2', 'mgmtRound', 'onShoreRound', 'hrRound'];
const SELF_ASSIGN_ROUNDS: RoundKey[] = ['techRound1', 'techRound2', 'mgmtRound'];

const ROLE_ROUNDS: Record<UserRole, RoundKey[]> = {
  HrAdmin: SECTION_ORDER,
  Recruiter: [],
  TechPanel: ['techRound1', 'techRound2'],
  MgmtPanel: ['mgmtRound'],
  OnShorePanel: ['onShoreRound'],
  HrPanel: ['hrRound']
};

@Injectable({ providedIn: 'root' })
export class WorkflowVisibilityService {
  constructor(private currentUser: CurrentUserService) {}

  // ---------- Stage readiness (unchanged) ----------

  techStageDecision(candidate: Candidate): RoundStatus {
    const r1 = candidate.techRound1.selection;
    const r2 = candidate.techRound2.selection;
    if (r1 === RoundStatus.Rejected) return RoundStatus.Rejected;
    if (r1 !== RoundStatus.Selected) return RoundStatus.Pending;
    if (r2 === RoundStatus.NotApplicable) return RoundStatus.Selected;
    if (r2 === RoundStatus.Rejected) return RoundStatus.Rejected;
    if (r2 === RoundStatus.Selected) return RoundStatus.Selected;
    return RoundStatus.Pending;
  }

  isTechRound2Reachable(candidate: Candidate): boolean {
    return candidate.techRound1.selection === RoundStatus.Selected
      && candidate.techRound2.selection !== RoundStatus.NotApplicable;
  }

  isMgmtReachable(candidate: Candidate): boolean { return this.techStageDecision(candidate) === RoundStatus.Selected; }
  isOnShoreReachable(candidate: Candidate): boolean { return candidate.mgmtRound.selection === RoundStatus.Selected; }
  isHrReachable(candidate: Candidate): boolean { return candidate.onShoreRound.selection === RoundStatus.Selected; }

  hasCandidateReached(candidate: Candidate, round: RoundKey): boolean {
    switch (round) {
      case 'techRound1': return candidate.prescreeningSelected === RoundStatus.Selected;
      case 'techRound2': return this.isTechRound2Reachable(candidate);
      case 'mgmtRound': return this.isMgmtReachable(candidate);
      case 'onShoreRound': return this.isOnShoreReachable(candidate);
      case 'hrRound': return this.isHrReachable(candidate);
    }
  }

  techRound2NotYetDecided(candidate: Candidate): boolean {
    return candidate.techRound1.selection === RoundStatus.Selected
      && candidate.techRound2.selection === RoundStatus.Pending
      && !candidate.techRound2.interviewedBy;
  }

  // ---------- Per-round access ----------

  getAccess(candidate: Candidate, round: RoundKey): RoundAccess {
    if (!this.hasCandidateReached(candidate, round)) {
      return { state: 'not-reached', message: 'Not reached yet.' };
    }

    const user = this.currentUser.get();

    // HR Admin: read-only viewer everywhere. Assignment for On-Shore/HR is a
    // separate action (people-picker), never a feedback-editing state.
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') {
      return { state: 'mine-locked' };
    }

    const myRounds = ROLE_ROUNDS[user.role];
    if (!myRounds.includes(round)) {
      return { state: 'no-access' };
    }

    const assignedTo = candidate[round].interviewedBy;
    const finalized = candidate[round].selection !== RoundStatus.Pending;

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

  /** Section-level gate for the detail page — stops rendering past the viewer's own round. */
  canViewRoundSection(round: RoundKey): boolean {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return true;
    const myRounds = ROLE_ROUNDS[user.role];
    if (!myRounds.length) return false;
    const maxIdx = Math.max(...myRounds.map(r => SECTION_ORDER.indexOf(r)));
    return SECTION_ORDER.indexOf(round) <= maxIdx;
  }

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

  roundFieldPrefix(round: RoundKey): string {
    const map: Record<RoundKey, string> = {
      techRound1: 'TechRound1', techRound2: 'TechRound2', mgmtRound: 'MgmtRound',
      onShoreRound: 'OnshoreRound', hrRound: 'HrRound' // matches real SharePoint casing
    };
    return map[round];
  }
}
```

## `candidate-detail.component.ts` — add section gating

Replace the four section getters:

```typescript
get showTechTable(): boolean {
  if (!this.candidate) return false;
  return this.visibility.canViewRoundSection('techRound1') && this.candidate.prescreeningSelected === RoundStatus.Selected;
}
get showMgmt(): boolean {
  if (!this.candidate) return false;
  return this.visibility.canViewRoundSection('mgmtRound') && this.visibility.isMgmtReachable(this.candidate);
}
get showOnShore(): boolean {
  if (!this.candidate) return false;
  return this.visibility.canViewRoundSection('onShoreRound') && this.visibility.isOnShoreReachable(this.candidate);
}
get showHr(): boolean {
  if (!this.candidate) return false;
  return this.visibility.canViewRoundSection('hrRound') && this.visibility.isHrReachable(this.candidate);
}
```

## Fix the On-Shore casing in the two admin-assign panels

**`onshore-panel.component.ts`** — change every `'OnShoreRound...'` field string to `'OnshoreRound...'`:
```typescript
this.candidateService.assignUserToRound(this.candidate, 'OnshoreRound', user)...
// and in submit():
const fields = {
  OnshoreRoundDepartment: this.department,
  OnshoreRoundInterviewSelection: this.decision,
  OnshoreRoundInterviewDate: new Date().toISOString().split('T')[0]
};
```

`hr-panel.component.ts` fields were already `HrRound...` — no change needed there.

## Add error visibility to writes (so failures aren't silent)

In every `.subscribe()` call across the panels and dashboard, add an error branch so a rejected PATCH actually tells you instead of doing nothing:

```typescript
this.candidateService.assignUserToRound(this.candidate, prefix, spUser).subscribe({
  next: () => { /* existing success logic */ },
  error: (err) => { console.error('Assign failed:', err); alert('Could not assign — check console for details.'); }
});
```

Apply that `{ next, error }` pattern to every `subscribe()` in `tech-round-panel`, `mgmt-panel`, `onshore-panel`, `hr-panel`, and the dashboard's `doAssign()`.

---

Fix `mapUser()` first — that alone should unlock the whole dashboard for Tech Panel. Then apply the rest. Rebuild and test with `TEST_ROLE = 'TechPanel'` against Rahul Mehta (unassigned Tech Round 1) — should now show **Assign to Me**. Let me know what you see.