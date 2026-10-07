Here is everything, in order. You touch 3 files.

## 1. `base64.validator.ts` (replace the whole file)

```ts
import { AbstractControl, ValidationErrors, ValidatorFn } from "@angular/forms";

// Allowed decoded values: NA, or a DN like cn=xxx,ou=group,ou=SRMS,ou=applications,dc=root
export const DN_PATTERN = /^(NA|cn=[A-Za-z0-9_-]+,ou=group,ou=SRMS,ou=applications,dc=root)$/i;

// true only if the value is valid base64 AND decodes to an allowed value
export function isEncodedDn(value: string): boolean {
  if (!value) return false;
  if (!/^[A-Za-z0-9+/]*={0,2}$/.test(value)) return false;
  if (value.length % 4 !== 0) return false;
  try {
    return DN_PATTERN.test(atob(value));
  } catch {
    return false;
  }
}

export function base64Validator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value === null || value === undefined || value === '')
      return null;
    if (typeof value !== 'string')
      return { base64: 'Value is not a string' };
    return isEncodedDn(value) ? null : { base64: "Invalid base64 string" };
  };
}
```

## 2. `metadata-management.component.ts` (4 edits)

**a) Line 7, the import:**
```ts
import { base64Validator, isEncodedDn } from 'src/app/utilities/CustomValidator/base64.validator';
```

**b) In `onSubmitClick`, replace the `this._base64Fields.forEach(...)` block (lines ~185-192) with:**
```ts
this._base64Fields.forEach(field => {
  const control = this.metadataManagementForm.get(field);
  const val = control?.value;
  // encode only if it is still plain text; never touch an already-encoded value
  if (val && !isEncodedDn(val)) {
    try {
      control?.setValue(btoa(val));
    } catch (e) {
      console.error(`Encoding failed for ${field}`, e);
    }
  }
  this.base64Mode[field] = 'encoded';
});
```

**c) Replace the whole `isValidBase64` method with:**
```ts
isValidBase64(value: string): boolean {
  return isEncodedDn(value);
}
```

**d) Replace the whole `toggleBase64` method with:**
```ts
toggleBase64(field: string) {
  const control = this.metadataManagementForm.get(field);
  const current = control?.value ?? '';
  if (!current) return;

  try {
    if (this.isValidBase64(current)) {
      control?.setValue(atob(current));   // encoded -> plain text
      this.base64Mode[field] = 'decoded';
    } else {
      control?.setValue(btoa(current));   // plain text -> encoded
      this.base64Mode[field] = 'encoded';
    }
  } catch (e) {
    console.error(`toggleBase64 failed for ${field}:`, e);
  }
}
```

The HTML needs no change, since it already calls `isValidBase64`.

## 3. `src/web.config` (replace the whole file)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension=".woff" />
      <remove fileExtension=".woff2" />
      <mimeMap fileExtension=".woff" mimeType="font/woff" />
      <mimeMap fileExtension=".woff2" mimeType="font/woff2" />
    </staticContent>
    <rewrite>
      <rules>
        <rule name="Angular Routes" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/SRMSOnboardingAutomation/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

## After deploying
1. Hard refresh (Ctrl+F5).
2. Open the `.../media/bootstrap-icons.woff2` URL. It should download a file, not show your app.
3. If it still shows your app, check that `media/bootstrap-icons.woff2` exists in the deployed folder (the pipeline may not be copying it).

## Behavior now
- Plain text (`cn=...` or `NA`) is encoded once on submit.
- Already-encoded values are never re-encoded.
- Short words like `abcd` count as plain text, because they don't decode to `NA` or a valid DN.
- If your allowed value format ever changes, edit `DN_PATTERN` in one place.

**Not changed, but worth checking:** the validator is on `ManualUpload` while the HTML field is `Members`, so `Members` has no base64 validation. If that's unintended, change `Members: new FormControl('')` in the form group to `Members: new FormControl('', base64Validator())`.