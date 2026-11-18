# Releasing Majorsilence.Forms

How a version gets from `main` to nuget.org, and how the TownSuite products that consume it move to it.
The pipeline is automated. The steps a person has to get right are bumping `Directory.Build.props` before
tagging, checking nuget.org after publishing, and bumping both products together.

## Who

- **Release owner:** _to be named on TownSuite/TownSuite.IssueTracker#17597_. This is a role, not a person.
  The release owner decides when to cut a version, clicks **Publish release**, and opens the product bump PRs.
- **Who may click "Publish release":** the release owner, or a backup the release owner names on that
  issue. GitHub lets anyone with write access to this repository publish a draft. Limiting it to these two
  is our rule, not a GitHub permission, so keep the list of people with write access short.
- nuget.org trusts `publish-nuget.yml` through Trusted Publishing (OIDC). The policy is on the nuget.org account
  that owns the `Majorsilence.*` ids (see the header of `publish-nuget.yml`). No API key is stored anywhere.

## The pipeline

1. **Tag push** runs `.github/workflows/release.yml`. It packs every package with the tag as the version
   (`build/build.cs`, `Version` and `Pack` targets) and builds the sample zips. Then it creates a
   **draft** GitHub Release with all of them attached.
2. **A person clicks "Publish release"** on the draft. Nothing reaches nuget.org before this.
3. Publishing runs `.github/workflows/publish-nuget.yml`. It checks out the tag, packs again on Linux,
   macOS and Windows, and pushes to nuget.org with `--skip-duplicate`.

## Cutting a version

1. `main` is green, and the [four gates](../CONTRIBUTING.md#the-four-gates) pass locally.
2. **Set `<Version>` in `Directory.Build.props` to the new version** in a PR, and merge it. Use the next
   number that has never been used for either a tag or a nuget.org version.
3. Tag the merge commit with the bare version, with no `v` prefix, and push the tag:
   `git tag 26.11.18 <sha> && git push origin 26.11.18`.
4. Wait for `release.yml`. Then open the draft and check that the asset names carry the new version and
   the notes are right.
5. The release owner clicks **Publish release**.
6. **Verify on nuget.org before telling anyone the version exists.** Every package must list the version,
   and its nuspec must name the tag's commit:

   ```bash
   V=26.11.18
   for id in majorsilence.forms majorsilence.forms.avalonia majorsilence.forms.headless \
             majorsilence.forms.telerik majorsilence.forms.webdriver \
             majorsilence.forms.theming.winforms majorsilence.forms.theming.avalonia; do
     curl -sf https://api.nuget.org/v3-flatcontainer/$id/index.json | grep -q "\"$V\"" \
       && echo "ok   $id" || echo "MISSING $id"
   done
   ```

   A green `publish-nuget.yml` run is **not** proof. Because of `--skip-duplicate`, a run in which every
   push was refused as `Conflict ... already exists` still reports success. New versions can take a few
   minutes to appear in the index.

### Why step 2 matters (26.11.13 and 26.11.17)

`release.yml` runs on a tag **push**, so it packs with the tag's version. `publish-nuget.yml` runs on a
`release` event, which `build/build.cs` did not treat as a tag push. It packed with the version in
`Directory.Build.props`. When a tag was pushed before that file was bumped, the two disagreed:

- Tag **26.11.13** had `26.11.12` in the props. `publish-nuget.yml` packed 26.11.12, nuget.org refused every
  package as a duplicate, and the run still passed. **26.11.13 was never published to nuget.org.**
- Tag **26.11.17** had `26.11.16` in the props. The draft's assets are named 26.11.17, but the code at that
  tag went to nuget.org as **26.11.16**. nuget.org's 26.11.16 is the 26.11.17 release, and there is no
  26.11.17 on nuget.org and no 26.11.16 tag.

`build/build.cs` now treats the `release` event as a tag build, and `publish-nuget.yml` hands the tag to it as
`PACK_VERSION`, so both workflows pack with the tag's version. Before pushing, `publish-nuget.yml` also checks
that every package carries the tag's version and fails if one does not. These fixes only apply to tags that
contain them, because the workflow and build script are read from the tag. Keep bumping
`Directory.Build.props` anyway. It is the version every untagged build, local pack and `LocalNugetPackages`
drop carries, and a props version that matches the tag removes doubt.

nuget.org versions are immutable: a version that is already there cannot be replaced. A release whose
packages never reached nuget.org at its own version (26.11.13, 26.11.17) can still get them: run
**Publish to nuget.org** by hand (Actions -> Run workflow, on `main`) and name the tag; `--skip-duplicate`
leaves anything already there alone. Otherwise find out what nuget.org actually holds (step 6, plus the
nuspec's `repository commit`), point the products at that number, and give the next release a number that
has not been used for a tag or on nuget.org.

## Bumping TownSuite Financial and Planning

Both products ship in the same merged desktop, so they must never pin different versions.

| Product | Where the version lives |
|---|---|
| TownSuite/Financial | `MajorsilenceFormsVersion` in `Code/Directory.Packages.props` |
| TownSuite/Planning (with Mapping) | `MajorsilenceFormsVersion` in the root `Directory.Packages.props` |

1. Only bump to a version you have verified on nuget.org (step 6 above).
2. Read the new nuspecs' dependency floors. `SkiaSharp`, `Avalonia*` and `Avalonia.Controls.*` are the ones
   the products also pin directly. If a floor rose, raise the product's direct pin in the same PR. A direct
   pin below the floor is an `NU1605` downgrade error on the `net10.0` flavour.

   ```bash
   curl -sLO https://api.nuget.org/v3-flatcontainer/majorsilence.forms.avalonia/$V/majorsilence.forms.avalonia.$V.nupkg
   unzip -p majorsilence.forms.avalonia.$V.nupkg '*.nuspec' | grep '<dependency'
   ```

3. Open the Financial and Planning bump PRs **together**, with the same version, referencing the same issue.
4. Run the gates on each before merging either PR:
   - Financial: `dotnet build Code/TownSuite.slnx`. Off Windows, also run
     `dotnet build Code/TownSuite.Winform -f net10.0 -p:IncludeNet48=false` and
     `dotnet build Code/TownSuite -f net10.0 -p:IncludeNet48=false` (see `Code/CLAUDE.md`).
   - Planning: `dotnet build Planning/TS_Planning.sln`, and
     `dotnet test Mapping/TownSuite.Mapping.TatukGis.MajorsilenceForms.Tests`.
5. Merge both in the **same release train**. A breaking change in this library lands in both products at once,
   so if either product's gates or smoke tests fail, hold both PRs.
6. For QA: in the installed Financial and Planning folders, the file version of `Majorsilence.Forms.dll` must be
   the same.

The release pace is high, with a version every few days. The release owner decides whether every release is
bumped into the products or only one per release train. Either way, the two products move together.
