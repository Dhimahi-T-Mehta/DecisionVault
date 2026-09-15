# DecisionVault — Decision Intelligence & Outcome Tracking Platform

DecisionVault is a full-stack web application for making decisions deliberately and
learning from them. You record a decision, weigh the options, commit to one with an
explicit confidence level and expected outcome, execute it, and then review what
actually happened. The platform scores how well reality matched expectations and
aggregates lifetime decision quality into dashboard and analytics views.

Built as a TatvaSoft internship project (15 days) demonstrating a production-style
layered architecture on both ends.

## Tech Stack

| Layer      | Technology |
|------------|------------|
| Frontend   | Angular 20 (standalone components, signals), TypeScript, hand-rolled SVG charts (no chart libraries) |
| Backend    | ASP.NET Core 9 Web API, layered (Domain / Application / Infrastructure / API) |
| Data       | PostgreSQL 17, EF Core 9 (Npgsql), code-first migrations |
| Auth       | JWT bearer tokens, BCrypt password hashing, role-based authorization (`Admin` / `User`) |
| Tests      | xUnit + Moq + EF Core InMemory (backend, 42 tests), Karma + Jasmine (frontend, 15 tests) |

## Architecture

```
frontend/decision-vault/            Angular 20 SPA
  src/app/core/                     AuthService (signals), authInterceptor, guards, API DTOs
  src/app/pages/                    login, decisions, dashboard, analytics, compare, admin, profile
  src/app/shared/                   DonutChart / BarChart / CompareChart, form controls

backend/
  DecisionVault.Domain/             Entities, enums, DecisionMetrics, domain exceptions
  DecisionVault.Application/        DTOs, validators, service interfaces + services, event types
  DecisionVault.Infrastructure/     EF Core DbContext, Repository/UnitOfWork, JWT + BCrypt
  DecisionVault.API/                Controllers, middleware, CORS, seeding, Swagger
  DecisionVault.Tests/              xUnit suite (lifecycle, metrics, auth, decisions, admin)
```

Patterns: Repository + Unit of Work (one `SaveChangesAsync` per use case), service
layer owning all business rules, DTO-boundaries, middleware exception → error
envelope mapping, event log for decision audit trails.

## Getting Started

Prerequisites: .NET SDK 9, Node 22, Docker (for PostgreSQL).

1. **Database** — start PostgreSQL:

   ```bash
   docker run -d --name decisionvault-pg -e POSTGRES_PASSWORD=decisionvault_dev \
     -e POSTGRES_DB=decisionvault -p 5433:5432 postgres:17.11
   ```

2. **Configuration** — copy `backend/DecisionVault.API/appsettings.json` values or
   use `appsettings.Development.json` (connection string + JWT secret). See
   `.env.example` for the shape of every setting.

3. **Backend** — migrations apply and seed data run automatically at startup:

   ```bash
   cd backend/DecisionVault.API
   dotnet run
   # → http://localhost:5000  (Swagger at /swagger)
   ```

4. **Frontend**:

   ```bash
   cd frontend/decision-vault
   npm install
   npm start
   # → http://localhost:4200
   ```

5. **Seed accounts** (created when `SeedDemoData` is enabled): demo user
   `demo@example.com / Demo#12345`, admin `admin@example.com / Admin#12345`.

## Testing

```bash
# Backend — 42 tests
cd backend && dotnet test

# Frontend — 15 tests (ChromeHeadless)
cd frontend/decision-vault && npx ng test --watch=false --browsers=ChromeHeadless
```

The backend suite runs services against the real EF Core InMemory provider (real
query pipeline, Repository/UnitOfWork) with fake `IPasswordHasher`/`IJwtTokenService`,
covering: the decision state machine (all allowed/disallowed transitions), metric
math, registration/login/password rules, finalize/review gating, ownership
enforcement, and admin role management.

## The Decision Lifecycle

```
Draft → Evaluating → Decided → InProgress → ReadyForReview → Reviewed
             ↑_______ (back edge allowed)
```

- **Draft/Evaluating**: add options (name, advantages, disadvantages, score, weight)
  and reasons (pro/con/note, categorized). Delete is unrestricted here.
- **Finalize** (Draft/Evaluating only): requires ≥ 2 options; pick one, set confidence
  (1–100), expected success (1–100) and the expected outcome. Status → `Decided`.
- **Execute**: `Decided → InProgress → ReadyForReview`.
- **Review** (only from `ReadyForReview`): actual outcome, rating 1–5, what went
  well/wrong, lessons, `wouldChooseAgain`. Status → `Reviewed` (terminal).

Scoring (`DecisionMetrics`):
- `AlignmentScore = max(0, 100 − |expected% − rating/5·100|)` — how close reality landed to expectation.
- Success = `rating ≥ 3 AND wouldChooseAgain`.
- `DecisionPerformanceScore = round(0.4·avgOutcomeRating + 0.3·successRate + 0.3·avgAlignment, 1)`.

## Documentation

- `docs/api-contract.md` — endpoint-by-endpoint contract (source of truth for both ends)
- `docs/architecture.md` — layers, patterns, request flow
- `docs/database-design.md` — schema, relationships, event log
- `docs/api-documentation.md` — running Swagger, error envelope, auth flow
- `docs/security.md` — hashing, JWT, authorization policy
- `docs/performance.md` — query strategy, indexes, pagination
- `docs/development-notes.md` — conventions, tradeoffs, known quirks

## License

Educational project — TatvaSoft internship, 2026.
