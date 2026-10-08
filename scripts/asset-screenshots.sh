#!/bin/bash
# Dev only: screenshots of other assets' Inspectors (see Assets/TripwireTests/Editor/DevAssetShots.cs) → Logs/asset-N.png
# Usage (after scripts/fetch-test-assets.sh): xvfb-run -a -s "-screen 0 1400x1100x24" scripts/asset-screenshots.sh
source "$(dirname "$0")/lib/unity.sh"
rm -f Logs/asset-*
start_unity Tripwire.Tests.DevAssetShots.Run Logs/asset-shots.log
for n in 0 1 2 3 4 5; do
  wait_for Logs/asset-ready-$n 900 || exit 1
  sleep 1
  shoot Logs/asset-$n.png
  echo "$n: $(cat Logs/asset-name-$n)"
  touch Logs/asset-done-$n
done
wait $pid
