# Performance

## Query strategy

- **No-tracking reads.** List, dashboard, and analytics queries normally call `Repository.QueryWhere(asNoTracking: true)`, which avoids change-tracker work. Tracking is used for mutations.
- **Composite indexes** (EF migration `InitialCreate`):
  - `decisions (UserId, Status, CreatedAt)` — the decisions list filter + sort
  - `decisions (UserId, CategoryId)` and `(UserId, ReviewDate)` — dashboard/analytics groupings
  - `decision_options (DecisionId, Name)`, `decision_events (DecisionId, CreatedAt)` —
    detail loads in event order
  - unique index `users.Name` (email uniqueness) and FK indexes `CategoryId`,
    `SelectedOptionId`
- **Detail loads use one round trip.** `Include(Category/Options/Reasons/Review/SelectedOption)` loads the related data together instead of relying on lazy loading and creating N+1 queries.
- **Server-side aggregation.** Dashboard and analytics `GroupBy` operations run in PostgreSQL. Only the aggregated results are returned to the application.
- **Search** checks the title case-insensitively. EF Core translates the `Contains` query to SQL `ILIKE`, and the status filter is applied in the same query.
- **Single commit per use case.** `UnitOfWork.SaveChangesAsync` commits the decision, options, reasons, and events together in one transaction.
- **Server-side pagination.** `page` and `pageSize` become `Skip/Take` operations and are sent with `totalCount`. The query uses `(UserId, Status, CreatedAt)`. The setup was checked with `page=1&pageSize=1`, which returned one item and the total count.

## Frontend

- Angular 20 standalone bundle, no chart/serialization libraries; charts are small SVG
  components computed from already-fetched DTOs.
- `OnPush`-friendly signal-based change detection (`provideZoneChangeDetection` with
  event coalescing) minimizes re-renders.
- Production build (`npx ng build`) outputs hashed, minified bundles; the dev server
  hot-reloads during development.

## Measurements from the development machine

| Operation | Latency |
|---|---|
| Login (BCrypt verify wf=11) | ~100 ms |
| Decisions list (5 rows, filtered) | < 30 ms |
| Dashboard summary (aggregates) | < 30 ms |
| Full backend test suite (47 tests, InMemory) | < 1 s |
| Angular production build | ~4.3 s |

## Scope boundaries

- No output caching; aggregates recompute per request.
- No CDN/asset pipeline beyond the Angular build output.
