#!/bin/bash
# Real mouse drags on the trigger Inspector (xdotool under Xvfb), driven with Assets/TripwireTests/Editor/DevDragCheck.cs.
# Usage: xvfb-run -a -s "-screen 0 1400x1100x24" scripts/drag-check.sh   → Logs/drag-state-{0,1,2,3}, Logs/drag-*.png
source "$(dirname "$0")/lib/unity.sh"
rm -f Logs/drag-*
start_unity Tripwire.Tests.DevDragCheck.Run Logs/drag.log
step() { wait_for "$1" || exit 1; }
center() { awk -F'\t' -v k="$2" '$1==k {printf "%d %d", $2+$4/2, $3+$5/2}' "$1"; }
bottom() { awk -F'\t' -v k="$2" '$1==k {printf "%d %d", $2+$4/2, $3+$5-6}' "$1"; }
drag() { # from x y → to x y, in small steps so Unity sees a drag
  xdotool mousemove $1 $2; sleep 0.3; xdotool mousedown 1; sleep 0.3
  for s in 1 2 3 4 5 6 7 8 9 10; do xdotool mousemove $(( $1 + ($3-$1)*s/10 )) $(( $2 + ($4-$2)*s/10 )); sleep 0.1; done
  sleep 0.5; xdotool mouseup 1; sleep 1
}

# 0: initial order.
step Logs/drag-rects-0; shoot Logs/drag-0.png; touch Logs/drag-done-0
# 1: drag the first event ("クリックしたとき") below the last card.
step Logs/drag-rects-1
drag $(center Logs/drag-rects-1 "grip:クリックしたとき") $(bottom Logs/drag-rects-1 "card:KEvent:2")
shoot Logs/drag-1.png; touch Logs/drag-done-1
# 2: drag the action "オブジェクトの表示を切り替える" (custom event) onto the folded start event card, appending it there.
step Logs/drag-rects-2
drag $(center Logs/drag-rects-2 "grip:オブジェクトの表示を切り替える") $(center Logs/drag-rects-2 "card:KEvent:0")
shoot Logs/drag-2.png; touch Logs/drag-done-2
# 3: drop the action "音を鳴らす" on the object drop area of "文字を変える" (last card): the card must move, not be eaten.
step Logs/drag-rects-3
drag $(center Logs/drag-rects-3 "grip:音を鳴らす") $(center Logs/drag-rects-3 "objects-area")
shoot Logs/drag-3.png; touch Logs/drag-done-3
# 4: Ctrl+click the grips of "音を鳴らす" and "テキストを変える" (both in the click event now), then drag one onto the
# start event card: both move there, in their order.
step Logs/drag-rects-4
for g in "grip:音を鳴らす" "grip:テキストを変える"; do
  xdotool mousemove $(center Logs/drag-rects-4 "$g"); sleep 0.3; xdotool keydown ctrl; xdotool click 1; xdotool keyup ctrl; sleep 0.5
done
drag $(center Logs/drag-rects-4 "grip:音を鳴らす") $(center Logs/drag-rects-4 "card:KEvent:0")
shoot Logs/drag-4.png; touch Logs/drag-done-4
wait $pid
for n in 0 1 2 3 4; do echo "state $n: $(cat Logs/drag-state-$n)"; done
