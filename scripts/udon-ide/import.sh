#!/bin/bash
# Import and compile. The editor sometimes hangs while shutting down ("Cleanup mono"): it is stopped then, after a
# grace period for a normal exit. A second pass catches errors of a first compile that ran before every file was imported.
source "$(dirname "$0")/../lib/unity.sh"
for pass in 1 2; do
  rm -f Logs/ide-import.log
  unity-2022 -batchmode -nographics -quit -projectPath "$PWD" -logFile Logs/ide-import.log >/dev/null 2>&1 &
  pid=$!
  for i in $(seq 1 240); do kill -0 $pid 2>/dev/null || break; grep -q "Cleanup mono" Logs/ide-import.log 2>/dev/null && { sleep 20; break; }; sleep 5; done
  stop_unity 3
  n=$(grep -c "error CS" Logs/ide-import.log)
  echo "import pass $pass: $n errors"
  [ "$n" = 0 ] && break
done
grep "error CS" Logs/ide-import.log | sort -u | head -5
