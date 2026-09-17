# Architecture

## Overview

The backend is split into four layers, while the Angular app is organized by feature. Data crossing a layer boundary is represented by DTOs; domain entities are not exposed outside the Application layer.

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

1. An Angular service calls `HttpClient`. `authInterceptor` adds `Authorization: Bearer` only for API-origin URLs and converts API error envelopes to `ApiClientError`.
2. The controller handles route/model binding, passes the request to the service, and returns the DTO directly with `200`, `201`, or `204`.
3. The service checks the current user through `ICurrentUser`, runs `DtoValidator`, coordinates the Repository/Unit of Work, adds the relevant `DecisionEvent` rows, and commits once with `UnitOfWork.SaveChangesAsync`.
4. If an exception reaches the middleware, it is mapped to the corresponding HTTP status: `ValidationException → 400`, `NotFoundException → 404`, `ConflictException → 409`, `ForbiddenException → 403`, and `UnauthorizedException → 401`.

## Backend patterns

- **Repository + Unit of Work.** `Repository<T>` wraps `DbSet<T>` and provides tracked and `AsNoTracking` query helpers. `UnitOfWork` exposes the typed repositories and one `SaveChangesAsync`. Services depend on `IUnitOfWork`, with one transaction for each use case.
- **Business rules stay out of controllers.** The decision state machine (`Decision.AllowedTransitions`), finalize/review checks, metric calculations, and the admin self-demotion guard are implemented in services or domain types.
- **Domain purity.** `DecisionVault.Domain` has no package references. The metric functions are static and can be tested without infrastructure code.
- **Exceptions at the API boundary.** Typed exceptions carry the information needed for an HTTP response, and the middleware is the only place that maps them to status codes.
- **Audit events.** Meaningful changes such as `Created`, `OptionAdded`, `ReasonAdded`, `OptionSelected`, `StatusChanged`, `Finalized`, and `Reviewed` add an immutable `DecisionEvent` row.

## Frontend patterns

- **Signals-first state.** `AuthService` stores `token`, `user`, and `expiry` as writable signals. `isAuthenticated` and `isAdmin` are computed from that state, and the session is restored from localStorage when the service is created.
- **Functional guards + interceptor.** `authGuard` and `adminGuard` return `UrlTree`s. The interceptor clears an expired session after a `401` and redirects to the login page with `?expired=1`.
- **Standalone components.** Each page is a standalone component. Modal flows are handled in the component, and the charts (`DonutChart`, `BarChart`, `CompareChart`) are small SVG components that use API data.
- **Typed DTO mirrors.** `core/models.ts` mirrors the DTOs in `docs/api-contract.md`, with enums represented as string unions.

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
