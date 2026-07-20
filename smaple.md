Fair — "Load more" (infinite append) and true **page-by-page navigation** are different UX, and the "15 of 0 shown" is a separate bug on top of that. Let me fix both.

## Why "15 of 0 shown"

`total` is initialized to `0` and only gets overwritten inside `next: page => { this.total = page.total ?? ... }`. If the request errored (which it was, from the `NaN` `$top` bug) or `page.total` came back `undefined` and the `??` fallback didn't fire correctly, `total` never updates from its initial `0` — while `rows` still holds whatever was last successfully loaded. That mismatch is exactly "15 of 0". Once the pagination fix below is in, this resolves itself, but I'm adding a safety fallback regardless.

## Redesigning to real Next/Previous pages

SharePoint's REST API only gives you a **forward** skip-token (`__next`) — there's no server-side "previous page" link. So Previous has to be done by **caching the page URLs you've already visited** and re-fetching (or replaying) from that history, not by asking the server to go backward.

**`dashboard.component.ts`** — replace `loadMore()`/`nextUrl` entirely with a page-history stack:

```typescript
pageSize = 25;
total = 0;
currentPage = 1;
private pageUrlHistory: (string | null)[] = [null]; // index 0 = first page (no url needed)
private nextUrl: string | null = null;

private reload(): void {
  this.loading = true;
  this.error = '';
  this.rows = [];
  this.nextUrl = null;
  this.currentPage = 1;
  this.pageUrlHistory = [null];

  const roles = this.currentUser.get().roles;

  if (this.tab === null) {
    const active$ = this.candidates.getActivePage(this.location, this.filters, roles, this.pageSize);
    const rejected$ = this.candidates.getRejectedPage(this.location, this.filters, roles, this.pageSize);
    forkJoin([active$, rejected$]).subscribe({
      next: ([actPage, rejPage]) => {
        this.rows = [...actPage.items, ...rejPage.items];
        this.total = (actPage.total ?? 0) + (rejPage.total ?? 0);
        this.nextUrl = null; // combined view has no "next" concept
        this.loading = false;
      },
      error: err => { this.loading = false; this.error = this.humanError(err); this.total = 0; },
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
      this.total = page.total ?? 0;
      this.loading = false;
    },
    error: err => { this.loading = false; this.error = this.humanError(err); this.total = 0; },
  });
}

get canGoNext(): boolean { return !!this.nextUrl && !this.loading; }
get canGoPrev(): boolean { return this.currentPage > 1 && !this.loading; }

get pageStart(): number { return this.rows.length ? (this.currentPage - 1) * this.pageSize + 1 : 0; }
get pageEnd(): number { return (this.currentPage - 1) * this.pageSize + this.rows.length; }

goNext(): void {
  if (!this.nextUrl) return;
  this.loading = true;
  this.candidates.getNextPage(this.nextUrl, this.location).subscribe({
    next: page => {
      this.pageUrlHistory.push(this.nextUrl); // remember the URL that GOT us to this new page
      this.currentPage++;
      this.rows = page.items;
      this.nextUrl = page.nextUrl;
      if (page.total != null) this.total = page.total;
      this.loading = false;
    },
    error: err => { this.loading = false; this.error = this.humanError(err); },
  });
}

goPrev(): void {
  if (this.currentPage <= 1) return;
  this.loading = true;
  this.currentPage--;
  const targetUrl = this.pageUrlHistory[this.currentPage - 1]; // null = re-run the original first-page query

  const request$ = targetUrl
    ? this.candidates.getNextPage(targetUrl, this.location)
    : (this.tab === 'active'
        ? this.candidates.getActivePage(this.location, this.filters, this.currentUser.get().roles, this.pageSize)
        : this.candidates.getRejectedPage(this.location, this.filters, this.currentUser.get().roles, this.pageSize));

  request$.subscribe({
    next: page => {
      this.rows = page.items;
      this.nextUrl = page.nextUrl;
      if (page.total != null) this.total = page.total;
      this.pageUrlHistory = this.pageUrlHistory.slice(0, this.currentPage); // trim any forward history past here
      this.loading = false;
    },
    error: err => { this.loading = false; this.error = this.humanError(err); },
  });
}

setPageSize(size: number): void {
  if (size === this.pageSize) return;
  this.pageSize = size;
  this.reload(); // always resets to page 1 with the new size — the only sane behavior
}
```

**`dashboard.component.html`** — replace the old footer:

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
    <button class="btn ghost sm" [disabled]="!canGoPrev" (click)="goPrev()">← Previous</button>
    <span class="dim small">Page {{ currentPage }}</span>
    <button class="btn ghost sm" [disabled]="!canGoNext" (click)="goNext()">Next →</button>
  </div>
</div>
```

**Why Previous re-fetches instead of just replaying cached rows in memory:** I could cache each page's actual `items` array too (not just the URL) so Previous is instant with zero network call — that's a nice upgrade if you want it, since a manager going back and forth doesn't need a fresh SharePoint round-trip each time. Say the word and I'll add an in-memory `pageCache: Candidate[][]` alongside the URL history so `goPrev()` just reads from cache instead of calling `getNextPage`/`getActivePage` again — only `goNext()` into brand-new territory would hit the network.

One caveat worth flagging: since filters/search/tab changes all call `reload()` (which resets `pageUrlHistory` to `[null]`), Previous only ever needs to go back within the *current* filter set — that's correct and matches how every paginated table works, just confirming it's intentional and not a gap.