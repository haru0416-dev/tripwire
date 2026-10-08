# Shared by the scripts that drive the editor. Sourcing it moves to the repository root.
# unity-2022 is a local wrapper outside the repository: Unity 2022.3 with the compat libraries Ubuntu 26.04 lacks.
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

# Starts the full editor on a dev harness (-executeMethod) in the background; sets $pid.
start_unity() { local method=$1 log=$2; shift 2; unity-2022 -projectPath "$PWD" -executeMethod "$method" -logFile "$log" "$@" & pid=$!; }

# Waits for a signal file the harness writes (default 600 s); fails if the editor exits first.
wait_for() {
  local file=$1 limit=${2:-600}
  for i in $(seq 1 "$limit"); do
    [ -f "$file" ] && return 0
    kill -0 $pid 2>/dev/null || { echo "unity exited early"; return 1; }
    sleep 1
  done
  echo "timeout: $file"; return 1
}

# Stops the editor: politely, then -9 (it sometimes hangs while closing).
stop_unity() { kill $pid 2>/dev/null; sleep "${1:-5}"; kill -9 $pid 2>/dev/null; }

shoot() { import -window root "$1"; }
