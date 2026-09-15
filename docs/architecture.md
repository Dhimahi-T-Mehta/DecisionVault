# Architecture

## Overview

DecisionVault uses a four-layer backend and a feature-folder Angular SPA. Every
cross-boundary exchange is DTO-typed; entities never leave the Application layer.

```
┌─────────────────────────────────────────────────────────────┐
│ Angular SPA (4200)                                          │
│  pages/ → core/ (AuthService signals, interceptor, guards)  │
│            shared/ (charts, controls)                       │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTP JSON, Bearer JWT
┌──────────────────────────▼──────────────────────────────────┐
│ DecisionVault.API — controllers, exception middleware,      │
│   CORS, Swagger, DbSeeder (MigrateAsync + optional demo)    │
├─────────────────────────────────────────────────────────────┤
│ DecisionVault.Application — DTOs, DtoValidator, services    │
│   (AuthService, DecisionService, AdminService, Dashboard…), │
│   IUnitOfWork/IRepository contracts, EventTypes             │
├─────────────────────────────────────────────────────────────┤
│ DecisionVault.Infrastructure — DecisionVaultDbContext,      │
│   Repository<T>, UnitOfWork, JwtTokenService,               │
│   BcryptPasswordHasher, HttpContextCurrentUser              │
├─────────────────────────────────────────────────────────────┤
│ DecisionVault.Domain — entities, enums, DecisionMetrics,    │
│   domain exceptions (no dependencies)                       │
└─────────────────────────────────────────────────────────────┘
                           │ EF Core 9 / Npgsql
                    ┌──────▼──────┐
                    │ PostgreSQL  │ schema: decision_vault
                    └─────────────┘
```

## Request flow

1. Angular service calls `HttpClient`; `authInterceptor` attaches `Authorization: Bearer` for API-origin URLs only and maps error envelopes to `ApiClientError`.
2. Controller validates route/model binding, delegates to a service, returns the DTO directly (`200`/`201`/`204`).
3. Service enforces authorization context (`ICurrentUser`), runs `DtoValidator`, orchestrates Repository/UoW, appends `DecisionEvent` audit rows, commits once via `UnitOfWork.SaveChangesAsync`.
4. On exception, middleware converts `ValidationException → 400`, `NotFoundException → 404`, `ConflictException → 409`, `ForbiddenException → 403`, `UnauthorizedException → 401` into the error envelope.

## Backend patterns

- **Repository + Unit of Work.** `Repository<T>` wraps `DbSet<T>` with tracked and
  `AsNoTracking` query helpers; `UnitOfWork` exposes typed repositories and a single
  `SaveChangesAsync`. Services depend only on `IUnitOfWork` — one transaction per use case.
- **Service layer owns rules.** The decision state machine
  (`Decision.AllowedTransitions`), finalize/review gating, metric computation, and
  admin self-demotion guard all live in services or domain types — never in controllers.
- **Domain purity.** `DecisionVault.Domain` has zero package references; metrics are
  pure static functions, trivially unit-testable.
- **Exceptions as control flow at the boundary.** Typed domain exceptions carry HTTP
  semantics; middleware is the single mapping point.
- **Audit events.** Every meaningful mutation (`Created`, `OptionAdded`,
  `ReasonAdded`, `OptionSelected`, `StatusChanged`, `Finalized`, `Reviewed`)
  appends an immutable `DecisionEvent` row.

## Frontend patterns

- **Signals-first state.** `AuthService` holds `token`/`user`/`expiry` as writable
  signals with `isAuthenticated` computed and `isAdmin` computed; localStorage
  persistence with rehydration on construction.
- **Functional guards + interceptor.** `authGuard`/`adminGuard` return `UrlTree`s;
  the interceptor clears stale sessions on `401` and redirects with `?expired=1`.
- **Standalone components** per page; modal flows handled in-component; charts are
  dependency-free SVG components (`DonutChart`, `BarChart`, `CompareChart`) computed
  from real API data.
- **Typed DTO mirrors** of `docs/api-contract.md` in `core/models.ts`; enums as string unions.

## Testing strategy

- Backend: xUnit against EF Core InMemory + real `Repository`/`UnitOfWork` (real LINQ
  pipeline including `Include`s), fake hasher/token services. `DecisionLifecycleTests`
  enumerates every allowed and forbidden transition; service tests assert exception
  types *and* messages where the message is an API contract.
- Frontend: Karma/Jasmine specs for session semantics, interceptor attachment/401
  handling, envelope→message mapping, and route guards.

## Tradeoffs

- InMemory provider (tests) ignores transactions — acceptable because services issue a
  single `SaveChangesAsync` per operation.
- Hand-rolled SVG charts avoid a chart dependency; acceptable for donut/bar/compare needs.
- Token revocation is expiry-based only; no server-side session store (documented in `security.md`).
