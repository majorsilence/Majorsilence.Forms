#!/usr/bin/env bash
#
# Runs the blocking-modal head checks (samples/Gallery.Wasm/ModalCheckForm.cs, issue #406) on an
# already-running Android emulator or device, one app launch per check, and prints each check's
# MFCHECK lines from logcat (tag MFCHECK) plus a one-line summary.
#
# Install the Debug head first:  dotnet build samples/Gallery.Android -t:Install -p:EnableAndroidTarget=true
# Usage: modal-check.sh [check ...]     (default: every blocking check and its awaitable form)
# Assumes adb is on PATH and exactly one device is attached. SECONDS_PER_CHECK (default 15) must exceed
# the form's watchdog (ModalCheckForm.HangSeconds, 10 s) for a hang to be reported as HUNG.

set -uo pipefail

PKG="com.majorsilence.gallery"
WAIT="${SECONDS_PER_CHECK:-15}"
CHECKS=("$@")
[ ${#CHECKS[@]} -gt 0 ] || CHECKS=(showdialog messagebox commondialog taskdialog showdialogasync messageboxasync taskdialogasync)

ACTIVITY="$(adb shell cmd package resolve-activity --brief "$PKG" | tail -n1 | tr -d '\r')"
[[ "$ACTIVITY" == "$PKG/"* ]] || { echo "FAIL: $PKG is not installed" >&2; exit 1; }

summary=()
for check in "${CHECKS[@]}"; do
  adb shell am force-stop "$PKG"
  adb logcat -c
  # -S restarts the app, so a picker or dialog a previous check left up cannot swallow this launch.
  adb shell am start -S --activity-clear-task -n "$ACTIVITY" -e check "$check" >/dev/null
  sleep "$WAIT"
  lines="$(adb logcat -d -s MFCHECK:I | grep -a 'MFCHECK ' | sed 's/^.*MFCHECK *: //')"
  adb shell am force-stop "$PKG"

  echo "== $check"
  echo "$lines"
  outcome="$(echo "$lines" | grep -aE '^MFCHECK (RETURNED|THREW|HUNG)' | head -n1 | sed 's/^MFCHECK //')"
  summary+=("$(printf '%-16s %s' "$check" "${outcome:-NO OUTCOME (did the app start?)}")")
done

echo
echo "Summary"
printf '  %s\n' "${summary[@]}"
