#!/usr/bin/env bash
#
# Runs the blocking-modal head checks (samples/Gallery.Wasm/ModalCheckForm.cs, issue #406) in a booted
# iOS simulator, one app launch per check, and prints each check's MFCHECK lines from the app's stdout
# plus a one-line summary.
#
# Build and install the simulator head first, e.g.:
#   dotnet build samples/Gallery.iOS -p:EnableIOSTarget=true -p:RuntimeIdentifier=iossimulator-arm64
#   xcrun simctl install booted samples/Gallery.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/Gallery.iOS.app
# Usage: modal-check.sh [check ...]     (default: every blocking check and its awaitable form)
# SECONDS_PER_CHECK (default 15) must exceed the form's watchdog (ModalCheckForm.HangSeconds, 10 s) for a
# hang to be reported as HUNG.

set -uo pipefail

BUNDLE="com.majorsilence.gallery"
WAIT="${SECONDS_PER_CHECK:-15}"
CHECKS=("$@")
[ ${#CHECKS[@]} -gt 0 ] || CHECKS=(showdialog messagebox commondialog taskdialog showdialogasync messageboxasync taskdialogasync)

LOG="$(mktemp -t mf-modal-check)"
trap 'rm -f "$LOG"' EXIT

summary=()
for check in "${CHECKS[@]}"; do
  # SIMCTL_CHILD_* is passed to the app's environment without the prefix; AppDelegate reads MF_CHECK.
  SIMCTL_CHILD_MF_CHECK="$check" xcrun simctl launch --console-pty --terminate-running-process booted "$BUNDLE" >"$LOG" 2>&1 &
  launcher=$!
  sleep "$WAIT"
  kill "$launcher" 2>/dev/null
  wait "$launcher" 2>/dev/null
  # Backgrounded: simctl terminate has been seen to block when the app is already gone.
  (xcrun simctl terminate booted "$BUNDLE" >/dev/null 2>&1 &)

  lines="$(grep -a 'MFCHECK ' "$LOG" | sed 's/^.*MFCHECK /MFCHECK /')"
  echo "== $check"
  echo "$lines"
  outcome="$(echo "$lines" | grep -aE '^MFCHECK (RETURNED|THREW|HUNG)' | head -n1 | sed 's/^MFCHECK //')"
  summary+=("$(printf '%-16s %s' "$check" "${outcome:-NO OUTCOME (did the app start?)}")")
  sleep 2
done

echo
echo "Summary"
printf '  %s\n' "${summary[@]}"
