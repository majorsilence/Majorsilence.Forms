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
    "Krypton.Docking/Krypton.Docking 2022.csproj": ("KryptonDockingManager", "KryptonDockableWorkspace"),
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


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)

    root = Path(sys.argv[1]).resolve()
    if not root.is_dir():
        raise SystemExit(f"error: {root} is not a directory")

    changed = patch_exception_dialog(root)
    changed += wire_standard_toolkit(root)
    print(f"{changed} file(s) changed" if changed else "unchanged  (already patched)")


if __name__ == "__main__":
    main()
