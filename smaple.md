You've found four real bugs plus two regressions. Let's fix all of them properly.

## Root cause of bugs #1–#3: "mine-locked" was being treated as "still open"

Once a round is finalized, `getAccess()` correctly returns `mine-locked` — but `getDashboardAction()` was treating *any* `mine-locked`/`mine-editable` round in your role list as reason to show **Open**, even for rounds you already finished. That's why Tech Round 1 kept showing Open after you submitted it, and why Management Panel still saw Open once they'd already completed Management Round and the candidate moved to On-Shore.

**Fix**: only look at the candidate's *currently active* round (the one that's reached-but-not-yet-finalized) — never a round you already completed.

## `core/services/workflow-visibility.service.ts` (updated — active-round logic + refresh-friendly)

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

  /** True only while the round is reached AND not yet finalized. */
  private isRoundActive(candidate: Candidate, round: RoundKey): boolean {
    if (round === 'techRound2' && candidate.techRound2.selection === RoundStatus.NotApplicable) return false;
    return this.hasCandidateReached(candidate, round) && candidate[round].selection === RoundStatus.Pending;
  }

  techRound2NotYetDecided(candidate: Candidate): boolean {
    return candidate.techRound1.selection === RoundStatus.Selected
      && candidate.techRound2.selection === RoundStatus.Pending
      && !candidate.techRound2.interviewedBy;
  }

  getAccess(candidate: Candidate, round: RoundKey): RoundAccess {
    if (!this.hasCandidateReached(candidate, round)) {
      return { state: 'not-reached', message: 'Not reached yet.' };
    }

    const user = this.currentUser.get();

    if (user.role === 'HrAdmin' || user.role === 'Recruiter') {
      return { state: 'mine-locked' }; // always read-only viewer, never editable
    }

    const myRounds = ROLE_ROUNDS[user.role];
    if (!myRounds.includes(round)) return { state: 'no-access' };

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

  canViewRoundSection(round: RoundKey): boolean {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return true;
    const myRounds = ROLE_ROUNDS[user.role];
    if (!myRounds.length) return false;
    const maxIdx = Math.max(...myRounds.map(r => SECTION_ORDER.indexOf(r)));
    return SECTION_ORDER.indexOf(round) <= maxIdx;
  }

  /** The single round this candidate is actively sitting at, restricted to the viewer's own rounds. Null if none. */
  private getMyActiveRound(candidate: Candidate): RoundKey | null {
    const user = this.currentUser.get();
    const myRounds = ROLE_ROUNDS[user.role];
    for (const r of myRounds) {
      if (this.isRoundActive(candidate, r)) return r;
    }
    return null;
  }

  canOpenCandidate(candidate: Candidate): boolean {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return true;

    const active = this.getMyActiveRound(candidate);
    if (!active) return false;
    const access = this.getAccess(candidate, active);
    return access.state === 'mine-editable' || access.state === 'mine-locked';
  }

  /** Dashboard action — driven ONLY by the candidate's current active round, never a finished one. */
  getDashboardAction(candidate: Candidate): 'open' | 'assignable' | 'locked' | 'not-in-workflow' {
    const user = this.currentUser.get();
    if (user.role === 'HrAdmin' || user.role === 'Recruiter') return 'open';

    const active = this.getMyActiveRound(candidate);
    if (!active) return 'not-in-workflow'; // nothing pending in this viewer's rounds right now

    const access = this.getAccess(candidate, active);
    if (access.state === 'mine-editable' || access.state === 'mine-locked') return 'open';
    if (access.state === 'assignable') return 'assignable';
    return 'locked';
  }

  roundFieldPrefix(round: RoundKey): string {
    const map: Record<RoundKey, string> = {
      techRound1: 'TechRound1', techRound2: 'TechRound2', mgmtRound: 'MgmtRound',
      onShoreRound: 'OnshoreRound', hrRound: 'HrRound'
    };
    return map[round];
  }
}
```

## Bug: page-refresh needed after Assign

`gridApi.refreshCells({ force: true })` doesn't reliably re-run function-based `cellRenderer`s in every AG Grid version. Use `applyTransaction` instead — it properly re-renders the specific row.

**`candidate-dashboard.component.ts`** (full rewrite — also restores stats, restores stage filter, adds server-side filtering)

```typescript
import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ColDef, GridOptions, GridApi, GridReadyEvent } from 'ag-grid-community';
import { Subject, debounceTime } from 'rxjs';
import { CandidateService } from '../../core/services/candidate.service';
import { WorkflowVisibilityService } from '../../core/services/workflow-visibility.service';
import { CurrentUserService } from '../../core/services/current-user.service';
import { Candidate } from '../../core/models/candidate.model';
import { RoundStatus } from '../../core/models/round-status.enum';
import { WorkflowPipelineCellComponent } from '../../shared/components/workflow-pipeline-cell/workflow-pipeline-cell.component';

@Component({
  selector: 'app-candidate-dashboard',
  templateUrl: './candidate-dashboard.component.html',
  styleUrls: ['./candidate-dashboard.component.scss']
})
export class CandidateDashboardComponent implements OnInit {

  rowData: Candidate[] = [];
  selectedRows: Candidate[] = [];

  quickFilterText = '';
  stageFilter = 'all';
  profileFilter = 'all';
  locationFilter = 'all';
  profileOptions: string[] = [];
  locationOptions: string[] = [];

  loading = true;
  errorMessage: string | null = null;
  gridApi!: GridApi;

  showSkipDialog = false;
  pendingSkipCandidates: Candidate[] = [];

  stats = { total: 0, active: 0, offer: 0, rejected: 0 };

  private searchTrigger = new Subject<void>();

  columnDefs: ColDef[] = [];

  gridOptions: GridOptions = {
    rowHeight: 72,
    headerHeight: 46,
    rowSelection: 'multiple',
    suppressRowClickSelection: true,
    animateRows: true,
    domLayout: 'autoHeight',
    getRowId: (params) => `${params.data.listSource}-${params.data.id}`,
    onSelectionChanged: () => { this.selectedRows = this.gridApi?.getSelectedRows() || []; }
  };

  constructor(
    private candidateService: CandidateService,
    private router: Router,
    public visibility: WorkflowVisibilityService,
    private currentUser: CurrentUserService
  ) {
    this.searchTrigger.pipe(debounceTime(400)).subscribe(() => this.loadCandidates());
  }

  ngOnInit(): void {
    this.buildColumns();
    this.loadCandidates();
  }

  get isMultiAssignRole(): boolean {
    return ['TechPanel', 'MgmtPanel'].includes(this.currentUser.get().role);
  }

  private buildColumns(): void {
    const showCheckbox = this.isMultiAssignRole;
    this.columnDefs = [
      ...(showCheckbox ? [{ headerCheckboxSelection: true, checkboxSelection: true, width: 48, pinned: 'left' as const, sortable: false, filter: false }] : []),
      {
        headerName: 'Candidate', field: 'candidateName', flex: 2, minWidth: 220,
        cellRenderer: (p: any) => `
          <div class="candidate-cell">
            <div class="avatar">${this.initials(p.data.candidateName)}</div>
            <div class="candidate-meta">
              <div class="name">${p.data.candidateName || 'Unnamed'}</div>
              <div class="sub">${p.data.candidateEmailId || '—'} · ID ${p.data.candidateId || '—'}</div>
            </div>
          </div>`
      },
      { headerName: 'Role / Profile', flex: 1.2, minWidth: 170, valueGetter: (p: any) => `${p.data.roleDesignation || '—'} · ${p.data.profile || '—'}` },
      { headerName: 'Location', field: 'location', flex: 0.8, minWidth: 110, valueFormatter: (p: any) => p.value || '—' },
      {
        headerName: 'Pre-Screen', flex: 1, minWidth: 140,
        cellRenderer: (p: any) => {
          const score = p.data.prescreeningScore; const result = p.data.prescreeningSelected;
          if (!score && !result) return `<span class="muted-pill">Not started</span>`;
          const cls = result === 'Selected' ? 'pass' : result === 'Rejected' ? 'reject' : 'wait';
          return `<span class="score-pill">${score ?? '—'}/100</span><span class="chip chip--${cls}">${result || 'Pending'}</span>`;
        }
      },
      { headerName: 'Stage', flex: 1.6, minWidth: 190, cellRenderer: WorkflowPipelineCellComponent },
      {
        headerName: '', flex: 1.1, minWidth: 130, sortable: false, filter: false,
        cellRenderer: (p: any) => this.actionCellHtml(p.data),
        onCellClicked: (p: any) => this.onActionClick(p.data)
      }
    ];
  }

  private buildFilterClause(): string | undefined {
    const clauses: string[] = [];
    const q = this.quickFilterText.trim();
    if (q) {
      const fields = ['CandidateName', 'CandidateEmailId', 'CandidatePhoneNumber', 'CandidateId', 'RoleDesignation', 'Profile', 'Location'];
      const orClause = fields.map(f => `substringof('${this.escapeODataValue(q)}',${f})`).join(' or ');
      clauses.push(`(${orClause})`);
    }
    if (this.profileFilter !== 'all') clauses.push(`Profile eq '${this.escapeODataValue(this.profileFilter)}'`);
    if (this.locationFilter !== 'all') clauses.push(`Location eq '${this.escapeODataValue(this.locationFilter)}'`);
    return clauses.length ? clauses.join(' and ') : undefined;
  }

  private escapeODataValue(v: string): string { return v.replace(/'/g, "''"); }

  loadCandidates(): void {
    this.loading = true;
    this.errorMessage = null;
    const filter = this.buildFilterClause();

    this.candidateService.getAllCandidates(filter).subscribe({
      next: (candidates) => {
        let filtered = candidates;
        if (this.stageFilter !== 'all') {
          filtered = candidates.filter(c => this.matchesStage(c, this.stageFilter));
        }
        this.rowData = filtered;
        this.deriveFilterOptions(candidates);
        this.computeStats(candidates);
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'This query is too large for SharePoint to run. A column used in the filter needs to be indexed.';
        this.loading = false;
      }
    });
  }

  private matchesStage(c: Candidate, stage: string): boolean {
    const map: Record<string, () => boolean> = {
      preScreen: () => c.prescreeningSelected !== RoundStatus.Selected,
      techRound1: () => c.prescreeningSelected === RoundStatus.Selected && c.techRound1.selection !== RoundStatus.Selected,
      techRound2: () => c.techRound1.selection === RoundStatus.Selected && this.visibility.techStageDecision(c) !== RoundStatus.Selected && c.techRound2.selection !== RoundStatus.NotApplicable,
      mgmtRound: () => this.visibility.techStageDecision(c) === RoundStatus.Selected && c.mgmtRound.selection !== RoundStatus.Selected,
      onShoreRound: () => c.mgmtRound.selection === RoundStatus.Selected && c.onShoreRound.selection !== RoundStatus.Selected,
      hrRound: () => c.onShoreRound.selection === RoundStatus.Selected && c.hrRound.selection !== RoundStatus.Selected,
      offer: () => c.hrRound.selection === RoundStatus.Selected,
      rejected: () => this.visibility.techStageDecision(c) === RoundStatus.Rejected || c.mgmtRound.selection === RoundStatus.Rejected || c.onShoreRound.selection === RoundStatus.Rejected || c.prescreeningSelected === RoundStatus.Rejected
    };
    return map[stage] ? map[stage]() : true;
  }

  private deriveFilterOptions(candidates: Candidate[]): void {
    this.profileOptions = [...new Set(candidates.map(c => c.profile).filter(Boolean))];
    this.locationOptions = [...new Set(candidates.map(c => c.location).filter(Boolean))];
  }

  private computeStats(candidates: Candidate[]): void {
    this.stats.total = candidates.length;
    this.stats.rejected = candidates.filter(c => this.matchesStage(c, 'rejected')).length;
    this.stats.offer = candidates.filter(c => c.hrRound.selection === RoundStatus.Selected).length;
    this.stats.active = this.stats.total - this.stats.rejected - this.stats.offer;
  }

  onGridReady(p: GridReadyEvent): void { this.gridApi = p.api; }
  onSearchChange(): void { this.searchTrigger.next(); }
  onFilterChange(): void { this.loadCandidates(); }
  resetFilters(): void { this.quickFilterText = ''; this.stageFilter = 'all'; this.profileFilter = 'all'; this.locationFilter = 'all'; this.loadCandidates(); }

  private actionCellHtml(c: Candidate): string {
    const action = this.visibility.getDashboardAction(c);
    if (action === 'open') return `<button class="open-btn">Open ↗</button>`;
    if (action === 'assignable') return `<button class="assign-btn">Assign to Me</button>`;
    if (action === 'not-in-workflow') return `<span class="locked-pill">Not in your workflow</span>`;
    return `<span class="locked-pill">🔒 Locked</span>`;
  }

  private onActionClick(c: Candidate): void {
    const action = this.visibility.getDashboardAction(c);
    if (action === 'open') this.openCandidate(c);
    if (action === 'assignable') this.assignSingle(c);
  }

  openCandidate(c: Candidate): void { this.router.navigate(['/candidates', c.listSource, c.id]); }
  assignSingle(c: Candidate): void { this.tryAssign([c]); }
  assignSelected(): void { if (this.selectedRows.length) this.tryAssign(this.selectedRows); }

  private tryAssign(candidates: Candidate[]): void {
    const role = this.currentUser.get().role;
    if (role === 'MgmtPanel') {
      const skipNeeded = candidates.filter(c => this.visibility.techRound2NotYetDecided(c));
      if (skipNeeded.length) {
        this.pendingSkipCandidates = skipNeeded;
        this.showSkipDialog = true;
        const rest = candidates.filter(c => !skipNeeded.includes(c));
        this.doAssign(rest, 'mgmtRound');
        return;
      }
      this.doAssign(candidates, 'mgmtRound');
      return;
    }
    if (role === 'TechPanel') {
      candidates.forEach(c => {
        const round = c.techRound1.selection === RoundStatus.Selected ? 'techRound2' : 'techRound1';
        this.doAssign([c], round as any);
      });
    }
  }

  get pendingSkipNames(): string[] { return this.pendingSkipCandidates.map(c => c.candidateName); }

  confirmSkip(): void {
    this.pendingSkipCandidates.forEach(c => { c.techRound2.selection = RoundStatus.NotApplicable; });
    this.doAssign(this.pendingSkipCandidates, 'mgmtRound');
    this.showSkipDialog = false;
    this.pendingSkipCandidates = [];
  }
  cancelSkip(): void { this.showSkipDialog = false; this.pendingSkipCandidates = []; }

  private doAssign(candidates: Candidate[], round: 'techRound1' | 'techRound2' | 'mgmtRound'): void {
    const user = this.currentUser.get();
    const spUser = { id: user.id, title: user.title, email: user.email };
    const prefix = this.visibility.roundFieldPrefix(round);
    candidates.forEach(c => {
      this.candidateService.assignUserToRound(c, prefix, spUser).subscribe({
        next: () => {
          (c as any)[round].interviewedBy = spUser;
          this.gridApi?.applyTransaction({ update: [c] }); // refresh just this row, no reload needed
        },
        error: (err) => { console.error('Assign failed:', err); alert('Could not assign — check console.'); }
      });
    });
  }

  private initials(name: string): string {
    if (!name) return '?';
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }
}
```

## `candidate-dashboard.component.html` (restored stats + full filters + assign bar)

```html
<div class="dashboard-container">
  <header class="dashboard-header"><h4>Recruitment Pipeline</h4><h1>Candidates</h1></header>

  <div class="stat-strip" *ngIf="!loading && !errorMessage">
    <div class="stat-card"><div class="stat-value">{{ stats.total }}</div><div class="stat-label">Total Candidates</div></div>
    <div class="stat-card stat-card--active"><div class="stat-value">{{ stats.active }}</div><div class="stat-label">In Progress</div></div>
    <div class="stat-card stat-card--pass"><div class="stat-value">{{ stats.offer }}</div><div class="stat-label">Offer Stage</div></div>
    <div class="stat-card stat-card--reject"><div class="stat-value">{{ stats.rejected }}</div><div class="stat-label">Rejected</div></div>
  </div>

  <div class="dashboard-toolbar" *ngIf="!loading && !errorMessage">
    <input type="text" class="search-input" placeholder="Search name, email, phone, ID, role, profile or location..."
      [(ngModel)]="quickFilterText" (ngModelChange)="onSearchChange()" />
    <select class="filter-select" [(ngModel)]="stageFilter" (ngModelChange)="onFilterChange()">
      <option value="all">All Stages</option>
      <option value="preScreen">Pre-Screen</option>
      <option value="techRound1">Tech Round 1</option>
      <option value="techRound2">Tech Round 2</option>
      <option