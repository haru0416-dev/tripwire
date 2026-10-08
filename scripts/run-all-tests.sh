#!/bin/bash
# Run every Tripwire test (dotnet Core + Unity EditMode/PlayMode) and write a summary to Logs/suite-summary.txt.
# Survives session loss when started with: systemd-run --user --unit=tripwire-tests -p MemoryMax=9G scripts/run-all-tests.sh
# (MemoryMax stops a runaway editor instead of starving the rest of the VPS.)
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$HOME/.local/share/mise/shims:$PATH"
summary=Logs/suite-summary.txt
echo "started $(date -Is)" > $summary
core=$(cd tests/Tripwire.Core.Tests && mise exec -- dotnet test 2>&1)
# A build error prints neither a pass nor a fail line: say so instead of leaving the Core line out.
echo "$core" | grep -E "成功!|失敗!|Passed!|Failed!" >> $summary || { echo "core BUILD FAILED"; echo "$core" | grep -E " error " | sort -u | head -5; } >> $summary
# Unity runs in batches, each in a fresh editor: one editor running every test grew to ~9 GB on this 11 GB VPS
# once the preset assets were installed. Each batch's peak memory goes into the summary.
batch() {
  local name=$1 filter=$2
  rm -rf Assets/TripwireTests/Temp Assets/TripwireTests/Temp.meta Assets/TripwireGenerated Assets/TripwireGenerated.meta Logs/$name-results.xml
  # The editor now and then hangs right after loading (the log stops at "Unloading ... Unused Serialized files"),
  # before any test runs: each batch runs in its own systemd scope that is stopped after 15 minutes, which ends the
  # whole process tree (xvfb-run, Xvfb, the editor). A batch that ran that long is retried once.
  local code start
  for attempt in 1 2; do
    start=$(date +%s)
    /usr/bin/time -f "%M" -o Logs/$name-maxrss.txt systemd-run --user --scope --quiet -p RuntimeMaxSec=900 -p TimeoutStopSec=30 -p MemoryMax=9G \
      xvfb-run -a -s "-screen 0 1280x720x24" unity-2022 -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
      -testFilter "$filter" -testResults "$PWD/Logs/$name-results.xml" -logFile Logs/$name.log
    code=$?
    # A stopped scope's editor can take a few seconds to exit and still holds the project: the next run would fail with
    # "another Unity instance is running". Wait for it (up to a minute).
    for i in $(seq 1 60); do pgrep -f "Unity.*-projectPath $PWD" >/dev/null || break; sleep 1; done
    [ $(( $(date +%s) - start )) -lt 900 ] && break
    echo "unity[$name] TIMEOUT after 15 min (attempt $attempt)" >> $summary
  done
  local rss
  rss=$(tail -1 Logs/$name-maxrss.txt 2>/dev/null | tr -cd '0-9')  # empty when the run was killed
  echo "unity[$name] exit=$code peak=$(( ${rss:-0} / 1024 ))MB" >> $summary
  python3 scripts/lib/test-results.py Logs/$name-results.xml $name >> $summary
}
batch api "Tripwire.Tests.UdonApiCompileTests;Tripwire.Tests.UnityMathTests;Tripwire.Tests.InspectorEditingTests;Tripwire.Tests.UdonSharpApiTests;Tripwire.Tests.SceneLoopTests;Tripwire.Tests.ExposureAgreement;Tripwire.Tests.VariableRenameTests;Tripwire.Tests.ApplyTrustTests;Tripwire.Tests.DataSafetyTests;Tripwire.Tests.MenuLanguageTests;Tripwire.Tests.ExportTests"
batch scenarios "Tripwire.Tests.ScenarioTests;Tripwire.Tests.ReapplyTests;Tripwire.Tests.FuzzTests"
batch assets "Tripwire.Tests.AssetPresetTests"
# Udon IDE (and udon-bridge's check and hot reload): editing, navigation, play-mode hot reload.
batch ide "UdonIde.Tests.EditingTests;UdonIde.Tests.NavigationTests;UdonIde.Tests.HotReloadTests;UdonIde.Tests.WindowFileTests"
echo "finished $(date -Is)" >> $summary
