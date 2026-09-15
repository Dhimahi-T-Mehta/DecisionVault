# API Documentation

Interactive reference: **Swagger UI at `http://localhost:5000/swagger`** (enabled in
Development). The machine-readable contract lives in `docs/api-contract.md`.

## Base URL & CORS

- API: `http://localhost:5000`
- SPA: `http://localhost:4200`
- CORS policy `frontend` allows the SPA origin.

## Authentication flow

1. `POST /api/auth/register` or `POST /api/auth/login` → `AuthResponse { token, expiresAt, user }`.
2. Client stores token (localStorage) and sends `Authorization: Bearer <jwt>` on every
   API call (attached automatically by `authInterceptor`).
3. Tokens are HS256-signed, 120-minute expiry, issuer `DecisionVault.API`,
   audience `DecisionVault.Client`. Claims: `sub` (user id), `email`, `role`.
4. A `401` mid-session triggers client-side session clearing and redirect to
   `/login?expired=1`.

## Response conventions

- **Success**: the DTO directly (`200`/`201`), or `204` for deletes. No wrapping envelope.
- **Error**: uniform envelope from exception middleware:

```json
{
  "success": false,
  "message": "Decision failed validation.",
  "errors": ["Title must be 3-200 characters.", "Confidence must be 1-100."],
  "timestamp": "2026-09-15T08:00:00Z"
}
```

| Exception | HTTP |
|---|---|
| `ValidationException` | 400 (message + per-field `errors[]`) |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| unhandled | 500 (`"An unexpected error occurred."`) |

- Enums serialize as strings everywhere (`JsonStringEnumConverter`).

## Endpoint map

| Area | Endpoints |
|---|---|
| Auth | `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `PUT /auth/profile`, `PUT /auth/change-password` |
| Categories | `GET /categories`, `POST /categories` (admin), `PUT /categories/{id}` (admin), `DELETE /categories/{id}` (admin) |
| Decisions | `GET /decisions` (`?status=&search=`), `POST /decisions`, `GET /decisions/{id}`, `PUT /decisions/{id}`, `DELETE /decisions/{id}`, `POST /decisions/{id}/options`, `DELETE /decisions/{id}/options/{optionId}`, `POST /decisions/{id}/reasons`, `DELETE /decisions/{id}/reasons/{reasonId}`, `POST /decisions/{id}/select`, `POST /decisions/{id}/transition`, `POST /decisions/{id}/finalize`, `POST /decisions/{id}/review`, `POST /decisions/{id}/compare` |
| Dashboard | `GET /dashboard/summary` |
| Analytics | `GET /analytics/summary` |
| Profile | `GET /profile/summary` |
| Admin | `GET /admin/users`, `PUT /admin/users/{id}/active`, `PUT /admin/users/{id}/role`, `GET /admin/stats` |

Full request/response shapes: `docs/api-contract.md`.

## Trying it with curl

```bash
# 1. Login
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"demo@example.com","password":"Demo#12345"}' | jq -r .token)

# 2. List decisions
curl -s http://localhost:5000/api/decisions -H "Authorization: Bearer $TOKEN" | jq

# 3. Error envelope example (bad role)
curl -s -X PUT http://localhost:5000/api/admin/users/2/role \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"role":"SuperAdmin"}' | jq
# → { "success": false, "message": "Role must be User or Admin.", ... }
```

## Validation rules (summary)

- Title 3–200 · confidence/expected success 1–100 · option name 1–100 ·
  option score & weight 0–10 · outcome rating 1–5 · password 8–72 with upper+lower+digit ·
  review date ≥ decision date · finalize requires ≥ 2 options and a selected option.
