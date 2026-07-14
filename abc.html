<div class="panel">
  <h3>Candidate Details</h3>
  <div class="detail-grid">
    <div class="detail-field"><label>Candidate ID</label><span>{{ candidate.candidateId || '—' }}</span></div>
    <div class="detail-field"><label>Full Name</label><span>{{ candidate.candidateName || '—' }}</span></div>
    <div class="detail-field"><label>Email</label><span>{{ candidate.candidateEmailId || '—' }}</span></div>
    <div class="detail-field"><label>Phone</label><span>{{ candidate.candidatePhoneNumber || '—' }}</span></div>
    <div class="detail-field"><label>Location</label><span>{{ candidate.location || '—' }}</span></div>
    <div class="detail-field"><label>Role / Designation</label><span>{{ candidate.roleDesignation || '—' }}</span></div>
    <div class="detail-field"><label>Profile</label><span>{{ candidate.profile || '—' }}</span></div>
    <div class="detail-field"><label>Allocated Biz Line</label><span>{{ candidate.allocatedBizLine || '—' }}</span></div>
    <div class="detail-field" *ngIf="candidate.cvUpload">
      <label>CV</label>
      <a [href]="candidate.cvUpload" target="_blank">View CV ↗</a>
    </div>
  </div>
</div>







.panel {
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 12px;
  padding: 22px 26px;

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







import { Component, Input } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';

@Component({
  selector: 'app-prescreen-panel',
  templateUrl: './prescreen-panel.component.html',
  styleUrls: ['../general-details-panel/general-details-panel.component.scss']
})
export class PrescreenPanelComponent {
  @Input() candidate!: Candidate;
}








<div class="panel">
  <h3>Pre-Screening</h3>
  <div class="detail-grid">
    <div class="detail-field"><label>Score</label><span>{{ candidate.prescreeningScore || '—' }} / 100</span></div>
    <div class="detail-field"><label>Result</label><span>{{ candidate.prescreeningSelected || 'Pending' }}</span></div>
    <div class="detail-field"><label>Test Date</label><span>{{ candidate.prescreeningTestDate || '—' }}</span></div>
    <div class="detail-field" *ngIf="candidate.prescreeningTestLink">
      <label>Test Result</label>
      <a [href]="candidate.prescreeningTestLink" target="_blank">Hirepro result ↗</a>
    </div>
  </div>
  <div class="comments-block" *ngIf="candidate.prescreeningComments">
    <label>Comments</label>
    <p>{{ candidate.prescreeningComments }}</p>
  </div>
</div>









.comments-block {
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--cohort-border);

  label { font-size: 11px; text-transform: uppercase; color: var(--cohort-muted); }
  p { margin: 6px 0 0; font-size: 14px; color: var(--cohort-text); }
}










import { Component, Input, OnChanges } from '@angular/core';
import { Candidate } from '../../../../core/models/candidate.model';
import { RoundKey, WorkflowVisibilityService } from '../../../../core/services/workflow-visibility.service';

@Component({
  selector: 'app-tech-round-panel',
  templateUrl: './tech-round-panel.component.html',
  styleUrls: ['./tech-round-panel.component.scss']
})
export class TechRoundPanelComponent implements OnChanges {
  @Input() candidate!: Candidate;
  @Input() roundKey!: RoundKey;

  isSkillGrid = false;
  visibilityInfo: any;

  constructor(private visibility: WorkflowVisibilityService) {}

  ngOnChanges(): void {
    this.isSkillGrid = this.roundKey === 'techRound1' || this.roundKey === 'techRound2Mgmt';
    this.visibilityInfo = this.visibility.getVisibilityFor(this.candidate, this.roundKey);
  }

  get round() {
    return this.candidate[this.roundKey];
  }

  get roundLabel(): string {
    const labels: Record<RoundKey, string> = {
      techRound1: 'Tech Round 1',
      techRound2Mgmt: 'Tech 2 / Management',
      onShoreRound: 'On-Shore Round',
      hrRound: 'HR Round — Final'
    };
    return labels[this.roundKey];
  }

  commentField(skillTechRound: 'techRound1Comment' | 'techRound2Comment') {
    return this.roundKey === 'techRound1' ? 'techRound1Comment' : 'techRound2Comment';
  }
}










<div class="panel">
  <div class="panel-header">
    <h3>{{ roundLabel }}</h3>
    <span class="lock-badge" *ngIf="visibilityInfo?.locked">
      🔒 {{ visibilityInfo.lockedMessage }}
    </span>
  </div>

  <!-- Skill grid for Tech Round 1 / Tech 2 -->
  <table class="skill-table" *ngIf="isSkillGrid && candidate.skills.length">
    <thead>
      <tr>
        <th>Skill</th>
        <th>Screening Score</th>
        <th>Round 1 Comment</th>
        <th *ngIf="roundKey === 'techRound2Mgmt'">Round 2 Comment</th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let skill of candidate.skills">
        <td class="skill-name">{{ skill.skillName }}</td>
        <td>{{ skill.screeningScore || '—' }}</td>
        <td>{{ skill.techRound1Comment || '—' }}</td>
        <td *ngIf="roundKey === 'techRound2Mgmt'">{{ skill.techRound2Comment || '—' }}</td>
      </tr>
    </tbody>
  </table>

  <!-- Overall feedback for On-Shore / HR -->
  <div class="overall-block" *ngIf="!isSkillGrid">
    <label>Overall Feedback</label>
    <p>{{ round.interviewFeedback || 'No feedback recorded yet.' }}</p>
  </div>

  <div class="round-footer">
    <div class="detail-field">
      <label>Interviewed By</label>
      <span>{{ round.interviewedBy?.title || 'Not assigned' }}</span>
    </div>
    <div class="detail-field">
      <label>Interview Date</label>
      <span>{{ round.interviewDate || '—' }}</span>
    </div>
    <div class="detail-field">
      <label>Decision</label>
      <span class="chip" [ngClass]="{
        'chip--pass': round.selection === 'Selected',
        'chip--reject': round.selection === 'Rejected',
        'chip--wait': round.selection === 'Pending'
      }">{{ round.selection }}</span>
    </div>
  </div>
</div>









.panel {
  background: white;
  border: 1px solid var(--cohort-border);
  border-radius: 12px;
  padding: 22px 26px;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;

  h3 { margin: 0; font-size: 15px; color: var(--cohort-text); }
}

.lock-badge {
  font-size: 12px;
  color: var(--status-wait-text);
  background: var(--status-wait-bg);
  padding: 4px 10px;
  border-radius: 999px;
}

.skill-table {
  width: 100%;
  border-collapse: collapse;

  th {
    text-align: left;
    font-size: 11px;
    text-transform: uppercase;
    color: var(--cohort-muted);
    padding: 8px 10px;
    border-bottom: 1px solid var(--cohort-border);
  }

  td {
    padding: 10px;
    font-size: 13.5px;
    color: var(--cohort-text);
    border-bottom: 1px solid var(--cohort-border);
  }

  .skill-name { font-weight: 600; }

  tr:hover td { background: var(--cohort-canvas); }
}

.overall-block {
  label { font-size: 11px; text-transform: uppercase; color: var(--cohort-muted); }
  p { margin: 6px 0 0; font-size: 14px; color: var(--cohort-text); }
}

.round-footer {
  display: flex;
  gap: 32px;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--cohort-border);
}

.detail-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  label { font-size: 11px; text-transform: uppercase; color: var(--cohort-muted); }
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










.ag-theme-alpine.cohort-grid {
  --ag-font-family: 'Segoe UI', system-ui, sans-serif;
  --ag-font-size: 13.5px;
  --ag-header-background-color: #FAFBFA;
  --ag-header-foreground-color: var(--cohort-muted);
  --ag-header-column-separator-display: none;
  --ag-row-hover-color: #F2F7F5;
  --ag-border-color: var(--cohort-border);
  --ag-row-border-color: var(--cohort-border);
  --ag-cell-horizontal-padding: 18px;
  --ag-selected-row-background-color: var(--status-pass-bg);

  .ag-header-cell-text {
    font-weight: 700;
    font-size: 11.5px;
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }

  .ag-row {
    transition: background-color 0.15s ease;
  }

  .ag-cell {
    display: flex;
    align-items: center;
  }
}













@import 'app/styles/tokens';
@import 'app/styles/ag-grid-theme';


