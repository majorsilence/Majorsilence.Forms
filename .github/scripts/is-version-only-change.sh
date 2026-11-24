#!/usr/bin/env bash
# Decides whether a pull request changes nothing but the <Version> line of Directory.Build.props, and writes
# `trivial=true|false` to $GITHUB_OUTPUT. dotnet.yml and release.yml call it from their `changes` job, so a
# release's version-bump PR does not spend an hour of runner time proving that one number changed.
#
# trivial=true only when ALL of these hold:
#   - the event is a pull_request (a tag, a push to main and a manual run always build everything);
#   - the PR touches exactly one file, Directory.Build.props, and modifies it (no rename, no add);
#   - every added or removed line in its patch is a <Version>...</Version> element.
# Anything else -- including a failed API call -- is trivial=false, so the failure mode is "CI runs as it always
# did", never "CI silently did not run".
#
# A path filter cannot do this: it is per file, and Directory.Build.props carries far more than the version.
#
# The test is a `gh api --jq` expression, so it needs the GitHub CLI (on every hosted runner) and nothing else.
# per_page=2 is deliberate: one file is the only answer that can be trivial, and asking for two is enough to see
# that there are more, with no pagination to get wrong.
#
# Inputs (env): EVENT_NAME, REPO (owner/name), PR_NUMBER, GH_TOKEN (for `gh`).
# To try it on a real PR:  EVENT_NAME=pull_request REPO=majorsilence/Majorsilence.Forms PR_NUMBER=727 \
#                          GITHUB_OUTPUT=/dev/stdout bash .github/scripts/is-version-only-change.sh
set -euo pipefail

out="${GITHUB_OUTPUT:-/dev/stdout}"

decide () {
  echo "trivial=$1" >> "$out"
  echo "$2"
  exit 0
}

[ "${EVENT_NAME:-}" = "pull_request" ] || decide false "Not a pull request: running everything."

# In the API's `patch`, hunk headers start with '@', context lines with a space, changes with '+' or '-'.
answer=$(gh api "repos/${REPO}/pulls/${PR_NUMBER}/files?per_page=2" --jq '
  length == 1
  and .[0].filename == "Directory.Build.props"
  and .[0].status == "modified"
  and ((.[0].patch // "") | split("\n") | map(select(test("^[+-]"))) as $changed
       | ($changed | length) > 0
         and ($changed | all(test("^[+-]\\s*<Version>[^<]+</Version>\\s*$"))))
' 2>/dev/null) || decide false "Could not list the PR's files: running everything."

if [ "$answer" = "true" ]; then
  decide true "Only the <Version> line of Directory.Build.props changed: skipping the build and test jobs."
fi

decide false "More than the version changed: running everything."
