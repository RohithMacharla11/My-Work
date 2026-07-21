That's a layout fix, not a logic change — the button just needs to move inside the input's wrapper and become an icon. Only the HTML/CSS around the search box changes; everything in `dashboard.component.ts` from the last message stays exactly as is.

## `dashboard.component.html` — replace the search input block

```html
<div class="search-box">
  <input
    type="text"
    class="search-input"
    placeholder="Search name, email, phone, ID..."
    [value]="pendingSearch"
    (input)="onSearch($any($event.target).value)"
    (keyup.enter)="runSearch()"
  />
  <button class="search-icon-btn" type="button" (click)="runSearch()" title="Search">
    <i class="icon-search"></i>
  </button>
</div>
```

(If you're not using an icon font like `icon-search`, swap that `<i>` for an inline SVG — see below.)

## Add to `dashboard.component.scss`

```scss
.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-input {
  width: 100%;
  padding-right: 36px;   // room for the icon so text doesn't run under it
}

.search-icon-btn {
  position: absolute;
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
  background: transparent;
  border: none;
  padding: 4px;
  display: grid;
  place-items: center;
  cursor: pointer;
  color: var(--muted, #8a8a8a);
}

.search-icon-btn:hover {
  color: var(--ink, #1a1a1a);
}
```

## If you don't have an icon font, use this inline SVG instead of `<i class="icon-search"></i>`

```html
<button class="search-icon-btn" type="button" (click)="runSearch()" title="Search">
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
    <circle cx="11" cy="11" r="7"></circle>
    <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
  </svg>
</button>
```

This puts the search icon flush inside the right edge of the same box the text sits in — `position: absolute` layers it over the input rather than pushing a separate button below/beside it, and the input's `padding-right` keeps typed text from running underneath the icon. No changes to `dashboard.component.ts` needed at all — `runSearch()` and `pendingSearch` from before are unchanged.