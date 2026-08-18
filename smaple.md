**46 occurrences across 6 files.** That's small — very manageable. Files: add-application, dialog, folder-management, fts-mapping, metadata-management, recordtype-management.

Also useful: your `styles.css` is only 5 imports, and `@once/ng-material` is there as suspected — but leave it for now, flex-layout is the real problem.

**My recommendation: option 2, Bootstrap classes.** You already ship Bootstrap 5, the files are few, and it removes the dependency permanently. Rough mapping:

| flex-layout | Bootstrap |
|---|---|
| `fxLayout="row"` | `class="d-flex flex-row"` |
| `fxLayout="column"` | `class="d-flex flex-column"` |
| `fxLayoutGap="10px"` | `class="gap-2"` (or inline `style="gap:10px"`) |
| `fxLayoutAlign="center center"` | `class="justify-content-center align-items-center"` |
| `fxFlex` | `class="flex-fill"` |
| `fxFlex="50"` | `style="flex: 0 0 50%"` |

Note these are *classes*, so merge into the element's existing `class` attribute rather than adding a second one.

**Start with one file** — `add-application.component.html`, since that's the modal you can see broken. Fix it, `ng serve`, confirm the modal looks right. Once the pattern is proven on one file, the other five are mechanical.

To see exactly what you're dealing with there:

```powershell
Get-ChildItem -Path src\app -Recurse -Include *.html | Select-String -Pattern "fxLayout|fxFlex" | Where-Object { $_.Path -like "*add-application*" }
```

Send me that output and I'll give you the exact replacements for those lines.

One thing worth saying: this is now ordinary front-end work, not upgrade work. The Angular 14 → 21 migration is genuinely finished.