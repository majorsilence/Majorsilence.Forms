#!/usr/bin/env python3
"""Post-migration fixups for the Krypton Extended Toolkit (run at the "Source/Krypton Toolkit" root).

Why this exists
---------------
Extended-Toolkit consumes Standard-Toolkit. Migrating it therefore turns up two DIFFERENT kinds of
problem, and it is worth keeping them apart:

  * Majorsilence.Forms gaps — fixed in the library, never here.
  * Upstream drift between the two Krypton repos — Extended is written against an older Standard
    surface than the Standard-Toolkit checkout beside it. These would fail on Windows with real
    WinForms too; they are not migration damage. That is what this script patches.

Idempotent: each replacement is skipped when its target text is already present, so re-running after a
re-migration is safe.

    python3 tools/fixups/krypton-extended-toolkit-bridge.py "<path>/Source/Krypton Toolkit"
"""

import sys
from pathlib import Path

import re

# Upstream moved the PUBLIC KryptonExceptionDialog into Krypton.Utilities and left an internal one behind
# in Krypton.Toolkit (Standard-Toolkit commit "Move the public facing version of KryptonExceptionDialog
# to Krypton.Utilities"). Extended still calls the Krypton.Toolkit name, which is now inaccessible to it
# -- a break that predates any migration and would happen on Windows with real WinForms too.
#
# KryptonMessageBox is the public equivalent and preserves what every one of these call sites wants: tell
# the user something failed, then carry on. A regex because the suite duplicates these helpers across
# projects with different exception variable names (e, ex, exc, wexc).
# Renamed upstream: KryptonToastNotificationIcon -> KryptonToastIcon, alongside the rest of the
# KryptonToast* family. Word-bounded so it cannot also rewrite an already-correct name.
RENAMED_TYPES = [
    (re.compile(r"\bKryptonToastNotificationIcon\b"), "KryptonToastIcon"),
    # Majorsilence.Forms ships its own ComponentResourceManager -- a real resx/resources reader, not a
    # shim -- and it is the one migrated designer code must use. Bare uses are ambiguous wherever
    # System.ComponentModel is also imported, so they are qualified rather than left to the compiler.
    (re.compile(r"(?<![\w.])ComponentResourceManager\("), "Majorsilence.Forms.ComponentResourceManager("),
]


def rename_drifted_types(root: Path) -> int:
    """Applies upstream renames and disambiguates types that collide with a BCL name."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = original = path.read_text(encoding="utf-8", errors="ignore")
        for pattern, replacement in RENAMED_TYPES:
            text = pattern.sub(replacement, text)

        if text != original:
            path.write_text(text, encoding="utf-8")
            print(f"renamed    {path.relative_to(root)}")
            changed += 1

    return changed


EXCEPTION_DIALOG = re.compile(r"KryptonExceptionDialog\.Show\(\s*(\w+)\s*,\s*null\s*,\s*null\s*\)")
EXCEPTION_DIALOG_REPLACEMENT = r"KryptonMessageBox.Show(\1.Message)"


def patch_exception_dialog(root: Path) -> int:
    """Rewrites every KryptonExceptionDialog.Show call; returns the number of files changed."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = path.read_text(encoding="utf-8", errors="ignore")
        if "KryptonExceptionDialog.Show" not in text:
            continue

        rewritten, count = EXCEPTION_DIALOG.subn(EXCEPTION_DIALOG_REPLACEMENT, text)
        if count:
            path.write_text(rewritten, encoding="utf-8")
            print(f"patched    {path.relative_to(root)}  ({count}x)")
            changed += 1

    return changed


# Extended consumes Krypton as a NuGet package upstream; the migrator drops that package (it is a
# Windows-only WinForms suite) and this repo builds Krypton from the Standard-Toolkit checkout beside it
# instead. Projects that already carried a Standard-Toolkit ProjectReference (usually inside a
# configuration-conditional block) are fine; the rest lose Krypton entirely and fail with CS0246 on every
# KryptonButton/KryptonPanel/... That wiring is repo layout, not something the migrator can infer, so it
# lives here.
STANDARD_TOOLKIT_PROJECTS = [
    "Krypton.Toolkit/Krypton.Toolkit 2022.csproj",
    "Krypton.Navigator/Krypton.Navigator 2022.csproj",
    "Krypton.Ribbon/Krypton.Ribbon 2022.csproj",
    "Krypton.Workspace/Krypton.Workspace 2022.csproj",
    "Krypton.Docking/Krypton.Docking 2022.csproj",
    "Krypton.Utilities/Krypton.Utilities.csproj",
]

# Which Krypton assembly provides which type prefix -- only what a project actually uses gets referenced.
TYPE_HINTS = {
    "Krypton.Toolkit/Krypton.Toolkit 2022.csproj": ("KryptonButton", "KryptonPanel", "KryptonLabel",
        "KryptonTextBox", "KryptonManager", "PaletteBase", "KryptonForm", "KryptonDataGridView",
        "KryptonColorTable", "VisualControlBase", "KryptonMessageBox", "PaletteMode"),
    "Krypton.Navigator/Krypton.Navigator 2022.csproj": ("KryptonNavigator", "KryptonPage"),
    "Krypton.Ribbon/Krypton.Ribbon 2022.csproj": ("KryptonRibbon",),
    "Krypton.Workspace/Krypton.Workspace 2022.csproj": ("KryptonWorkspace",),
    "Krypton.Docking/Krypton.Docking 2022.csproj": ("KryptonDockingManager", "KryptonDockableWorkspace",
        "Krypton.Docking."),
    "Krypton.Utilities/Krypton.Utilities.csproj": ("KryptonExceptionDialog", "KryptonToast"),
}


def wire_standard_toolkit(root: Path) -> int:
    """Adds the Standard-Toolkit ProjectReferences a project needs but does not have."""
    import xml.etree.ElementTree as ET

    changed = 0
    for csproj in sorted(root.glob("*/*.csproj")):
        if "Backup" in csproj.name:
            continue

        text = csproj.read_text(encoding="utf-8")
        if "Standard-Toolkit" in text:
            continue   # already wired (often inside a configuration-conditional block)

        sources = " ".join(
            f.read_text(encoding="utf-8", errors="ignore")
            for f in csproj.parent.rglob("*.cs")
            if "/obj/" not in str(f) and "/bin/" not in str(f))

        needed = [proj for proj in STANDARD_TOOLKIT_PROJECTS
                  if any(hint in sources for hint in TYPE_HINTS[proj])]
        if not needed:
            continue

        # ../../../../Standard-Toolkit/Source/Krypton Components/<project>, from
        # Extended-Toolkit/Source/Krypton Toolkit/<project>/.
        refs = "\n".join(
            f'    <ProjectReference Include="$(MSBuildThisFileDirectory)..\\..\\..\\..\\'
            f'Standard-Toolkit\\Source\\Krypton Components\\{proj.replace("/", chr(92) + chr(92))}" />'
            for proj in needed)

        insert_at = text.rindex("</Project>")
        csproj.write_text(f"{text[:insert_at]}  <ItemGroup>\n{refs}\n  </ItemGroup>\n\n{text[insert_at:]}",
                          encoding="utf-8")
        print(f"wired      {csproj.parent.name}  ({len(needed)} Krypton project ref(s))")
        changed += 1

    return changed


PERMISSION_SET = re.compile(
    r"^(\s*)(\[(?:module:\s*)?PermissionSet(?:Attribute)?\s*\([^\]]*\)\s*\])", re.M)

# The same attribute written as one entry of a comma-separated attribute list -- `[\n Permission...(),\n
# Serializable,\n]`. It has no brackets of its own, so the pattern above cannot see it; here the whole
# entry line goes, comma included, leaving the rest of the list intact.
PERMISSION_SET_ENTRY = re.compile(
    r"^[ \t]*(?:module:\s*)?PermissionSet(?:Attribute)?\s*\([^)]*\)\s*,[ \t]*\r?\n", re.M)


def strip_permission_sets(root: Path) -> int:
    """Comments out Code Access Security attributes, which modern .NET removed outright."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = original = path.read_text(encoding="utf-8", errors="ignore")
        if "PermissionSet" not in text:
            continue

        text = PERMISSION_SET_ENTRY.sub("", text)
        rewritten, count = PERMISSION_SET.subn(
            r"\1// Code Access Security was removed in .NET Core+; the attribute has no replacement.\n"
            r"\1// \2", text)
        if count or rewritten != original:
            path.write_text(rewritten, encoding="utf-8")
            print(f"patched    {path.relative_to(root)}  (CAS attribute)")
            changed += 1

    return changed


# Code Access Security is not merely absent from .NET Core -- it was removed by design, and the shim
# package that still carries the type names throws PlatformNotSupportedException from the very methods
# used here. So referencing that package would trade a compile error for a runtime one. These call sites
# all sit behind a "does this thread need a security context capture?" test that can only ever answer no
# now, which makes every assert below it dead code. Stripping it is what the .NET porting guidance says
# to do, and it leaves the surrounding file/grammar logic untouched.
CAS_LINE_REPLACEMENTS = [
    # The gate itself: no security manager exists, so no capture is ever required.
    ("SecurityManager.CurrentThreadRequiresSecurityContextCapture()", "false"),
]

# Whole statements that exist only to assert/revert a permission -- deleted outright.
CAS_STATEMENTS = re.compile(
    r"^[ \t]*(?:"
    r"new\s+(?:FileIOPermission|ReflectionPermission)\s*\([^;]*?\)\s*\.\s*Assert\s*\(\)"
    r"|CodeAccessPermission\s*\.\s*RevertAssert\s*\(\)"
    r"|_internetPermissionSet\s*\.\s*(?:PermitOnly|AddPermission)\s*\([^;]*?\)"
    r"|_internetPermissionSet\s*=\s*PolicyLevel[^;]*?"
    r"|private\s+PermissionSet\s+_internetPermissionSet"
    r")\s*;[ \t]*\r?\n", re.M)


def strip_code_access_security(root: Path) -> int:
    """Removes CAS asserts and the permission sets they act on; returns files changed."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = original = path.read_text(encoding="utf-8", errors="ignore")
        if not any(k in text for k in ("FileIOPermission", "CodeAccessPermission",
                                       "SecurityManager", "_internetPermissionSet")):
            continue

        for old, new in CAS_LINE_REPLACEMENTS:
            text = text.replace(old, new)
        text = CAS_STATEMENTS.sub("", text)

        if text != original:
            path.write_text(text, encoding="utf-8")
            print(f"patched    {path.relative_to(root)}  (CAS removed)")
            changed += 1

    return changed


# Majorsilence.Forms' Form is not a Control -- it composes an internal root ControlAdapter and forwards to
# it, reachable as `Controls.Owner`. So a form passing `this` where a Control is wanted does not compile.
# The Standard-Toolkit bridge solves the same problem the same way; these are the Extended-side sites.
# Each entry is (relative path, exact old text, new text) and is applied only if the old text is present,
# which is what keeps re-running safe.
FORM_AS_CONTROL = [
    ("Krypton.Toolkit.Suite.Extended.Forms/Controls Visuals/VisualKryptonFormExtended.cs",
     "_buttonManager = new ButtonSpecManagerDraw(this, Redirector",
     "_buttonManager = new ButtonSpecManagerDraw(Controls.Owner, Redirector"),
    ("Krypton.Toolkit.Suite.Extended.Forms/Controls Visuals/VisualKryptonFormExtended.cs",
     "ViewManager = new ViewManager(this, _drawDocker);",
     "ViewManager = new ViewManager(Controls.Owner, _drawDocker);"),
    ("Krypton.Toolkit.Suite.Extended.Forms/Controls Visuals/VisualKryptonFormExtended.cs",
     "using (ViewLayoutContext context = new(this, Renderer))",
     "using (ViewLayoutContext context = new(Controls.Owner, Renderer))"),
    ("Krypton.Toolkit.Suite.Extended.Forms/Controls Visuals/VisualKryptonFormExtended.cs",
     "using ViewLayoutContext context = new(this, Renderer);",
     "using ViewLayoutContext context = new(Controls.Owner, Renderer);"),
    ("Krypton.Toolkit.Suite.Extended.Forms/Controls Visuals/VisualKryptonFormExtended.cs",
     "using RenderContext context = new(this, null, Bounds, Renderer);",
     "using RenderContext context = new(Controls.Owner, null, Bounds, Renderer);"),
    ("Krypton.Toolkit.Suite.Extended.Dock.Extender/Controls Toolkit/KryptonFloatableForm.cs",
     "_dockState.Container.Parent = this;",
     "_dockState.Container.Parent = Controls.Owner;"),
    ("Krypton.Toolkit.Suite.Extended.Dock.Extender/Controls Toolkit/KryptonFloatableForm.cs",
     "_dockState.Splitter.Parent = this;",
     "_dockState.Splitter.Parent = Controls.Owner;"),
    # KryptonFileCopier is itself a KryptonForm, and a monitor's SynchronizationObject is
    # ISynchronizeInvoke -- which the root control implements and a form, not being a Control, does not.
    ("Krypton.Toolkit.Suite.Extended.File.Copier/UX/KryptonFileCopier.cs",
     "monitor.SynchronizationObject = this;",
     "monitor.SynchronizationObject = Controls.Owner;"),
    ("Examples/DockExtenderExample.cs",
     "_dockExtender = new(this);",
     "_dockExtender = new(Controls.Owner);"),
    ("Examples/DockExtenderExample.cs",
     "ToolStripItem item = viewToolStripMenuItem.DropDownItems.Add(floatables.Text);",
     "ToolStripItem item = (ToolStripItem)viewToolStripMenuItem.DropDownItems.Add(floatables.Text);"),
    # A drop-down anchored to `this` -- with a form there, the (Control, Point) overload does not apply and
    # the call silently reaches (Point, ToolStripDropDownDirection) instead.
    ("Krypton.Toolkit.Suite.Extended.Notifications/UX/PopUp/KryptonPopUpNotificationWindow.cs",
     "PopUp.OptionsMenu.Show(this, new Point(",
     "PopUp.OptionsMenu.Show(Controls.Owner, new Point("),
]

# In Majorsilence.Forms the ToolStripItem/MenuItem hierarchy is INVERTED relative to WinForms:
# ToolStripItem derives from MenuItem, not the other way round. So menu collections are MenuItem-typed and
# handing their contents to a ToolStripItem-typed variable is a downcast, not the upcast it is on Windows.
# Every item these particular call sites touch was added as a ToolStripMenuItem, so the cast holds; a cast
# here is the boundary adaptation, rather than reshaping a core menu type that thousands of tests pin.
TOOLSTRIP_ITEM_CASTS = [
    ("Krypton.Toolkit.Suite.Extended.Controls/Components/General/Popup.cs",
     "OwnerItem = popupControl?.Items[0];",
     "OwnerItem = (ToolStripItem?)popupControl?.Items[0];"),
    ("Krypton.Toolkit.Suite.Extended.IO/Classes/MRU/MostRecentlyUsedFileManager.cs",
     "item = _parentMenuItem.DropDownItems.Add(value);",
     "item = (ToolStripItem)_parentMenuItem.DropDownItems.Add(value);"),
    ("Krypton.Toolkit.Suite.Extended.IO/Classes/MRU/MostRecentlyUsedFileManager.cs",
     "item = _parentMenuItem.DropDownItems.Add(_clearListText);",
     "item = (ToolStripItem)_parentMenuItem.DropDownItems.Add(_clearListText);"),
    ("Krypton.Toolkit.Suite.Extended.Tool.Strip.Items/Classes/IO/MostRecentlyUsedFileManager.cs",
     "tSI = _parentMenuItem.DropDownItems.Add(s);",
     "tSI = (ToolStripItem)_parentMenuItem.DropDownItems.Add(s);"),
    ("Krypton.Toolkit.Suite.Extended.Tool.Strip.Items/Classes/IO/MostRecentlyUsedFileManager.cs",
     'tSI = _parentMenuItem.DropDownItems.Add("&Clear list");',
     'tSI = (ToolStripItem)_parentMenuItem.DropDownItems.Add("&Clear list");'),
]

# One-off null dereferences that fire before a field is assigned. Each is an ordering bug in the upstream
# constructor rather than anything the migration changed, and each aborts the process on first use.
NULL_GUARDS = [
    # KryptonCalendar's constructor does work that reaches its own Days array before the array exists: it
    # assigns HighlightRanges (whose setter calls UpdateHighlights) and builds its item collection (whose
    # CollectionChanged calls the renderer's PerformItemsLayout), and BOTH walk Days -- but _days is not
    # allocated until the SetViewRange call ten lines further down. Opening the Calendar example aborted the
    # process on the first of those, then on the second once the first was guarded.
    #
    # Guarding each consumer is whack-a-mole; there are several and nothing stops more appearing.
    # Initialising the field to an empty array fixes all of them at once, and is what the code means anyway:
    # before a view range is set there are no days, and "no days" is an empty sequence rather than null.
    # Consumers then iterate zero times or read Length 0 -- correct behaviour, not a suppressed error.
    ("Krypton.Toolkit.Suite.Extended.Calendar/Controls/KryptonCalendar.cs",
     "    private CalendarDay?[] _days;",
     "    // Empty until SetViewRange allocates it: the constructor reaches this array through\n"
     "    // HighlightRanges and through the item collection before that call happens.\n"
     "    private CalendarDay?[] _days = [];"),
    # Weeks has exactly the same lifetime as Days -- also allocated by SetViewRange (and already assigned
    # `[]` on one branch there, which is the upstream author agreeing this is the empty value).
    ("Krypton.Toolkit.Suite.Extended.Calendar/Controls/KryptonCalendar.cs",
     "    private CalendarWeek[] _weeks;",
     "    // Empty until SetViewRange allocates it, for the same reason as _days above.\n"
     "    private CalendarWeek[] _weeks = [];"),
    # CircularProgressBar disposes its background brush before creating it: RecreateBackgroundBrush runs
    # from the constructor (via the BackColor/style setters) when _backBrush is still null.
    ("Krypton.Toolkit.Suite.Extended.Circular.ProgressBar/Controls/CircularProgressBar.cs",
     """            _backBrush.Dispose();
            _backBrush = new SolidBrush(BackColor);""",
     """            // Null on the first call: this runs from the constructor, before the field is assigned.
            _backBrush?.Dispose();
            _backBrush = new SolidBrush(BackColor);"""),
    # ...and the real fix for the renderer: this method walks Days and Weeks throughout, so running it
    # before either exists cannot produce anything useful. Guarding each field access inside it was
    # whack-a-mole -- the crash moved from Days at line ~738 to Weeks at ~1108 -- whereas one early return
    # covers the whole method. The item collection's CollectionChanged reaches here from the Calendar
    # constructor, which is the path that has no view range yet.
    ("Krypton.Toolkit.Suite.Extended.Calendar/Classes/CalendarRenderer.cs",
     """    public void PerformItemsLayout()
    {
        bool alldaychanged = false;""",
     """    public void PerformItemsLayout()
    {
        // No view range yet means no days and no weeks, and this method is a walk over both. Reached from
        // the Calendar constructor via the item collection's CollectionChanged, before SetViewRange runs.
        if (Calendar.Days.Length == 0)
        {
            return;
        }

        bool alldaychanged = false;"""),
]


# Resources the code asks for that no longer exist anywhere in the repo. Not a migration casualty: the
# .resx was lost upstream and the hard-coded ResourceManager base name was never updated, so this throws
# MissingManifestResourceException on Windows too. Where the strings are recoverable from context they are
# substituted literally -- the alternative is a dead example, and a null caption renders blank anyway.
LOST_RESOURCES = [
    ("Krypton.Toolkit.Suite.Extended.Navi.Suite/Classes/Layout/NaviLayoutEngineOffice.cs",
     """        ResourceManager rm = new ResourceManager(
            "NaviSuite.Properties.Resources.Text", Assembly.GetExecutingAssembly());""",
     """        // The "NaviSuite.Properties.Resources.Text" resx this used to read is not in the repo, and
        // neither are its four strings -- see LOST_RESOURCES in the fixup script. The captions below are
        // the navigation-pane menu labels these items mimic, substituted so the menu reads correctly."""),
    ("Krypton.Toolkit.Suite.Extended.Navi.Suite/Classes/Layout/NaviLayoutEngineOffice.cs",
     'rm.GetString("BarShowMore")', '"Show More Buttons"'),
    ("Krypton.Toolkit.Suite.Extended.Navi.Suite/Classes/Layout/NaviLayoutEngineOffice.cs",
     'rm.GetString("BarShowLess")', '"Show Fewer Buttons"'),
    ("Krypton.Toolkit.Suite.Extended.Navi.Suite/Classes/Layout/NaviLayoutEngineOffice.cs",
     'rm.GetString("BarOptions")', '"Navigation Pane Options..."'),
    ("Krypton.Toolkit.Suite.Extended.Navi.Suite/Classes/Layout/NaviLayoutEngineOffice.cs",
     'rm.GetString("BarAddOrRemove")', '"Add or Remove Buttons"'),
]


def apply_null_guards(root: Path) -> int:
    """Guards constructor-ordering null dereferences."""
    changed = 0
    for relative, old, new in NULL_GUARDS + LOST_RESOURCES:
        path = root / relative
        if not path.is_file():
            print(f"skipped    {relative}  (not found)")
            continue

        text = path.read_text(encoding="utf-8")
        if old not in text:
            continue

        path.write_text(text.replace(old, new, 1), encoding="utf-8")
        print(f"guarded    {relative}  (null deref)")
        changed += 1

    return changed


# Projects that already carry SOME Standard-Toolkit reference are skipped by wire_standard_toolkit, so a
# project missing just one assembly needs it named explicitly. (project, Standard-Toolkit project to add).
EXTRA_PROJECT_REFS = [
    ("Krypton.Toolkit.Suite.Extended.Core/Krypton.Toolkit.Suite.Extended.Core 2022.csproj",
     "Krypton.Docking/Krypton.Docking 2022.csproj"),
    # KryptonToastIcon and the rest of the toast family live in Krypton.Utilities, not Krypton.Toolkit.
    ("Krypton.Toolkit.Suite.Extended.ToastNotification/Krypton.Toolkit.Suite.Extended.ToastNotification 2022.csproj",
     "Krypton.Utilities/Krypton.Utilities.csproj"),
]


def add_extra_project_refs(root: Path) -> int:
    """Adds individually-missing Standard-Toolkit references to already-wired projects."""
    changed = 0
    for relative, needed in EXTRA_PROJECT_REFS:
        csproj = root / relative
        if not csproj.is_file():
            print(f"skipped    {relative}  (not found)")
            continue

        text = csproj.read_text(encoding="utf-8")
        escaped = needed.replace("/", chr(92))
        if escaped in text:
            continue

        ref = (f'    <ProjectReference Include="$(MSBuildThisFileDirectory)..\\..\\..\\..\\'
               f'Standard-Toolkit\\Source\\Krypton Components\\{escaped}" />')
        insert_at = text.rindex("</Project>")
        csproj.write_text(f"{text[:insert_at]}  <ItemGroup>\n{ref}\n  </ItemGroup>\n\n{text[insert_at:]}",
                          encoding="utf-8")
        print(f"referenced {Path(relative).parent.name} -> {Path(needed).name}")
        changed += 1

    return changed

# `Drawing.Graphics` is ambiguous inside a namespace that also sees System.Drawing: it binds there and
# fails, because this fork's Graphics lives in Majorsilence.Forms.Drawing. Fully qualifying settles it.
QUALIFY_DRAWING = [
    ("Krypton.Toolkit.Suite.Extended.Ribbon/Classes/Generic/RibbonMerger.cs",
     "using var g = Drawing.Graphics.FromHwnd(",
     "using var g = Majorsilence.Forms.Drawing.Graphics.FromHwnd("),
]


def bridge_form_as_control(root: Path) -> int:
    """Routes `this` through Controls.Owner where a form is used as a Control."""
    changed = set()
    for relative, old, new in FORM_AS_CONTROL + QUALIFY_DRAWING + TOOLSTRIP_ITEM_CASTS:
        path = root / relative
        if not path.is_file():
            print(f"skipped    {relative}  (not found)")
            continue

        text = path.read_text(encoding="utf-8")
        if old not in text:
            continue

        path.write_text(text.replace(old, new), encoding="utf-8")
        changed.add(relative)

    for relative in sorted(changed):
        print(f"bridged    {relative}")

    return len(changed)


# Expression-bodied event raisers written as `EventName.Invoke(...)` rather than `EventName?.Invoke(...)`.
# With no subscriber the event is null and the raise is a NullReferenceException that takes the process
# down -- not a port artefact, it does the same on Windows. It surfaced immediately: the Examples designer
# assigns KryptonOKDialogButton.ParentWindow, whose setter raises ParentWindowChanged, which nothing
# subscribes to, so opening the Button Items example aborted the app.
#
# Restricted to lines that also declare a void-returning method, so a LINQ chain that happens to read
# `something.Invoke(` (or `.Select(`) on an expression-bodied property is left alone.
UNGUARDED_EVENT_RAISE = re.compile(
    r"(?P<head>\bvoid\s+\w+\s*\([^)]*\)\s*=>\s*)(?P<event>[A-Za-z_]\w*)\.Invoke\s*\(")


def guard_event_raises(root: Path) -> int:
    """Makes expression-bodied event raisers null-conditional."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = path.read_text(encoding="utf-8", errors="ignore")
        if ".Invoke(" not in text:
            continue

        rewritten, count = UNGUARDED_EVENT_RAISE.subn(
            lambda m: f"{m.group('head')}{m.group('event')}?.Invoke(", text)
        if count:
            path.write_text(rewritten, encoding="utf-8")
            print(f"guarded    {path.relative_to(root)}  ({count}x event raise)")
            changed += 1

    return changed


# The block-bodied form of the same bug: `SomeEvent.Invoke(...);` as a statement. Guarding these needs more
# care than the expression-bodied pass above, because `.Invoke(` is also how a Control marshals a delegate
# onto the UI thread -- and there a null target is a real bug that must not be masked. So a site is only
# rewritten when the SAME FILE declares that identifier as an event or as a callback delegate field. Sites
# already inside an `if (x != null)` get a redundant `?.`, which is harmless and cheaper than proving intent.
EVENT_DECLARATION = re.compile(r"\bevent\s+[\w.<>,?\[\]\s]+?\s+(?P<name>\w+)\s*(?:;|\{|=)")
# The type may contain spaces once comments are stripped (`Action<Attributes   >`), so the type class
# allows whitespace and the name must be followed by a declarator to keep the lazy match honest.
CALLBACK_FIELD = re.compile(
    r"\b(?:public|private|protected|internal)\s+[\w.<>,?\[\]\s]+?\s*\b(?P<name>\w*Callback)\b\s*(?:\{|;|=)")
BLOCK_RAISE = re.compile(r"(?<![\w.?])(?P<name>[A-Za-z_]\w*)\.Invoke\s*\(")

# C# also lets you raise an event by invoking the delegate directly -- `ItemsPositioned(this, e);` with no
# `.Invoke` at all. Textually that is indistinguishable from an ordinary method call, which is why this is
# only ever applied to identifiers the file declares as events: 2,340 lines in the suite match the shape and
# almost all of them are method calls. The Calendar's OnItemsPositioned/OnItemSelected are written this way,
# and both were live NREs.
DIRECT_RAISE = re.compile(r"(?P<indent>^[ \t]*)(?P<name>[A-Za-z_]\w*)\s*\((?P<args>[^;()]*)\)\s*;", re.M)
BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.S)
NEVER_NULL_DELEGATE = re.compile(
    r"\b(?P<name>\w+)\s*(?:\{\s*get;\s*set;\s*\})?\s*=\s*(?:delegate\s*\{|\([^)]*\)\s*=>)")


def guard_block_event_raises(root: Path) -> int:
    """Guards statement-form event/callback raises, per-file and only for declared events."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = path.read_text(encoding="utf-8", errors="ignore")

        # Deliberately NOT gated on ".Invoke(" being present: the direct-invocation form below has no
        # ".Invoke" in it, and skipping on that string hid the Calendar's raises entirely.
        if "event " not in text and "Callback" not in text:
            continue

        # Declarations are scanned with block comments stripped: an inline `/*control*/` between a generic
        # type and its name broke the type pattern and hid a genuinely null-defaulted callback
        # (CommonDialogHandler.ClickCallback, three unguarded raises).
        declarations = BLOCK_COMMENT.sub(" ", text)
        raisable = {m.group("name") for m in EVENT_DECLARATION.finditer(declarations)}
        raisable |= {m.group("name") for m in CALLBACK_FIELD.finditer(declarations)}

        # A delegate initialised to `= delegate { }` or `= (s, e) => { }` is never null by design -- the
        # ScottPlot code does this deliberately. Guarding it would be noise, so leave those alone.
        raisable -= {m.group("name") for m in NEVER_NULL_DELEGATE.finditer(declarations)}
        if not raisable:
            continue

        rewritten, count = BLOCK_RAISE.subn(
            lambda m: (f"{m.group('name')}?.Invoke(" if m.group("name") in raisable
                       else m.group(0)), text)

        rewritten, direct = DIRECT_RAISE.subn(
            lambda m: (f"{m.group('indent')}{m.group('name')}?.Invoke({m.group('args')});"
                       if m.group("name") in raisable else m.group(0)), rewritten)
        count += direct

        if count and rewritten != text:
            path.write_text(rewritten, encoding="utf-8")
            print(f"guarded    {path.relative_to(root)}  ({count}x declared-event raise)")
            changed += 1

    return changed


MISSING_DEBUG_HELPER = re.compile(
    r"^([ \t]*)(Utilities\.DebugWithIndentation\([^;]*?\);)[ \t]*\r?\n", re.M)


def strip_missing_debug_helper(root: Path) -> int:
    """Comments out calls to a debug-only helper class Extended references but does not define."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = path.read_text(encoding="utf-8", errors="ignore")
        if "Utilities.DebugWithIndentation" not in text:
            continue

        rewritten, count = MISSING_DEBUG_HELPER.subn(
            r"\1// The Utilities debug helper this called is not defined anywhere in the suite; the call\n"
            r"\1// only ever compiled in configurations that excluded it.\n\1// \2\n", text)
        if count:
            path.write_text(rewritten, encoding="utf-8")
            print(f"patched    {path.relative_to(root)}  ({count}x missing debug helper)")
            changed += 1

    return changed


# Walking up `Parent` from a control can never arrive at a Form here -- the chain tops out at the form's
# root ControlAdapter, because a Form is not a Control. So `parent is KryptonForm` is always false and the
# cast that follows it does not even compile. MFFormBridge.AsKryptonForm asks the question that actually
# works: is this control the root adapter of a KryptonForm? The Standard-Toolkit bridge defines it.
KRYPTON_FORM_PROBE = re.compile(
    r"if \(parent is KryptonForm\)\s*\{\s*"
    r"KryptonForm form = \(KryptonForm\)parent;\s*"
    r"(form\.\w+ = this;)\s*\}", re.S)

# Raw strings: a plain "\\1" here is the octal escape for byte 0x01, not a group reference.
KRYPTON_FORM_PROBE_REPLACEMENT = (
    r"if (MFFormBridge.AsKryptonForm(parent) is { } form)" "\n"
    r"        {" "\n"
    r"            \1" "\n"
    r"        }")


def bridge_krypton_form_probe(root: Path) -> int:
    """Rewrites `parent is KryptonForm` walks to go through MFFormBridge."""
    changed = 0
    for path in root.rglob("*.cs"):
        if "/obj/" in str(path) or "/bin/" in str(path):
            continue

        text = path.read_text(encoding="utf-8", errors="ignore")
        if "parent is KryptonForm" not in text:
            continue

        rewritten, count = KRYPTON_FORM_PROBE.subn(KRYPTON_FORM_PROBE_REPLACEMENT, text)
        if count:
            path.write_text(rewritten, encoding="utf-8")
            print(f"bridged    {path.relative_to(root)}  ({count}x KryptonForm probe)")
            changed += 1

    return changed


# Types whose namespace a project uses without importing it, relying on a global using that this repo
# layout does not provide. Added to the project's existing GlobalUsings.cs. (project dir, namespace).
GLOBAL_USINGS = [
    ("Krypton.Toolkit.Suite.Extended.ToastNotification", "Krypton.Utilities"),
    ("Examples", "Krypton.Utilities"),
]


def add_global_usings(root: Path) -> int:
    """Appends missing global usings to a project's GlobalUsings.cs."""
    changed = 0
    for project, namespace in GLOBAL_USINGS:
        path = root / project / "GlobalUsings.cs"
        if not path.is_file():
            print(f"skipped    {project}/GlobalUsings.cs  (not found)")
            continue

        text = path.read_text(encoding="utf-8")
        directive = f"global using {namespace};"
        if directive in text:
            continue

        path.write_text(f"{text.rstrip()}\n\n{directive}\n", encoding="utf-8")
        print(f"imported   {project}  -> {namespace}")
        changed += 1

    return changed


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)

    root = Path(sys.argv[1]).resolve()
    if not root.is_dir():
        raise SystemExit(f"error: {root} is not a directory")

    changed = patch_exception_dialog(root)
    changed += wire_standard_toolkit(root)
    changed += strip_permission_sets(root)
    changed += strip_code_access_security(root)
    changed += bridge_form_as_control(root)
    changed += strip_missing_debug_helper(root)
    changed += rename_drifted_types(root)
    changed += add_extra_project_refs(root)
    changed += bridge_krypton_form_probe(root)
    changed += add_global_usings(root)
    changed += guard_event_raises(root)
    changed += guard_block_event_raises(root)
    changed += apply_null_guards(root)
    print(f"{changed} file(s) changed" if changed else "unchanged  (already patched)")


if __name__ == "__main__":
    main()
