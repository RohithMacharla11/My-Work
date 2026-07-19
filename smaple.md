Good catch — found the exact spots. Two of these panels build the separator as unconditional literal text, so it shows up even when the thing after it is empty.

**Fix 1 — `mgmt-round-panel.component.html`.** Find:
```html
<small *ngIf="candidate.mgmtRound.interviewedBy">
  {{ candidate.mgmtRound.interviewedBy.title }} · {{ formatDate(candidate.mgmtRound.interviewDate) }}
</small>
```
Replace with:
```html
<small *ngIf="candidate.mgmtRound.interviewedBy">
  {{ candidate.mgmtRound.interviewedBy.title }}
  <ng-container *ngIf="candidate.mgmtRound.interviewDate"> · {{ formatDate(candidate.mgmtRound.interviewDate) }}</ng-container>
</small>
```
(This was showing "Name ·" with a trailing dot and nothing after it when a manager was assigned but hadn't submitted feedback with a date yet.)

**Fix 2 — `onshore-round-panel.component.html`.** Find:

```html
<span class="interviewer">{{ access.assignedToName || '-' }}</span>
<span class="date-time" *ngIf="candidate.onshoreRound.interviewDate">
  - {{ formatDate(candidate.onshoreRound.interviewDate) }}
</span>
```
Replace with (matching the pattern your HR round panel already uses correctly):
```html
<span class="interviewer" *ngIf="access.assignedToName">{{ access.assignedToName }}</span>
<span class="dim" *ngIf="!access.assignedToName">Unassigned</span>
<span class="separator" *ngIf="access.assignedToName && candidate.onshoreRound.interviewDate"></span>
<span class="date-time" *ngIf="candidate.onshoreRound.interviewDate">
  {{ formatDate(candidate.onshoreRound.interviewDate) }}
</span>
```

**Fix 3 — `candidate-detail.component.html`, hero header.** Find the role/profile line:
```html
<span>{{ candidate.roleDesignation }} · {{ candidate.profile }}</span>
```

Replace with:
```html
<span>{{ candidate.roleDesignation }}</span>
<span class="separator" *ngIf="candidate.roleDesignation && candidate.profile"> · </span>
<span *ngIf="candidate.profile">{{ candidate.profile }}</span>
```

Tech Round panel is already fine — it doesn't have this problem, no change needed there.

Save, recompile, refresh. That covers Management, Onshore, and the hero header — the three spots where a stray dot/dash could show with nothing on one side of it.