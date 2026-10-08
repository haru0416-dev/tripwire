#!/bin/bash
# Usage: xvfb-run -a -s "-screen 0 1280x900x24" ./shot.sh → Logs/ide.png (and Logs/ide-timing.txt)
# Stops Unity itself on a compile error or when the window never signals, so no editor is left holding the project.
source "$(dirname "$0")/../lib/unity.sh"
rm -f Logs/ide-* Logs/ide.log
# Import and compile first: a new file's first compile pass can fail before the file is imported.
unity-2022 -batchmode -nographics -quit -projectPath "$PWD" -logFile Logs/ide-import.log
start_unity UdonIde.Dev.DevShot.Run Logs/ide.log
for i in $(seq 1 300); do
  [ -f Logs/ide-ready ] && break
  kill -0 $pid 2>/dev/null || { echo "unity exited early"; exit 1; }
  if grep -q "error CS" Logs/ide.log 2>/dev/null && grep -q "script compilation time" Logs/ide.log; then
    grep "error CS" Logs/ide.log | sort -u | head -5; stop_unity; exit 1
  fi
  sleep 1
done
[ -f Logs/ide-ready ] || { echo "timeout"; stop_unity; exit 1; }
sleep 1; shoot Logs/ide.png; touch Logs/ide-done; wait $pid; echo "unity exit=$?"
grep -E "Exception" Logs/ide.log | sort -u | head -5
