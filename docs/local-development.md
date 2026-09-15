# Local Development — Windows Setup Guide

Complete setup for a laptop that has nothing installed except Windows and internet
access. Every command below was verified against the actual repository (a pristine
clone was booted end-to-end against a brand-new PostgreSQL 17 instance).

---

## 1. System requirements

| | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 64-bit | Windows 11 |
| RAM | 8 GB | 16 GB |
| Disk | 10 GB free (SDK + npm + DB) | 15 GB |
| Internet | required for installs | — |

---

## 2. Required software (exact versions)

| Tool | Version | Why |
|---|---|---|
| **Git for Windows** | latest | clone/repo operations |
| **.NET SDK** | **9.0** (any 9.0.x; developed on 9.0.318) | backend targets `net9.0` |
| **Node.js** | **22 LTS** (≥ 22.12; developed on v22.23) | Angular 20 requires Node ≥ 20.19 or ≥ 22.12 |
| **npm** | 10.x (bundled with Node 22) | package install |
| **PostgreSQL** | **17** (developed on 17.11) | database; container image `postgres:17.11` |
| **pgAdmin 4** | optional | GUI for PostgreSQL (bundled with the Windows installer) |
| **VS Code** or **Visual Studio 2022** (≥ 17.12) | optional | editing/IDE |

Angular CLI does **not** need a global install — the project pins it in
`devDependencies` and runs via `npm start` / `npx ng`.

---

## 3. Install and verify

1. Install each tool from the links above (defaults are fine; remember the PostgreSQL
   `postgres` password you choose).
2. Open **PowerShell** and verify:

```powershell
git --version        # git version 2.x
dotnet --version     # 9.0.xxx
node --version       # v22.x.x
npm --version        # 10.x.x
psql --version       # psql (PostgreSQL) 17.x   (may need PATH refresh/re-login)
```

If `psql` is not found, add `C:\Program Files\PostgreSQL\17\bin` to `PATH`
(or use pgAdmin / the Docker option below).

---

## 4. Get the code

```powershell
git clone <repository-url>
cd DecisionVault
```

---

## 5. PostgreSQL setup

### Option A — Docker (recommended; mirrors the dev setup)

```powershell
docker run -d --name decisionvault-pg `
  -e POSTGRES_PASSWORD=decisionvault_dev `
  -e POSTGRES_DB=decisionvault `
  -p 5433:5432 postgres:17.11
```

Connection params: host `localhost`, port **5433**, user `postgres`,
password `decisionvault_dev`, database `decisionvault`.

### Option B — Local PostgreSQL 17 install

1. Install PostgreSQL 17 (Windows installer), remember your password.
2. Ensure the service runs: `Get-Service postgresql*` → `Running`.
3. Create the database (pgAdmin or psql):

```sql
CREATE DATABASE decisionvault;
```

4. Note your port (default **5432**) — you'll adjust the connection string below.

### Verify connectivity

```powershell
# Option A
docker exec decisionvault-pg pg_isready -U postgres
# Option B
psql -U postgres -c "SELECT version();"
```

---

## 6. Environment configuration

Development settings ship ready-to-run in
`backend/DecisionVault.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DecisionVault": "Host=localhost;Port=5433;Database=decisionvault;Username=postgres;Password=decisionvault_dev"
  },
  "Jwt": {
    "Issuer": "DecisionVault.API",
    "Audience": "DecisionVault.Client",
    "Secret": "dev-only-secret-key-min-32-characters-long!!",
    "ExpiryMinutes": 480
  },
  "SeedDemoData": true
}
```

Adjust for your setup:

- **Option B (local install)**: change `Port=5433` → `5432` and
  `Password=decisionvault_dev` → your postgres password.
- The `Jwt:Secret` must be ≥ 32 characters or the API refuses to start.

For non-development environments, use `.env.example` as the checklist and set values
via `appsettings.{Environment}.json` or environment variables
(`ConnectionStrings__DecisionVault`, `Jwt__Secret`, `Jwt__ExpiryMinutes`,
`SeedDemoData`). **Never commit real secrets**; the committed production
`appsettings.json` intentionally has an empty secret and `CHANGE_ME` password.

Frontend API URL (only change if you move the backend off port 5000):
`frontend/decision-vault/src/environments/environment.ts` → `apiBaseUrl`.

---

## 7. Backend — restore, migrate, run

EF Core migrations (`InitialCreate`, `AddCheckConstraints`) live in
`backend/DecisionVault.Infrastructure/Data/Migrations` and are **applied automatically
at startup** (the seeder runs `MigrateAsync` before seeding).

```powershell
cd backend\DecisionVault.API
dotnet restore
dotnet build
dotnet run --urls http://localhost:5000
```

Expected first-run log lines:

```
info: ... Seeded default categories
info: ... Seeded Admin account admin@example.com
info: ... Seeded User account demo@example.com
Now listening on: http://localhost:5000
```

Explicit migration (only if you want to run it yourself):

```powershell
dotnet tool install --global dotnet-ef
cd backend\DecisionVault.API
dotnet ef database update --project ..\DecisionVault.Infrastructure
```

Verify: open **http://localhost:5000/swagger** — the Swagger UI loads and
`GET /api/categories` (Authorize with a token) works.

---

## 8. Frontend — install, run

New terminal:

```powershell
cd frontend\decision-vault
npm install
npm start
```

- App: **http://localhost:4200**
- The dev server binds port 4200 by default (configured in `angular.json`/`ng serve`).

---

## 9. Run both + first login

Keep both terminals open (backend 5000, frontend 4200), then open
**http://localhost:4200** in a browser and sign in:

| Role | Email | Password |
|---|---|---|
| Demo user | `demo@example.com` | `Demo#12345` |
| Admin | `admin@example.com` | `Admin#12345` |

**Development/demo credentials only** — created by the seeder when `SeedDemoData=true`.
Production setups create users via registration.

Smoke the journey: create a decision → add two options → add a reason → select an
option (status `Decided`) → transition `In Progress` → `Ready for Review` → review it
→ check **Dashboard** and **Analytics** update from real data.

---

## 10. Tests

```powershell
# Backend — expect: Passed! 47, Failed 0
cd backend
dotnet test

# Frontend — expect: TOTAL: 15 SUCCESS  (needs local Chrome)
cd frontend\decision-vault
npx ng test --watch=false --browsers=ChromeHeadless

# API-level E2E + security smoke (backend must be running)
bash scripts/e2e-journey.sh          # or: Git Bash on Windows
bash scripts/security-smoke.sh
```

---

## 11. Troubleshooting

### PostgreSQL connection failure (`Connection refused` / `28P01`)
- Service running? `Get-Service postgresql*` (Option B) or
  `docker ps` shows `decisionvault-pg` healthy (Option A).
- Port matches the connection string (`5433` for Docker, usually `5432` local)?
- Username/password correct? `28P01` = password authentication failed.
- Database `decisionvault` exists (`CREATE DATABASE decisionvault;`).
- Test raw connectivity first (`pg_isready`), then the connection string.

### Port already in use (`EADDRINUSE` / address in use)
- Backend 5000: `dotnet run --urls http://localhost:5001` — but then update
  `environment.ts` `apiBaseUrl` to match and re-check CORS (`Program.cs` allows
  origins `http://localhost:4200`).
- Frontend 4200: `npm start -- --port 4300`.
- Find the offender (PowerShell): `Get-NetTCPConnection -LocalPort 5000 | Select OwningProcess`
  then `tasklist /fi "PID eq <pid>"`.

### `npm install` fails
```powershell
Remove-Item -Recurse -Force node_modules, package-lock.json
npm cache clean --force
npm install
```
Also confirm Node 22 (`node --version`) — Node 18/20 too old for Angular 20 in this
workspace setup.

### `dotnet restore` / `build` fails
- `dotnet --version` must print **9.0.x** (8.x cannot build `net9.0` targets).
- Corrupt restore: `dotnet clean`, delete `bin`/`obj`, re-run `dotnet build`.

### EF migration failure
- Must run from `backend/DecisionVault.API` (the startup project that registers the
  DbContext and connection string).
- Database must exist and be reachable before `MigrateAsync`/`dotnet ef` runs.
- If using `dotnet-ef` manually, install the tool (`dotnet tool install --global dotnet-ef`)
  and pass `--project ..\DecisionVault.Infrastructure`.
- Startup already migrates automatically — a manual `database update` on a
  fully-migrated DB is a no-op, not an error.

### CORS error in browser console
- Backend CORS policy allows exactly `http://localhost:4200` / `https://localhost:4200`
  (`backend/DecisionVault.API/Program.cs`). Run the frontend on 4200, or extend the
  policy if you intentionally serve the app elsewhere.

### API refuses to start: "Jwt:Secret is missing or shorter than 32 characters"
- Set a ≥ 32-char secret in `appsettings.Development.json` (or
  `Jwt__Secret` environment variable).

---

## 12. Fresh-machine checklist

- [ ] `git --version`, `dotnet --version` (9.x), `node --version` (22.x), `psql --version` (17.x) all succeed
- [ ] PostgreSQL reachable; `decisionvault` database exists
- [ ] Connection string + JWT secret configured for your environment
- [ ] `dotnet run` backend → Swagger at `http://localhost:5000/swagger`
- [ ] `npm install && npm start` frontend → `http://localhost:4200`
- [ ] Demo login works; create → decide → review → dashboard shows data
- [ ] `dotnet test` → 47/47; `ng test` → 15/15
