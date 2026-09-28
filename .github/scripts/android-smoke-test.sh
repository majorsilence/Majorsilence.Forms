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
# do not both fire, in the right order, on that real transition. Finally it presses KEYCODE_BACK twice:
# once with GalleryAvaloniaApp's startup popup ("sheet") open, which must cancel and close only the
# popup and keep the app foreground (register item F11's acceptance criterion), and once more with no
# popup left, which must proceed to exit like the platform's own default back behaviour. It also checks
# GalleryApplication's F13_HAPTICS_SMOKE result -- Haptics.IsSupported true and Tap/Impact/Vibrate all
# running with no exception -- though the acceptance criterion for feeling anything real needs a human on
# a real phone: this emulator has no vibrator to prove that part. It also posts an F14 notification
# (channel, importance, permission, ongoing, full-screen intent), confirms via dumpsys that it actually
# posted, and replays a notification tap to prove LocalNotifications.Tapped fires.
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

# -g above is documented to grant every runtime permission the manifest declares, but a real run on this
# API 34 image showed the F14 smoke notification silently dropped (Show() itself never throws -- Android
# 13+'s own contract for a missing POST_NOTIFICATIONS grant is to no-op, not raise) even though -g was
# passed, so this grants it a second, explicit way, belt-and-braces. Harmless if -g already covered it.
adb shell pm grant "$PKG" android.permission.POST_NOTIFICATIONS 2>/dev/null || true

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

# GalleryApplication.RunHapticsSmokeTest (register item F13): proves the plumbing (IsSupported true,
# Tap/Impact/Vibrate all run with no exception) -- this emulator has no vibrator, so it cannot prove
# anything was actually felt; that half of the acceptance criterion needs a human on a real phone.
echo "Checking for the F13 Haptics smoke-test result ..."
if grep -q "F13_HAPTICS_SMOKE.*FAIL" <<<"$LOG"; then
  echo "$LOG" | grep "F13_HAPTICS_SMOKE" >&2
  fail "F13 Haptics smoke test reported FAIL (see F13_HAPTICS_SMOKE lines above)"
elif grep -q "F13_HAPTICS_SMOKE.*PASS" <<<"$LOG"; then
  echo "F13 Haptics smoke test: PASS"
else
  echo "$LOG" | grep "F13_HAPTICS_SMOKE" >&2 || true
  fail "no F13_HAPTICS_SMOKE PASS line in logcat within ${SETTLE_SECONDS}s"
fi

# GalleryApplication.RunNotificationsSmokeTest (register item F14, Android half): channels, importance,
# permission, an ongoing full-screen-intent notification, and the tap callback. Unlike F13, this emulator
# has a real notification centre, so this independently confirms via dumpsys that the notification
# actually posted -- not just that Show() didn't throw -- and drives the tap callback for real by
# replaying the exact launch intent a tap's PendingIntent sends (a literal notification-shade gesture is
# not simulated here, the same "input event, not a pixel-coordinate swipe" idiom F10/F11 already use).
# adb install -g above already grants every runtime permission the manifest declares, POST_NOTIFICATIONS
# included, so there is no interactive system dialog to work around here.
echo "Checking for the F14 Notifications smoke-test result ..."
# AndroidNotificationBackend logs its own caught exceptions under MajorsilenceFormsNotifications, a
# separate tag from the app's own F14_NOTIFICATIONS_SMOKE -- grepped here too so a silently-caught
# exception (Show() itself never throws back to the app) is visible in CI, not just locally.
if grep -q "F14_NOTIFICATIONS_SMOKE.*FAIL" <<<"$LOG"; then
  echo "$LOG" | grep -E "F14_NOTIFICATIONS_SMOKE|MajorsilenceFormsNotifications" >&2
  fail "F14 Notifications smoke test reported FAIL (see F14_NOTIFICATIONS_SMOKE lines above)"
elif grep -q "F14_NOTIFICATIONS_SMOKE.*PASS" <<<"$LOG"; then
  echo "F14 Notifications smoke test: PASS"
else
  echo "$LOG" | grep -E "F14_NOTIFICATIONS_SMOKE|MajorsilenceFormsNotifications" >&2 || true
  fail "no F14_NOTIFICATIONS_SMOKE PASS line in logcat within ${SETTLE_SECONDS}s"
fi

echo "Confirming the F14 smoke notification actually posted (dumpsys notification) ..."
NOTIF_DUMP="$(adb shell dumpsys notification --noredact 2>/dev/null || true)"
dump_notification_diagnostics() {
  echo "$NOTIF_DUMP" >&2
  echo "----- $PKG POST_NOTIFICATIONS grant state -----" >&2
  adb shell dumpsys package "$PKG" 2>/dev/null | grep -A2 POST_NOTIFICATIONS >&2 || true
}
if ! grep -q "f14-smoke" <<<"$NOTIF_DUMP"; then
  dump_notification_diagnostics
  fail "no posted notification found on channel f14-smoke in dumpsys notification"
fi
if ! grep -q "F14 smoke test" <<<"$NOTIF_DUMP"; then
  dump_notification_diagnostics
  fail "the posted notification's title is missing from dumpsys notification"
fi
if ! grep -q "fullscreenIntent=PendingIntent" <<<"$NOTIF_DUMP"; then
  dump_notification_diagnostics
  fail "the posted notification has no fullscreenIntent in dumpsys notification"
fi
echo "F14 notification confirmed posted: channel, title and full-screen intent all present"

# AndroidNotificationBackend.TappedExtraKey's literal value, embedded here rather than read from
# anywhere: changing that constant must update this line too, or this check silently stops proving
# anything.
echo "Replaying a notification tap (register item F14's tap callback) ..."
adb logcat -c || true
# --activity-single-top forces Intent.FLAG_ACTIVITY_SINGLE_TOP: without it, `am start` targeting a task
# already at the front is a pure no-op ("Warning: Activity not started, its current task has been brought
# to the front") that never reaches OnNewIntent at all -- confirmed by a real CI run where nothing (not
# even that warning) showed up in logcat afterward, not guessed. Output is not discarded this time, so a
# repeat failure's own `am` output -- not just a second silent absence -- lands in the CI log.
AM_OUTPUT="$(adb shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -p "$PKG" --activity-single-top --ei majorsilence_forms_notification_tapped_id 1001 2>&1)"
AM_STATUS=$?
echo "$AM_OUTPUT"
[ "$AM_STATUS" -eq 0 ] || fail "could not replay the notification tap intent"
sleep 3

LOG="$(adb logcat -d 2>/dev/null)"
if ! grep -q "F14_NOTIFICATIONS_SMOKE.*Tapped:1001" <<<"$LOG"; then
  # MainActivity.OnCreate/OnNewIntent both log F14_MAINACTIVITY_DEBUG with whether the replayed extra
  # actually arrived -- dumped in full here (not just grepped) since the generic last-200-lines dump in
  # fail()'s own diagnostics has shown up empty before, apparently racing emulator teardown.
  echo "----- full logcat since the tap replay -----" >&2
  echo "$LOG" >&2
  fail "no F14_NOTIFICATIONS_SMOKE Tapped:1001 line in logcat after replaying the tap intent"
fi
echo "F14 tap callback: LocalNotifications.Tapped fired with the right id"

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

# Register item F11: the platform back button/gesture. GalleryAvaloniaApp shows a small PopupWindow
# ("sheet") right after MainForm.Shown (see App.cs). The acceptance criterion is specifically "closes a
# sheet without leaving the app", not just "does something on back", so this checks the foreground
# activity (not just process liveness -- Android can leave a finished activity's process resident) both
# times: still $PKG after the first press (popup cancelled it), no longer $PKG after the second (nothing
# left to cancel it, so the platform's default back behaviour proceeded).
echo "Pressing back once (popup open) ..."
adb logcat -c || true
adb shell input keyevent KEYCODE_BACK || fail "could not send KEYCODE_BACK"
sleep 2

TOP="$(adb shell dumpsys activity activities 2>/dev/null | grep -m1 -iE 'mResumedActivity|topResumedActivity' || true)"
case "$TOP" in
  *"$PKG"*) : ;;
  *) fail "$PKG is no longer the foreground activity after the first back press (got: $TOP) -- the popup's BackRequested should have cancelled it and kept the app open" ;;
esac

LOG="$(adb logcat -d 2>/dev/null)"
if ! grep -q "F11_BACKBUTTON_SMOKE.*cancelled" <<<"$LOG"; then
  echo "$LOG" | grep "F11_BACKBUTTON_SMOKE" >&2 || true
  fail "no F11_BACKBUTTON_SMOKE cancelled line in logcat after the first back press"
fi
echo "F11 back button (popup open): cancelled, app still foreground"

echo "Pressing back again (no popup open) ..."
adb shell input keyevent KEYCODE_BACK || fail "could not send KEYCODE_BACK"
sleep 2

TOP="$(adb shell dumpsys activity activities 2>/dev/null | grep -m1 -iE 'mResumedActivity|topResumedActivity' || true)"
case "$TOP" in
  *"$PKG"*) fail "$PKG is still the foreground activity after the second back press (got: $TOP) -- with no popup open, back should proceed to exit like normal platform behaviour" ;;
  *) : ;;
esac
echo "F11 back button (no popup): unhandled, app exited normally"

echo "PASS: $PKG installed, launched, stayed alive ${SETTLE_SECONDS}s with no fatal exception, held the foreground, the F8/F9 audio smoke tests passed, F10's Suspended/Resumed + Form.Activated/Deactivate all fired on a real background/foreground cycle, F11's back button closed the popup then exited the app, F13's Haptics plumbing (IsSupported, Tap/Impact/Vibrate) ran with no exception, and F14's notification posted with the right channel/title/full-screen-intent and its tap callback fired."
