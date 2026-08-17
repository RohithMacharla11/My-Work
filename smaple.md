# 1. The gating question: does ONCE support modern Angular?
npm view @once/ng-material versions --json
npm view @once/bootstrap-core versions --json
npm view @once/ng-material@latest peerDependencies

# 2. Your environment
node -v
npm -v
npm config get registry

# 3. What's actually installed vs declared (the Material 12 / core 14 gap)
npm ls @angular/core @angular/material @angular/cdk ag-grid-angular ag-grid-community

# 4. Does the current build even pass right now?
npx ng build 2>&1 | tail -40

# 5. Are the specs real or scaffolds?
wc -l src/app/**/*.spec.ts
cat src/app/services/api.service.spec.ts