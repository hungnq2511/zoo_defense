#!/bin/bash
# Zoo Defense -> TestFlight, in one command (run on the Mac, from the project folder):
#   ./Tools/testflight.sh
# Steps: Unity builds the Xcode project -> xcodebuild archives -> uploads to App Store Connect.
# Upload login, pick ONE:
#   a) Xcode > Settings > Accounts: your Apple ID is signed in (simplest), or
#   b) App Store Connect API key: export ASC_KEY_ID=... ASC_ISSUER_ID=... ASC_KEY_PATH=/path/AuthKey_XXXX.p8
# Options: ZOO_TEAM_ID=XXXXXXXXXX (else BuildConfig/ios.json), UNITY=/path/to/Unity, SKIP_UNITY=1 (reuse Builds/iOS)
set -euo pipefail
cd "$(dirname "$0")/.."
PROJECT="$(pwd)"
UNITY_VERSION="$(grep m_EditorVersion: ProjectSettings/ProjectVersion.txt | awk '{print $2}')"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
OUT="$PROJECT/Builds/iOS"
ARCHIVE="$PROJECT/Builds/ZooDefense.xcarchive"
LOG="$PROJECT/Builds/unity_ios_build.log"
mkdir -p "$PROJECT/Builds"

command -v xcodebuild >/dev/null || { echo "❌ Chưa có Xcode. Cài Xcode từ App Store rồi chạy: sudo xcode-select -s /Applications/Xcode.app"; exit 1; }

if [ "${SKIP_UNITY:-0}" != "1" ]; then
  [ -x "$UNITY" ] || { echo "❌ Không thấy Unity $UNITY_VERSION ở $UNITY (đặt biến UNITY=...)"; exit 1; }
  if pgrep -f "Unity.app.*-projectPath.*$(basename "$PROJECT")" >/dev/null || [ -f "$PROJECT/Temp/UnityLockfile" ]; then
    echo "❌ Project đang mở trong Unity Editor. Đóng Editor, hoặc dùng menu Zoo Defense > iOS > Build Xcode Project rồi chạy: SKIP_UNITY=1 ./Tools/testflight.sh"
    exit 1
  fi
  echo "▶ 1/3 Unity build Xcode project (log: $LOG)…"
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" -buildTarget iOS \
    -executeMethod ZooDefense.EditorTools.IosBuild.BuildFromCommandLine -logFile "$LOG" \
    || { echo "❌ Unity build lỗi. Xem: $LOG"; grep -E "error|\[iOS\]" "$LOG" | tail -20; exit 1; }
fi
[ -d "$OUT/Unity-iPhone.xcodeproj" ] || { echo "❌ Không thấy $OUT/Unity-iPhone.xcodeproj"; exit 1; }

TEAM="${ZOO_TEAM_ID:-$(python3 -c "import json;print(json.load(open('BuildConfig/ios.json')).get('teamId',''))")}"
[ -n "$TEAM" ] || { echo "❌ Thiếu Team ID: điền teamId trong BuildConfig/ios.json"; exit 1; }

AUTH=()
if [ -n "${ASC_KEY_ID:-}" ]; then
  AUTH=(-authenticationKeyID "$ASC_KEY_ID" -authenticationKeyIssuerID "$ASC_ISSUER_ID" -authenticationKeyPath "$ASC_KEY_PATH")
fi

echo "▶ 2/3 Archive (team $TEAM)…"
rm -rf "$ARCHIVE"
# Ad SDKs come through CocoaPods: then Xcode must build the workspace, not the bare project.
if [ -f "$OUT/Podfile" ] && [ ! -d "$OUT/Unity-iPhone.xcworkspace" ]; then
  command -v pod >/dev/null || { echo "❌ Cần CocoaPods cho SDK quảng cáo: brew install cocoapods"; exit 1; }
  (cd "$OUT" && pod install)
fi
if [ -d "$OUT/Unity-iPhone.xcworkspace" ]; then SRC=(-workspace "$OUT/Unity-iPhone.xcworkspace"); else SRC=(-project "$OUT/Unity-iPhone.xcodeproj"); fi
xcodebuild "${SRC[@]}" -scheme Unity-iPhone -configuration Release \
  -destination "generic/platform=iOS" -archivePath "$ARCHIVE" \
  DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic \
  -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"} archive | tail -25

echo "▶ 3/3 Upload lên App Store Connect / TestFlight…"
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$OUT/ExportOptions.plist" \
  -exportPath "$PROJECT/Builds/export" -allowProvisioningUpdates ${AUTH[@]+"${AUTH[@]}"} | tail -25

echo "✅ Đã gửi bản build. Sau ~5–20 phút Apple xử lý xong, bản build hiện ở App Store Connect > TestFlight."
