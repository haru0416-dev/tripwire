#!/usr/bin/env bash
# Activate a Unity Personal seat on this machine via the licensing client bundled with the editor.
# Run it in your own terminal: the password is read without echo and never stored.
# The seat stays used until returned: scripts/unity-activate.sh --return
set -euo pipefail
CLIENT="$HOME/Unity/Editors/2022.3.22f1/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client"
export LD_LIBRARY_PATH="$HOME/Unity/compat-lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

if [[ "${1:-}" == "--return" ]]; then
  exec "$CLIENT" --return-ulf
fi

IFS= read -rp "Unity ID email: " UNITY_EMAIL
UNITY_EMAIL="${UNITY_EMAIL//[[:space:]]/}"
IFS= read -rsp "Unity ID password: " UNITY_PASSWORD; echo
"$CLIENT" --activate-all --include-personal --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"
unset UNITY_PASSWORD
echo "--- entitlements ---"
"$CLIENT" --showEntitlements | head -20
