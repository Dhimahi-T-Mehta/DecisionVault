# Performance

## Query strategy

- **No-tracking reads.** `Repository.QueryWhere(asNoTracking: true)` (default for
  list/dashboard/analytics paths) skips change-tracker overhead; tracking is enabled
  only for mutations.
- **Composite indexes** (EF migration `InitialCreate`):
  - `decisions (UserId, Status, CreatedAt)` — the decisions list filter + sort
  - `decisions (UserId, CategoryId)` and `(UserId, ReviewDate)` — dashboard/analytics groupings
  - `decision_options (DecisionId, Name)`, `decision_events (DecisionId, CreatedAt)` —
    detail loads in event order
  - unique index `users.Name` (email uniqueness) and FK indexes `CategoryId`,
    `SelectedOptionId`
- **Detail loads in one round trip** — `Include(Category/Options/Reasons/Review/SelectedOption)`
  instead of lazy-loading N+1s.
- **Server-side aggregation.** Dashboard/analytics `GroupBy` executes in PostgreSQL
  (rows grouped before materialization); only aggregates cross the wire, not raw rows.
- **Search** is a case-insensitive title `Contains` translated to SQL `ILIKE`,
  combined with the status filter in the same query.
- **Single commit per use case** via `UnitOfWork.SaveChangesAsync` — one transaction,
  decision + options + reasons + events atomically.
- **Server-side pagination** on the decisions list: `page`/`pageSize` translate to
  `Skip/Take` with a `totalCount`, executed against the composite index
  `(UserId, Status, CreatedAt)`; verified page=1&pageSize=1 returns one item + total.

## Frontend

- Angular 20 standalone bundle, no chart/serialization libraries; charts are small SVG
  components computed from already-fetched DTOs.
- `OnPush`-friendly signal-based change detection (`provideZoneChangeDetection` with
  event coalescing) minimizes re-renders.
- Production build (`npx ng build`) outputs hashed, minified bundles; the dev server
  hot-reloads during development.

## Measured (dev box)

| Operation | Latency |
|---|---|
| Login (BCrypt verify wf=11) | ~100 ms |
| Decisions list (5 rows, filtered) | < 30 ms |
| Dashboard summary (aggregates) | < 30 ms |
| Full backend test suite (47 tests, InMemory) | < 1 s |
| Angular production build | ~4.3 s |

## Scope boundaries (documented, not implemented)

- No output caching; aggregates recompute per request.
- No CDN/asset pipeline beyond the Angular build output.
