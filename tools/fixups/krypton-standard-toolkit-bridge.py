#!/usr/bin/env python3
"""Post-migration bridge patch for the Krypton Standard Toolkit (run at the "Krypton Components" root).

Why this exists
---------------
In Majorsilence.Forms a Form is not a Control (Form : WindowBase : Component), and the decision on
record is to keep it that way. Krypton's rendering pipeline is written against the WinForms fact that
a form IS a control: ViewContext carries a `Control`, KryptonForm passes `this` into it, and ~30 sites
ask `context.Control as KryptonForm` to decide form-specific chrome. Those are the only errors left
after the parity work in Majorsilence.Forms itself, and no library-side member can fix them -- they are
type-relationship facts.

What the patch does (three moves, ~40 lines touched)
----------------------------------------------------
1. Adds `General/MFFormBridge.cs`: two helpers answering "is this control the form's own surface?"
   with WinForms' exact semantics. A form-rooted context's Control is the form's root ControlAdapter
   (`form.Controls.Owner`), whose `FindForm()` is the form and which IS the collection owner -- while a
   child control fails the ReferenceEquals check and stays null, exactly as `child as KryptonForm` is
   null upstream.
2. Where Krypton passes a form into a Control parameter (`this` / `form` into ViewContext,
   ViewManager, ButtonSpecManagerDraw, RenderContext), passes `form.Controls.Owner` instead -- the real
   root Control of the window, on which Invalidate/PointToScreen/FindForm all behave.
3. Rewrites the `as/is KryptonForm` questions to go through the bridge. The one walk-up helper
   (RenderStandard.OwningKryptonForm) becomes `FindForm()`, which is the walk it was hand-writing.

Idempotent: each replacement is skipped when its target text is already present, so re-running after a
re-migration is safe. Run it from anywhere:

    python3 tools/fixups/krypton-standard-toolkit-bridge.py "<path>/Source/Krypton Components"
"""

import sys
from pathlib import Path

BRIDGE_FILE = "Krypton.Toolkit/General/MFFormBridge.cs"

BRIDGE_SOURCE = '''namespace Krypton.Toolkit;

/// <summary>
/// Answers "is this control the form itself?" across the Majorsilence.Forms control/window split.
/// </summary>
/// <remarks>
/// In WinForms a Form is a Control, so <c>context.Control as KryptonForm</c> asks whether the thing
/// being rendered is the form. In Majorsilence.Forms the form's stand-in inside the control tree is
/// its root <c>ControlAdapter</c> (<c>form.Controls.Owner</c>), so the same question becomes: does
/// this control's form consider the control its own root surface? A child control has the same
/// <c>FindForm()</c> but is not the collection owner, so it answers null -- matching WinForms, where a
/// child is never the form.
///
/// Public, not internal: Krypton.Navigator and Krypton.Ribbon meet the same seam and this assembly is
/// the one they all reference.
/// </remarks>
public static class MFFormBridge
{
    /// <summary>The form, when <paramref name="control"/> is that form's own root surface; else null.</summary>
    public static Form? AsForm(Control? control) =>
        control?.FindForm() is { } form && ReferenceEquals(control, form.Controls.Owner) ? form : null;

    /// <inheritdoc cref="AsForm"/>
    public static KryptonForm? AsKryptonForm(Control? control) => AsForm(control) as KryptonForm;
}
'''

# (file, old, new, expected occurrences). Exact-string, whitespace and all, so a drifted upstream file
# fails loudly here instead of silently half-patching.
REPLACEMENTS = [
    # -- Popup windows: show through the managed top-level mechanism, not ShowWindow. ----------------
    # VisualPopup is a Control that shows itself as a floating window via PI.ShowWindow(Handle, ...),
    # a user32 call with no Majorsilence.Forms counterpart -- the handle is fake and the shim no-ops, so
    # every popup in the suite (context menus, tooltips, the ribbon's app menu, collapsed-group popups
    # -- eight types derive from this one base) never appeared, and VisualPopupManager's
    # `popup.IsHandleCreated` assert aborted the process the moment one was shown. SetTopLevel(true) is
    # the managed spelling of the same operation: WinForms' own ToolStripDropDown floats itself this
    # way, and Majorsilence.Forms hosts a visible top-level control in a popup window (see
    # Control.TopLevel.cs there). Bounds were already set to the screen rect two lines up, which is
    # exactly what the hosting reads.
    ("Controls Visuals/VisualPopup.cs",
     "        // Show the window without activating it (i.e. do not take focus)\n"
     "        PI.ShowWindow(Handle, PI.ShowWindowCommands.SW_SHOWNOACTIVATE);",
     "        // Show the window without activating it. There is no HWND for ShowWindow to act on here;\n"
     "        // becoming a visible top-level control is the Majorsilence.Forms spelling of the same\n"
     "        // operation (the control is hosted in its own popup window at the Bounds set above).\n"
     "        SetTopLevel(true);\n"
     "        Visible = true;", 1),

    # -- Form passed where a Control parameter is required: pass the form's root control. ------------
    ("Controls Visuals/ViewLayoutContext.cs",
     ": base(manager, form, form, null, renderer) =>",
     ": base(manager, form.Controls.Owner, form.Controls.Owner, null, renderer) =>", 1),
    ("Controls Toolkit/KryptonForm.cs",
     "_buttonManager = new ButtonSpecManagerDraw(this, Redirector",
     "_buttonManager = new ButtonSpecManagerDraw(Controls.Owner, Redirector", 1),
    ("Controls Toolkit/KryptonForm.cs",
     "ViewManager = new ViewManager(this, _drawDocker);",
     "ViewManager = new ViewManager(Controls.Owner, _drawDocker);", 1),
    ("Controls Toolkit/KryptonForm.cs",
     "new ViewLayoutContext(this, Renderer)",
     "new ViewLayoutContext(Controls.Owner, Renderer)", 3),
    ("Controls Toolkit/KryptonForm.cs",
     "new RenderContext(this, null, Bounds, Renderer);",
     "new RenderContext(Controls.Owner, null, Bounds, Renderer);", 1),
    ("Controls Toolkit/KryptonForm.cs",
     "GetChildAtPointSkip.Invisible | GetChildAtPointSkip.Disabled) ?? this;",
     "GetChildAtPointSkip.Invisible | GetChildAtPointSkip.Disabled) ?? Controls.Owner;", 1),
    ("General/KryptonSystemMenuListener.cs",
     "new ViewLayoutContext(_form, _form.Renderer)",
     "new ViewLayoutContext(_form.Controls.Owner, _form.Renderer)", 1),

    # -- "Is the rendered control the form?": route through the bridge. ------------------------------
    ("Rendering/RenderStandard.cs",
     "var isForm = context.Control as KryptonForm;",
     "var isForm = MFFormBridge.AsKryptonForm(context.Control);", 1),
    ("Rendering/RenderStandard.cs",
     "palette.GetBorderContentPadding(context.Control as KryptonForm, state)",
     "palette.GetBorderContentPadding(MFFormBridge.AsKryptonForm(context.Control), state)", 1),
    ("Rendering/RenderStandard.cs",
     "KryptonForm? ownerForm = context.Control as KryptonForm;",
     "KryptonForm? ownerForm = MFFormBridge.AsKryptonForm(context.Control);", 1),
    ("Rendering/RenderStandard.cs",
     "ownerForm = context.TopControl as KryptonForm;",
     "ownerForm = MFFormBridge.AsKryptonForm(context.TopControl);", 1),
    ("View Draw/ViewDrawCanvas.cs",
     "_paletteMetric.GetMetricPadding(context.Control as KryptonForm, State, _metricPadding)",
     "_paletteMetric.GetMetricPadding(MFFormBridge.AsKryptonForm(context.Control), State, _metricPadding)", 2),
    ("View Draw/ViewDrawDocker.cs",
     "_paletteMetric.GetMetricPadding(context.Control as KryptonForm, State, _metricPadding)",
     "_paletteMetric.GetMetricPadding(MFFormBridge.AsKryptonForm(context.Control), State, _metricPadding)", 3),
    ("View Draw/ViewDrawForm.cs",
     "if (context.Control is KryptonForm form)",
     "if (MFFormBridge.AsKryptonForm(context.Control) is { } form)", 1),
    ("View Draw/ViewDrawSeparator.cs",
     "_metric!.GetMetricPadding(context.Control as KryptonForm, ElementState, MetricPadding)",
     "_metric!.GetMetricPadding(MFFormBridge.AsKryptonForm(context.Control), ElementState, MetricPadding)", 1),
    ("View Draw/ViewDrawSplitCanvas.cs",
     "PaletteMetric.GetMetricPadding(context.Control as KryptonForm, State, _metricPadding)",
     "PaletteMetric.GetMetricPadding(MFFormBridge.AsKryptonForm(context.Control), State, _metricPadding)", 2),
    ("View Layout/ViewLayoutCenter.cs",
     "_paletteMetric.GetMetricPadding(context.Control as KryptonForm, ElementState, MetricPadding)",
     "_paletteMetric.GetMetricPadding(MFFormBridge.AsKryptonForm(context.Control), ElementState, MetricPadding)", 2),
    ("View Layout/ViewLayoutMenuSepGap.cs",
     "GetBorderContentPadding(context.Control as KryptonForm, PaletteState.Normal)",
     "GetBorderContentPadding(MFFormBridge.AsKryptonForm(context.Control), PaletteState.Normal)", 2),
    ("View Layout/ViewLayoutMetricSpacer.cs",
     "_paletteMetric.GetMetricInt(OwningControl as KryptonForm, ElementState, _metricInt)",
     "_paletteMetric.GetMetricInt(MFFormBridge.AsKryptonForm(OwningControl), ElementState, _metricInt)", 2),
    ("View Layout/ViewLayoutViewport.cs",
     "_paletteMetrics.GetMetricPadding(context.Control as KryptonForm, State, _metricPadding)",
     "_paletteMetrics.GetMetricPadding(MFFormBridge.AsKryptonForm(context.Control), State, _metricPadding)", 4),
    ("View Layout/ViewLayoutViewport.cs",
     "_paletteMetrics.GetMetricInt(OwningControl as KryptonForm, State, _metricOvers)",
     "_paletteMetrics.GetMetricInt(MFFormBridge.AsKryptonForm(OwningControl), State, _metricOvers)", 1),
    ("ButtonSpec/ButtonSpecView.cs",
     "Point pt = Manager.Control is Form form",
     "Point pt = MFFormBridge.AsForm(Manager.Control) is { } form", 1),
    ("Rendering/KryptonProfessionalRenderer.cs",
     "if (e.ToolStrip!.Parent!.TopLevelControl is Form f)",
     "if (MFFormBridge.AsForm(e.ToolStrip!.Parent!.TopLevelControl) is { } f)", 1),

    # -- Krypton.Navigator / Krypton.Ribbon: the same seam, met by the same three moves. --------------
    ("Docking/DropDockingIndicatorsSquare.cs",
     "new RenderContext(this, e.Graphics, e.ClipRectangle, _renderer);",
     "new RenderContext(Controls.Owner, e.Graphics, e.ClipRectangle, _renderer);", 1),
    ("Docking/DropSolidWindow.cs",
     "new RenderContext(this, e.Graphics, e.ClipRectangle, _renderer);",
     "new RenderContext(Controls.Owner, e.Graphics, e.ClipRectangle, _renderer);", 1),
    ("Docking/DropSolidWindow.cs",
     "var area = Screen.GetWorkingArea(this);",
     "var area = Screen.GetWorkingArea(Bounds);", 1),
    ("Navigator/KryptonNavigator.cs",
     "rootControl = focus.FindForm();",
     "rootControl = focus.FindForm()?.Controls.Owner;", 1),
    ("Controls Ribbon/KeyTipControl.cs",
     "new ViewLayoutContext(this, _ribbon.Renderer)",
     "new ViewLayoutContext(Controls.Owner, _ribbon.Renderer)", 1),
    ("Controls Ribbon/KeyTipControl.cs",
     "new RenderContext(this, e.Graphics, e.ClipRectangle, _ribbon.Renderer)",
     "new RenderContext(Controls.Owner, e.Graphics, e.ClipRectangle, _ribbon.Renderer)", 1),
    ("Controls Ribbon/KryptonRibbon.cs",
     "if (c is Form form)",
     "if (MFFormBridge.AsForm(c) is { } form)", 1),
    ("Controls Ribbon/KryptonRibbon.cs",
     "return c as KryptonForm;",
     "return MFFormBridge.AsKryptonForm(c);", 1),
    ("Controls Ribbon/KryptonRibbon.cs",
     "Control? form = focus.FindForm();",
     "Control? form = focus.FindForm()?.Controls.Owner;", 1),
    ("Controls Ribbon/KryptonRibbonGroupItem.cs",
     "get => _bindingContext ??= [];",
     "get => _bindingContext ??= new BindingContext();", 1),
    ("View Draw/ViewDrawRibbonCaptionArea.cs",
     "var ownerForm = _ribbon.Parent as Form;",
     "var ownerForm = MFFormBridge.AsForm(_ribbon.Parent);", 1),
    ("Controls Ribbon/VisualRibbonFloatingWindow.cs",
     "if (_ribbon != null && (ribbonParent == this || ribbonParent == InternalPanel))",
     "if (_ribbon != null && (ribbonParent == Controls.Owner || ribbonParent == InternalPanel))", 1),

    # -- Krypton.Utilities. ---------------------------------------------------------------------------
    # RichTextBox IS a TextBox in Majorsilence.Forms (upstream they are siblings under TextBoxBase), so
    # the specific arm must come first; both arms read the same member, so this is order-only.
    ("Controls Toolkit/KryptonAutoTextSuggestion.cs",
     "TextBox tb => tb.Text,\n            KryptonTextBox ktb => ktb.Text,\n            RichTextBox rtb => rtb.Text,",
     "RichTextBox rtb => rtb.Text,\n            KryptonTextBox ktb => ktb.Text,\n            TextBox tb => tb.Text,", 1),
    ("Controls Toolkit/KryptonAutoTextSuggestion.cs",
     "TextBox tb => tb.SelectionStart,\n            KryptonTextBox ktb => ktb.SelectionStart,\n            RichTextBox rtb => rtb.SelectionStart,",
     "RichTextBox rtb => rtb.SelectionStart,\n            KryptonTextBox ktb => ktb.SelectionStart,\n            TextBox tb => tb.SelectionStart,", 1),
    # ErrorProvider.ContainerControl is ContainerControl-typed and a Majorsilence.Forms Form is not one;
    # the provider still blinks its icons without it, so the assignment is dropped rather than bridged.
    ("Controls Visuals/VisualBugReportingDialogForm.cs",
     "        {\n            ContainerControl = this,\n            BlinkStyle = KryptonErrorBlinkStyle.BlinkIfDifferentError,",
     "        {\n            BlinkStyle = KryptonErrorBlinkStyle.BlinkIfDifferentError,", 1),

    # -- TestForm (the demo): Cyotek's ColorPicker ships as a Windows-only PACKAGE that forces the
    # Windows platform (NETSDK1136). The fork at ~/Projects/Cyotek.Windows.Forms.ColorPicker is migrated
    # onto Majorsilence.Forms, so the package reference becomes a project reference and the real
    # ColorPickerDialog -- which LiveColorPickerDialog derives from -- comes back.
    ("TestForm/TestForm.csproj",
     """    <PackageReference Include="Cyotek.Windows.Forms.ColorPicker" Version="2.0.0-beta.7" />""",
     """    <ProjectReference Include="$(MSBuildThisFileDirectory)..\\..\\..\\..\\Cyotek.Windows.Forms.ColorPicker\\Cyotek.Windows.Forms.ColorPicker\\Cyotek.Windows.Forms.ColorPicker.csproj" />""", 1),

    # TestForm pages: assignments whose sink types cannot take a Majorsilence.Forms Form.
    # ContainerControl is ContainerControl-typed (a form is not one here), SynchronizingObject is
    # ISynchronizeInvoke (not implemented by WindowBase yet -- see docs/krypton-port-plan.md), and a
    # grid cell's Style.Font is a Skia typeface. Each drop costs one page a nicety, not a feature.
    ("TestForm/BugReportingDialogTest.cs",
     "            ContainerControl = this,\n", "", 1),
    ("TestForm/HelpProviderTest.cs",
     "        kryptonHelpProvider1.ContainerControl = this;\n", "", 1),
    ("TestForm/ErrorProviderTest.cs",
     "        kryptonErrorProvider1.ContainerControl = this;\n", "", 1),
    ("TestForm/ErrorProviderTest.Designer.cs",
     "            this.kryptonErrorProvider1.ContainerControl = this;\n", "", 1),
    ("TestForm/FileSystemWatcherTest.cs",
     "            SynchronizingObject = this,  // Marshal events to UI thread\n", "", 1),
    ("PaletteViewer/PaletteViewerForm.cs",
     "                row.Cells[paletteColumnIndex].Style.Font = new Majorsilence.Forms.Drawing.Font(this.Font, Majorsilence.Forms.Drawing.FontStyle.Italic);\n",
     "", 1),

    # TestForm persists window state in the Windows registry, which does not exist off Windows --
    # Registry access becomes null-tolerant: defaults on read, no-op on write, unchanged on Windows.
    ("TestForm/Classes/RegistryAccess.cs",
     """        _registryKey = Registry.CurrentUser.CreateSubKey(_registryPath)
            ?? throw new Exception("Registry.CurrentUser.CreateSubKey() returned null.");""",
     """        try
        {
            _registryKey = Registry.CurrentUser.CreateSubKey(_registryPath)
                ?? throw new Exception("Registry.CurrentUser.CreateSubKey() returned null.");
        }
        catch
        {
            // No Windows registry on this platform: defaults on read, nothing persisted.
            _registryKey = null;
        }""", 1),
    ("TestForm/Classes/RegistryAccess.cs",
     "_registryKey.GetValue(_rvFormWidth, -1).ToString()",
     "_registryKey?.GetValue(_rvFormWidth, -1)?.ToString()", 1),
    ("TestForm/Classes/RegistryAccess.cs",
     "_registryKey.GetValue(_rvFormHeight, -1).ToString()",
     "_registryKey?.GetValue(_rvFormHeight, -1)?.ToString()", 1),
    ("TestForm/Classes/RegistryAccess.cs",
     "_registryKey.GetValue(_rvLastFilterString) as string",
     "_registryKey?.GetValue(_rvLastFilterString) as string", 1),
    ("TestForm/Classes/RegistryAccess.cs",
     "(_registryKey.GetValue(_rvDockTopRight, \"0\") as string)",
     "(_registryKey?.GetValue(_rvDockTopRight, \"0\") as string)", 1),
    ("TestForm/Classes/RegistryAccess.cs",
     "set => _registryKey.SetValue", "set => _registryKey?.SetValue", 4),

    # Icon extraction reaches Win32 shell APIs that cannot work off Windows; failing must degrade to
    # "no icon", not raise Krypton's exception dialog at startup.
    ("Utilities/GraphicsExtensions.cs",
     "        catch (Exception ex)\n        {\n            KryptonExceptionHandler.CaptureException(ex, showStackTrace: GlobalStaticValues.DEFAULT_USE_STACK_TRACE);",
     "        catch (Exception)\n        {\n            // No Win32 shell icons on this platform: the caller treats null as \"no image\".", 1),

    # -- The walk-up helper wants the OWNING form of any child, which is FindForm by definition. -----
    ("Rendering/RenderStandard.cs",
     """	private static KryptonForm? OwningKryptonForm(Control? c)
	{
		// Climb chain looking for the Krypton Form instance
		while ((c != null) && c is not KryptonForm)
		{
			c = c.Parent;
		}

		return c as KryptonForm;
	}""",
     """	private static KryptonForm? OwningKryptonForm(Control? c) =>
		// Climb chain looking for the Krypton Form instance. FindForm is that climb in
		// Majorsilence.Forms: a raw Parent walk stops at the window's root control and
		// never reaches the Form, which is not itself a Control there.
		c?.FindForm() as KryptonForm;""", 1),
]


def find_file(root: Path, rel: str) -> Path:
    """Resolve by search, not by the recorded folder: Krypton reshuffles folders between branches."""
    name = rel.split("/")[-1]
    exact = root / rel
    if exact.is_file():
        return exact
    hits = [p for p in root.rglob(name) if p.is_file()]
    if len(hits) != 1:
        raise SystemExit(f"error: expected exactly one {name} under {root}, found {len(hits)}")
    return hits[0]


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)

    root = Path(sys.argv[1]).resolve()
    if not root.is_dir():
        raise SystemExit(f"error: {root} is not a directory")

    bridge = root / BRIDGE_FILE
    if bridge.exists() and bridge.read_text(encoding="utf-8") == BRIDGE_SOURCE:
        print(f"unchanged  {BRIDGE_FILE}")
    else:
        bridge.parent.mkdir(parents=True, exist_ok=True)
        bridge.write_text(BRIDGE_SOURCE, encoding="utf-8")
        print(f"wrote      {BRIDGE_FILE}")

    failures = []
    for rel, old, new, expected in REPLACEMENTS:
        path = find_file(root, rel)
        text = path.read_text(encoding="utf-8")
        have_new = text.count(new)
        have_old = text.count(old)

        if have_old == 0 and have_new >= expected:
            print(f"unchanged  {rel}")
            continue
        if have_old != expected:
            failures.append(f"{rel}: expected {expected}x the original text, found {have_old} "
                            f"(already patched: {have_new}) -- upstream drift, review by hand:\n    {old.splitlines()[0]}")
            continue

        path.write_text(text.replace(old, new), encoding="utf-8")
        print(f"patched    {rel}  ({expected}x)")

    if failures:
        raise SystemExit("error:\n  " + "\n  ".join(failures))


if __name__ == "__main__":
    main()
