Found it exactly — thank you for the screenshots, this made it very clear. Look at your "actions" footer section (around line 226 onward): there are now **two separate "Submit Tech Round 2" buttons** sitting next to each other (one with the new mandatory-overall check, one without), and the **"Save Tech Round 1 (Admin)" button is missing entirely**. When the mandatory-overall change got pasted in, it landed as a new line instead of replacing the old one, and the Round 1 admin button got dropped in the process. That's exactly what broke the layout and gave you the duplicate button.

Here's the corrected `tech-round-panel.component.html`, complete, with only that one section fixed — everything else (including your `showTechRound2Column` gating and the mandatory-overall checks) is preserved exactly as you have it:

```html
<section class="card" id="section-tech" *ngIf="r1.state !== 'no-access'">
  <div class="card-head">
    <h2>Technical assessment</h2>
    <span class="chip idle"><i></i>Screening scores locked</span>
  </div>

  <div class="scroll-x">
    <table class="grid feedback">
      <thead>
        <tr>
          <th class="col-skill">Skill</th>
          <th class="col-score">Pre-Screening Marks</th>

          <!-- Tech Round 1 column header -->
          <th class="col-round">
            <div class="round-head">
              <b>Tech Round 1</b>
              <span class="chip you sm" *ngIf="r1.canEdit"><i></i>Your turn</span>
              <span class="chip pass sm" *ngIf="!r1.canEdit && decisionOf('techRound1') === Sel">
                <i></i>Recommended
              </span>
              <span class="chip wait sm" *ngIf="!r1.canEdit && decisionOf('techRound1') === Rej">
                <i></i>Not Recommended
              </span>
              <span class="chip wait sm" *ngIf="!r1.canEdit && r1.state === 'readonly' &&
                r1.assignedToName && decisionOf('techRound1') !== Sel && decisionOf('techRound1') !== Rej">
                <i></i>Assigned to {{ r1.assignedToName }}
              </span>
              <span class="chip idle sm" *ngIf="!r1.canEdit && r1.state === 'assignable'"><i></i>Unassigned</span>
              <span class="chip idle sm" *ngIf="!r1.canEdit && r1.state === 'not-reached'"><i></i>Pending</span>
            </div>
            <small>
              {{ interviewer("techRound1") }}
              <span *ngIf="candidate.techRound1.interviewDate">
                · {{ formatFriendlyDate(candidate.techRound1.interviewDate) }}
              </span>
            </small>
          </th>

          <!-- Tech Round 2 column header -->
          <th class="col-round" *ngIf="showTechRound2Column">
            <div class="round-head">
              <b>Tech Round 2</b>
              <span class="chip na sm" *ngIf="r2.state === 'skipped'"><i></i>Skipped</span>
              <span class="chip you sm" *ngIf="r2.canEdit"><i></i>Your turn</span>
              <span class="chip pass sm" *ngIf="
                  !r2.canEdit &&
                  r2.state !== 'skipped' &&
                  decisionOf('techRound2') === Sel
                "><i></i>Selected</span>
              <span class="chip rej sm" *ngIf="
                  !r2.canEdit &&
                  r2.state !== 'skipped' &&
                  decisionOf('techRound2') === Rej
                "><i></i>Rejected</span>
              <span class="chip wait sm" *ngIf="
                  !r2.canEdit &&
                  r2.state === 'readonly' &&
                  r2.assignedToName &&
                  decisionOf('techRound2') !== Sel &&
                  decisionOf('techRound2') !== Rej
                "><i></i>Assigned to {{ r2.assignedToName }}</span>
              <span class="chip idle sm" *ngIf="!r2.canEdit && r2.state === 'assignable'"><i></i>Unassigned</span>
              <span class="chip idle sm" *ngIf="!r2.canEdit && r2.state === 'not-reached'"><i></i>Pending</span>
            </div>
            <small *ngIf="r2.state !== 'skipped'">
              {{ interviewer("techRound2") }}
              <span *ngIf="candidate.techRound2.interviewDate">
                · {{ formatFriendlyDate(candidate.techRound2.interviewDate) }}
              </span>
            </small>
            <small *ngIf="r2.state === 'skipped'">Not conducted</small>
          </th>
        </tr>
      </thead>

      <tbody>
        <!-- one row per skill -->
        <tr *ngFor="let s of candidate.skills; let i = index">
          <td class="col-skill">
            <div class="skill-row-flex">
              <b>{{ s.label }}</b>
              <span class="max-marks" *ngIf="s.maxMarks">Max {{ s.maxMarks }}</span>
            </div>
          </td>
          <td class="col-score">
            <span class="score">{{ s.screeningScore || '—' }}</span>
          </td>

          <!-- R1 cell -->
          <td class="col-round">
            <textarea *ngIf="r1.canEdit" [(ngModel)]="r1Comments[i]" placeholder="Enter your feedback…"></textarea>
            <ng-container *ngIf="!r1.canEdit">
              <span class="fb" *ngIf="s.round1Comment">{{ s.round1Comment }}</span>
              <span class="dim" *ngIf="!s.round1Comment">{{
                r1.state === "editable" || r1.state === "assignable"
                  ? "Awaiting feedback"
                  : "—"
              }}</span>
            </ng-container>
          </td>

          <!-- R2 cell -->
          <td class="col-round" *ngIf="showTechRound2Column">
            <span class="dim" *ngIf="r2.state === 'skipped'">N/A</span>
            <ng-container *ngIf="r2.state !== 'skipped'">
              <textarea *ngIf="r2.canEdit" [(ngModel)]="r2Comments[i]" placeholder="Enter your feedback…"></textarea>
              <ng-container *ngIf="!r2.canEdit">
                <span class="fb" *ngIf="s.round2Comment">{{ s.round2Comment }}</span>
                <span class="dim" *ngIf="!s.round2Comment">{{
                  r2.state === "assignable" ? "Awaiting feedback" : "—"
                }}</span>
              </ng-container>
            </ng-container>
          </td>
        </tr>

        <!-- overall comment row -->
        <tr class="row-overall">
          <td class="col-skill"><b>Overall comment <span class="req">*</span></b></td>
          <td class="col-score dim">—</td>
          <td class="col-round">
            <textarea *ngIf="r1.canEdit" [(ngModel)]="r1Overall"
              placeholder="Overall comment for this round…"></textarea>
            <ng-container *ngIf="!r1.canEdit">
              <span class="fb" *ngIf="candidate.techRound1.feedback">{{
                candidate.techRound1.feedback
              }}</span>
              <span class="dim" *ngIf="!candidate.techRound1.feedback">—</span>
            </ng-container>
          </td>
          <td class="col-round" *ngIf="showTechRound2Column">
            <span class="dim" *ngIf="r2.state === 'skipped'">N/A</span>
            <ng-container *ngIf="r2.state !== 'skipped'">
              <textarea *ngIf="r2.canEdit" [(ngModel)]="r2Overall"
                placeholder="Overall comment for this round…"></textarea>
              <ng-container *ngIf="!r2.canEdit">
                <span class="fb" *ngIf="candidate.techRound2.feedback">{{
                  candidate.techRound2.feedback
                }}</span>
                <span class="dim" *ngIf="!candidate.techRound2.feedback">—</span>
              </ng-container>
            </ng-container>
          </td>
        </tr>

        <!-- decision row -->
        <tr class="row-decision">
          <td class="col-skill"><b>Decision</b></td>
          <td class="col-score dim">—</td>

          <!-- R1 decision -->
          <td class="col-round">
            <div class="selector" *ngIf="r1.canEditDecision">
              <button class="selopt neutral-y" [class.on]="r1Decision === Sel" (click)="r1Decision = Sel">✓
                Recommended</button>
              <button class="selopt neutral-n" [class.on]="r1Decision === Rej" (click)="r1Decision = Rej">X Not
                Recommended</button>
            </div>
            <div class="selector locked-decision" *ngIf="r1.canEdit && !r1.canEditDecision">
              <button class="selopt neutral-y" [class.on]="decisionOf('techRound1') === Sel" disabled>✓
                Recommended</button>
              <button class="selopt neutral-n" [class.on]="decisionOf('techRound1') === Rej" disabled>X Not
                Recommended</button>
            </div>
            <ng-container *ngIf="!r1.canEdit">
              <span class="chip pass sm" *ngIf="decisionOf('techRound1') === Sel"><i></i>Recommended</span>
              <span class="chip wait sm" *ngIf="decisionOf('techRound1') === Rej"><i></i>Not Recommended</span>
              <span class="dim"
                *ngIf="decisionOf('techRound1') !== Sel && decisionOf('techRound1') !== Rej">Pending</span>
            </ng-container>
          </td>

          <!-- R2 decision -->
          <td class="col-round" *ngIf="showTechRound2Column">
            <span class="chip na sm" *ngIf="r2.state === 'skipped'"><i></i>N/A</span>
            <ng-container *ngIf="r2.state !== 'skipped'">
              <div class="selector" *ngIf="r2.canEditDecision">
                <button class="selopt neutral-y" [class.on]="r2Decision === Sel" (click)="r2Decision = Sel">✓
                  Selected</button>
                <button class="selopt neutral-n" [class.on]="r2Decision === Rej" (click)="r2Decision = Rej">X
                  Rejected</button>
              </div>
              <div class="selector locked-decision" *ngIf="r2.canEdit && !r2.canEditDecision">
                <button class="selopt neutral-y" [class.on]="decisionOf('techRound2') === Sel" disabled>✓
                  Selected</button>
                <button class="selopt neutral-n" [class.on]="decisionOf('techRound2') === Rej" disabled>X
                  Rejected</button>
              </div>
              <ng-container *ngIf="!r2.canEdit">
                <span class="chip pass sm" *ngIf="decisionOf('techRound2') === Sel"><i></i>Selected</span>
                <span class="chip wait sm" *ngIf="decisionOf('techRound2') === Rej"><i></i>Rejected</span>
                <span class="dim"
                  *ngIf="decisionOf('techRound2') !== Sel && decisionOf('techRound2') !== Rej">Pending</span>
              </ng-container>
            </ng-container>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- assign CTA (shown when the active tech round is unassigned & claimable) -->
  <div class="editfoot" *ngIf="r1.state === 'assignable' || r2.state === 'assignable'">
    <div class="notice info" *ngIf="r1.state === 'assignable'">
      Tech Round 1 is unassigned.
      <button class="btn primary sm" (click)="assignToMe('techRound1')">
        Assign to me
      </button>
    </div>
    <div class="notice info" *ngIf="r2.state === 'assignable' && canSelfAssignR2()">
      Tech Round 2 is unassigned.
      <button class="btn primary sm" (click)="assignToMe('techRound2')">
        Assign to me
      </button>
    </div>
  </div>

  <!-- submit footer (shown for whichever round is editable) -->
  <div class="actions">
    <button class="btn primary" *ngIf="r1.canEditDecision" [disabled]="!r1Decision || !r1Overall.trim() || saving"
      (click)="submit('techRound1')">Submit Tech Round 1</button>
    <button class="btn primary" *ngIf="r1.canEdit && !r1.canEditDecision" [disabled]="saving"
      (click)="submit('techRound1', true)">Save Tech Round 1 (Admin)</button>
    <span class="chip pass" *ngIf="justSaved === 'techRound1'"><i></i>Saved</span>

    <button class="btn primary" *ngIf="r2.canEditDecision" [disabled]="!r2Decision || !r2Overall.trim() || saving"
      (click)="submit('techRound2')">Submit Tech Round 2</button>
    <button class="btn primary" *ngIf="r2.canEdit && !r2.canEditDecision" [disabled]="saving"
      (click)="submit('techRound2', true)">Save Tech Round 2 (Admin)</button>
    <span class="chip pass" *ngIf="justSaved === 'techRound2'"><i></i>Saved</span>
  </div>

  <div class="dim small" *ngIf="lastAdminEdit('techRound1') as edit" style="margin-top:6px">
    Tech Round 1 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
  </div>
  <div class="dim small" *ngIf="lastAdminEdit('techRound2') as edit" style="margin-top:2px">
    Tech Round 2 last edited by {{ edit.name }} · {{ formatFriendlyDate(edit.date) }}
  </div>
</section>
```

That's the whole file, corrected. Just replace your current `tech-round-panel.component.html` with this and the duplicate button / broken layout should be gone.

Let me know if that clears it up, or if anything still looks off after you drop it in.