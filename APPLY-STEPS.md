# SRMS UI restore — apply order

Do these in order. Only step 6 needs a terminal.

---

## Why the new build looks worse than the old one

Three separate causes, only one of which is "CSS is missing":

1. **`densite: -2`** in `theme.scss` — `mat.theme` validates its keys, so an
   unknown key fails the whole Sass file and the global stylesheet never
   compiles. This is the "CSS is totally gone" symptom.
2. **`.mat-mdc-form-field { width: 100% }` was removed.** An MDC form field is
   `display:inline-flex` with a 180px intrinsic infix, so without that rule it
   refuses to fill its `col-sm-3` — that is the crammed, overlapping look in
   the localhost screenshots.
3. **`placeholder` is no longer a floating label.** Material 7 floated it;
   Material 15+ does not. That grey caption above each value in the old build
   is the single biggest reason it reads as "clean". It needs `<mat-label>`,
   which is a template change — CSS cannot do it. Step 5.

---

## Step 1 — `src/theme.scss`

Replace the whole file with the supplied `theme.scss`.
Fixes the `densite` typo, keeps density -2, forces light colour scheme.

## Step 2 — `src/bnp-design-system.css`

New file. Drop it in `src/` alongside `styles.css`.
This is the "one common CSS for everything" — dialogs, fields, selects,
checkboxes, buttons, tabs, ag-grid, loader.

## Step 3 — `src/styles.css`

Reduce to imports only. Bootstrap was being loaded a third time here; the
`angular.json` entry in step 4 covers it.

```css
/* Global styles. Bootstrap + icons are loaded from angular.json. */
@import "../node_modules/@once/ng-material/public/css/once-ng-material.css";
@import "../node_modules/ag-grid-community/styles/ag-grid.css";
@import "../node_modules/ag-grid-community/styles/ag-theme-balham.css";
```

## Step 4 — `angular.json`

Replace the `styles` array under `architect > build > options`.
**Order matters**: Bootstrap first so we can override it, our design system
last so it wins over `@once/ng-material`.

```json
"styles": [
  "node_modules/bootstrap/dist/css/bootstrap.min.css",
  "node_modules/bootstrap-icons/font/bootstrap-icons.css",
  "src/theme.scss",
  "src/styles.css",
  "src/bnp-design-system.css"
],
```

Note `indigo-pink.css` is gone. It was a legacy M2 theme fighting the M3
`mat.theme` output — you cannot run both.

## Step 5 — floating labels

```
.\add-mat-labels.ps1
```

Previews only. Read the list, then:

```
.\add-mat-labels.ps1 -Apply
```

It skips any file that already has a `<mat-label>`, so it is safe to re-run.

## Step 6 — restart

`angular.json` changes do not hot-reload.

```
ng serve
```

---

## Step 7 — template fixes (after you have seen step 6 render)

### 7a. Dialog containers

Every dialog-hosted component uses `class="container"`, which has a fixed
max-width per breakpoint and squeezes the form inside the 900px dialog.
Change to `container-fluid` in:

- `add-application.component.html`
- `folder-management.component.html`
- `fts-mapping.component.html`
- `recordtype-management.component.html`
- `metadata-management.component.html`

### 7b. Duplicate `class` on every ag-grid

All five grids have two separate `class` attributes. HTML keeps the first and
silently drops the rest, so `ag-theme-balham` is never applied:

```html
<!-- currently -->
class="separator"
style="width: 100%; height: 33vw;"
class="ag-theme-balham"

<!-- should be -->
class="separator ag-theme-balham"
style="width: 100%; height: 33vw;"
```

Files: `view-applications`, `view-folder-mapping`, `view-fts-mapping`,
`view-metadata`, `view-record-types` (`.component.html`).

### 7c. Dialog widths

`--mat-dialog-container-max-width` is raised to `94vw` in the design system, so
the width you pass now actually applies. Metadata has ~30 fields and needs
more than the default:

- `view-metadata.component.ts` -> both `_dialog.open(MetadataManagementComponent, ...)`
  calls: add `width: '1200px'`
- `view-record-types.component.ts`, `view-folder-mapping.component.ts`,
  `view-fts-mapping.component.ts`: add `width: '900px'`

---

## Design notes

**Colour.** Everything comes from the CEFS UI palette, brand green only:
`#00915A` primary, `#025C39` dark, `#E8F4EF` tint, `#22BF84` light.
Button gradient is the documented `bg-gradient-brand` token
(`#00915A -> #025C39`) rather than an invented mid-tone. Neutrals are CEFS too,
which is convenient: `#B4BABF`, `#5C6166` and `#17181A` are exactly the values
`once-ng-material.css` used, so field underlines and labels match the old build
to the pixel. Info blue, purple and the other accent ramps are deliberately
unused.

**Depth.** Every shadow is two layers (a tight contact shadow plus a wide
ambient one) — single-layer shadows read as flat grey smudge. Raised buttons
also carry `inset 0 1px 0 rgba(255,255,255,.22)`, a one-pixel top highlight,
which is what actually sells a moulded edge. The dialog backdrop is blurred so
the panel separates from the grid behind it.

**Restraint.** One accent, used once per surface: a 4px brand rule across the
top of each dialog, a 3px brand ink bar under the active tab, a 3px brand edge
on the row being edited. Everything else is neutral. On a compliance tool the
data is the subject; the chrome should stay quiet.

**The inline-style problem.** Around fifteen buttons carry
`style="background-color: rgb(0 156 152)"` — the old @once teal. Inline styles
beat stylesheets, so instead of `!important` the design system paints a
`background-image` gradient, which layers over `background-color` and wins
cleanly. Deleting those inline styles is still worth doing later; nothing
breaks if you do.

**Known compromise.** `.mat-mdc-*` and `.mdc-*` are Angular internals, not
public API, and can change between major versions. Where a supported token
existed (`--mat-sys-*`, `--mdc-checkbox-*`, `--mat-tab-header-*`, `--ag-*`)
this stylesheet uses it. The form-field internals had no token equivalent, so
those rules are the maintenance debt you are knowingly taking on. They are
grouped in section 4 so an upgrade has one place to look.
