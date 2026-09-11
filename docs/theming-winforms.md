# CSS theming for real System.Windows.Forms apps

`Majorsilence.Forms.Theming.WinForms` applies the same CSS theme subset described in
[theming.md](theming.md) to **real `System.Windows.Forms` controls**. It exists for mixed migration
apps: Majorsilence.Forms controls embedded in a WinForms shell (`MajorsilenceFormsPresenter`,
`WinFormsInterop`), or WinForms controls hosted inside Majorsilence.Forms. Without it the
Majorsilence half follows the `.css` file and the WinForms half does not, and the two drift apart
visually. With it, one stylesheet — same tokens, same selectors, same diagnostics — restyles both, as
far as WinForms allows.

Windows only (`net8.0-windows` / `net10.0-windows`). It depends on the Majorsilence.Forms core for the
parser and the host-neutral `ThemeStyleSheet.Tokens` / `Rules` model, not on any renderer.

## Using it

```csharp
using Majorsilence.Forms.Theming.WinForms;

[STAThread]
static void Main ()
{
    ApplicationConfiguration.Initialize ();

    // Before the first form: the two process-wide switches (Application.SetDefaultFont,
    // Application.SetColorMode) only take effect then. Throws ThemeCssException on errors.
    WinFormsCssTheme.Apply (File.ReadAllText ("Themes/graphite.css"));

    var main = new MainForm ();
    WinFormsCssTheme.Track (main);    // style it now and keep styling controls added later
    Application.Run (main);
}
```

| Member | What it does |
|---|---|
| `WinFormsCssTheme.Apply (string css)` | Parses, throws `ThemeCssException` on errors, applies to `Theme` and to every open and tracked form. |
| `WinFormsCssTheme.Apply (ThemeStyleSheet sheet)` | Applies whatever parsed, errors or not — what a live editor wants mid-edit. |
| `WinFormsCssTheme.Watch (path)` | Applies the file now and again on every save (debounced). Dispose the returned watcher to stop. The Theme Studio workflow. |
| `WinFormsCssTheme.Track (form)` | Styles a form and hooks `ControlAdded` (recursively) and `HandleCreated`, so late controls and the title bar are themed. Forms open at apply time are tracked automatically. |
| `WinFormsCssTheme.Diagnostics` | What WinForms could not express in the last apply (see below). |
| `WinFormsCssTheme.Support` / `WinFormsThemeSupport` | The property × control matrix this page is generated from. |

Every apply also runs `Theme.ApplyStyleSheet`, so embedded Majorsilence.Forms surfaces change with the
native controls; and once any member of `WinFormsCssTheme` has been used, a sheet applied from the
Majorsilence.Forms side (`Theme.LoadFromCss`, `Theme.ApplyTheme`) is mirrored onto WinForms through
`Theme.StyleSheetApplied`.

## How it works

WinForms has no styling system to hook, only per-control properties, a handful of renderer seams and
two process-wide switches. The applier therefore *walks the control tree and sets properties*:

- **Tokens** become the defaults: `--background-color` / `--foreground-color` on each `Form` (WinForms
  ambient inheritance carries them to every child that sets none of its own), `--control-low-color` on
  lists, trees, grids and text inputs, `--control-mid-color` on buttons and combo boxes,
  `--border-low-color` on flat button borders and grid lines, `--accent-color` on hovered buttons and
  grid selection, `--control-highlight-low-color` on hovered menu items and on the selected
  ListBox / ListView / TreeView item, `--accent-color-2` on the `ProgressBar` fill (its `ForeColor`,
  honoured once visual styles are switched off for that window — the grammar has no `ProgressBar`
  selector, so this is token-only).
- **Control rules** override those per type, through the seams in the matrix below: `FlatStyle.Flat` +
  `FlatAppearance` on buttons, `BorderStyle` for `border: none`, `DefaultCellStyle` /
  `ColumnHeadersDefaultCellStyle` / `RowHeadersDefaultCellStyle` / `AlternatingRowsDefaultCellStyle`
  on `DataGridView`, `DrawMode.OwnerDrawFixed` on a `TabControl` when a `TabStrip` rule exists.
- **Strips** — `MenuStrip`, `ToolStrip`, `StatusStrip`, `ContextMenuStrip` and every drop-down — are
  painted by a `ToolStripProfessionalRenderer` whose `ProfessionalColorTable` is built from the tokens
  and the `Menu` / `ToolBar` / `StatusBar` / `MenuDropDown` rules, installed on
  `ToolStripManager.Renderer`. Item text follows the `::item` / `::item:hover` colours.
- **Process-wide**: `Application.SetDefaultFont` from `Form { font-family; font-size }` (or `--ui-font` /
  `--font-size`), and on .NET 9+ `Application.SetColorMode (Dark)` when `--background-color` is dark.
  Both only work before the first window exists; a late apply reports that as an info diagnostic and
  still sets fonts per form.
- **Title bar**: on Windows 11, `DwmSetWindowAttribute` sets the caption colour to the form background,
  the caption text to its foreground, the frame to `Form { border-color }`, and the dark-mode flag from
  the background luminance.

### Explicit per-control values win

A control whose `BackColor`, `ForeColor` or `Font` the app set explicitly (the designer wrote it, or
code did before the theme was applied) keeps it — detected through `PropertyDescriptor.ShouldSerializeValue`,
the public spelling of `ShouldSerializeBackColor` and friends. A value the app changes *after* the
theme was applied is also left alone from then on. The applier only writes properties it owns, so a
re-apply moves exactly what the previous apply set.

### Diagnostics

The rule the whole subset is built on is *never silently no-op*. Every declaration in an applied sheet
is checked against the matrix:

| Situation | Severity | Example |
|---|---|---|
| Selector with no WinForms counterpart | info | `'NavigationPane' has no System.Windows.Forms counterpart; the rule styles Majorsilence.Forms controls only.` |
| Property that applies approximately | info | `'Button { border-radius }' applies approximately on WinForms (Button): A rounded Region clips the button; corners are not anti-aliased.` |
| Property WinForms cannot express | warning | `'Button:hover { color }' is not supported on WinForms (Button) and was skipped there: ...` |
| Process-wide switch applied too late | info | `Application.SetDefaultFont can only run before the first window is created; ...` |

They use the same `ThemeCssDiagnostic` type as the parser, so the Theme Studio and a coding assistant
see one list. Parse problems stay on the sheet (`ThemeStyleSheet.Diagnostics`); the WinForms list is
`WinFormsCssTheme.Diagnostics`.

## Support matrix

Generated from `WinFormsThemeSupport`; a test fails when the code and this section drift. Regenerate
with `MAJORSILENCE_WRITE_THEMING_WINFORMS_DOC=1 dotnet test tests/Majorsilence.Forms.Theming.WinForms.Tests`.

<!-- BEGIN GENERATED: winforms-support (WinFormsThemeSupport.ToMarkdown) -->
### Selector mapping

| Selector | System.Windows.Forms | Notes |
|---|---|---|
| `Form` | `Form` | BackColor / ForeColor / Font (the WinForms ambient defaults every child inherits); the Windows 11 title bar and frame via DwmSetWindowAttribute. |
| `Button` | `Button` | FlatStyle.Flat + FlatAppearance. Mapped buttons are switched to FlatStyle.Flat so their colours are honoured. |
| `Label` | `Label` | BackColor / ForeColor / Font. |
| `LinkLabel` | `LinkLabel` | LinkColor; :hover color maps to ActiveLinkColor. |
| `TextBox` | `TextBox, DateTimePicker` | BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None. |
| `NumericUpDown` | `NumericUpDown` | BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None. |
| `ComboBox` | `ComboBox` | BackColor / ForeColor / Font (FlatStyle.Flat so the colours are honoured). |
| `CheckBox` | `CheckBox` | BackColor / ForeColor / Font; the box glyph follows ForeColor only in FlatStyle.Flat, which is not forced. |
| `RadioButton` | `RadioButton` | BackColor / ForeColor / Font; the glyph stays native-drawn. |
| `Panel` | `Panel, FlowLayoutPanel, TableLayoutPanel, TabPage, SplitterPanel` | BackColor / ForeColor. |
| `GroupBox` | `GroupBox` | ForeColor is the caption; the frame is native-drawn. |
| `SplitContainer` | `SplitContainer` | BackColor is the splitter bar (the panels are Panels). |
| `Splitter` | `Splitter` | BackColor. |
| `PictureBox` | `PictureBox` | BackColor. |
| `ListBox` | `ListBox, CheckedListBox` | BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None. Items are owner-drawn (DrawMode.OwnerDrawFixed) so the selection takes the ::selection colours; CheckedListBox and lists the app already owner-draws stay as they are. |
| `ListView` | `ListView` | BackColor / ForeColor / Font. In Details view the items and column headers are owner-drawn so the selection takes the ::selection colours; other views, lists with CheckBoxes and lists the app already owner-draws stay native. |
| `TreeView` | `TreeView` | BackColor / ForeColor / Font (node lines follow ForeColor via LineColor); the selected node's text cell is owner-drawn (DrawMode.OwnerDrawText). |
| `DataGridView` | `DataGridView` | BackgroundColor, GridColor, DefaultCellStyle and the header/selection/alternating-row cell styles (EnableHeadersVisualStyles is switched off). |
| `Menu` | `MenuStrip` | A ToolStripProfessionalRenderer built from the theme's tokens is installed on ToolStripManager.Renderer. |
| `MenuDropDown` | `ContextMenuStrip, ToolStripDropDownMenu` | A ToolStripProfessionalRenderer built from the theme's tokens is installed on ToolStripManager.Renderer. |
| `ToolBar` | `ToolStrip` | A ToolStripProfessionalRenderer built from the theme's tokens is installed on ToolStripManager.Renderer. |
| `StatusBar` | `StatusStrip` | A ToolStripProfessionalRenderer built from the theme's tokens is installed on ToolStripManager.Renderer. |
| `TabControl` | `TabControl` | TabPage backgrounds; any colour on the header band needs owner draw (see TabStrip). |
| `TabStrip` | `TabControl (the header row, via DrawMode.OwnerDrawFixed)` | Owner-drawn tab headers; WinForms tabs do not repaint on hover. |
| `TrackBar` | `TrackBar` | BackColor only; the groove and thumb are native-drawn. |
| `MonthCalendar` | `MonthCalendar` | BackColor / ForeColor apply to the day grid; the title keeps visual styles. |
| `ScrollBar` | `HScrollBar, VScrollBar` | Native-drawn; no colour applies without owner-drawing a replacement control. |
| `PropertyGrid` | `PropertyGrid` | ViewBackColor / ViewForeColor / LineColor / HelpBackColor. |
| `NavigationPane` | — | System.Windows.Forms has no Outlook-style navigation pane; the rule is reported as an info diagnostic and skipped. |
| `Ribbon` | — | System.Windows.Forms has no ribbon control; the rule is reported as an info diagnostic and skipped. |

### Property support

Unsupported rows are reported as diagnostics when a stylesheet uses them — never silently ignored.

| Rule | Property | Support | Notes |
|---|---|---|---|
| `Form` | `background-color` | native | Form.BackColor; also the Windows 11 title-bar colour (DWMWA_CAPTION_COLOR). |
| `Form` | `color` | native | Form.ForeColor (inherited by children) and the title-bar caption text (DWMWA_TEXT_COLOR). |
| `Form` | `border-color` | approximate | The window frame colour on Windows 11 (DWMWA_BORDER_COLOR); older Windows ignores it. |
| `Form` | `font-family` | native | Form.Font — the ambient font every child without its own inherits. Application.SetDefaultFont is used too when no window exists yet. |
| `Form` | `font-size` | native | Form.Font — the ambient font every child without its own inherits. Application.SetDefaultFont is used too when no window exists yet. |
| `Form` | `font-weight` | native | Form.Font — the ambient font every child without its own inherits. Application.SetDefaultFont is used too when no window exists yet. |
| `Form` | `font-style` | native | Form.Font — the ambient font every child without its own inherits. Application.SetDefaultFont is used too when no window exists yet. |
| `Button` | `background-color` | native | Button.BackColor / ForeColor (FlatStyle.Flat). |
| `Button` | `color` | native | Button.BackColor / ForeColor (FlatStyle.Flat). |
| `Button` | `border-width` | native | FlatAppearance.BorderSize; 0 ('border: none') removes the border. |
| `Button` | `border-color` | native | FlatAppearance.BorderColor. |
| `Button` | `border-radius` | approximate | A rounded Region clips the button; corners are not anti-aliased. |
| `Button` | `font-family` | native | Button.Font. |
| `Button` | `font-size` | native | Button.Font. |
| `Button` | `font-weight` | native | Button.Font. |
| `Button` | `font-style` | native | Button.Font. |
| `Button:hover` | `background-color` | native | FlatAppearance.MouseOverBackColor (MouseDownBackColor is derived from it). |
| `Label` | `background-color` | native | Label.BackColor / ForeColor. |
| `Label` | `color` | native | Label.BackColor / ForeColor. |
| `Label` | `font-family` | native | Label.Font. |
| `Label` | `font-size` | native | Label.Font. |
| `Label` | `font-weight` | native | Label.Font. |
| `Label` | `font-style` | native | Label.Font. |
| `LinkLabel` | `background-color` | native | LinkLabel.BackColor. |
| `LinkLabel` | `color` | native | LinkLabel.LinkColor (and ForeColor). |
| `LinkLabel` | `font-family` | native | LinkLabel.Font. |
| `LinkLabel` | `font-size` | native | LinkLabel.Font. |
| `LinkLabel` | `font-weight` | native | LinkLabel.Font. |
| `LinkLabel` | `font-style` | native | LinkLabel.Font. |
| `LinkLabel:hover` | `color` | approximate | ActiveLinkColor — WinForms shows it while the link is pressed, not on hover. |
| `TextBox` | `background-color` | native | TextBox.BackColor / ForeColor. |
| `TextBox` | `color` | native | TextBox.BackColor / ForeColor. |
| `TextBox` | `font-family` | native | TextBox.Font. |
| `TextBox` | `font-size` | native | TextBox.Font. |
| `TextBox` | `font-weight` | native | TextBox.Font. |
| `TextBox` | `font-style` | native | TextBox.Font. |
| `TextBox` | `border-width` | approximate | 0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px. |
| `NumericUpDown` | `background-color` | native | NumericUpDown.BackColor / ForeColor. |
| `NumericUpDown` | `color` | native | NumericUpDown.BackColor / ForeColor. |
| `NumericUpDown` | `font-family` | native | NumericUpDown.Font. |
| `NumericUpDown` | `font-size` | native | NumericUpDown.Font. |
| `NumericUpDown` | `font-weight` | native | NumericUpDown.Font. |
| `NumericUpDown` | `font-style` | native | NumericUpDown.Font. |
| `NumericUpDown` | `border-width` | approximate | 0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px. |
| `ComboBox` | `background-color` | native | ComboBox.BackColor / ForeColor (FlatStyle.Flat). |
| `ComboBox` | `color` | native | ComboBox.BackColor / ForeColor (FlatStyle.Flat). |
| `ComboBox` | `font-family` | native | ComboBox.Font. |
| `ComboBox` | `font-size` | native | ComboBox.Font. |
| `ComboBox` | `font-weight` | native | ComboBox.Font. |
| `ComboBox` | `font-style` | native | ComboBox.Font. |
| `CheckBox` | `background-color` | native | CheckBox.BackColor / ForeColor (the glyph stays native-drawn). |
| `CheckBox` | `color` | native | CheckBox.BackColor / ForeColor (the glyph stays native-drawn). |
| `CheckBox` | `font-family` | native | CheckBox.Font. |
| `CheckBox` | `font-size` | native | CheckBox.Font. |
| `CheckBox` | `font-weight` | native | CheckBox.Font. |
| `CheckBox` | `font-style` | native | CheckBox.Font. |
| `RadioButton` | `background-color` | native | RadioButton.BackColor / ForeColor (the glyph stays native-drawn). |
| `RadioButton` | `color` | native | RadioButton.BackColor / ForeColor (the glyph stays native-drawn). |
| `RadioButton` | `font-family` | native | RadioButton.Font. |
| `RadioButton` | `font-size` | native | RadioButton.Font. |
| `RadioButton` | `font-weight` | native | RadioButton.Font. |
| `RadioButton` | `font-style` | native | RadioButton.Font. |
| `Panel` | `background-color` | native | Panel.BackColor / ForeColor. |
| `Panel` | `color` | native | Panel.BackColor / ForeColor. |
| `Panel` | `font-family` | native | Panel.Font (inherited by children). |
| `Panel` | `font-size` | native | Panel.Font (inherited by children). |
| `Panel` | `font-weight` | native | Panel.Font (inherited by children). |
| `Panel` | `font-style` | native | Panel.Font (inherited by children). |
| `Panel` | `border-width` | approximate | 0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px. |
| `GroupBox` | `background-color` | native | GroupBox.BackColor / ForeColor (the caption). |
| `GroupBox` | `color` | native | GroupBox.BackColor / ForeColor (the caption). |
| `GroupBox` | `font-family` | native | GroupBox.Font. |
| `GroupBox` | `font-size` | native | GroupBox.Font. |
| `GroupBox` | `font-weight` | native | GroupBox.Font. |
| `GroupBox` | `font-style` | native | GroupBox.Font. |
| `SplitContainer` | `background-color` | native | SplitContainer.BackColor / ForeColor. |
| `SplitContainer` | `color` | native | SplitContainer.BackColor / ForeColor. |
| `Splitter` | `background-color` | native | Splitter.BackColor / ForeColor. |
| `Splitter` | `color` | native | Splitter.BackColor / ForeColor. |
| `PictureBox` | `background-color` | native | PictureBox.BackColor / ForeColor. |
| `PictureBox` | `color` | native | PictureBox.BackColor / ForeColor. |
| `PictureBox` | `border-width` | approximate | 0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px. |
| `ListBox` | `background-color` | native | ListBox.BackColor / ForeColor. |
| `ListBox` | `color` | native | ListBox.BackColor / ForeColor. |
| `ListBox` | `font-family` | native | ListBox.Font. |
| `ListBox` | `font-size` | native | ListBox.Font. |
| `ListBox` | `font-weight` | native | ListBox.Font. |
| `ListBox` | `font-style` | native | ListBox.Font. |
| `ListBox` | `border-width` | approximate | 0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px. |
| `ListBox::selection` | `background-color` | approximate | Owner-drawn items (DrawMode.OwnerDrawFixed): the selected item's fill and text. Not applied to CheckedListBox (DrawMode.Normal only) or to a list the app already owner-draws. |
| `ListBox::selection` | `color` | approximate | Owner-drawn items (DrawMode.OwnerDrawFixed): the selected item's fill and text. Not applied to CheckedListBox (DrawMode.Normal only) or to a list the app already owner-draws. |
| `ListView` | `background-color` | native | ListView.BackColor / ForeColor. |
| `ListView` | `color` | native | ListView.BackColor / ForeColor. |
| `ListView` | `font-family` | native | ListView.Font. |
| `ListView` | `font-size` | native | ListView.Font. |
| `ListView` | `font-weight` | native | ListView.Font. |
| `ListView` | `font-style` | native | ListView.Font. |
| `ListView::selection` | `background-color` | approximate | Owner-drawn rows in Details view (OwnerDraw = true; headers are drawn in the list's colours too). Icon/tile/list views, lists with CheckBoxes and lists the app already owner-draws keep SystemColors.Highlight. |
| `ListView::selection` | `color` | approximate | Owner-drawn rows in Details view (OwnerDraw = true; headers are drawn in the list's colours too). Icon/tile/list views, lists with CheckBoxes and lists the app already owner-draws keep SystemColors.Highlight. |
| `TreeView` | `background-color` | native | TreeView.BackColor. |
| `TreeView` | `color` | native | TreeView.ForeColor and LineColor. |
| `TreeView` | `font-family` | native | TreeView.Font. |
| `TreeView` | `font-size` | native | TreeView.Font. |
| `TreeView` | `font-weight` | native | TreeView.Font. |
| `TreeView` | `font-style` | native | TreeView.Font. |
| `TreeView` | `border-width` | approximate | 0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px. |
| `TreeView::selection` | `background-color` | approximate | DrawMode.OwnerDrawText paints the selected node's text cell (glyphs, lines and images stay native). Not applied to a tree the app already owner-draws. |
| `TreeView::selection` | `color` | approximate | DrawMode.OwnerDrawText paints the selected node's text cell (glyphs, lines and images stay native). Not applied to a tree the app already owner-draws. |
| `DataGridView` | `background-color` | native | DataGridView.BackgroundColor and DefaultCellStyle.BackColor. |
| `DataGridView` | `color` | native | DefaultCellStyle.ForeColor. |
| `DataGridView` | `border-color` | native | DataGridView.GridColor (the cell grid lines). |
| `DataGridView` | `font-family` | native | DefaultCellStyle.Font. |
| `DataGridView` | `font-size` | native | DefaultCellStyle.Font. |
| `DataGridView` | `font-weight` | native | DefaultCellStyle.Font. |
| `DataGridView` | `font-style` | native | DefaultCellStyle.Font. |
| `DataGridView::header` | `background-color` | native | ColumnHeadersDefaultCellStyle (EnableHeadersVisualStyles = false). |
| `DataGridView::header` | `color` | native | ColumnHeadersDefaultCellStyle (EnableHeadersVisualStyles = false). |
| `DataGridView::header` | `font-family` | native | ColumnHeadersDefaultCellStyle.Font. |
| `DataGridView::header` | `font-size` | native | ColumnHeadersDefaultCellStyle.Font. |
| `DataGridView::header` | `font-weight` | native | ColumnHeadersDefaultCellStyle.Font. |
| `DataGridView::header` | `font-style` | native | ColumnHeadersDefaultCellStyle.Font. |
| `DataGridView::row-header` | `background-color` | native | RowHeadersDefaultCellStyle. |
| `DataGridView::row-header` | `color` | native | RowHeadersDefaultCellStyle. |
| `DataGridView::selection` | `background-color` | native | DefaultCellStyle.SelectionBackColor / SelectionForeColor (headers included). |
| `DataGridView::selection` | `color` | native | DefaultCellStyle.SelectionBackColor / SelectionForeColor (headers included). |
| `DataGridView::alternating-row` | `background-color` | native | AlternatingRowsDefaultCellStyle.BackColor. |
| `Menu` | `background-color` | native | MenuStrip.BackColor and the renderer's strip gradient. |
| `Menu` | `color` | native | MenuStrip.ForeColor (item text). |
| `Menu` | `font-family` | native | MenuStrip.Font. |
| `Menu` | `font-size` | native | MenuStrip.Font. |
| `Menu` | `font-weight` | native | MenuStrip.Font. |
| `Menu` | `font-style` | native | MenuStrip.Font. |
| `Menu::item` | `background-color` | native | The renderer paints items with these colours. |
| `Menu::item` | `color` | native | The renderer paints items with these colours. |
| `Menu::item:hover` | `background-color` | native | The renderer's hovered/open item colours (MenuItemSelected and the hover text colour). |
| `Menu::item:hover` | `color` | native | The renderer's hovered/open item colours (MenuItemSelected and the hover text colour). |
| `MenuDropDown` | `background-color` | native | The renderer's drop-down background (image margin included). |
| `MenuDropDown` | `color` | native | Drop-down item text. |
| `MenuDropDown` | `border-color` | native | The renderer's MenuBorder colour. |
| `MenuDropDown` | `font-family` | native | ToolStripDropDownMenu.Font (set when the drop-down is themed). |
| `MenuDropDown` | `font-size` | native | ToolStripDropDownMenu.Font (set when the drop-down is themed). |
| `MenuDropDown` | `font-weight` | native | ToolStripDropDownMenu.Font (set when the drop-down is themed). |
| `MenuDropDown` | `font-style` | native | ToolStripDropDownMenu.Font (set when the drop-down is themed). |
| `MenuDropDown::item` | `background-color` | native | The renderer paints drop-down items with these colours. |
| `MenuDropDown::item` | `color` | native | The renderer paints drop-down items with these colours. |
| `MenuDropDown::item:hover` | `background-color` | native | The renderer's hovered item colours. |
| `MenuDropDown::item:hover` | `color` | native | The renderer's hovered item colours. |
| `ToolBar` | `background-color` | native | ToolStrip.BackColor and the renderer's strip gradient. |
| `ToolBar` | `color` | native | ToolStrip.ForeColor (item text). |
| `ToolBar` | `font-family` | native | ToolStrip.Font. |
| `ToolBar` | `font-size` | native | ToolStrip.Font. |
| `ToolBar` | `font-weight` | native | ToolStrip.Font. |
| `ToolBar` | `font-style` | native | ToolStrip.Font. |
| `ToolBar::item` | `background-color` | native | The renderer paints items with these colours. |
| `ToolBar::item` | `color` | native | The renderer paints items with these colours. |
| `ToolBar::item:hover` | `background-color` | native | The renderer's hovered/checked item colours. |
| `ToolBar::item:hover` | `color` | native | The renderer's hovered/checked item colours. |
| `StatusBar` | `background-color` | native | StatusStrip.BackColor and the renderer's status gradient. |
| `StatusBar` | `color` | native | StatusStrip.ForeColor. |
| `StatusBar` | `font-family` | native | StatusStrip.Font. |
| `StatusBar` | `font-size` | native | StatusStrip.Font. |
| `StatusBar` | `font-weight` | native | StatusStrip.Font. |
| `StatusBar` | `font-style` | native | StatusStrip.Font. |
| `TabControl` | `background-color` | approximate | Applies to each TabPage's BackColor; the control's own band stays native-drawn. |
| `TabControl` | `color` | approximate | Applies to each TabPage's ForeColor. |
| `TabControl` | `font-family` | native | TabControl.Font. |
| `TabControl` | `font-size` | native | TabControl.Font. |
| `TabControl` | `font-weight` | native | TabControl.Font. |
| `TabControl` | `font-style` | native | TabControl.Font. |
| `TabStrip` | `background-color` | approximate | Owner-drawn tab headers (DrawMode.OwnerDrawFixed); the band around them stays native. |
| `TabStrip` | `color` | approximate | Owner-drawn tab headers (DrawMode.OwnerDrawFixed); the band around them stays native. |
| `TabStrip` | `font-family` | native | TabControl.Font. |
| `TabStrip` | `font-size` | native | TabControl.Font. |
| `TabStrip` | `font-weight` | native | TabControl.Font. |
| `TabStrip` | `font-style` | native | TabControl.Font. |
| `TabStrip::item` | `background-color` | approximate | The owner-drawn tab's fill and caption. |
| `TabStrip::item` | `color` | approximate | The owner-drawn tab's fill and caption. |
| `TabStrip::selected` | `background-color` | approximate | The owner-drawn selected tab's fill and caption. |
| `TabStrip::selected` | `color` | approximate | The owner-drawn selected tab's fill and caption. |
| `TabStrip::selected` | `border-bottom-color` | approximate | An accent underline drawn inside the owner-drawn selected tab. |
| `TabStrip::selected` | `border-bottom-width` | approximate | An accent underline drawn inside the owner-drawn selected tab. |
| `TrackBar` | `background-color` | native | TrackBar.BackColor. |
| `MonthCalendar` | `background-color` | approximate | MonthCalendar.BackColor / ForeColor recolour the day grid; the title bar and today circle keep visual styles. |
| `MonthCalendar` | `color` | approximate | MonthCalendar.BackColor / ForeColor recolour the day grid; the title bar and today circle keep visual styles. |
| `PropertyGrid` | `background-color` | native | PropertyGrid.BackColor, ViewBackColor and HelpBackColor. |
| `PropertyGrid` | `color` | native | PropertyGrid.ViewForeColor, HelpForeColor and CategoryForeColor. |
| `PropertyGrid` | `border-color` | native | PropertyGrid.LineColor (the category/grid lines). |
| `PropertyGrid` | `font-family` | native | PropertyGrid.Font. |
| `PropertyGrid` | `font-size` | native | PropertyGrid.Font. |
| `PropertyGrid` | `font-weight` | native | PropertyGrid.Font. |
| `PropertyGrid` | `font-style` | native | PropertyGrid.Font. |

### Unsupported (reported, then skipped)

| Rule | Property | Why |
|---|---|---|
| `Form` | `border-width`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The window frame is drawn by Windows; only its colour is themable (border-color, Windows 11). |
| `Button` | `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms control borders are uniform; use border-width / border-color. |
| `Button:hover` | `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | FlatAppearance only recolours the hovered background; text and border do not change on hover. |
| `Label` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | Labels have no border to style (BorderStyle has no colour). |
| `LinkLabel` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | Links have no border to style. |
| `LinkLabel:hover` | `background-color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Only the link colour has a pressed-state seam. |
| `TextBox` | `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). |
| `NumericUpDown` | `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). The spin arrows stay native-drawn. |
| `ComboBox` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The combo border and drop arrow are native-drawn; no colour is settable. |
| `CheckBox` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The check glyph is native-drawn. |
| `RadioButton` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The radio glyph is native-drawn. |
| `Panel` | `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). |
| `GroupBox` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The frame line is native-drawn; recolouring it needs an owner-drawn GroupBox. |
| `SplitContainer` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Only colours apply to the splitter bar. |
| `Splitter` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Only colours apply to a splitter bar. |
| `PictureBox` | `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). |
| `ListBox` | `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). |
| `ListView` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). |
| `TreeView` | `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | WinForms draws this control's border natively and exposes no colour for it (BorderStyle only). |
| `DataGridView` | `border-width`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The outer border is BorderStyle only; grid lines take a colour (border-color) but no width or radius. |
| `DataGridView::header` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | Header separator lines follow the grid colour; they have no seam of their own. |
| `DataGridView::row-header` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Row-header separators follow the grid colour. |
| `DataGridView::selection` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | WinForms draws no outline around the current cell beyond the focus rectangle. |
| `Menu` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The strip has no border seam beyond the renderer's own. |
| `MenuDropDown` | `border-width`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | Only the border colour of a drop-down is themable. |
| `ToolBar` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The strip has no border seam beyond the renderer's own. |
| `StatusBar` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The strip has no border seam. |
| `TabControl` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The frame around the pages is native-drawn. |
| `TabStrip` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The header band is native-drawn beyond the tabs themselves. |
| `TabStrip::item:hover` | `background-color`, `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | WinForms tabs do not repaint on hover. |
| `TabStrip::selected` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Only the fill, caption and underline of the selected tab are drawn. |
| `TabStrip::item` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Only the fill and caption of a tab are drawn. |
| `TrackBar` | `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | The groove, thumb and ticks are native-drawn. |
| `TrackBar:hover` | `background-color`, `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | WinForms track bars have no hover seam. |
| `MonthCalendar` | `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | The calendar chrome is native-drawn (TitleBackColor and friends only work with visual styles off, process-wide). |
| `ScrollBar` | `background-color`, `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | WinForms scroll bars are native-drawn; BackColor/ForeColor have no visible effect. |
| `ScrollBar::thumb` | `background-color`, `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | The thumb is native-drawn. |
| `ScrollBar::arrow` | `background-color`, `color`, `border-width`, `border-color`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`, `font-family`, `font-size`, `font-weight`, `font-style` | The arrows are native-drawn. |
| `PropertyGrid` | `border-width`, `border-radius`, `border-top-width`, `border-right-width`, `border-bottom-width`, `border-left-width`, `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color` | The grid draws no themable outer border. |
<!-- END GENERATED: winforms-support -->

## Out of scope

- Reproducing Majorsilence.Forms' pixel look in WinForms (rounded borders on text boxes, custom scroll
  bars). The goal is *one stylesheet, two hosts, no surprises*, with the gaps documented — not a
  WinForms skin engine.
- Owner-drawing every native control. The applier owner-draws only what the matrix marks
  *approximate* — tab headers, the selected ListBox item, the selected TreeView node's text, ListView
  rows and headers in Details view — and never a control the app already owner-draws. The `GroupBox`
  frame, check/radio glyphs, TrackBar and scroll bars stay native.

## Theme Studio for WinForms

`samples/ThemeStudio.WinForms` is the Windows-only head of the Theme Studio: the same editor and
diagnostics, with a real `System.Windows.Forms` preview panel (one of each mapped control) applying
through `WinFormsCssTheme`. `--screenshot out.png [theme.css]` renders the preview with
`Control.DrawToBitmap` and exits — the WinForms equivalent of the Avalonia head's `--render-headless`.
There is no headless WinForms: a desktop session is still required, and the window is shown off-screen
at zero opacity for the capture (child windows only exist once the form has been shown).
