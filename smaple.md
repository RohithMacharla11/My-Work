Right — when I rewrote the dashboard for multi-select, the Stage column got swapped to a plain text `valueGetter` and the dot-pipeline renderer got orphaned. Let's put it back, wired to the corrected model.

## 1. `workflow-pipeline-cell.component.ts` (finalized version)

```typescript
import { Component } from '@angular/core';
import { ICellRendererAngularComp } from 'ag-grid-angular';
import { ICellRendererParams } from 'ag-grid-community';
import { Candidate } from '../../../core/models/candidate.model';
import { RoundStatus } from '../../../core/models/round-status.enum';
import { WorkflowVisibilityService } from '../../../core/services/workflow-visibility.service';

type StageKey = 'preScreen' | 'tech1' | 'tech2' | 'mgmt' | 'onshore' | 'hr';

const STAGE_ORDER: StageKey[] = ['preScreen', 'tech1', 'tech2', 'mgmt', 'onshore', 'hr'];
const STAGE_LABEL: Record<StageKey, string> = {
  preScreen: 'Pre-Screen', tech1: 'Tech Round 1', tech2: 'Tech Round 2',
  mgmt: 'Management Round', onshore: 'On-Shore Round', hr: 'HR Round'
};

@Component({
  selector: 'app-workflow-pipeline-cell',
  template: `
    <div class="wf-cell">
      <div class="wf-dots">
        <span *ngFor="let s of stages" class="wf-dot" [ngClass]="'wf-dot--' + dotState(s)"></span>
      </div>
      <div class="wf-label" [class.wf-label--rejected]="isRejected">{{ label }}</div>
    </div>
  `,
  styleUrls: ['./workflow-pipeline-cell.component.scss']
})
export class WorkflowPipelineCellComponent implements ICellRendererAngularComp {
  candidate!: Candidate;
  stages = STAGE_ORDER;
  currentIndex = -1;
  isRejected = false;
  label = '';

  constructor(private visibility: WorkflowVisibilityService) {}

  agInit(params: ICellRendererParams): void {
    this.candidate = params.data;
    this.compute();
  }

  refresh(): boolean { return false; }

  private compute(): void {
    const c = this.candidate;
    const techDecision = this.visibility.techStageDecision(c);

    this.isRejected =
      c.prescreeningSelected === RoundStatus.Rejected ||
      techDecision === RoundStatus.Rejected ||
      c.mgmtRound.selection === RoundStatus.Rejected ||
      c.onShoreRound.selection === RoundStatus.Rejected;

    if (this.isRejected) {
      this.label = 'Rejected';
      // Freeze the dots at whichever stage caused the rejection
      if (c.prescreeningSelected === RoundStatus.Rejected) this.currentIndex = 0;
      else if (c.techRound1.selection === RoundStatus.Rejected) this.currentIndex = 1;
      else if (c.techRound2.selection === RoundStatus.Rejected) this.currentIndex = 2;
      else if (c.mgmtRound.selection === RoundStatus.Rejected) this.currentIndex = 3;
      else if (c.onShoreRound.selection === RoundStatus.Rejected) this.currentIndex = 4;
      return;
    }

    if (c.hrRound.selection === RoundStatus.Selected) { this.label = 'Offer Stage'; this.currentIndex = 6; return; }
    if (c.onShoreRound.selection === RoundStatus.Selected) { this.label = STAGE_LABEL.hr; this.currentIndex = 5; return; }
    if (c.mgmtRound.selection === RoundStatus.Selected) { this.label = STAGE_LABEL.onshore; this.currentIndex = 4; return; }
    if (techDecision === RoundStatus.Selected) { this.label = STAGE_LABEL.mgmt; this.currentIndex = 3; return; }
    if (c.techRound1.selection === RoundStatus.Selected) { this.label = STAGE_LABEL.tech2; this.currentIndex = 2; return; }
    if (c.prescreeningSelected === RoundStatus.Selected) { this.label = STAGE_LABEL.tech1; this.currentIndex = 1; return; }

    this.label = STAGE_LABEL.preScreen;
    this.currentIndex = 0;
  }

  dotState(stage: StageKey): 'done' | 'current' | 'rejected' | 'todo' {
    const idx = STAGE_ORDER.indexOf(stage);
    if (this.isRejected) {
      if (idx < this.currentIndex) return 'done';
      if (idx === this.currentIndex) return 'rejected';
      return 'todo';
    }
    if (idx < this.currentIndex) return 'done';
    if (idx === this.currentIndex) return 'current';
    return 'todo';
  }
}
```

## 2. `workflow-pipeline-cell.component.scss`

```scss
.wf-cell { display: flex; flex-direction: column; justify-content: center; height: 100%; gap: 5px; }
.wf-dots { display: flex; gap: 5px; }
.wf-dot {
  width: 9px; height: 9px; border-radius: 50%; background: var(--cohort-border); transition: all .2s ease;
  &--done { background: var(--cohort-primary); }
  &--current { background: var(--cohort-primary); box-shadow: 0 0 0 3px var(--status-pass-bg); }
  &--rejected { background: var(--status-reject-text); }
  &--todo { background: var(--cohort-border); }
}
.wf-label { font-size: 12.5px; font-weight: 600; color: var(--cohort-muted); &--rejected { color: var(--status-reject-text); } }
```

## 3. Wire it back into `candidate-dashboard.component.ts`

Replace the Stage column definition:

```typescript
import { WorkflowPipelineCellComponent } from '../../shared/components/workflow-pipeline-cell/workflow-pipeline-cell.component';

// ...inside buildColumns(), replace the Stage column with:
{
  headerName: 'Stage',
  flex: 1.6,
  minWidth: 190,
  cellRenderer: WorkflowPipelineCellComponent
},
```

(Remove the old plain `valueGetter: (p) => this.stageLabel(p.data)` version — `stageLabel()` on the dashboard component can stay for anywhere else that needs plain text, but the grid column itself now uses the dot-renderer component.)

That restores the six-dot green/red progress indicator per row, correctly reflecting the new 6-stage pipeline (Pre-Screen → Tech 1 → Tech 2 → Management → On-Shore → HR), with dots freezing red at whichever stage a rejection happened. Rebuild and confirm it's back.