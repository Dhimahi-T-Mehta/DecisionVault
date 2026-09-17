# Security

## Password storage

- **BCrypt** via `BCrypt.Net-Next`. Registration and password changes use `BCrypt.EnhancedHashPassword(password, workFactor: 11)`, and login uses `EnhancedVerify`. On the development machine, hashing takes about 100 ms.
- Plaintext passwords are never stored. BCrypt performs the password-hash comparison.
- Password rules are checked on the server by `DtoValidator.ValidatePassword`: 8–72 characters, with at least one uppercase letter, one lowercase letter, and one digit. The frontend applies the same rules for immediate validation.

## Authentication

- **JWT Bearer** uses HS256. Tokens contain `sub` (user id), `email`, and `role` claims.
- The API validates the signature, issuer (`DecisionVault.API`), audience (`DecisionVault.Client`), and token lifetime. Clock skew is 30 seconds.
- The JWT secret is symmetric and configured per environment through `Jwt:Secret` and must be at least 32 characters. The committed `appsettings.json` contains an empty secret. Development uses a development-only key, while production must supply the secret through `Jwt__Secret` or another secret store. Real secrets should never be committed.
- Expiry is 120 minutes in the documented API behavior (480 in development). There is no refresh-token flow, so an expired session requires another login. Revocation is also expiry-based; a leaked token remains valid until it expires. This was kept within the internship scope. A production version would need refresh tokens and a deny-list.

## Authorization

- The role claim contains either `User` or `Admin`.
- `Admin`-only endpoints are protected with `[Authorize(Roles = "Admin")]`
  (users list, activation, role management, stats, category write/delete).
- Ownership enforced in services, not just attributes: every decision operation loads
  via `LoadOwnedDecisionAsync`, which throws `404` for a missing decision and `403`
  when a non-admin touches another user's decision. Admins may read (moderation)
  but the UI scopes all lists to the acting user except the admin area.
- **Self-demotion guard**: an admin cannot change their own role to `User`; the API returns `403`.
- Deactivated users (`IsActive = false`) receive `403` when they try to log in. Existing tokens continue to work until they expire.

## Transport & session

- Tokens are stored in `localStorage` (`dv_token`, `dv_user`, `dv_expiry`). The current frontend does not bypass Angular's built-in sanitizer (`bypassSecurityTrust*` is not used).
- The interceptor sends the Bearer token **only** to API-origin URLs.
- A `401` during an active session clears the stored session and redirects to `/login?expired=1`.
- CORS is limited to `http(s)://localhost:4200`; any header and method are allowed, and credentials are not used.
- All inputs are validated on the server through `DtoValidator`, including lengths, ranges, and email format. EF parameterization is used throughout the application path, with no raw SQL in the application code.

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
- Smoke-verified in the release pass: no/invalid/tampered tokens → 401; user hitting
  admin endpoints → 403; cross-user read/modify/delete → 403; invalid transitions,
  duplicate options, and double reviews → 409; error envelope never leaks stack
  traces or SQL details.


## Error handling

- Login failures use the same response for an unknown email and a wrong password: `401 "Invalid email or password."`. This avoids revealing whether an account exists.
- Registration returns `409` when the email is already in use.

## Verification

```bash
# Admin-only endpoint as plain user → 403
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/admin/users \
  -H "Authorization: Bearer <user-token>"

# No token → 401
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/decisions
```
