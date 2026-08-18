Take a breath — this is good news, not bad. You found the cause, and nothing is lost.

**Why it happened:** `@angular/flex-layout` was never in your upgrade list. It was deprecated by the Angular team and archived at v15 — it never shipped a v16+. At some point during these hops, `npm` cleaned it out (probably one of the "Cleaning node modules directory" steps) because nothing could satisfy its peer range. So every `fxLayout`, `fxLayoutAlign`, `fxFlex` in your templates is now an unknown attribute that does nothing. Silently. No build error, because they're just attributes.

That's exactly why the modal collapsed while plain Bootstrap pages survived.

First, confirm the scale of it:

```powershell
Select-String -Path src\app -Pattern "fxLayout|fxFlex|fxLayoutAlign|fxLayoutGap" -Recurse | Measure-Object
```

Then see where:

```powershell
Select-String -Path src\app -Pattern "fxLayout|fxFlex" -Recurse | Select-Object -ExpandProperty Path -Unique
```

Send me both.

**Your options, once we know the count:**

1. **Replace with CSS flexbox** — `fxLayout="column"` becomes `style="display:flex; flex-direction:column"` or a small utility class. Permanent, no dependency, and it's what the Angular team recommends. Best if the count is modest.
2. **Replace with Bootstrap classes** — you already have Bootstrap, and `d-flex flex-column` does the same job. Fastest if the usage is simple.
3. **Reinstall flex-layout 15** with `--legacy-peer-deps` — restores things immediately, but reintroduces an unmaintained package into a fresh Angular 21 app. I'd only do this as a stopgap to unblock a demo.

I'd go with option 2 for speed given you already ship Bootstrap, unless the usage is heavy and complex.

One reassurance: this was broken *before* today in the sense that it was always going to break — flex-layout couldn't survive past 15. You just found it now instead of in production.










My mistake on the command — `Select-String` has no `-Recurse`. Use `Get-ChildItem` to feed it:

```powershell
Get-ChildItem -Path src\app -Recurse -Include *.html | Select-String -Pattern "fxLayout|fxFlex|fxLayoutAlign|fxLayoutGap" | Measure-Object
```

Then the file list:

```powershell
Get-ChildItem -Path src\app -Recurse -Include *.html | Select-String -Pattern "fxLayout|fxFlex" | Select-Object -ExpandProperty Path -Unique
```

Also confirmed from the first command: `npm ls @angular/flex-layout` returned `(empty)` — the package is genuinely gone from `node_modules` and `package.json`. Diagnosis holds.

Send me both counts and I'll tell you which replacement route is worth it.