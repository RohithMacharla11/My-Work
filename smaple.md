Good — here's the full implementation plan. I'm giving you exact code changes per file. Since I've only seen these files as photos (not live repo access), verify line numbers/exact surrounding code before pasting — but the logic below is complete and self-consistent.

## 1. SharePoint — statuses to configure

Add a **Choice column** named `Status` to `MumbaiInterview`, `ChennaiInterview`, `BangloreInterview` with exactly these 7 values (must match your `WorkflowStatus` enum strings verbatim):

```
Rejected
Pending with Tech Round 1
Pending with Tech Round 2 or Management Round
Pending with Management Round
Pending with Onshore Round or HR Round
Pending with Onshore
Pending with HR
```

Nothing else changes in SharePoint — you already have this column and `computeStatus()`/`patchStatus()` writing to it.

## 2. `odata-filter.service.ts`

Remove `prescreen` param entirely — Status supersedes it — and add the status-clause builder with reverse-psychology include/exclude:

```typescript
import { UserRole, WorkflowStatus, ROLE_STATUS_MAP, POWER_ROLES } from '../config/cohort.config';

const ALL_ACTIVE_STATUSES: WorkflowStatus[] =
  Object.values(WorkflowStatus).filter(s => s !== WorkflowStatus.Rejected);

/** Builds the Status clause for a tab, given the user's roles. Returns null = no filter needed. */
export function buildStatusFilter(roles: UserRole[], tab: 'active' | 'rejected'): string {
  if (tab === 'rejected') {
    return `Status eq '${WorkflowStatus.Rejected}'`;
  }

  if (roles.some(r => POWER_ROLES.includes(r))) return ''; // sees all active statuses, no clause

  const allowed = new Set<WorkflowStatus>();
  for (const role of roles) {
    (ROLE_STATUS_MAP[role] ?? []).forEach(s => {
      if (s !== WorkflowStatus.Rejected) allowed.add(s);
    });
  }

  if (allowed.size === 0) return `Status eq 'zzz-no-access'`; // safety net, matches nothing
  if (allowed.size >= ALL_ACTIVE_STATUSES.length) return '';

  const excluded = ALL_ACTIVE_STATUSES.filter(s => !allowed.has(s));

  // reverse psychology: whichever list is shorter wins
  return allowed.size <= excluded.length
    ? [...allowed].map(s => `Status eq '${s}'`).join(' or ')
    : excluded.map(s => `Status ne '${s}'`).join(' and ');
}

/** buildDashboardFilter — drop the old prescreen param, keep role/profile/cohort/type/search */
export function buildDashboardFilter(filters: DashboardFilters): string {
  const clauses: string[] = [];
  const role = eqClause('RoleDesignation', filters.role);
  const profile = eqClause('Profile', filters.profile);
  const type = eqClause('InterviewType', filters.interviewType);
  const cohort = eqClause('Cohort', filters.cohort);
  const search = searchGroup(filters.search);
  [role, profile, type, cohort, search].forEach(c => { if (c) clauses.push(c); });
  return clauses.join(' and ');
}
```

## 3. `sharepoint.service.ts` — add count support

```typescript
export interface ListQuery {
  select?: string[];
  expand?: string[];
  filter?: string;
  orderby?: string;
  top?: number;
  inlinecount?: boolean;   // NEW
}

export interface Page<T> {
  items: T[];
  nextUrl: string | null;
  total?: number;          // NEW
}

private buildListUrl(listTitle: string, q: ListQuery): string {
  const params: string[] = [];
  if (q.select?.length) params.push(`$select=${q.select.join(',')}`);
  if (q.expand?.length) params.push(`$expand=${q.expand.join(',')}`);
  if (q.filter) params.push(`$filter=${q.filter}`);
  if (q.orderby) params.push(`$orderby=${q.orderby}`);
  params.push(`$top=${q.top ?? SERVER_PAGE_SIZE}`);
  if (q.inlinecount) params.push(`$inlinecount=allpages`);   // NEW
  return `${this.ctx.list(listTitle)}/items?${params.join('&')}`;
}

getPageByUrl<T>(url: string): Observable<Page<T>> {
  return this.http.get<any>(url, { headers: this.jsonHeaders() }).pipe(
    map(res => ({
      items: (res?.d?.results ?? []) as T[],
      nextUrl: (res?.d?.__next as string) ?? null,
      total: res?.d?.__count != null ? Number(res.d.__count) : undefined,   // NEW
    })),
  );
}
```

`$inlinecount=allpages` is preserved in `__next` automatically by SharePoint, so `total` stays populated across Load More pages too — no extra plumbing needed there.

## 4. `candidate.service.ts`

```typescript
private queryFor(
  tab: 'active' | 'rejected',
  filters: DashboardFilters,
  location: LocationKey,
  roles: UserRole[],
  pageSize: number,
): ListQuery {
  const statusClause = buildStatusFilter(roles, tab);
  const otherClause = buildDashboardFilter(filters);
  const locClause = `Location eq '${location.replace(/'/g, "''")}'`;

  const parts = [statusClause, otherClause, locClause].filter(Boolean);
  const combined = parts.map(p => (parts.length > 1 ? `(${p})` : p)).join(' and ');

  return {
    select: SELECT_FIELDS,
    expand: EXPAND_FIELDS,
    filter: combined,
    orderby: 'Modified desc',
    top: pageSize,
    inlinecount: true,
  };
}

getActivePage(location: LocationKey, filters: DashboardFilters, roles: UserRole[], pageSize = SERVER_PAGE_SIZE): Observable<Page<Candidate>> {
  return this.sp
    .getPage<any>(LOCATION_LIST[location], this.queryFor('active', filters, location, roles, pageSize))
    .pipe(map(page => this.mapPage(page, location)));
}

getRejectedPage(location: LocationKey, filters: DashboardFilters, roles: UserRole[], pageSize = SERVER_PAGE_SIZE): Observable<Page<Candidate>> {
  return this.sp
    .getPage<any>(LOCATION_LIST[location], this.queryFor('rejected', filters, location, roles, pageSize))
    .pipe(map(page => this.mapPage(page, location)));
}

private mapPage(page: Page<any>, location: LocationKey): Page<Candidate> {
  return {
    items: page.items.map(i => this.mapCandidate(i, location)),
    nextUrl: page.nextUrl,
    total: page.total,   // NEW — carry through
  };
}
```

`getNextPage` needs no changes — it already follows `nextUrl` via `getPageByUrl`, which now also returns `total`.

## 5. `dashboard.component.ts`

**Remove entirely**: `candidateVisibleForUser()` and the role-filter line inside `filteredRows` (`rows = rows.filter(c => this.candidateVisibleForUser(c))`). That's the client-side pass being replaced.

**`filteredRows` getter** now only does the client-side text search (as agreed, kept client-side for now):

```typescript
get filteredRows(): Candidate[] {
  let rows = this.rows;
  if (this.searchTerm) {
    rows = rows.filter(c =>
      this.UI_SEARCHABLE_COLUMNS.some(col => {
        const val = (c as any)[col];
        return val && val.toString().toLowerCase().includes(this.searchTerm);
      }),
    );
  }
  return rows;
}
```

**`reload()`** — pass roles + pageSize, and capture `total`:

```typescript
pageSize = 25;               // NEW
total = 0;                    // NEW, replaces relying on rows.length for the count display

private reload(): void {
  this.loading = true;
  this.error = '';
  this.rows = [];
  this.nextUrl = null;

  const roles = this.currentUser.get().roles;

  if (this.tab === null) {
    const active$ = this.candidates.getActivePage(this.location, this.filters, roles, this.pageSize);
    const rejected$ = this.candidates.getRejectedPage(this.location, this.filters, roles, this.pageSize);

    forJoin([active$, rejected$]).subscribe({
      next: ([actPage, rejPage]) => {
        this.rows = [...actPage.items, ...rejPage.items];
        this.total = (actPage.total ?? 0) + (rejPage.total ?? 0);   // NEW
        this.nextUrl = null;
        this.loading = false;
      },
      error: err => { this.loading = false; this.error = this.humanError(err); },
    });
    return;
  }

  const source$ = this.tab === 'active'
    ? this.candidates.getActivePage(this.location, this.filters, roles, this.pageSize)
    : this.candidates.getRejectedPage(this.location, this.filters, roles, this.pageSize);

  source$.subscribe({
    next: page => {
      this.rows = page.items;
      this.nextUrl = page.nextUrl;
      this.total = page.total ?? page.items.length;   // NEW, fallback safety
      this.loading = false;
    },
    error: err => { this.loading = false; this.error = this.humanError(err); },
  });
}

loadMore(): void {
  if (!this.nextUrl) return;
  this.loading = true;
  this.candidates.getNextPage(this.nextUrl, this.location).subscribe({
    next: page => {
      this.rows = this.rows.concat(page.items);
      this.nextUrl = page.nextUrl;
      if (page.total != null) this.total = page.total;   // NEW, keeps count in sync
      this.loading = false;
    },
    error: err => { this.loading = false; this.error = this.humanError(err); },
  });
}

/** NEW — page size selector */
setPageSize(size: number): void {
  if (size === this.pageSize) return;
  this.pageSize = size;
  this.reload();
}
```

**Template** (`dashboard.component.html`) — change the footer text and add the size selector next to Load More:

```html
<div class="card-foot">
  <span class="dim small">{{ rows.length }} of {{ total }} shown</span>
  <select class="filter" [(ngModel)]="pageSize" (change)="setPageSize($any($event.target).value)">
    <option [ngValue]="25">25</option>
    <option [ngValue]="50">50</option>
    <option [ngValue]="100">100</option>
  </select>
  <span class="dim small" *ngIf="loading">Loading…</span>
  <button class="btn sm" *ngIf="canLoadMore" (click)="loadMore()">Load more</button>
</div>
```

## 6. One thing I need you to verify — status updates on unassign

`computeStatus()` and `patchStatus()` are correct and already handle every stage. But your unassign flows (`confirmUnassign()`) do things like resetting Tech Round 2 back to `Pending` when a manager who skipped it gets unassigned, or resetting Onshore back to `Pending` when HR unassigns the HR round. **Each of those resets needs `patchStatus()` called right after**, or the `Status` column will go stale and the candidate will vanish from/appear in the wrong role's filtered view — since visibility is now driven entirely by that column.

Check each `next: () => { ... this.reload(); }` block inside `confirmUnassign()` — if it doesn't already call `this.candidates.patchStatus(candidate)` before `reload()`, add it there. Same check applies to every `submitRound()` and `assignInterviewer()` success callback across the round panels (tech/mgmt/onshore/hr) — I haven't seen those panel component files yet, so I can't confirm this from what's uploaded. Upload `tech-round-panel.component.ts`, `mgmt-round-panel.component.ts`, `onshore-round-panel.component.ts`, and `hr-round-panel.component.ts` next and I'll check exactly this.