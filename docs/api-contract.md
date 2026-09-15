# DecisionVault — API Contract (v1)

Single source of truth for backend controllers and Angular services. All endpoints under `/api`.
**Rules:**
- Auth: `Authorization: Bearer <jwt>` except `/auth/register`, `/auth/login`.
- Envelope: success responses return the DTO directly. Errors return `ApiError`:

```json
{ "success": false, "message": "...", "errors": ["..."], "timestamp": "2026-09-15T10:00:00Z" }
```

- Dates: ISO-8601 UTC strings. IDs: integers. Enums serialize as strings both ways (System.Text.Json string enum converter + frontend string unions).
- CORS: allow `http://localhost:4200`.

## Enums
- `DecisionStatus`: `Draft, Evaluating, Decided, InProgress, ReadyForReview, Reviewed` (0..5)
- `ReasonType`: `Pro, Con, Note` (0..2)
- `CategoryKind`: `Career, Education, Financial, Health, Technology, Personal, Business, Other` (0..7)

## Auth
| Method | Path | Auth | Body | Success | Notes |
|---|---|---|---|---|---|
| POST | /auth/register | – | RegisterRequest | 201 `AuthResponse` | 409 email exists |
| POST | /auth/login | – | LoginRequest | 200 `AuthResponse` | 401 invalid credentials, 403 deactivated |
| GET | /auth/me | user | – | 200 `UserDto` | 401 |
| PUT | /auth/profile | user | UpdateProfileRequest | 200 `UserDto` | 409 email conflict |
| PUT | /auth/change-password | user | ChangePasswordRequest | 200 message string | 400 wrong current password |

RegisterRequest: `{ fullName (2-100), email, password (8-72, 1 upper, 1 lower, 1 digit) }`
AuthResponse: `{ token, expiresAt, user: UserDto }`
UserDto: `{ id, fullName, email, role: "User"|"Admin", isActive, createdAt }`
UpdateProfileRequest: `{ fullName 2-100, email }`
ChangePasswordRequest: `{ currentPassword, newPassword (same rules) }`

## Categories (authenticated)
- GET /categories → 200 `CategoryDto[]` `{ id, name, kind, description? }`
- POST /categories (Admin) → 201; PUT /categories/{id} (Admin) → 200; DELETE /categories/{id} (Admin) → 204 (409 if in use)

## Decisions
All decision endpoints are user-scoped: a non-admin sees only own decisions; owner checks enforced in services. Admin may GET any decision by id (read-only, for support) but user endpoints never list other users' data to normal users.

- GET /decisions?search=&categoryId=&status=&outcome=(Successful|Unsuccessful|PendingReview)&minConfidence=&maxConfidence=&sortBy=(CreatedAt|Title|Confidence|ReviewDate)&sortDir=(asc|desc)&page=1&pageSize=10
  → 200 `PagedResult<DecisionListItemDto>` `{ items, page, pageSize, totalCount, totalPages }`
  - `outcome=PendingReview` means status ∈ {Draft, Evaluating, Decided, InProgress, ReadyForReview} OR (Reviewed with null IsSuccessful)
- GET /decisions/{id} → 200 `DecisionDto` (404 if not owner/admin)
- POST /decisions → 201 `DecisionDto`
- PUT /decisions/{id} — core fields only: title, description, categoryId, decisionDate, reviewDate, confidenceScore, expectedSuccessScore, expectedOutcome → 200 `DecisionDto` (409 if already Reviewed for confidence/expected edits)
- DELETE /decisions/{id} → 204 (cascades options/reasons/review/events)

### Options
- POST /decisions/{id}/options → 201 `DecisionOptionDto` (only while status Draft/Evaluating; 409 otherwise)
- PUT /decisions/{id}/options/{optionId} → 200 (same status rule)
- DELETE /decisions/{id}/options/{optionId} → 204 (409 if it is the SelectedOption of a Decided+ decision)
- DecisionOptionDto: `{ id, decisionId, name (1-100), description?, advantages?, disadvantages?, score (0-10, nullable), weight (0-10, default 5) }`

### Reasons
- POST /decisions/{id}/reasons → 201 `DecisionReasonDto` (Draft/Evaluating/Decided)
- DELETE /decisions/{id}/reasons/{reasonId} → 204
- DecisionReasonDto: `{ id, decisionId, type: "Pro"|"Con"|"Note", category (1-60), text (1-500) }`

### Lifecycle
- POST /decisions/{id}/select-option `{ optionId }` → 200 `DecisionDto` — transitions Draft|Evaluating → Decided (409 invalid transition)
- POST /decisions/{id}/transition `{ status: "InProgress"|"ReadyForReview"|"Evaluating" }` → 200 `DecisionDto` — allowed forward moves only per state machine
- State machine: `Draft → Evaluating → Decided → InProgress → ReadyForReview → Reviewed`; backward moves prohibited except Decided→Evaluating (re-open evaluation, clears selection).
- POST /decisions/{id}/finalize `{ selectedOptionId?, confidenceScore (1-100), expectedSuccessScore (1-100), expectedOutcome (1-1000 chars) }` → 200 `DecisionDto`
  - Allowed from Draft (requires ≥2 options + selectedOptionId) or Evaluating (requires selectedOptionId) or Decided/InProgress (updates expectations). Sets status Decided if not already. Records event.

### Review
- POST /decisions/{id}/review → 201 `DecisionReviewDto` — requires status ReadyForReview (or InProgress with explicit `allowFromInProgress:true`? **NO**: requires ReadyForReview only); 409 otherwise; only one review per decision (409 on duplicate)
- GET /decisions/{id}/review → 200 `DecisionReviewDto` | 404 if none
- ReviewRequest: `{ actualOutcome (1-1000), outcomeRating (1-5), whatWentWell?, whatWentWrong?, lessonsLearned?, wouldChooseAgain (bool) }`
- DecisionReviewDto: `{ id, decisionId, actualOutcome, outcomeRating, whatWentWell?, whatWentWrong?, lessonsLearned?, wouldChooseAgain, isSuccessful, reviewedAt }`
- `isSuccessful` (server-computed, transparent): `outcomeRating >= 3 && wouldChooseAgain`
- POST /decisions/{id}/review sets status → Reviewed and records event.

### Timeline
- GET /decisions/{id}/timeline → 200 `DecisionEventDto[]` `{ id, decisionId, eventType, description, createdAt }` ordered createdAt DESC, id DESC

## Dashboard & Analytics (authenticated, own data)
- GET /dashboard → 200 `DashboardDto`:
```json
{
  "totalDecisions": 0, "pendingReview": 0, "successful": 0, "unsuccessful": 0,
  "averageConfidence": 0.0, "decisionAccuracy": 0.0,
  "decisionsByCategory": [{ "categoryId": 1, "categoryName": "Career", "count": 0 }],
  "outcomeDistribution": [{ "label": "Successful", "count": 0 }],
  "decisionsByStatus": [{ "status": "Draft", "count": 0 }],
  "recentDecisions": [DecisionListItemDto],
  "monthlyTrend": [{ "year": 2026, "month": 9, "total": 0, "successful": 0 }]
}
```
  - `decisionAccuracy` (documented formula): mean over reviewed decisions of `confidenceScore/100` alignment — `accuracy = Σ min(confidence,100·isSuccessful→rating-normalized) ...` **Concrete formula:** for each reviewed decision, `alignment = 1 − |expectedSuccessScore − actualScore|/100` where `actualScore = outcomeRating/5·100`; `decisionAccuracy = round(100·mean(alignment))`, 0 when no reviewed decisions.
- GET /analytics → 200 `AnalyticsDto`:
```json
{
  "confidenceDistribution": [{ "bucket": "0-20", "count": 0 }],
  "expectedVsActual": [{ "decisionId": 1, "title": "...", "expected": 0, "actual": 0 }],
  "decisionPerformanceScore": 0.0,
  "successRateTrend": [{ "year": 2026, "month": 9, "successRate": 0.0 }]
}
```
  - `decisionPerformanceScore` (0-100, application-defined, documented): `0.4·avg(outcomeRating)/5·100 + 0.3·successRate + 0.3·avg(alignment)` over reviewed decisions; 0 when none reviewed. Buckets: 0-20,21-40,41-60,61-80,81-100 over confidenceScore.

## Admin (role Admin)
- GET /admin/dashboard → 200 `AdminDashboardDto` `{ totalUsers, activeUsers, totalDecisions, decisionsByStatus[], decisionsByCategory[], overallSuccessRate, recentActivity: [{ id, eventType, description, userEmail, createdAt }] }`
- GET /admin/users?page=&pageSize=&search= → `PagedResult<AdminUserDto>` `{ id, fullName, email, role, isActive, createdAt, decisionCount }`
- PUT /admin/users/{id}/status `{ isActive: bool }` → 200 `AdminUserDto` (403 attempting to deactivate self)
- GET /admin/activity?page=&pageSize= → `PagedResult<ActivityDto>` (recent events across users, no decision content — event types + category-level info only)

## Profile extras
- GET /profile/summary → 200 `{ memberSince, totalDecisions, reviewedCount, avgOutcomeRating }`

## Status codes
200 OK · 201 Created · 204 No Content · 400 ValidationError · 401 Unauthorized · 403 Forbidden (role/ownership/deactivated) · 404 NotFound · 409 Conflict (invalid transition, duplicate, in-use) · 500 Internal (generic message, no stack trace)
