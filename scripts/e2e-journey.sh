#!/bin/bash
# DecisionVault E2E journey + negative scenarios. Prints PASS/FAIL per step.
API="${DV_API_URL:-http://localhost:5000}"
TMPDIR=$(mktemp -d); trap "rm -rf $TMPDIR" EXIT
J() { python3 -c "import sys,json;d=json.load(sys.stdin);print(d$1)" 2>/dev/null; }
ok(){ echo "PASS: $1"; }
bad(){ echo "FAIL: $1"; FAILED=1; }
FAILED=0
E2E_EMAIL="e2e.$(date +%s)@dv.test"
# 1. Register fresh user
R=$(curl -s -X POST $API/api/auth/register -H 'Content-Type: application/json' -d "{\"fullName\":\"E2E Journey\",\"email\":\"$E2E_EMAIL\",\"password\":\"E2e#12345\"}")
T=$(echo "$R" | J '["token"]')
[ -n "$T" ] && ok "register+token" || bad "register: $R"

# 2. Login returns same-shape response
L=$(curl -s -X POST $API/api/auth/login -H 'Content-Type: application/json' -d "{\"email\":\"$E2E_EMAIL\",\"password\":\"E2e#12345\"}")
T=$(echo "$L" | J '["token"]')
[ -n "$T" ] && ok "login" || bad "login: $L"
AUTH="Authorization: Bearer $T"

# NEG: wrong password -> 401 uniform message
W=$(curl -s -o "$TMPDIR/w.json" -w '%{http_code}' -X POST $API/api/auth/login -H 'Content-Type: application/json' -d "{\"email\":\"$E2E_EMAIL\",\"password\":\"Wrong#99999\"}")
WMSG=$(cat "$TMPDIR/w.json" | J '["message"]')
case "$WMSG" in "Invalid email or password.") ok "wrong pw uniform message" ;; *) bad "wrong pw message: $WMSG" ;; esac

# 3. Categories list
C=$(curl -s $API/api/categories -H "$AUTH")
CID=$(echo "$C" | python3 -c 'import sys,json;print(json.load(sys.stdin)[0]["id"])')
[ -n "$CID" ] && ok "categories list" || bad "categories: $C"

# 4. Create decision
D=$(curl -s -X POST $API/api/decisions -H "$AUTH" -H 'Content-Type: application/json' -d '{"title":"E2E: choose a framework","description":null,"categoryId":'$CID',"decisionDate":null,"reviewDate":null,"confidenceScore":60,"expectedSuccessScore":70,"expectedOutcome":null}')
DID=$(echo "$D" | J '["id"]')
ST=$(echo "$D" | J '["status"]')
[ "$ST" = "Draft" ] && ok "create→Draft" || bad "create: $D"

# NEG: invalid confidence
N=$(curl -s -o "$TMPDIR/n.json" -w '%{http_code}' -X POST $API/api/decisions -H "$AUTH" -H 'Content-Type: application/json' -d '{"title":"bad","categoryId":'$CID',"confidenceScore":0,"expectedSuccessScore":70}')
E1=$(cat "$TMPDIR/n.json" | J '["errors"][0]')
[ "$N" = 400 ] && [ -n "$E1" ] && ok "invalid confidence 400+field errors" || bad "neg create: $N $E1"

# 5. Add two options, add reason
O1=$(curl -s -X POST $API/api/decisions/$DID/options -H "$AUTH" -H 'Content-Type: application/json' -d '{"name":"Angular","advantages":null,"disadvantages":null,"score":8,"weight":7}')
OID1=$(echo "$O1" | J '["id"]')
O2=$(curl -s -X POST $API/api/decisions/$DID/options -H "$AUTH" -H 'Content-Type: application/json' -d '{"name":"React","advantages":null,"disadvantages":null,"score":7,"weight":8}')
OID2=$(echo "$O2" | J '["id"]')
[ -n "$OID1" ] && [ -n "$OID2" ] && ok "options added" || bad "options: $O1 / $O2"

# NEG: duplicate option name → 409
DUP=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID/options -H "$AUTH" -H 'Content-Type: application/json' -d '{"name":"angular","score":5,"weight":5}')
[ "$DUP" = 409 ] && ok "dup option 409" || bad "dup option: $DUP"

# NEG: invalid option score → 400
BADW=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID/options -H "$AUTH" -H 'Content-Type: application/json' -d '{"name":"BadOpt","score":99,"weight":5}')
[ "$BADW" = 400 ] && ok "option score 99 → 400" || bad "option score: $BADW"

RE=$(curl -s -X POST $API/api/decisions/$DID/reasons -H "$AUTH" -H 'Content-Type: application/json' -d '{"type":"Pro","category":"Ecosystem","text":"Huge community"}')
[ -n "$(echo "$RE" | J '["id"]')" ] && ok "reason added" || bad "reason: $RE"

# 6. Select option → Decided, finalize → expectations set
S=$(curl -s -X POST $API/api/decisions/$DID/select-option -H "$AUTH" -H 'Content-Type: application/json' -d '{"optionId":'$OID1'}')
[ "$(echo "$S" | J '["status"]')" = "Decided" ] && ok "select→Decided" || bad "select: $S"

# NEG: select on Decided → 409
SEL2=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID/select-option -H "$AUTH" -H 'Content-Type: application/json' -d '{"optionId":'$OID2'}')
[ "$SEL2" = 409 ] && ok "select when Decided → 409" || bad "select Decided: $SEL2"

# NEG: add option when Decided → 409
ADD2=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID/options -H "$AUTH" -H 'Content-Type: application/json' -d '{"name":"Late","score":5,"weight":5}')
[ "$ADD2" = 409 ] && ok "add option when Decided → 409" || bad "add option Decided: $ADD2"

# 7. Reopen → Evaluating clears selection (new behavior)
RO=$(curl -s -X POST $API/api/decisions/$DID/transition -H "$AUTH" -H 'Content-Type: application/json' -d '{"status":"Evaluating"}')
[ "$(echo "$RO" | J '["status"]')" = "Evaluating" ] && [ "$(echo "$RO" | J '["selectedOptionId"]')" = "None" ] && ok "reopen→Evaluating+selection cleared" || bad "reopen: $RO"

# 8. Re-select, → InProgress → ReadyForReview
curl -s -o /dev/null -X POST $API/api/decisions/$DID/select-option -H "$AUTH" -H 'Content-Type: application/json' -d '{"optionId":'$OID1'}'
IP=$(curl -s -X POST $API/api/decisions/$DID/transition -H "$AUTH" -H 'Content-Type: application/json' -d '{"status":"InProgress"}')
RF=$(curl -s -X POST $API/api/decisions/$DID/transition -H "$AUTH" -H 'Content-Type: application/json' -d '{"status":"ReadyForReview"}')
[ "$(echo "$RF" | J '["status"]')" = "ReadyForReview" ] && ok "InProgress→ReadyForReview" || bad "rf: $IP $RF"

# NEG: skip lifecycle Draft→Reviewed
D2=$(curl -s -X POST $API/api/decisions -H "$AUTH" -H 'Content-Type: application/json' -d '{"title":"Second decision","categoryId":'$CID',"confidenceScore":50,"expectedSuccessScore":60}')
DID2=$(echo "$D2" | J '["id"]')
SKIP=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID2/transition -H "$AUTH" -H 'Content-Type: application/json' -d '{"status":"Reviewed"}')
[ "$SKIP" = 409 ] && ok "skip transition 409" || bad "skip: $SKIP"

# 9. Review → Reviewed; metrics populated
RV=$(curl -s -X POST $API/api/decisions/$DID/review -H "$AUTH" -H 'Content-Type: application/json' -d '{"actualOutcome":"Shipped on time","outcomeRating":4,"whatWentWell":null,"whatWentWrong":null,"lessonsLearned":null,"wouldChooseAgain":true}')
[ "$(echo "$RV" | J '["isSuccessful"]')" = "True" ] && ok "review success computed" || bad "review: $RV"

# NEG: review again → 409
RV2=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID/review -H "$AUTH" -H 'Content-Type: application/json' -d '{"actualOutcome":"x","outcomeRating":3,"wouldChooseAgain":true}')
[ "$RV2" = 409 ] && ok "double review 409" || bad "double review: $RV2"

# NEG: invalid rating 9 → 400
RV9=$(curl -s -o /dev/null -w '%{http_code}' -X POST $API/api/decisions/$DID2/review -H "$AUTH" -H 'Content-Type: application/json' -d '{"actualOutcome":"x","outcomeRating":9,"wouldChooseAgain":true}')
[ "$RV9" = 400 ] && ok "rating 9 → 400" || bad "rating9: $RV9 (need ReadyForReview first, 400 expected only if reachable)"

# 10. Timeline reflects events
TL=$(curl -s $API/api/decisions/$DID/timeline -H "$AUTH")
CNT=$(echo "$TL" | python3 -c 'import sys,json;print(len(json.load(sys.stdin)))')
[ "$CNT" -ge 8 ] && ok "timeline events ($CNT)" || bad "timeline: $CNT"

# 11. Dashboard + analytics + profile
DB=$(curl -s $API/api/dashboard -H "$AUTH")
[ -n "$(echo "$DB" | J '["totalDecisions"]')" ] && ok "dashboard" || bad "dashboard: $DB"
AN=$(curl -s $API/api/dashboard/analytics -H "$AUTH")
echo "$AN" | grep -q decisionPerformanceScore && ok "analytics" || bad "analytics: $AN"
PS=$(curl -s $API/api/profile/summary -H "$AUTH")
echo "$PS" | grep -q memberSince && ok "profile summary" || bad "profile: $PS"

# 12. Pagination shape
PG=$(curl -s "$API/api/decisions?page=1&pageSize=1&sortBy=createdAt&sortDir=desc" -H "$AUTH")
TOT=$(echo "$PG" | J '["totalCount"]')
PGN=$(echo "$PG" | python3 -c 'import sys,json;print(len(json.load(sys.stdin)["items"]))')
[ "$PGN" = 1 ] && [ -n "$TOT" ] && ok "pagination page=1 size=1 total=$TOT" || bad "pagination: $PG"

# 13. Cleanup own data
DEL1=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE $API/api/decisions/$DID -H "$AUTH")
DEL2=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE $API/api/decisions/$DID2 -H "$AUTH")
[ "$DEL1" = 204 ] && [ "$DEL2" = 204 ] && ok "delete own decisions" || bad "delete: $DEL1 $DEL2"

echo "===="
[ $FAILED = 0 ] && echo "E2E RESULT: ALL PASS" || echo "E2E RESULT: FAILURES ABOVE"
