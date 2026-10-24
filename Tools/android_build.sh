#!/bin/bash
# Zoo Defense -> Google Play: builds a signed Android App Bundle (.aab), in one command (run on the Mac, from the project folder):
#   ./Tools/android_build.sh
# First run: creates the upload key BuildConfig/android_signing/zoodefense-upload.keystore + keystore.properties.
#   BACK UP that folder (password manager / private drive). Never commit it. If it is lost, Google Play can reset the
#   upload key (Play Console > Test and release > App integrity), but that takes a few days.
# Options: UNITY=/path/to/Unity (else the version in ProjectSettings/ProjectVersion.txt from Unity Hub)
set -euo pipefail
cd "$(dirname "$0")/.."
PROJECT="$(pwd)"
UNITY_VERSION="$(grep m_EditorVersion: ProjectSettings/ProjectVersion.txt | awk '{print $2}')"
EDITOR_DIR="/Applications/Unity/Hub/Editor/$UNITY_VERSION"
UNITY="${UNITY:-$EDITOR_DIR/Unity.app/Contents/MacOS/Unity}"
ANDROID_PLAYER="$EDITOR_DIR/PlaybackEngines/AndroidPlayer"
LOG="$PROJECT/Builds/unity_android_build.log"
SIGN_DIR="$PROJECT/BuildConfig/android_signing"
KEYSTORE="$SIGN_DIR/zoodefense-upload.keystore"
PROPS="$SIGN_DIR/keystore.properties"
mkdir -p "$PROJECT/Builds"

[ -x "$UNITY" ] || { echo "❌ Không thấy Unity $UNITY_VERSION ở $UNITY (đặt biến UNITY=...)"; exit 1; }
[ -d "$ANDROID_PLAYER" ] || { echo "❌ Chưa cài Android Build Support. Unity Hub > Installs > $UNITY_VERSION > Add modules > Android Build Support (tick cả OpenJDK và Android SDK & NDK Tools)."; exit 1; }

# 1) Upload key (once)
if [ ! -f "$KEYSTORE" ]; then
  KEYTOOL="$ANDROID_PLAYER/OpenJDK/bin/keytool"
  [ -x "$KEYTOOL" ] || KEYTOOL="$(command -v keytool || true)"
  [ -n "$KEYTOOL" ] || { echo "❌ Không thấy keytool (OpenJDK của Unity)."; exit 1; }
  mkdir -p "$SIGN_DIR"
  PASS="$(openssl rand -hex 16)"
  echo "▶ Tạo khoá upload Android lần đầu…"
  "$KEYTOOL" -genkeypair -v -keystore "$KEYSTORE" -storetype PKCS12 -alias upload -keyalg RSA -keysize 2048 \
    -validity 10000 -storepass "$PASS" -keypass "$PASS" \
    -dname "CN=Zoo Defense, O=Nutrica, C=VN" >/dev/null
  cat > "$PROPS" <<EOF
# Zoo Defense upload key. BACK THIS FOLDER UP and never share or commit it.
storeFile=zoodefense-upload.keystore
storePassword=$PASS
keyAlias=upload
keyPassword=$PASS
EOF
  chmod 600 "$PROPS" "$KEYSTORE"
  echo "✅ Đã tạo khoá: $KEYSTORE"
  echo "   ⚠️  Hãy sao lưu thư mục BuildConfig/android_signing ngay (mất khoá = phải xin Google đặt lại)."
fi
prop() { grep "^$1=" "$PROPS" | cut -d= -f2-; }
export ZOO_KEYSTORE_PATH="$KEYSTORE"
export ZOO_KEYSTORE_PASS="$(prop storePassword)"
export ZOO_KEY_ALIAS="$(prop keyAlias)"
export ZOO_KEY_PASS="$(prop keyPassword)"

# 2) Unity build
if pgrep -f "Unity.app.*-projectPath.*$(basename "$PROJECT")" >/dev/null || [ -f "$PROJECT/Temp/UnityLockfile" ]; then
  echo "❌ Project đang mở trong Unity Editor. Đóng Editor rồi chạy lại."
  exit 1
fi
echo "▶ Unity build Android App Bundle (log: $LOG)… lần đầu có thể mất 10–20 phút."
"$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" -buildTarget Android \
  -executeMethod ZooDefense.EditorTools.AndroidBuild.BuildFromCommandLine -logFile "$LOG" \
  || { echo "❌ Unity build lỗi. Xem: $LOG"; grep -E "error|\[Android\]" "$LOG" | tail -25; exit 1; }

AAB="$(cat "$PROJECT/Builds/Android/last_build.txt")"
echo "✅ Xong: $PROJECT/$AAB"
echo "   Tải file này lên Google Play Console > Test and release > (Internal / Closed testing) > Create new release."
open -R "$PROJECT/$AAB" 2>/dev/null || true
