
set -e
set -m

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Backend  -> http://localhost:5050"
(cd "$ROOT_DIR/server" && dotnet run --launch-profile http) &
SERVER_PID=$!

echo "Frontend -> http://localhost:5173"
(cd "$ROOT_DIR/client" && npm run dev) &
CLIENT_PID=$!

trap 'kill -TERM -$SERVER_PID -$CLIENT_PID 2>/dev/null' EXIT

wait
