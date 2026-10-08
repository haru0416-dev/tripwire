#!/bin/bash
# Usage: ./test.sh <filter>  (EditMode tests). Retries when no results come: the editor hangs after loading now and then.
source "$(dirname "$0")/../lib/unity.sh"
for attempt in 1 2 3; do
  rm -f Logs/test-results.xml
  timeout 900 xvfb-run -a -s "-screen 0 1280x720x24" unity-2022 -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "$1" -testResults "$PWD"/Logs/test-results.xml -logFile Logs/test.log >/dev/null 2>&1
  [ -f Logs/test-results.xml ] && break
  echo "attempt $attempt: no results"
done
python3 scripts/lib/test-results.py Logs/test-results.xml
