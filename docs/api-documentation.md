# API Documentation

Use **Swagger UI at `http://localhost:5000/swagger`** for the interactive API reference in development. The detailed request and response contract is kept in `docs/api-contract.md`.

## Base URL & CORS

- API: `http://localhost:5000`
- SPA: `http://localhost:4200`
- CORS policy `frontend` allows the SPA origin.

## Authentication flow

1. `POST /api/auth/register` or `POST /api/auth/login` returns `AuthResponse { token, expiresAt, user }`.
2. The client stores the token in localStorage and sends `Authorization: Bearer <jwt>` with API calls. `authInterceptor` adds the header automatically.
3. Tokens use HS256 and contain the `sub` (user id), `email`, and `role` claims. The issuer is `DecisionVault.API`, the audience is `DecisionVault.Client`, and the expiry is 120 minutes.
4. If an API call returns `401` while the user is signed in, the client clears the session and redirects to `/login?expired=1`.

## Response conventions

- **Success**: the API returns the DTO directly for `200`/`201`, or `204` for deletes. Successful responses are not wrapped in an envelope.
- **Error**: the exception middleware returns the same error envelope for all handled errors:

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

For complete request and response shapes, see `docs/api-contract.md`.

## Try the API with curl

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
