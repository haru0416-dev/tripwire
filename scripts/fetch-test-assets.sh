#!/bin/bash
# Installs real third-party world assets into this dev project, for the preset tests only (AssetPresetTests).
# They are not part of Tripwire and are never committed (.gitignore): packages go to Packages/<name>/,
# USharpVideo (no package) to Assets/TripwireTestAssets/. Pinned versions, checked by SHA-256.
# Usage: scripts/fetch-test-assets.sh        (remove: scripts/fetch-test-assets.sh --remove)
set -euo pipefail
cd "$(dirname "$0")/.."

PACKAGES=(
  # name version sha256 url
  "dev.architech.sdk 0.22.0 7aa2ebbfacf92d720065b8785fe5aa1c6d5fd6955cd1277119cf50e432d2354d https://gitlab.com/api/v4/projects/39463625/packages/generic/ArchiTech.SDK/v0.22.0/ArchiTech.SDK_v0.22.0.zip"
  "dev.architech.protv 3.0.0-beta.29.4 32293c451df1462a17016b5359a8fb2e2cf960633ef25c8b1a6bc228ac989d73 https://gitlab.com/api/v4/projects/27741679/packages/generic/ArchiTech.ProTV/v3.0.0-beta.29.4/ArchiTech.ProTV_v3.0.0-beta.29.4.zip"
  "idv.jlchntoz.vrcw-foundation 0.0.68 f46c75153cfe12af642ce093f001183a0d0514a571c28e5a96ee6ba92af30ffb https://github.com/JLChnToZ/vrcw-foundation/releases/download/0.0.68/idv.jlchntoz.vrcw-foundation-0.0.68.zip"
  "idv.jlchntoz.lazyswitch 0.10.2 73c0ce0df8ee1009c884b65e077045a74c2b71d38ec1015270cc96b2d41f1fe0 https://github.com/JLChnToZ/lazyswitch/releases/download/0.10.2/idv.jlchntoz.lazyswitch-0.10.2.zip"
  "idv.jlchntoz.vvmw 1.8.2 c3867d3ad568b0d52ab9b9efcf982ae7757e77eae2acd4a79872587b7a6a3dcb https://github.com/JLChnToZ/VVMW/releases/download/1.8.2/idv.jlchntoz.vvmw-1.8.2.zip"
  "com.texelsaur.common 2.1.1 b1a045d8fde68a8f1fadbeb5852ee1c4f9564f903bde0866e6ce24998e9cac31 https://github.com/vrctxl/CommonTXL/releases/download/2.1.1/com.texelsaur.common-2.1.1.zip"
  "com.texelsaur.video 2.5.1 7be76dad5b0ed1c462238f71f282fee53178dc5428c2c9cc7e284674a3fd812a https://github.com/vrctxl/VideoTXL/releases/download/2.5.1/com.texelsaur.video-2.5.1.zip"
)
USHARPVIDEO_COMMIT=d91aa97a3a89aa844ae186405b244d754775808b # tag v1.0.1

if [ "${1:-}" = "--remove" ]; then
  for p in "${PACKAGES[@]}"; do rm -rf "Packages/${p%% *}"; done
  rm -rf Assets/TripwireTestAssets Assets/TripwireTestAssets.meta
  echo "removed"; exit 0
fi

tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
for p in "${PACKAGES[@]}"; do
  read -r name version sha url <<<"$p"
  if [ -f "Packages/$name/package.json" ] && grep -q "\"version\": *\"$version\"" "Packages/$name/package.json"; then echo "$name $version: present"; continue; fi
  curl -sfL "$url" -o "$tmp/$name.zip"
  echo "$sha  $tmp/$name.zip" | sha256sum -c --quiet
  rm -rf "Packages/$name"; mkdir -p "Packages/$name"
  unzip -q "$tmp/$name.zip" -d "Packages/$name"
  echo "$name $version: installed"
done

if [ ! -d Assets/TripwireTestAssets/USharpVideo ]; then
  git -C "$tmp" init -q usv && git -C "$tmp/usv" fetch -q --depth 1 https://github.com/MerlinVR/USharpVideo "$USHARPVIDEO_COMMIT"
  git -C "$tmp/usv" checkout -q FETCH_HEAD
  mkdir -p Assets/TripwireTestAssets && cp -r "$tmp/usv/Assets/USharpVideo" Assets/TripwireTestAssets/
  echo "USharpVideo v1.0.1: installed"
else
  echo "USharpVideo: present"
fi
