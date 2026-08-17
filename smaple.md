Your guard bypass is working — the 401 isn't coming from Angular at all. It's the API rejecting the request.

**Why it still fails:** `ssc-ecm-api.dev.echonet` authenticates via an SSO session cookie scoped to `.echonet`. Your browser has that cookie, which is why pasting the URL works. But your app runs on `localhost:8080`, so the XHR is **cross-origin** — the browser won't attach an `.echonet` cookie to a request from a `localhost` origin, and the API almost certainly doesn't allowlist `localhost` in CORS either. `withCredentials: true` can't fix that. Commenting out the guard only skips the client-side route check; it does nothing to the outgoing HTTP call.

The fix is to stop making it cross-origin: proxy through the Angular dev server and inject your real cookie server-side.

---

**1. `proxy.conf.js`** (create at project root — use `.js`, not `.json`, so you can set headers)

```js
const COOKIE = process.env.SRMS_COOKIE || '';

module.exports = {
  "/srms": {
    target: "https://ssc-ecm-api.dev.echonet",
    secure: false,
    changeOrigin: true,
    logLevel: "debug",
    onProxyReq: (proxyReq) => {
      if (COOKIE) proxyReq.setHeader('cookie', COOKIE);
      proxyReq.setHeader('origin', 'https://ssc-ecm.apps.dev.echonet');
      proxyReq.setHeader('referer', 'https://ssc-ecm.apps.dev.echonet/');
    }
  }
};
```

Get the cookie: in your VM browser, log into `https://ssc-ecm.apps.dev.echonet`, F12 → Network → click any API call → Request Headers → copy the **entire** `Cookie:` value. Then:

```
set SRMS_COOKIE=<paste the whole cookie string>
ng serve --proxy-config proxy.conf.js --port 8080
```

It expires — when 401s come back, re-copy it.

**2. `dynamic.env.ts`** — add a localhost case instead of editing `default:` (leave `default` alone so UAT/PROD aren't affected):

```ts
case "http://localhost:8080":
case "http://localhost:4200":
  environment = {
    srms_api_base_url: "/srms/v1/",   // relative → goes through the proxy
    sso_client_name: "SSC-ECM-APPS-AND-SERVICES_SRMS_OIDC_DEV",
    sso_client_id: "000b3e82-703e-1983-a340-4eb30af30000",
    sso_url: "",
    sso_redirect_url: "",
    aps_eldap_group: "SRMS_EMEA_RMO_V"
  }
  break;
```

The relative URL is the whole point — requests now go to `localhost:8080/srms/v1/...`, same-origin, no CORS, and the proxy adds the auth.

**3. `config.service.ts`** — your early `return [...]` has a real bug: it returns an array where callers expect a Promise, so any `.then()` on `loadUserGroups()` will throw. Gate it properly:

```ts
loadUserGroups(): Promise<string[]> {
  if (!environment.sso_url) {            // local mode
    this._currentUserGroups = ['SRMS_EMEA_RMO_V', 'SRMS_RMO_V'];
    return Promise.resolve(this._currentUserGroups);
  }
  return this._httpClient.get(`${environment.srms_api_base_url}onboarding/usergroups`, {
    headers: new HttpHeaders({ "Accept": "application/json" }),
    withCredentials: true
  }).toPromise().then((groups: any) => {
    this._currentUserGroups = groups
      .filter((dn: string) => /(?:^|,)ou=srms(?:,|$)/i.test(dn))
      .map((dn: string) => { const m = dn.match(/^cn=([^,]+)/i); return m ? m[1] : null; })
      .filter(Boolean);
    return this._currentUserGroups;
  });
}

getCurrentUserGroups(): string[] {
  return this._currentUserGroups && this._currentUserGroups.length
    ? this._currentUserGroups
    : ['SRMS_EMEA_RMO_V', 'SRMS_RMO_V'];
}
```

**4. `auth.guard.ts`** — replace the commented block with a flag so it can't ship broken:

```ts
canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean {
  if (!environment.sso_url) return true;        // local: skip SSO
  const ssoToken = this.auth.getSSOToken();
  if (!ssoToken) { this.auth.redirectToSSO(); return false; }
  return true;
}
```

---

One caveat: if your VM can't reach `ssc-ecm-api.dev.echonet` over the network at all (only the browser can, via a proxy/PAC file), the dev-server proxy will fail too. Test first with `curl -v -H "cookie: <your cookie>" https://ssc-ecm-api.dev.echonet/srms/v1/onboarding/applications` from the VM terminal. If that returns data, the proxy will work. If it can't connect, your only option is mocking the API with an `HttpInterceptor` that returns canned `IApplication[]` data — tell me and I'll write that instead.