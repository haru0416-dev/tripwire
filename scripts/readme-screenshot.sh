#!/bin/bash
# The README's picture: an applied trigger in the Inspector → docs/images/inspector-<lang>.png (lang: ja or en).
# Usage: for l in ja en; do xvfb-run -a -s "-screen 0 1400x1100x24" scripts/readme-screenshot.sh $l; done
# (one language per Xvfb: a second editor in the same display showed only black). Driven by DevReadmeShot.cs.
source "$(dirname "$0")/lib/unity.sh"
lang=${1:-ja}
# Batch mode writes the trigger's script; the next start compiles it, so the shot shows the trigger applied.
TW_LANG=$lang unity-2022 -batchmode -nographics -quit -projectPath "$PWD" -executeMethod Tripwire.Tests.DevReadmeShot.Prepare -logFile Logs/readme-prepare.log >/dev/null 2>&1
unity-2022 -batchmode -nographics -quit -projectPath "$PWD" -logFile Logs/readme-import.log >/dev/null 2>&1
rm -f Logs/readme-ready Logs/readme-done # a stale signal would be taken as ready (the shot would show the splash)
TW_LANG=$lang start_unity Tripwire.Tests.DevReadmeShot.Run Logs/readme-shot.log
wait_for Logs/readme-ready || { stop_unity; exit 1; }
sleep 1
shoot Logs/readme-$lang-full.png
touch Logs/readme-done
stop_unity 10
echo "$lang: $(cat Logs/readme-state.txt)"
# The window (440 wide at 20,40), its empty lower part trimmed, an even margin in the window's own color.
bg=$(convert Logs/readme-$lang-full.png -format '%[pixel:p{30,930}]' info:)
convert Logs/readme-$lang-full.png -crop 440x880+20+40 +repage -fuzz 3% -trim +repage -bordercolor "$bg" -border 14 docs/images/inspector-$lang.png
