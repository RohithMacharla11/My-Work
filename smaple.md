Two separable bugs here — let me fix both properly.

## 1. Pagination — switch to true numbered pages (not "next-link chaining")

The URL-history approach I gave you last time is fragile and doesn't behave like AG Grid because SharePoint's `__next` link only moves forward. The cleaner fix: SharePoint's REST API also supports `$skip`, which lets you jump to **any** page directly — exactly like AG Grid's numbered pagination. Combined with `$inlinecount=allpages` (which you already have), this gives you real page-N navigation with a proper page count.

**`sharepoint.service.ts`** — add `skip` to the query builder:

```typescript
export interface ListQuery {
  select?: string[];
  expand?: string[];
  filter?: string;
  orderby?: string;
  top?: number;
  skip?: number;          // NEW
  inlinecount?: boolean;
}

private buildListUrl(listTitle: string, q: ListQuery): string {
  const params: string[] = [];
  if (q.select?.length) params.push(`$select=${q.select.join(',')}`);
  if (q.expand?.length) params.push(`$expand=${q.expand.join(',')}`);
  if (q.filter) params.push(`$filter=${q.filter}`);
  if (q.orderby) params.push(`$orderby=${q.orderby}`);
  params.push(`$top=${q.top ?? SERVER_PAGE_SIZE}`);
  if (q.skip) params.push(`$skip=${q.skip}`);              // NEW
  if (q.inlinecount) params.push(`$inlinecount=allpages`);
  return `${this.ctx.list(listTitle)}/items?${params.join('&')}`;
}
```

**`candidate.service.ts`** — `queryFor` takes a page number and computes skip:

```typescript
private queryFor(
  tab: 'active' | 'rejected',
  filters: DashboardFilters,
  location: LocationKey,
  roles: UserRole[],
  pageSize: number,
  page: number,           // NEW
): ListQuery {
  const statusClause = buildStatusFilter(roles, tab);
  const otherClause = buildDashboardFilter(filters);
  const locClause = `Location eq '${location.replace(/'/g, "''")}'`;
  const parts = [statusClause, otherClause, locClause].filter(Boolean);
  const combined = parts.join(' and ');

  return {
    select: DASHBOARD_SELECT_FIELDS,
    filter: combined,
    orderby: 'Modified desc',
    top: pageSize,
    skip: (page - 1) * pageSize,   // NEW
    inlinecount: true,
  };
}

getActivePage(location: LocationKey, filters: DashboardFilters, roles: UserRole[], pageSize: number, page: number): Observable<Page<Candidate>> {
  return this.sp.getPage<any>(LOCATION_LIST[location], this.queryFor('active', filters, location, roles, pageSize, page))
    .pipe(map(p => this.mapPage(p, location)));
}

getRejectedPage(location: LocationKey, filters: DashboardFilters, roles: UserRole[], pageSize: number, page: number): Observable<Page<Candidate>> {
  return this.sp.getPage<any>(LOCATION_LIST[location], this.queryFor('rejected', filters, location, roles, pageSize, page))
    .pipe(map(p => this.mapPage(p, location)));
}
```

**`dashboard.component.ts`** — real page state, no more `nextUrl`/`loadMore`:

```typescript
pageSize = 25;
currentPage = 1;
total = 0;

get totalPages(): number { return Math.max(1, Math.ceil(this.total / this.pageSize)); }
get canGoNext(): boolean { return this.currentPage < this.totalPages && !this.loading; }
get canGoPrev(): boolean { return this.currentPage > 1 && !this.loading; }
get pageStart(): number { return this.rows.length ? (this.currentPage - 1) * this.pageSize + 1 : 0; }
get pageEnd(): number { return (this.currentPage - 1) * this.pageSize + this.rows.length; }

private reload(): void {
  this.loading = true;
  this.error = '';
  this.rows = [];
  const roles = this.currentUser.get().roles;

  if (this.tab === null) {
    const active$ = this.candidates.getActivePage(this.location, this.filters, roles, this.pageSize, this.currentPage);
    const rejected$ = this.candidates.getRejectedPage(this.location, this.filters, roles, this.pageSize, this.currentPage);
    forkJoin([active$, rejected$]).subscribe({
      next: ([a, r]) => {
        this.rows = [...a.items, ...r.items];
        this.total = (a.total ?? 0) + (r.total ?? 0);
        this.loading = false;
      },
      error: err => { this.loading = false; this.error = this.humanError(err); this.total = 0; },
    });
    return;
  }

  const source$ = this.tab === 'active'
    ? this.candidates.getActivePage(this.location, this.filters, roles, this.pageSize, this.currentPage)
    : this.candidates.getRejectedPage(this.location, this.filters, roles, this.pageSize, this.currentPage);

  source$.subscribe({
    next: page => { this.rows = page.items; this.total = page.total ?? 0; this.loading = false; },
    error: err => { this.loading = false; this.error = this.humanError(err); this.total = 0; },
  });
}

goToPage(n: number): void {
  if (n < 1 || n > this.totalPages || n === this.currentPage) return;
  this.currentPage = n;
  this.reload();
}
goNext(): void { this.goToPage(this.currentPage + 1); }
goPrev(): void { this.goToPage(this.currentPage - 1); }

setPageSize(size: number): void {
  if (size === this.pageSize) return;
  this.pageSize = size;
  this.currentPage = 1;   // always reset to page 1 on size change
  this.reload();
}
```

Also **reset `currentPage = 1`** inside `setTab()`, `setLocation()`, `onFilterChange()`, `onSearch()`, and `clearFilters()` — any time the underlying result set changes, you must go back to page 1, otherwise you could land on a page number that no longer exists for the new filter.

**Template:**
```html
<div class="card-foot">
  <span class="dim small">{{ pageStart }}–{{ pageEnd }} of {{ total }} shown</span>
  <select class="filter" [ngModel]="pageSize" (ngModelChange)="setPageSize($event)">
    <option [ngValue]="10">10</option>
    <option [ngValue]="25">25</option>
    <option [ngValue]="50">50</option>
    <option [ngValue]="100">100</option>
  </select>
  <span class="dim small" *ngIf="loading">Loading…</span>
  <div class="page-nav">
    <button class="btn ghost sm" [disabled]="!canGoPrev" (click)="goPrev()">← Prev</button>
    <span class="dim small">Page {{ currentPage }} of {{ totalPages }}</span>
    <button class="btn ghost sm" [disabled]="!canGoNext" (click)="goNext()">Next →</button>
  </div>
</div>
```

One honest caveat: `$skip` on large SharePoint lists (thousands+ of items) is slower than skip-token chaining because the server still has to enumerate past the skipped rows. For a per-cohort candidate list (dozens to low hundreds), this is fine and gives you the exact AG-Grid-style behavior you asked for. If a cohort ever grows into the thousands, this is worth revisiting — flagging it now so it's a conscious tradeoff, not a surprise later.

## 2. Tech Round 2 "Assign" button showing for MgmtPanel — confirmed bug

Found it, and it's a real logic bug, not a config typo. Two things compound:

**A) Config contradicts its own comment.** In `cohort.config.ts`, `ROUNDS.techRound2.selfAssign` is `['TechPanel', 'MgmtPanel']` — but the comment directly above it says *"anyone on the tech panel (or HR) can pick it up."* HR's ability comes through `isHRUser()` separately (rounds with empty `selfAssign` default to HR-only) — so `MgmtPanel` was never supposed to be in that array. Fix:

```typescript
techRound2: { prefix: 'TechRound2', name: 'Tech Round 2', selfAssign: ['TechPanel'], assignGroup: ['Tech Interview Panel', 'Mgmt Interview Panel'] },
```

**B) The bigger bug is in `getAccess()` itself** — it doesn't even check `selfAssign` for the "assignable" state. It uses a broader `inPanel` flag (`editableRounds().includes(key)`), which is your **visibility hierarchy** (`ROLE_ROUNDS`) — and `ROLE_ROUNDS.MgmtPanel` deliberately includes `'techRound2'` so managers can *view* it. But `getAccess()` was letting that same visibility flag also grant the **claim/assign** state, which is wrong — visibility and ownership are two different concerns and got conflated:

```typescript
// workflow.service.ts — getAccess()
if (active && !locked) {
  if (!assignedTo && (canEditByRole || this.ownsRoundByRole(key))) {   // FIXED — was `inPanel`
    return { state: 'assignable', canEdit: false };
  }
  if (mine || canEditByRole) return { state: 'editable', canEdit: true };
}
```

`ownsRoundByRole(key)` already exists and correctly checks the `selfAssign` array (or falls back to HR-only if empty) — it just wasn't being used here.

**Result after both fixes:** MgmtPanel members still **see** the Tech Round 2 card when it's reached (correct — they need visibility into it before their own round), but it now renders as **`readonly`** for them — no "Assign to me" button, since they're not in `techRound2`'s `selfAssign` list and the state calculation now actually respects that.

**One thing to double check on your end**, since I can't verify without seeing `getDashboardAction()` again: the dashboard's `canSelfAssign` was already correctly built from `ROUNDS[active].selfAssign.some(...)` (not `inPanel`), so the dashboard-row "Assign to me" icon was likely *already correct* — this bug was specific to the round-panel page inside candidate detail. If you were also seeing the assign icon for Tech Round 2 on the **dashboard row** while logged in as MgmtPanel, tell me and I'll trace `getDashboardAction()` again, since that'd mean there's a second occurrence of this same mistake I haven't caught yet.