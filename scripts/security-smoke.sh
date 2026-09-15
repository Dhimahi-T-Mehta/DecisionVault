#!/bin/bash
# Final security smoke — DecisionVault release pass
API="${DV_API_URL:-http://localhost:5000}"
P=0; F=0
ok()  { P=$((P+1)); echo "PASS: $1"; }
bad() { F=$((F+1)); echo "FAIL: $1 — $2"; }
chk() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1" "expected=$3 got=$2"; fi; }

# 1. unauthenticated → 401
C=$(curl -s -o /dev/null -w '%{http_code}' $API/api/decisions)
chk "no token → 401" "$C" "401"

# 2. invalid JWT → 401
C=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer garbage.token.here" $API/api/decisions)
chk "invalid jwt → 401" "$C" "401"

# 3. tampered signature → 401
TOK=$(curl -s -X POST $API/api/auth/login -H 'Content-Type: application/json' -d '{"email":"demo@example.com","password":"Demo#12345"}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
TAM="${TOK%?}x"
C=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TAM" $API/api/decisions)
chk "tampered jwt → 401" "$C" "401"

# 4. user hitting admin endpoint → 403
C=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TOK" $API/api/admin/users)
chk "user→admin endpoint → 403" "$C" "403"

# 5. expired-session handling: UI-side, verify via short-exp token? API has 480min. Skip live expiry; verify invalid-user-in-JWT case:
# cross-user: demo cannot read admin's… need a decision owned by another user. Use user id 6 (e2e) decision if exists, else create as admin.
ATOK=$(curl -s -X POST $API/api/auth/login -H 'Content-Type: application/json' -d '{"email":"admin@example.com","password":"Admin#12345"}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
CAT=$(curl -s -H "Authorization: Bearer $ATOK" $API/api/categories | python3 -c 'import sys,json;print(json.load(sys.stdin)[0]["id"])' 2>/dev/null || curl -s -H "Authorization: Bearer $ATOK" $API/api/categories | python3 -c 'import sys,json;d=json.load(sys.stdin);print(d["items"][0]["id"] if "items" in d else d[0]["id"])')
# admin creates a decision
NEW=$(curl -s -X POST $API/api/decisions -H "Authorization: Bearer $ATOK" -H 'Content-Type: application/json' -d "{\"title\":\"Sec smoke $RANDOM\",\"categoryId\":$CAT,\"description\":\"x\"}")
NID=$(echo "$NEW" | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')
# demo reads it → expect 403 (admin read allowed for admin only; demo non-admin, other user's decision)
C=$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TOK" $API/api/decisions/$NID)
chk "cross-user read → 403" "$C" "403"
# demo modifies it → 403
C=$(curl -s -o /dev/null -w '%{http_code}' -X PUT -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' -d '{"title":"hijack","categoryId":'$CAT'}' $API/api/decisions/$NID)
chk "cross-user modify → 403" "$C" "403"
# demo deletes it → 403
C=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE -H "Authorization: Bearer $TOK" $API/api/decisions/$NID)
chk "cross-user delete → 403" "$C" "403"
# cleanup: admin deletes own
C=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE -H "Authorization: Bearer $ATOK" $API/api/decisions/$NID)
chk "admin cleanup delete → 204" "$C" "204"

# 6. invalid rating validation → 400
DID=$(curl -s -X POST $API/api/decisions -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' -d "{\"title\":\"Rate smoke $RANDOM\",\"categoryId\":$CAT}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')
C=$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' -d '{"actualOutcome":"x","outcomeRating":9}' $API/api/decisions/$DID/review)
chk "invalid rating 9 → 400" "$C" "400"

# 7. duplicate option → 409
curl -s -o /dev/null -X POST -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' -d '{"name":"Opt A"}' $API/api/decisions/$DID/options
C=$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' -d '{"name":"opt a"}' $API/api/decisions/$DID/options)
chk "duplicate option (case-insensitive) → 409" "$C" "409"

# 8. invalid lifecycle skip → 409 (Draft → Reviewed directly)
C=$(curl -s -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $TOK" -H 'Content-Type: application/json' -d '{"Status":"Reviewed"}' $API/api/decisions/$DID/transition)
chk "lifecycle skip Draft→Reviewed → 409" "$C" "409"

# 9. friendly error envelope shape (no stack traces)
MSG=$(curl -s -X POST $API/api/auth/login -H 'Content-Type: application/json' -d '{"email":"demo@example.com","password":"nope"}')
echo "$MSG" | python3 -c '
import sys, json
d = json.load(sys.stdin)
assert d["success"] is False and "message" in d and "timestamp" in d, d
s = json.dumps(d).lower()
assert "stack" not in s and "sqlstate" not in s and "exception" not in s, s
print("PASS: error envelope friendly, no internals")
' && P=$((P+1)) || { F=$((F+1)); echo "FAIL: envelope check"; }

# cleanup demo smoke decision
curl -s -o /dev/null -X DELETE -H "Authorization: Bearer $TOK" $API/api/decisions/$DID

echo "SEC RESULT: PASS=$P FAIL=$F"
[ $F -eq 0 ]