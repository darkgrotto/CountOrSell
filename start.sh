#!/usr/bin/env bash
# CountOrSell startup script
# Starts the ASP.NET Core API and the Vite dev server.
#
# Usage:  ./start.sh [--api-port PORT] [--web-port PORT]
#
#   --api-port PORT   Port for the ASP.NET Core API  (default: 5000)
#   --web-port PORT   Port for the Vite dev server   (default: 5173)
#
# Examples:
#   ./start.sh
#   ./start.sh --api-port 7000 --web-port 3000

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$SCRIPT_DIR/src/CountOrSell.Api"
WEB_DIR="$SCRIPT_DIR/src/CountOrSell-web"

API_PORT=5000
WEB_PORT=5173
API_PID=""
WEB_PID=""

# ── Argument parsing ─────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-port) API_PORT="$2"; shift 2 ;;
    --web-port) WEB_PORT="$2"; shift 2 ;;
    *) echo "[WARN] Unknown argument: $1"; shift ;;
  esac
done

# ── Helpers ──────────────────────────────────────────────────────────────────

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
err()  { echo "[$(date '+%H:%M:%S')] ERROR: $*" >&2; }

check_tool() {
  local cmd="$1" url="$2"
  if ! command -v "$cmd" &>/dev/null; then
    err "'$cmd' not found."
    [[ -n "$url" ]] && echo "       Download: $url" >&2
    exit 1
  fi
}

free_port() {
  local port=$1
  local pid
  # netstat on Windows prints PID in last column; kill any process using the port
  pid=$(netstat -ano 2>/dev/null \
    | grep "LISTENING" \
    | grep ":${port}[[:space:]]" \
    | awk '{print $NF}' \
    | head -1)
  if [[ -n "$pid" && "$pid" -gt 0 ]]; then
    log "Port $port in use by PID $pid — killing it..."
    taskkill //F //PID "$pid" &>/dev/null || kill "$pid" 2>/dev/null || true
    sleep 1
  fi
}

cleanup() {
  echo ""
  log "Shutting down..."
  [[ -n "$WEB_PID" ]] && kill "$WEB_PID" 2>/dev/null && log "Frontend stopped."
  [[ -n "$API_PID" ]] && kill "$API_PID" 2>/dev/null && log "API stopped."
  exit 0
}

trap cleanup SIGINT SIGTERM

# ── Preflight checks ─────────────────────────────────────────────────────────

check_tool dotnet "https://dotnet.microsoft.com/download"
check_tool npm    "https://nodejs.org/"

log "CountOrSell — starting services"
echo "────────────────────────────────────────"

# ── Port cleanup ─────────────────────────────────────────────────────────────

free_port $API_PORT

# ── Vite cache ───────────────────────────────────────────────────────────────
# Clear the Vite dep cache to avoid EPERM errors (common on OneDrive paths)

VITE_CACHE="$WEB_DIR/node_modules/.vite"
if [[ -d "$VITE_CACHE" ]]; then
  log "Clearing Vite cache..."
  rm -rf "$VITE_CACHE" 2>/dev/null || true
fi

# ── API ──────────────────────────────────────────────────────────────────────

log "Restoring .NET packages..."
dotnet restore "$SCRIPT_DIR/src/CountOrSell.sln" --nologo -v q

log "Starting API on http://localhost:$API_PORT ..."
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project "$API_DIR" \
  --no-launch-profile \
  --urls "http://localhost:$API_PORT" \
  2>&1 | sed 's/^/[API] /' &
API_PID=$!

# Wait until the API is accepting TCP connections (up to 30 s)
log "Waiting for API to be ready..."
for i in $(seq 1 30); do
  if (echo > /dev/tcp/localhost/$API_PORT) 2>/dev/null; then
    log "API is ready."
    break
  fi
  sleep 1
  if [[ $i -eq 30 ]]; then
    log "API did not respond within 30 s — continuing anyway."
  fi
done

# ── Frontend ─────────────────────────────────────────────────────────────────

log "Installing frontend dependencies..."
(cd "$WEB_DIR" && npm install --silent)

log "Starting frontend dev server..."
(cd "$WEB_DIR" && npm run dev -- --port $WEB_PORT) \
  2>&1 | sed 's/^/[WEB] /' &
WEB_PID=$!

# ── Summary ──────────────────────────────────────────────────────────────────

echo ""
echo "════════════════════════════════════════"
echo "  API           http://localhost:$API_PORT"
echo "  API (Swagger)  http://localhost:$API_PORT/swagger"
echo "  Frontend      http://localhost:$WEB_PORT"
echo "════════════════════════════════════════"
echo "  Press Ctrl+C to stop all services."
echo ""

# Keep script alive until a child exits or user presses Ctrl+C
wait
