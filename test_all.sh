#!/usr/bin/env bash
set -Eeuo pipefail

BASE_URL="${BASE_URL:-http://localhost:5001}"
GATEWAY_URL="${GATEWAY_URL:-http://localhost:8080}"
START_SERVICES="${START_SERVICES:-false}"
PASSED=0
FAILED=0
USER_ID="e2e-user"
OTHER_USER_ID="e2e-other-user"
CLIENT_ORDER_ID="e2e-$(date +%s)-$RANDOM"

pass() { PASSED=$((PASSED + 1)); printf '[PASS] %s\n' "$1"; }
fail() { FAILED=$((FAILED + 1)); printf '[FAIL] %s\n' "$1"; }
assert_eq() { [[ "$1" == "$2" ]] && pass "$3" || fail "$3 (expected '$2', got '$1')"; }

if [[ "${1:-}" == "--start" ]]; then START_SERVICES=true; fi

if [[ "$START_SERVICES" == "true" ]]; then
  docker compose down -v --remove-orphans >/dev/null 2>&1 || true
  docker compose up -d --build --wait
fi

for _ in $(seq 1 45); do
  if curl -fsS "$BASE_URL/health" >/dev/null; then break; fi
  sleep 2
done
curl -fsS "$BASE_URL/health" >/dev/null && pass "Direct API instance health endpoint is ready" || fail "Direct API instance health endpoint is ready"
curl -fsS "$GATEWAY_URL/health" >/dev/null && pass "Nginx gateway health endpoint is ready" || fail "Nginx gateway health endpoint is ready"

payload=$(printf '{"clientOrderId":"%s","symbol":"aapl","price":225.50,"volume":10}' "$CLIENT_ORDER_ID")
create_file=$(mktemp)
status=$(curl -sS -o "$create_file" -w '%{http_code}' -H "X-User-Id: $USER_ID" -H 'Content-Type: application/json' -d "$payload" "$BASE_URL/api/orders")
assert_eq "$status" "201" "New order returns HTTP 201"
order_id=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["id"])' "$create_file")
state=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["state"])' "$create_file")
assert_eq "$state" "active" "New order starts in Active state"

retry_status=$(curl -sS -o /dev/null -w '%{http_code}' -H "X-User-Id: $USER_ID" -H 'Content-Type: application/json' -d "$payload" "$BASE_URL/api/orders")
assert_eq "$retry_status" "200" "Idempotent retry returns HTTP 200"

for _ in $(seq 1 15); do
  state=$(curl -fsS -H "X-User-Id: $USER_ID" "$BASE_URL/api/orders/$order_id" | python3 -c 'import json,sys; print(json.load(sys.stdin)["state"])')
  [[ "$state" != "active" ]] && break
  sleep 2
done
assert_eq "$state" "inactive" "Expired order transitions to Inactive state"

printf 'Passed: %s\nFailed: %s\n' "$PASSED" "$FAILED"
[[ "$FAILED" -eq 0 ]]
