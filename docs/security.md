# Security

## Password storage

- **BCrypt** via `BCrypt.Net-Next` — `BCrypt.EnhancedHashPassword(password, workFactor: 11)`
  (SHA-512 variant) on register/change; `EnhancedVerify` on login. Work factor 11
  (~100ms/hash on the dev box) balances brute-force resistance and login latency.
- Plaintext passwords never persist; hashes are compared constant-time inside BCrypt.
- Password policy enforced server-side (`DtoValidator.ValidatePassword`): 8–72 chars,
  at least one uppercase, lowercase and digit. The same policy runs client-side for UX.

## Authentication

- **JWT Bearer** (HS256). Claims: `sub` (user id), `email`, `role`.
- Signature, issuer (`DecisionVault.API`), audience (`DecisionVault.Client`) and
  lifetime are all validated; clock skew 30s.
- Secret is symmetric and configured per environment (`Jwt:Secret`, min 32 chars).
  The committed `appsettings.json` ships an **empty** secret; development uses a
  dev-only key. Production deployments must inject the secret (env var
  `Jwt__Secret` or a mounted secret) — never commit real secrets.
- Expiry: 120 minutes (480 in development). No refresh-token flow — re-login on
  expiry. Token revocation is expiry-based; a leaked token is valid until it expires.
  Acceptable for the internship scope; production would add refresh tokens with a
  deny-list.

## Authorization

- Role claim with two roles: `User`, `Admin`.
- `Admin`-only endpoints are protected with `[Authorize(Roles = "Admin")]`
  (users list, activation, role management, stats, category write/delete).
- Ownership enforced in services, not just attributes: every decision operation loads
  via `LoadOwnedDecisionAsync`, which throws `404` for a missing decision and `403`
  when a non-admin touches another user's decision. Admins may read (moderation)
  but the UI scopes all lists to the acting user except the admin area.
- **Self-demotion guard**: an admin cannot demote their own account (403) — prevents
  locking all admins out.
- Deactivated users (`IsActive = false`) fail login with 403; existing tokens keep
  working until expiry by design.

## Transport & session

- Tokens live in `localStorage` (`dv_token`, `dv_user`, `dv_expiry`); XSS-safe as long
  as Angular's built-in sanitizer is not bypassed (`bypassSecurityTrust*` is not used).
- The interceptor sends the Bearer token **only** to API-origin URLs.
- A `401` mid-session clears storage and redirects to `/login?expired=1`.
- CORS restricted to `http(s)://localhost:4200`, any header/method, no credentials.
- All inputs validated server-side (`DtoValidator`) — length/range checks plus email
  format; EF parameters prevent SQL injection (no raw SQL anywhere).

## Data integrity (defense in depth)

- Unique indexes back identity and domain rules: `users.Email` unique,
  `(decision_id, name)` unique per decision (duplicate option names are rejected in
  the service with a friendly 409; the index catches the race).
- **CHECK constraints** (migration `AddCheckConstraints`) mirror `DtoValidator`
  ranges at the DB level: `confidence_score` 1–100 and `ExpectedSuccessScore` 1–100
  on `decisions`; `Score`/`Weight` 0–10 on `decision_options`. Verified live on
  PostgreSQL — violating writes fail with SQLSTATE 23514.
- `GlobalExceptionMiddleware` maps unique-index races (`PostgresException` 23505) to
  `409 "A record with these details already exists."` instead of a 500.


## Error discipline

- Auth failures are indistinguishable on purpose: bad email and wrong password both
  return `401 "Invalid email or password."` (no account enumeration).
- Registration reports email conflicts as `409` — email is treated as public info.

## Verification

```bash
# Admin-only endpoint as plain user → 403
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/admin/users \
  -H "Authorization: Bearer <user-token>"

# No token → 401
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/decisions
```
