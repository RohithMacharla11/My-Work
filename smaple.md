Let's stop patching piecemeal — here are complete, self-contained blocks for every file involved in the tech panel's admin-edit feature. Replace each named section wholesale.

## `workflow.service.ts` — confirm this exact `RoundAccess` interface and `getAccess()` method

```ts
export interface RoundAccess {
  state: AccessState;
  canEdit: boolean;
  canEditDecision: boolean;
  assignedToName?: string;
}
```

```ts
getAccess(c: Candidate, key: RoundKey): RoundAccess {
  const me = this.currentUser.get();

  if (!this.canView(c, key)) return { state: 'no-access', canEdit: false, canEditDecision: false };
  if (key === 'techRound2' && this.isTechRound2Skipped(c))
    return { state: 'skipped', canEdit: false, canEditDecision: false };
  if (key === 'onshoreRound' && this.isOnshoreSkipped(c))
    return { state: 'skipped', canEdit: false, canEditDecision: false };

  if (!this.hasReached(c, key)) return { state: 'not-reached', canEdit: false, canEditDecision: false };

  const locked = this.isRoundLocked(c, key);
  const active = key === 'mgmtRound'
    ? this.hasReached(c, 'mgmtRound') && this.status(c, 'mgmtRound') === S.Pending
    : this.activeRound(c) === key;
  const assignedTo = this.assignee(c, key);
  const mine = !!assignedTo && assignedTo.id === me.id;
  const canEditByRole = this.currentUser.canEditAnything();
  const isAdmin = this.currentUser.isAdmin();

  if (active && !locked) {
    if (!assignedTo && (canEditByRole || this.ownsRoundByRole(key))) {
      return { state: 'assignable', canEdit: false, canEditDecision: false };
    }
    if (mine || canEditByRole) {
      return {
        state: 'editable',
        canEdit: true,
        canEditDecision: mine && !isAdmin,
        assignedToName: assignedTo?.title,
      };
    }
  }

  if (isAdmin) {
    return { state: 'editable', canEdit: true, canEditDecision: false, assignedToName: assignedTo?.title };
  }

  return { state: 'readonly', canEdit: false, canEditDecision: false, assignedToName: assignedTo?.title };
}
```

**This is the load-bearing piece.** If HR Admin is still seeing the normal "Submit Tech Round 1" button (which requires `r1.canEditDecision === true`), the most likely cause is that **this exact method isn't what's currently in your file** — perhaps an earlier version without the `&& !isAdmin` piece got saved instead. Please replace your `getAccess()` with this block exactly, character for character, and save.

## `tech-round-panel.component.ts` — full relevant section

```ts
import { Component, Input, OnChanges, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { Candidate, RoundStatus, SharePointUser } from '../../../../core/models/candidate.model';
import { CandidateService } from '../../../../core/services/candidate.service';
import { CurrentUserService } from '../../../../core/services/current-user.service';
import { RoundAccess, WorkflowService } from '../../../../core/services/workflow.service';
import { ROUNDS, techCommentCol, AUDIT_TRAIL_COL } from '../../../../core/config/cohort.config';
import { formatFriendlyDate } from '../../../../core/config/cohort.config';
import { appendAuditEntry, latestAuditEntryFor } from '../../../../core/utils/audit-trail.util';
import { Router } from '@angular/router';

type TechKey = 'techRound1' | 'techRound2';

@Component({
  selector: 'app-tech-round-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tech-round-panel.component.html',
  styleUrls: ['../round-panel.shared.scss'],
})
export class TechRoundPanelComponent implements OnChanges {
  @Input() candidate!: Candidate;
  @Output() changed = new EventEmitter<void>();

  r1!: RoundAccess;
  r2!: RoundAccess;

  r1Comments: string[] = [];
  r1Overall = '';
  r1Decision: RoundStatus | null = null;

  r2Comments: string[] = [];
  r2Overall = '';
  r2Decision: RoundStatus | null = null;

  readonly Sel = RoundStatus.Selected;
  readonly Rej = RoundStatus.Rejected;
  saving = false;

  constructor(
    private workflow: WorkflowService,
    private candidates: CandidateService,
    private currentUser: CurrentUserService,
    private router: Router
  ) {}

  ngOnChanges(): void {
    this.r1 = this.workflow.getAccess(this.candidate, 'techRound1');
    this.r2 = this.workflow.getAccess(this.candidate, 'techRound2');
    this.r1Comments = this.candidate.skills.map(s => s.round1Comment);
    this.r2Comments = this.candidate.skills.map(s => s.round2Comment);
    this.r1Overall = this.candidate.techRound1.feedback ?? '';
    this.r2Overall = this.candidate.techRound2.feedback ?? '';
  }

  interviewer(round: TechKey): string {
    return this.candidate[round].interviewedBy?.title ?? 'Not assigned';
  }
  interviewDate(round: TechKey): string {
    return this.candidate[round].interviewDate ?? '-';
  }
  decisionOf(round: TechKey): RoundStatus {
    return this.candidate[round].selection;
  }
  access(round: TechKey): RoundAccess {
    return round === 'techRound1' ? this.r1 : this.r2;
  }

  assignToMe(round: TechKey): void {
    const me = this.currentUser.get();
    const user: SharePointUser = { id: me.id, title: me.title, email: me.email };
    this.candidates.assignInterviewer(this.candidate, ROUNDS[round].prefix, user).subscribe(() => {
      this.candidate[round].interviewedBy = user;
      this.ngOnChanges();
      this.changed.emit();
    });
  }

  formatFriendlyDate(date: string | null) {
    return formatFriendlyDate(date);
  }

  canSelfAssignR2(): boolean {
    return this.currentUser.hasRole('TechPanel');
  }

  lastAdminEdit(round: TechKey) {
    return latestAuditEntryFor(this.candidate.auditTrail, ROUNDS[round].name);
  }

  submit(round: TechKey, adminMode = false): void {
    const access = this.access(round);
    const decision = round === 'techRound1' ? this.r1Decision : this.r2Decision;
    if (!adminMode && (!decision || !access.canEditDecision)) return;
    if (this.saving) return;

    const comments = round === 'techRound1' ? this.r1Comments : this.r2Comments;
    const overall = round === 'techRound1' ? this.r1Overall : this.r2Overall;
    const prefix = ROUNDS[round].prefix;
    const me = this.currentUser.get();

    const fields: Record<string, any> = {
      [`${prefix}InterviewFeedback`]: overall,
      [`${prefix}InterviewDate`]: new Date().toISOString(),
    };
    if (decision !== null) fields[`${prefix}InterviewSelection`] = decision;
    if (!adminMode) fields[`${prefix}InterviewedById`] = me.id;
    this.candidate.skills.forEach((s, i) => {
      fields[techCommentCol(s.slotIndex, round)] = comments[i];
    });

    let newTrail = this.candidate.auditTrail ?? '';
    if (adminMode) {
      newTrail = appendAuditEntry(newTrail, {
        name: me.title,
        date: new Date().toISOString(),
        component: ROUNDS[round].name,
      });
      fields[AUDIT_TRAIL_COL] = newTrail;
    }

    this.saving = true;
    this.candidates.submitRound(this.candidate, fields).subscribe({
      next: () => {
        if (decision !== null) this.candidate[round].selection = decision as RoundStatus;
        this.candidate[round].feedback = overall;
        this.candidate.skills.forEach((s, i) => {
          if (round === 'techRound1') s.round1Comment = comments[i];
          else s.round2Comment = comments[i];
        });
        if (adminMode) this.candidate.auditTrail = newTrail;

        this.candidates.patchStatus(this.candidate).subscribe({
          next: () => {
            this.saving = false;
            this.ngOnChanges();
            this.changed.emit();
            this.router.navigate(['/success']);
          },
          error: () => { this.saving = false; },
        });
      },
      error: () => { this.saving = false; },
    });
  }
}
```

## `tech-round-panel.component.html` — decision row (R1 + R2) and actions footer, full blocks

**Decision row** — replace the whole `<tr class="row-decision">...</tr>`:
```html
<tr class="row-decision">
  <td class="col-skill"><b>Decision</b></td>
  <td class="col-score dim">—</td>

  <!-- R1 decision -->
  <td class="col-round">
    <div class="selector" *ngIf="r1.canEditDecision">
      <button class="selopt neutral-y" [class.on]="r1Decision === Sel" (click)="r1Decision = Sel">✓ Recommended</button>
      <button class="selopt neutral-n" [class.on]="r1Decision === Rej" (click)="r1Decision = Rej">X Not Recommended</button>
    </div>
    <div class="selector locked-decision" *ngIf="r1.canEdit && !r1.canEditDecision">
      <button class="selopt neutral-y" [class.on]="decisionOf('techRound1') === Sel" disabled>✓ Recommended</button>
      <button class="selopt neutral-n" [class.on]="decisionOf('techRound1') === Rej" disabled>X Not Recommended</button>
    </div>
    <ng-container *ngIf="!r1.canEdit">
      <span class="chip neutral" *ngIf="decisionOf('techRound1') === Sel"><i></i>Recommended</span>
      <span class="chip neutral" *ngIf="decisionOf('techRound1') === Rej"><i></i>Not Recommended</span>
      <span class="dim" *ngIf="decisionOf('techRound1') !== Sel && decisionOf('techRound1') !== Rej">Pending</span>
    </ng-container>
  </td>

  <!-- R2 decision -->
  <td class="col-round">
    <span class="chip na" *ngIf="r2.state === 'skipped'"><i></i>N/A</span>
    <ng-container *ngIf="r2.state !== 'skipped'">
      <div class="selector" *ngIf="r2.canEditDecision">
        <button class="selopt neutral-y" [class.on]="r2Decision === Sel" (click)="r2Decision = Sel">✓ Recommended</button>
        <button class="selopt neutral-n" [class.on]="r2Decision === Rej" (click)="r2Decision = Rej">X Not Recommended</button>
      </div>
      <div class="selector locked-decision" *ngIf="r2.canEdit && !r2.canEditDecision">
        <button class="selopt neutral-y" [class.on]="decisionOf('techRound2') === Sel" disabled>✓ Recommended</button>
        <button class="selopt neutral-n" [class.on]="decisionOf('techRound2') === Rej" disabled>X Not Recommended</button>
      </div>
      <ng-container *ngIf="!r2.canEdit">
        <span class="chip neutral" *ngIf="decisionOf('techRound2') === Sel"><i></i>Recommended</span>
        <span class="chip neutral" *ngIf="decisionOf('techRound2') === Rej"><i></i>Not Recommended</span>
        <span class="dim" *ngIf="decisionOf('techRound2') !== Sel && decisionOf('techRound2') !== Rej">Pending</span>
      </ng-container>
    </ng-container>
  </td>
</tr>
```

**Actions footer** — replace the `<div class="actions">...</div>` and the two admin-edit lines after it:
```html
<div class="actions">
  <button class="btn primary" *ngIf="r1.canEditDecision" [disabled]="!r1Decision || saving" (click)="submit('techRound1')">Submit Tech Round 1</button>
  <button class="btn primary" *ngIf="r1.canEdit && !r1.canEditDecision" [disabled]="saving" (click)="submit('techRound1', true)">Save Tech Round 1 (Admin)</button>

  <button class="btn primary" *ngIf="r2.canEditDecision" [disabled]="!r2Decision || saving" (click)="submit('techRound2')">Submit Tech Round 2</button>
  <button class="btn primary" *ngIf="r2.canEdit && !r2.canEditDecision" [disabled]="saving" (click)="submit('techRound2', true)">Save Tech Round 2 (Admin)</button>
</div>
<div class="dim small" *ngIf="lastAdminEdit('techRound1') as edit" style="margin-top:6px">
  Tech Round 1 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
</div>
<div class="dim small" *ngIf="lastAdminEdit('techRound2') as edit" style="margin-top:2px">
  Tech Round 2 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
</div>
```

## After pasting all three blocks

1. Save all files, confirm the terminal shows a clean rebuild with **zero errors** (paste it here if there are any — a compile error would silently prevent the new template from ever rendering, which fits "nothing changed").
2. Hard-refresh the browser (Ctrl+Shift+R) to bypass any cached bundle.
3. As HR Admin, open a candidate where Tech Round 1 is the active round and you are **not** the assigned interviewer. You should now see the locked (greyed, disabled) Recommended/Not Recommended buttons plus a separate **"Save Tech Round 1 (Admin)"** button below.

If after all that it's still showing only the old single "Submit Tech Round 1" button, the fault is almost certainly that `getAccess()` isn't returning `canEditDecision` correctly for your test user — at that point paste me a fresh screenshot of the *saved* `getAccess()` method plus the browser console output, and I'll trace it from actual runtime values instead of source code guesses.