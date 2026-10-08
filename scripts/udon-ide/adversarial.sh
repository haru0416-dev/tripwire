#!/bin/bash
# Usage: ./adversarial.sh <cases|typing|stress|all>. Retries when the editor hangs right after loading
# (the log stops at "Unloading ... Unused Serialized files" and nothing runs).
source "$(dirname "$0")/../lib/unity.sh"
for attempt in 1 2 3; do
  rm -f Logs/adversarial.txt
  unity-2022 -batchmode -nographics -projectPath "$PWD" -executeMethod UdonIde.Dev.DevAdversarial.Run -only "$1" -logFile Logs/adversarial.log >/dev/null 2>&1 &
  pid=$!
  for i in $(seq 1 360); do
    kill -0 $pid 2>/dev/null || break
    # No output after 3 minutes: the hang after loading.
    if [ $i -eq 36 ] && [ ! -f Logs/adversarial.txt ]; then echo "attempt $attempt: hung after loading"; kill -9 $pid; break; fi
    sleep 5
  done
  kill -0 $pid 2>/dev/null && { echo "timeout"; kill -9 $pid; }
  grep -q "^done" Logs/adversarial.txt 2>/dev/null && break
done
cat Logs/adversarial.txt
