# Database Design

PostgreSQL 17, EF Core 9 code-first, single migration `InitialCreate`.
All application tables live in schema **`decision_vault`**
(`modelBuilder.HasDefaultSchema("decision_vault")`). Migrations apply automatically
at API startup (`DbSeeder.SeedAsync → Database.MigrateAsync()`), followed by optional
demo seeding (`SeedDemoData` flag).

## Entity-relationship overview

```
users 1───* decisions *───1 categories
                │ 1───* decision_options   (owning side)
                │ 1───* decision_reasons
                │ 1───1 decision_reviews
                │ 1───* decision_events
                └── SelectedOptionId *───1 decision_options (ON DELETE SET NULL)
```

## Tables

### users
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| FullName | varchar(100) | required, 2–100 |
| Email | varchar(200) | required, unique index, validated format |
| PasswordHash | varchar(500) | BCrypt work-factor 11 |
| Role | varchar | `User` \| `Admin` |
| IsActive | bool | deactivated accounts cannot log in |
| CreatedAt / UpdatedAt | timestamptz | audit |

### categories
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| Name | varchar(100) | required |
| Kind | varchar | `Career, Education, Financial, Health, Technology, Personal, Business, Other` |
| Description | varchar, null | |

### decisions
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| UserId | int FK → users | ON DELETE CASCADE |
| CategoryId | int FK → categories | ON DELETE RESTRICT (categories in use cannot be deleted) |
| Title | varchar(200) | 3–200 |
| Description | varchar, null | |
| Status | varchar | `Draft, Evaluating, Decided, InProgress, ReadyForReview, Reviewed` |
| DecisionDate / ReviewDate | timestamptz, null | ReviewDate ≥ DecisionDate when both set |
| confidence_score | int, null | 1–100; locked after review |
| ExpectedSuccessScore | int, null | 1–100; locked after review |
| ExpectedOutcome | varchar, null | set at finalize |
| SelectedOptionId | int FK → decision_options, null | ON DELETE SET NULL (services null it explicitly before delete) |
| CreatedAt / UpdatedAt | timestamptz | |

### decision_options
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| DecisionId | int FK → decisions | ON DELETE CASCADE |
| Name | varchar(100) | required, 1–100 |
| Description / Advantages / Disadvantages | varchar, null | |
| Score | int, null | 0–10 option quality score |
| Weight | int | 0–10 importance weight |

### decision_reasons
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| DecisionId | int FK → decisions | ON DELETE CASCADE |
| Type | varchar | `Pro \| Con \| Note` |
| Category | varchar | free-text grouping (e.g. "Salary", "Growth") |
| Text | varchar(500) | |

### decision_reviews (1:1 with decisions)
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| DecisionId | int FK → decisions | ON DELETE CASCADE, unique per decision |
| ActualOutcome | varchar | required |
| outcome_rating | int | 1–5 |
| WhatWentWell / WhatWentWrong / LessonsLearned | varchar, null | |
| WouldChooseAgain | bool | |
| IsSuccessful | bool | computed at save: `rating ≥ 3 AND WouldChooseAgain` |
| ReviewedAt | timestamptz | |

### decision_events (audit log)
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| DecisionId | int FK → decisions | ON DELETE CASCADE |
| EventType | varchar | `Created, OptionAdded, ReasonAdded, OptionSelected, StatusChanged, Finalized, Reviewed` |
| Description | varchar | human-readable detail |
| CreatedAt | timestamptz | append-only; never updated |

## Referential actions summary

| Relation | Action | Rationale |
|---|---|---|
| decision → user | CASCADE | user deletion removes their data |
| decision → category | RESTRICT | shared reference data is protected |
| option/reason/review/event → decision | CASCADE | children are owned by the decision |
| decision.SelectedOptionId → option | SET NULL | deleting an option must not delete the decision |

## Query patterns

- Decision list: filtered by owner (or all for admin), optional `status` and `search`
  (title contains, case-insensitive), ordered by `CreatedAt DESC`.
- Detail loads use `Include(Options).Include(Reasons).Include(Review).Include(Category)`
  in one round trip.
- Dashboard/analytics aggregates use server-side `GroupBy` (rows grouped before
  materialization) — category breakdowns, success rate, average outcome rating.

## Verification

```bash
docker exec decisionvault-pg psql -U postgres -d decisionvault \
  -c "SELECT table_name FROM information_schema.tables WHERE table_schema='decision_vault' ORDER BY 1"
```
