#!/bin/bash
# Usage: xvfb-run -a -s "-screen 0 1280x900x24" ./shots.sh N  -> Logs/ide-0.png .. ide-(N-1).png
source "$(dirname "$0")/../lib/unity.sh"
rm -f Logs/ide-* Logs/ide.log
start_unity UdonIde.Dev.DevShot.Run Logs/ide.log
for k in $(seq 0 $(($1 - 1))); do
  wait_for Logs/ide-ready-$k 400 || { kill -9 $pid 2>/dev/null; exit 1; }
  sleep 1; shoot Logs/ide-$k.png; touch Logs/ide-done-$k
done
wait $pid; echo "unity exit=$?"
