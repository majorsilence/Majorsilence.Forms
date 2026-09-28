#!/usr/bin/env bash
#
# Emulator smoke test for the published samples/Gallery.Android APK.
#
# The CI "android" job succeeding only proves the android workload could compile and package the head
# -- it says nothing about whether the APK actually boots. The startup crash that motivated moving the
# head into the solution (MainActivity needed an AppCompat-derived theme) was device-only: it compiled
# and packaged fine and only blew up on launch. This script installs the APK on an already-booted
# emulator, launches it, and fails if the process dies or logcat shows a fatal exception / ANR during
# the first ~25 seconds -- or if GalleryApplication's F8_AUDIO_SMOKE or F9_AUDIOPLAYER_SMOKE self-tests
# (register items F8/F9: IAudioBackend and AudioPlayer on Android) do not report PASS in that window. It
# then backgrounds the app and brings it back (KEYCODE_HOME, then relaunch) and fails if
# Application.Suspended/Resumed and the single-view host's Form.Activated/Deactivate (register item F10)
# do not both fire, in the right order, on that real transition.
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

# GalleryApplication.RunAudioSmokeTest (register item F8): plays a bundled .wav and a system sound
# through IAudioBackend on a background thread and logs one PASS/FAIL line. A missing line means it
# never even ran within the settle window, which is its own failure, not silently ignored.
echo "Checking for the F8 audio smoke-test result ..."
if grep -q "F8_AUDIO_SMOKE.*FAIL" <<<"$LOG"; then
  echo "$LOG" | grep "F8_AUDIO_SMOKE" >&2
  fail "F8 audio smoke test reported FAIL (see F8_AUDIO_SMOKE lines above)"
elif grep -q "F8_AUDIO_SMOKE.*PASS" <<<"$LOG"; then
  echo "F8 audio smoke test: PASS"
else
  echo "$LOG" | grep "F8_AUDIO_SMOKE" >&2 || true
  fail "no F8_AUDIO_SMOKE PASS line in logcat within ${SETTLE_SECONDS}s"
fi

# GalleryApplication.RunAudioPlayerSmokeTest (register item F9): AudioPlayer's volume/loop/Usage/Completed
# surface, same reasoning as F8_AUDIO_SMOKE above.
echo "Checking for the F9 AudioPlayer smoke-test result ..."
if grep -q "F9_AUDIOPLAYER_SMOKE.*FAIL" <<<"$LOG"; then
  echo "$LOG" | grep "F9_AUDIOPLAYER_SMOKE" >&2
  fail "F9 AudioPlayer smoke test reported FAIL (see F9_AUDIOPLAYER_SMOKE lines above)"
elif grep -q "F9_AUDIOPLAYER_SMOKE.*PASS" <<<"$LOG"; then
  echo "F9 AudioPlayer smoke test: PASS"
else
  echo "$LOG" | grep "F9_AUDIOPLAYER_SMOKE" >&2 || true
  fail "no F9_AUDIOPLAYER_SMOKE PASS line in logcat within ${SETTLE_SECONDS}s"
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

# Register item F10: Application.Suspended/Resumed and the single-view root host's Form.Activated/
# Deactivate. Nothing in-process can trigger a real background/foreground transition, so this drives
# one externally -- KEYCODE_HOME, then relaunch via monkey -- and greps logcat for both pairs of
# GalleryApplication's F10_LIFECYCLE log lines (see App.cs).
echo "Backgrounding $PKG (KEYCODE_HOME) ..."
adb shell input keyevent KEYCODE_HOME || fail "could not send KEYCODE_HOME"
sleep 3

LOG="$(adb logcat -d 2>/dev/null)"
if ! grep -q "F10_LIFECYCLE.*Suspended" <<<"$LOG"; then
  echo "$LOG" | grep "F10_LIFECYCLE" >&2 || true
  fail "no F10_LIFECYCLE: Suspended line in logcat after backgrounding $PKG"
fi
if ! grep -q "F10_LIFECYCLE.*Form.Deactivate" <<<"$LOG"; then
  echo "$LOG" | grep "F10_LIFECYCLE" >&2 || true
  fail "no F10_LIFECYCLE: Form.Deactivate line in logcat after backgrounding $PKG"
fi
echo "F10 lifecycle (background): Suspended and Form.Deactivate both fired"

echo "Foregrounding $PKG again ..."
adb logcat -c || true
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1 || fail "monkey could not relaunch $PKG"
sleep 3

LOG="$(adb logcat -d 2>/dev/null)"
if ! grep -q "F10_LIFECYCLE.*Resumed" <<<"$LOG"; then
  echo "$LOG" | grep "F10_LIFECYCLE" >&2 || true
  fail "no F10_LIFECYCLE: Resumed line in logcat after foregrounding $PKG"
fi
if ! grep -q "F10_LIFECYCLE.*Form.Activated" <<<"$LOG"; then
  echo "$LOG" | grep "F10_LIFECYCLE" >&2 || true
  fail "no F10_LIFECYCLE: Form.Activated line in logcat after foregrounding $PKG"
fi
echo "F10 lifecycle (foreground): Resumed and Form.Activated both fired"

echo "PASS: $PKG installed, launched, stayed alive ${SETTLE_SECONDS}s with no fatal exception, held the foreground, the F8/F9 audio smoke tests passed, and F10's Suspended/Resumed + Form.Activated/Deactivate all fired on a real background/foreground cycle."
