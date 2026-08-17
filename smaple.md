Confirmed — that's Windows Integrated Auth. The giveaway is **`Persistent-Auth: true`**, which IIS only sends on a Negotiate/Kerberos-authenticated connection. That's also why the cookie jar is empty: your Windows identity does the work, no session cookie involved.

Two useful things in those response headers:

- `Access-Control-Allow-Credentials: true` and `Allow-Methods: GET, POST, PUT, PATCH, DELETE, OPTIONS` — CORS is at least partly configured.
- No `Access-Control-Allow-Origin` visible, but that's expected: this was a plain navigation with no `Origin` header, so the server had nothing to echo. Can't tell from here whether it'd allow `localhost`.

So: the browser can authenticate from localhost too, since Kerberos is per-connection and not origin-bound. CORS is the only unknown, and you can just switch it off for a dev-only browser profile.

**1. Close every Edge window** (check Task Manager for stray `msedge.exe`), then run:

```
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --disable-web-security --user-data-dir=C:\temp\devprofile --auth-server-allowlist="*.echonet" --auth-negotiate-delegate-allowlist="*.echonet"
```

**2. Set the localhost case back to the absolute URL** in `dynamic.env.ts`:

```ts
srms_api_base_url: "https://ssc-ecm-api.dev.echonet/srms/v1/",
```

Delete `proxy.conf.js` — not needed.

**3. `ng serve --port 8080`**, open `http://localhost:8080` in that browser, then paste this in its console as a 10-second check before you trust the app:

```js
fetch('https://ssc-ecm-api.dev.echonet/srms/v1/onboarding/applications',
  { credentials: 'include', headers: { 'Accept': 'application/json' } })
  .then(r => r.json()).then(console.log).catch(console.error);
```

If that logs your ~68 applications, you're done — the `withCredentials: true` already in `ApiService` will carry the auth for every call.

One thing to watch: the response came back as `application/xml` (WCF wrapping JSON in a `z:anyType` string) because your navigation sent an HTML `Accept`. Angular's default `Accept` puts JSON first so it should be fine, but if you get parse errors, add `headers: new HttpHeaders({'Accept': 'application/json'})` to the `ApiService` calls alongside `withCredentials`.

Also — while that URL is open, hit **Ctrl+S** and save the JSON. If the flags turn out to be blocked by group policy, that file becomes the mock data and I'll write the interceptor around it.