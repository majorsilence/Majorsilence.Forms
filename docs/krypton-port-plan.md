# Krypton Standard Toolkit port — remaining work

All seven Krypton projects compile against Majorsilence.Forms (verified in a mirror). The decision on
record: `Form : WindowBase` stays; the form/control seam is bridged **Krypton-side** by
`tools/fixups/krypton-standard-toolkit-bridge.py` (idempotent; run at the `Source/Krypton Components`
root). Each task below is independent and sized for a single session.

## 1. Bring the real Standard-Toolkit tree to green (mechanical)

The real clone is migrated in place but was migrated before the `System.Media` mapping existed and
still has Windows TFMs. In order:

1. `Source/Krypton Components/Directory.Build.props`: every `<TargetFrameworks>` → `net10.0`
   (drop `net4x`/`-windows`), and normalise the `\` path separators in its imports.
2. Repo-root `nuget.config` restricted to nuget.org (the private ProGet feed 403s).
3. In each project's global-usings file: `global using System.Media;` →
   `global using Majorsilence.Forms.Media;` (the migrator now does this; a re-run also works, but see
   task 3 first).
4. `python3 tools/fixups/krypton-standard-toolkit-bridge.py ".../Source/Krypton Components"`.
5. Krypton.Utilities only: it has TWO globals files and the migrator put the BCL-preference aliases in
   both (CS1537) — remove the alias lines from `Global/Globals.cs`, keep `Globals/GlobalDeclarations.cs`.
6. Build each project; expect 0. Any stragglers will be small — follow the shapes in this repo's
   `KryptonPortParity*.cs` and the bridge script's comments.

## 1b. DONE 2026-08-14: real Standard-Toolkit green, Cyotek migrated, Extended-Toolkit started

- **Standard-Toolkit (real repo)**: §1 executed — props TFMs to net10.0, nuget.org-only nuget.config,
  bridge script applied, System.Media remapped, Utilities alias dedupe. All six projects AND TestForm
  build at 0 errors.
- **Cyotek.Windows.Forms.ColorPicker fork**: migrated in place; library and tests build at 0 and its own
  **52 tests pass**. TestForm now project-references it, so the real ColorPickerDialog is back (the
  bridge script's package-drop entries were replaced with the project reference). Its *demo* is blocked
  only by `TheArtOfDev.HtmlRenderer.WinForms`, an unrelated Windows-only third-party package.
- **Extended-Toolkit**: migrated (670 of 2,367 files); **37 of 66 projects at 0 errors**, the rest
  totalling ~316 errors across 71 unique causes (was effectively unbuildable; single projects were at
  1,500+). Fixups live in `tools/fixups/krypton-extended-toolkit-bridge.py`.

### Extended-Toolkit: what remains, by cause

Ordered by leverage. None are large; several are legacy-.NET-Framework rather than migration issues.

1. **`PermissionSet` / `PermissionSetAttribute` (52)** — .NET Framework Code Access Security, *removed*
   from modern .NET. These attributes are dead weight upstream too; delete the declarations (a fixup
   script entry). Not an MF gap.
2. **`MenuItem` → `ToolStripItem` conversion (14)** — MF hierarchy: `ToolStripItem : MenuItem`, so the
   conversion runs the wrong way. Same family as the TreeNode inversion already fixed; check whether the
   collection should be typed `ToolStripItem`.
3. **`CodeDomProvider` (12)** — same shape as the System.Configuration fix already made: add
   `System.CodeDom` to the migrator's `DesktopSdkReplacements` table in `ProjectConverter.cs`.
4. **`DoWorkEventArgs` ambiguous (12)** — MF declares one that collides with the BCL's; add it to the
   migrator's `BclPreferredTypes` (it already handles BackgroundWorker/PropertyTabScope this way).
5. **`SnapLine` / `SnapLineType` / `SnapLinePriority` (30)** — design-time snap lines; add to
   `Majorsilence.Forms.Design` beside the other designer types.
6. **Long tail (~70 more)** — assorted single-site gaps; run the per-project sweep in this doc's
   "how to measure" note below and take them by frequency.

**How to measure:** from `Extended-Toolkit/Source/Krypton Toolkit`, build each `*/*.csproj` (skipping
`*Backup*`), grep `error CS`, and aggregate — that per-cause histogram is what made the first three
fixes obvious, each of which cleared hundreds of errors at once.

## 2. TestForm (the demo)

Blocked only on `Cyotek.Windows.Forms.ColorPicker` (forces the Windows platform, NETSDK1136). Remove
the package (`--map` removePackages, or hand-edit the csproj) and replace its use sites — MF's
`ColorDialog` covers the dialog uses; panel-embedded picker controls can become a stub panel first.
Then run it: see the memory notes on launching from the output dir and the screenshot workflow.

## 3. StartScreen runtime — items 1 and 2 FIXED 2026-08-14; item 3 open

TestForm launches, themes and renders, but StartScreen's anchored children are invisible. Probe at
`scratchpad/kprobe` (in-process AutomationSession dump; run with the Win32 shim dylibs copied into
`runtimes/osx/native`). Three pinned facts, all Majorsilence.Forms-side:

1. **[FIXED]** Two library fixes: the Avalonia backend's pre-open `ClientSize` now answers with the
   pending `Width`/`Height` instead of Avalonia's invented default, and `LayoutAnchoredControls` skips
   the pass against a degenerate DisplayRectangle for ALL containers (was AutoSize-only) -- a themed
   form's root panel reports 0x0 transiently mid-construction, the pass collapsed every anchored child
   against it, and the next re-init laundered the collapse into the stored deltas. Pinned by
   `KryptonPortParityTests.AnchoredChild_SurvivesATransientlyCollapsedParent`. Original diagnosis:
   anchor deltas captured against the backend's default window size (1440x675 here) while
   `InitializeComponent` runs, so when the designer's `ClientSize` (876x729) finally applies, anchored
   children collapse: `876 - 12 - (1402 - 538) = 0` — the observed zero width, exactly. WinForms avoids
   this by deferring anchor capture until `ResumeLayout`, after the final size is set. Fix direction:
   `UpdateAnchorInfo` must not capture while the element's container has layout suspended (or must
   re-capture on resume). See the long comment already in `DockAndAnchorLayout.UpdateAnchorInfo` — this
   is the nonzero-but-wrong variant of the degenerate-capture problem it describes.
2. **[CLOSED — not a bug]** The 902x633 was StartScreen's own ctor: `this.Size = new Size(902, 633)`
   deliberately overrides the designer. With honest pre-open reads it became traceable in one run.
3. **Post-show programmatic `Form.Size` writes are silently ignored even for a plain Form** (Avalonia
   backend path). Independent bug; the probe demonstrates it.

[FIXED 2026-08-14] The invisible button text was GraphicsPath figure semantics in
Majorsilence.Forms.Drawing.Common: GDI+ connects segments appended to an open figure with implicit
lines, and AddArc/AddLine/AddBezier didn't -- so a rounded border built the canonical GDI+ way (four
corner arcs, edges implied) enclosed no area, Region(path) rasterized empty, and Krypton's
ViewDrawCanvas clipped every rounded-corner control's content to nothing. Palette-dependent because
square-border palettes never build that path. Pinned by GraphicsPathFigureTests. Diagnosed via the
offscreen frame probe (scratchpad/kprobe) + env-gated clip tracing -- that toolchain is the way to
chase any future paint bug.

Older note (superseded): the command-link button FACES don't draw their heading/description text (layout and
chrome are correct; also fixed on the way: `ShowFocusCues` made protected-internal so Krypton's
NonPublic reflection finds it, and internal `Control.PaintTransparentBackground(PaintEventArgs,
Rectangle, Region)` added -- both reached by reflection, so absence was a runtime NRE, not a compile
error. Peel the next layer the same way: run TestForm, read the PaintFrame stack, fix, repeat.)

[FIXED 2026-08-14] Double-open on button click: MF raised Click unconditionally on pointer-up, but a
control that raises its own click (Krypton's view controllers) sets ControlStyles.StandardClick=false
precisely to suppress the standard raise. RaiseClick/RaiseDoubleClick now honour the styles; pinned by
KryptonPortParityTests.RaiseClick_HonoursStandardClickStyle.

Dark-theme contrast, measured (2026-08-14): the PALETTES are exonerated -- PaletteOffice2010Black and
PaletteMicrosoft365Black genuinely specify light-gray button faces (RGB 189/169) with DARK text
(70,70,70); that is Krypton's authentic dark-theme design. The divergence is that rendered text comes
out near-WHITE on those palettes: prime suspect is AccurateText.DrawString's brush-type fallback --
`brush is SolidBrush ... is LinearGradientBrush ... else GetContentShortTextColor1(LabelNormalControl)`
-- which on a dark global palette returns near-white. If the `is` tests fail (brush subtype from
CreateColorBrush?) every text draw silently takes the fallback colour. Verify with the offscreen probe:
trace which branch fires under Office2010Black, then fix the brush-type dispatch or the fallback.

Office 2007/2010 Black contrast, RESOLVED as palette-faithful (2026-08-14): traced end to end --
Krypton passes SolidBrush(Color.White) for command-link text, and PaletteOffice2010Black itself
specifies ButtonCommand text=White on back1/back2 = RGB 189/169 light gray, colorStyle=Linear. MF
renders exactly the palette's numbers; white-on-light-gray is the palette data. Command links are a
newer Krypton.Utilities control, likely under-tested against the old dark palettes upstream. Options
if it matters: verify on real WinForms/Windows for an upstream report, or have TestForm set
CommandLinkTextValues colors per-theme. NOT an MF rendering bug.

Still open, user-reported: some THEMES still paint badly (compare palettes side by side; the fixed
GraphicsPath/clip machinery may not be the whole story for gradient-heavy palettes), and assorted
visual glitches on child forms (BadgeTest shows minor artefacts). Chase with the offscreen frame probe.

Also observed: `WindowBase.RenderFrame` invokes `OnResize` during first paint — a Krypton override that
calls Invalidate from OnResize could re-enter; keep in mind when testing fix (1).

## 3b. Migrator hardening (found by this port)

- **Global-alias idempotency (CS1537):** `AddBclPreferenceAliases` adds per-assembly `global using X = …`
  aliases per FILE, so a project with two global-usings files gets duplicates — and a re-run duplicates
  within one file. Needs project-scoped awareness or an already-present check across globals files.
  Test case: Krypton.Utilities' `Globals/GlobalDeclarations.cs` + `Global/Globals.cs`.
- `--output` still omits implicit `Directory.Build.props`/`.targets` and binary assets.

## 4. Library follow-ups (each small, gate-checked)

- **`RichTextBox : TextBox`** here; upstream they are siblings under `TextBoxBase`. The bridge script
  papers over one consequence (pattern-arm order). Rebasing is the real fix; measure blast radius first.
- **`ErrorProvider.ContainerControl`** cannot accept a Form (dropped by the script at one site). If more
  sites appear, consider what the provider actually needs from it here.
- **Baseline triage:** `tests/.../ControlWindowParityBaseline.txt` (~190 entries) — classify each as
  "belongs on a window" (add it; the Krypton port pulled ~10 off the list this way) or "genuinely N/A"
  (annotate). The test names any new gap automatically.
- **Mobile/browser audio:** desktop audio is real now (`Media/NativeAudio.cs` spawns the OS player;
  see COMPATIBILITY_MATRIX "Majorsilence.Forms.Media"). Android/iOS/wasm stay silent until a backend
  supplies a native path -- `NativeAudio.LauncherOverride` is currently the only seam, promote it to a
  proper backend hook when the Uno backend grows audio.
- **Drop-down open-path events:** `DropDownOpening/Opened` fire via `ShowDropDown()` but not when the
  strip opens the menu from a click (`MenuItem.ShowDropDown` doesn't know about them).

Gates for every task: Release build (analyzers), `MF_HEADLESS_SCALE=2 dotnet test`, ApiDiff `--check`,
and the two baselines (regenerate deliberately, never to silence).
