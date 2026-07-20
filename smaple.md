Got it — clean three-zone footer: left = count for current page, middle = page-size selector, right = prev/next. Here's the exact layout and the component wiring to make sure it actually renders and functions.

## `dashboard.component.html` — footer only

```html
<div class="card-foot pager">

  <!-- LEFT: count for current page -->
  <div class="pager-left">
    <span class="dim small">{{ rows.length }} item{{ rows.length === 1 ? '' : 's' }} on this page</span>
    <span class="dim small" *ngIf="loading">Loading…</span>
  </div>

  <!-- MIDDLE: page size selector -->
  <div class="pager-mid">
    <label class="dim small" for="pageSizeSelect">Show</label>
    <select id="pageSizeSelect" class="filter" [ngModel]="pageSize" (ngModelChange)="setPageSize($event)">
      <option [ngValue]="10">10</option>
      <option [ngValue]="25">25</option>
      <option [ngValue]="50">50</option>
      <option [ngValue]="100">100</option>
    </select>
    <span class="dim small">per page</span>
  </div>

  <!-- RIGHT: prev / page indicator / next -->
  <div class="pager-right">
    <button class="btn ghost sm" [disabled]="!canGoPrev" (click)="goPrev()">← Previous</button>
    <span class="dim small">Page {{ currentPage }} of {{ totalPages }}</span>
    <button class="btn ghost sm" [disabled]="!canGoNext" (click)="goNext()">Next →</button>
  </div>

</div>
```

## `dashboard.component.scss` (or wherever `.card-foot` lives) — add the layout

```scss
.pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  padding: 12px 16px;
}
.pager-left, .pager-mid, .pager-right {
  display: flex;
  align-items: center;
  gap: 8px;
}
.pager-mid { flex: 1; justify-content: center; }
```

## `dashboard.component.ts` — confirm these exact members exist (this is the full, final version — replace whatever pagination code you have now with this)

```typescript
pageSize = 25;
currentPage = 1;
total = 0;

get totalPages(): number { return Math.max(1, Math.ceil(this.total / this.pageSize)); }
get canGoNext(): boolean { return this.currentPage < this.totalPages && !this.loading; }
get canGoPrev(): boolean { return this.currentPage > 1 && !this.loading; }

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
  this.currentPage = 1;
  this.reload();
}
```

**Also add `this.currentPage = 1;` right before `this.reload()`** inside `setLocation()`, `setTab()`, `onFilterChange()`, `onSearch()`, and `clearFilters()` — otherwise changing a filter while on page 3 could request a page that no longer exists for the new result set.

## Checklist if it still doesn't appear after pasting this in

1. **Is `total` actually being returned by SharePoint?** Open dev tools → Network → find the list request → Payload tab → confirm `$inlinecount=allpages` is present in the query string, and check the Response tab for `d.__count`. If that's missing, `total` stays `0`, `totalPages` becomes `1`, and Next/Previous will look permanently disabled — that's a `sharepoint.service.ts` wiring issue (confirm `getPageByUrl` is reading `res?.d?.__count` from the earlier fix), not a template issue.
2. **Is `FormsModule` imported** in `dashboard.component.ts`'s standalone `imports` array? `[ngModel]`/`(ngModelChange)` on the `<select>` won't bind without it — you already have it listed from earlier screenshots, just confirming since a missing import silently breaks two-way binding without a console error in some Angular versions.
3. **Hard refresh / clear cache** — if you're testing in the same tab you've been debugging in for a while, an old compiled bundle can linger.

Paste back what you see in Network → Response for `__count` if the count/pages still don't show up correctly, and I'll narrow it from there.