# Development Notes

Conventions, tradeoffs and quirks learned while building DecisionVault. Read this
before changing shared code.

## Conventions

- **Contract first.** `docs/api-contract.md` is the single source of truth. Change the
  contract, then backend DTOs, then `core/models.ts` — in that order. Both sides
  serialize enums as strings.
- **Error envelope everywhere.** Services throw typed exceptions
  (`ValidationException`, `NotFoundException`, `ConflictException`, `ForbiddenException`,
  `UnauthorizedException`); only middleware maps them to HTTP. Controllers stay thin.
- **One `SaveChangesAsync` per use case** through `IUnitOfWork`.
- **Frontend naming**: signals (`token()`, `user()`, `isAdmin`), functional
  guards/interceptor, standalone components, DTO interfaces mirroring the contract.
- Git identity for this project: `DecisionVault Dev <student@example.com>`.

## Tooling gotchas (this workspace)

- `dotnet` is user-local. Before any dotnet command:
  ```bash
  export PATH="$HOME/.dotnet/tools:$HOME/.dotnet:$PATH"
  export DOTNET_ROOT="$HOME/.dotnet"
  ```
- PostgreSQL runs in Docker on **host port 5433** (container 5432):
  `docker exec decisionvault-pg psql -U postgres -d decisionvault -c '...'`.
  Always schema-qualify with `decision_vault`.
- Long-running processes (API, ng serve) are supervised via the session hub
  (`decisionvault-api`, `decisionvault-web`), not backgrounded shells.

## Testing notes

- Backend tests use **EF Core InMemory** with the real `DecisionVaultDbContext`,
  `Repository<T>` and `UnitOfWork` (47 tests: auth, decisions CRUD, lifecycle transitions
  incl. the Decided→Evaluating re-open, finalize/review scoring, duplicate-option conflicts,
  cross-user authorization, admin service, dashboard). Mocking `IQueryable` with Moq alone
  breaks because services call `FirstOrDefaultAsync`/`ToListAsync` (needs
  `IAsyncQueryProvider`). The InMemory provider is safe here because `OnModelCreating`
  contains no Npgsql-specific configuration (column naming, CHECK constraints are
  metadata-only — verify constraint behavior against PostgreSQL, see `security.md`).
- `ServiceFixture` seeds one category + two users and shares them via
  `IClassFixture`; each test class gets a fresh database (unique name per fixture).
- Assert exception **types**, and messages only where the message is an API contract
  (e.g. `"Invalid email or password."`, `"already exists on this decision."`).
- Frontend specs (15) run with `--browsers=ChromeHeadless` (Chrome available at
  `~/.local/bin/google-chrome`). End-to-end HTTP journey lives in `/tmp/e2e.sh` during
  dev; API-level negative scenarios (dup option 409, skip transition 409, double review 409)
  are also asserted there.
- **Accessibility pass**: global `:focus-visible { outline: 2px solid var(--accent) }`
  ring in `styles.scss` (UA default focus is near-invisible on the navy theme);
  verified by keyboard-tabbing through the sidebar in a real browser.

- Deleting an option referenced as `SelectedOptionId` is handled two ways: the FK is
  `ON DELETE SET NULL`, and the service explicitly nulls it on re-open — belt and
  suspenders for the InMemory provider.

## Known quirks

- **`DecisionOptionRequest.Weight` is non-nullable `int`** on the backend. The
  frontend must send `weight: v.weight ?? 0` — an explicit `null` yields a 400 during
  model binding.
- The reason modal's submit button is labeled **"Add"** (not "Save"), and its
  `category` field is required.
- Dashboard/analytics KPI labels concatenate label+number without a space in
  `textContent` ("1Compared decisions") — cosmetic only.
- `AuthService.applySession()` is public purely so tests can seed a session without
  an HTTP round trip.
- Analytics KPI text renders without an inner space (label+number concatenation);
  a deliberate cosmetic tradeoff.
- Dashboard month-axis labels include a two-digit year (`Mar '26`) via `Point.year` —
  keeps Jan/Apr across year boundaries unambiguous; tooltips already showed full dates.
- Responsive pass at 1920/768/390 px: `overflow-x: auto` on the admin `.card` keeps
  the users table inside the viewport on phones (rows scroll, page doesn't).

## Tradeoffs / future work

- Token refresh + server-side revocation list.
- Pagination on decision list (`OFFSET/FETCH` fits the existing index).
- E2E browser tests (currently manual, verified via browser automation runs).
- Chart library if requirements grow beyond donut/bar/compare.
