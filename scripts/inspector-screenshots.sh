#!/bin/bash
# Usage: xvfb-run -a -s "-screen 0 1400x1100x24" scripts/inspector-screenshots.sh  → Logs/shot-{0,1,2,3}.png
# (normal / advanced settings open / action picker / trigger list). TW_WIDTH=n: Inspector width; TW_EMPTY=1: a new trigger;
# TW_MINW=n: list layout parts wider than n px in Logs/minwidth.txt; TW_CLICK="x y": click there in state 0 (shot-0-click.png);
# TW_HOVER="x y;x y": hover each point in state 0 (shot-0-hover-N.png). Driven by Assets/TripwireTests/Editor/DevScreenshot.cs.
# Run inside xvfb-run: start the full editor, screenshot each state the editor signals.
source "$(dirname "$0")/lib/unity.sh"
rm -f Logs/shot-*  # stale signals from an earlier run would be taken as ready
start_unity Tripwire.Tests.DevScreenshot.Run Logs/shot.log
for n in 0 1 2 3; do
  wait_for Logs/shot-ready-$n || { kill $pid 2>/dev/null; exit 1; }
  sleep 1
  # The picker (state 2): real mouse moves and a click on a category, so non-repaint events run too.
  if [ $n = 2 ]; then
    for y in 190 210 230 250 270; do xdotool mousemove 120 $y; sleep 0.2; done
    xdotool mousemove 120 230 click 1; sleep 0.5; xdotool mousemove 120 210 click 1; sleep 0.5
  fi
  shoot Logs/shot-$n.png
  # The header menu (⋮ at the right end of the first line): opened by a real click, shot, closed.
  if [ $n = 0 ]; then
    w=$(sed -E 's/.*width:([0-9.]+).*/\1/' Logs/shot-ready-0 | cut -d. -f1)
    xdotool mousemove $((20 + w - 14)) 50 click 1; sleep 0.7
    shoot Logs/shot-0-menu.png
    xdotool key Escape; sleep 0.3
    # TW_CLICK="x y": also click a screen point (a popup, a button) and shoot it as shot-0-click.png.
    if [ -n "$TW_CLICK" ]; then
      xdotool mousemove $TW_CLICK click 1; sleep 0.7
      shoot Logs/shot-0-click.png
      xdotool key Escape; sleep 0.3
    fi
    # TW_HOVER="x y;x y": rest the mouse on each point until its tooltip shows, shot as shot-0-hover-N.png.
    i=0; IFS=';' read -ra points <<< "$TW_HOVER"
    for p in "${points[@]}"; do
      xdotool mousemove 5 5; sleep 0.3; xdotool mousemove $p; sleep 2
      shoot Logs/shot-0-hover-$i.png; i=$((i + 1))
    done
  fi
  echo "state $n: $(cat Logs/shot-ready-$n)"
  touch Logs/shot-done-$n
done
wait $pid
echo "unity exit=$?"
# GUI exceptions only show in the log (the screenshots look fine): report them.
errors=$(grep -E "Exception|GUI Error|Mismatched LayoutGroup" Logs/shot.log | grep -v "^  at \|^UnityEngine\." | sort -u)
[ -n "$errors" ] && { echo "GUI errors:"; echo "$errors" | head -10; }
