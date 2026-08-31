#!/usr/bin/env bash
#
# Emulator smoke test for the published samples/Gallery.Android APK.
#
# The CI "android" job succeeding only proves the android workload could compile and package the head
# -- it says nothing about whether the APK actually boots. The startup crash that motivated moving the
# head into the solution (MainActivity needed an AppCompat-derived theme) was device-only: it compiled
# and packaged fine and only blew up on launch. This script installs the APK on an already-booted
# emulator, launches it, and fails if the process dies or logcat shows a fatal exception / ANR during
# the first ~25 seconds.
#
# Usage: android-smoke-test.sh <apk-or-dir> [screenshot-output-path]
#   <apk-or-dir>  a *-Signed.apk file, or a directory to search for one (recursively).
# Assumes `adb` is on PATH and exactly one emulator/device is attached (the emulator-runner action
# guarantees this). Kept to a single argument that resolves the APK itself, because the
# emulator-runner action runs each `script:` line as its own shell -- a `$(...)` from one line does
# not survive to the next.

set -uo pipefail

TARGET="${1:?usage: android-smoke-test.sh <apk-or-dir> [screenshot.png]}"
SHOT="${2:-}"
PKG="com.majorsilence.gallery"
SETTLE_SECONDS=25

if [ -d "$TARGET" ]; then
  APK="$(find "$TARGET" -name '*-Signed.apk' | head -n1)"
  [ -n "$APK" ] || { echo "FAIL: no *-Signed.apk under $TARGET" >&2; exit 1; }
else
  APK="$TARGET"
fi
[ -f "$APK" ] || { echo "FAIL: APK not found: $APK" >&2; exit 1; }

fail() { echo "FAIL: $*" >&2; dump_diagnostics; exit 1; }

dump_diagnostics() {
  echo "----- last 200 logcat lines -----" >&2
  adb logcat -d -t 200 2>/dev/null >&2 || true
  echo "----- activity state -----" >&2
  adb shell dumpsys activity activities 2>/dev/null | grep -iE "mResumedActivity|mFocusedApp|$PKG" >&2 || true
}

echo "Waiting for the emulator to finish booting..."
adb wait-for-device
until [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = "1" ]; do sleep 2; done
adb shell input keyevent 82 >/dev/null 2>&1 || true   # dismiss the lock screen if present

echo "Installing $APK ..."
adb install -r -g "$APK" || fail "adb install returned non-zero"

echo "Clearing logcat and launching $PKG ..."
adb logcat -c || true
# monkey resolves and starts the LAUNCHER activity without needing the (Xamarin-mangled) class name.
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1 || fail "monkey could not launch $PKG"

echo "Letting it run for ${SETTLE_SECONDS}s ..."
for i in $(seq 1 "$SETTLE_SECONDS"); do
  sleep 1
  if ! adb shell pidof "$PKG" >/dev/null 2>&1; then
    fail "process $PKG is no longer running after ${i}s -- it crashed or was killed on startup"
  fi
done

echo "Scanning logcat for fatal signals ..."
LOG="$(adb logcat -d 2>/dev/null)"
if grep -qE "FATAL EXCEPTION|E AndroidRuntime|ANR in $PKG|Force finishing activity .*$PKG" <<<"$LOG"; then
  echo "$LOG" | grep -E "FATAL EXCEPTION|AndroidRuntime|ANR in $PKG|Force finishing" -A 20 >&2
  fail "logcat shows a fatal exception / ANR for $PKG"
fi

# The scene draws into an Avalonia SurfaceView; if MainActivity threw during OnCreate the emulator
# would be showing the launcher, not our package, so confirm we own the foreground.
TOP="$(adb shell dumpsys activity activities 2>/dev/null | grep -m1 -iE 'mResumedActivity|topResumedActivity' || true)"
echo "Foreground activity: $TOP"
case "$TOP" in
  *"$PKG"*) : ;;
  "") echo "WARNING: could not read the resumed activity from dumpsys; relying on the crash checks above" >&2 ;;
  *) fail "$PKG is not the foreground activity after launch (got: $TOP)" ;;
esac

if [ -n "$SHOT" ]; then
  adb exec-out screencap -p > "$SHOT" 2>/dev/null && echo "Saved screenshot to $SHOT" || echo "WARNING: screencap failed" >&2
fi

echo "PASS: $PKG installed, launched, stayed alive ${SETTLE_SECONDS}s with no fatal exception, and held the foreground."
