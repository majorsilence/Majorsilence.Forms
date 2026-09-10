# Theming with CSS

Majorsilence.Forms apps can be themed with a **small, fully documented subset of CSS**. A theme is a
`.css` file that sets the framework's colour and font tokens and, optionally, styles individual control
types. The subset is deliberately restricted so that:

- a **person** can learn the whole language from this one page;
- a **coding assistant** given this page (or the output of `ThemeCssReference.ToMarkdown ()`) can
  write a valid theme first time, and gets a precise, actionable error for anything outside the subset;
- the **[Theme Studio](#theme-studio-live-preview)** sample can apply an edit the moment it is typed.

Everything below is enforced by the parser and checked by tests (`ThemeCssTests`, `ThemeCssDocTests`);
the reference tables are generated from `ThemeCssReference`, so they cannot drift from the code.

> Prefer XML? The older `<Theme>` XML format ([`Theme.Xml.cs`](../src/Majorsilence.Forms/Theme.Xml.cs))
> still works, sets the same tokens, and can be mixed with CSS: an XML theme may use a CSS theme as its
> `base` and vice versa. XML has no equivalent of the per-control rules.

## Quick start

```csharp
using Majorsilence.Forms;

// 1. Apply a theme file straight away (the @theme header is optional here):
Theme.LoadFromCssFile ("Themes/ocean.css");

// 2. Or register it by name and switch between themes at runtime:
Theme.RegisterThemeCssFromFile ("Themes/ocean.css");   // returns "Ocean" from its @theme header
Theme.ApplyTheme ("Ocean");
Theme.SetBuiltInTheme (BuiltInTheme.Light);            // back to a built-in: resets everything

// 3. Start a new theme from the current one:
File.WriteAllText ("mine.css", Theme.ExportCss ("Mine", "Light"));
```

A complete theme:

```css
/* Ocean: a deep blue-green dark theme. */
@theme "Ocean" extends Dark;

:root {
  --brand: #1e90ff;                        /* your own variable */

  --accent-color: var(--brand);            /* theme tokens: one per Theme property */
  --accent-color-2: #006994;
  --background-color: #0a1929;
  --control-low-color: #0a1929;
  --control-mid-color: #102a43;
  --border-low-color: #15395c;
  --foreground-color: #cfe8ff;
  --foreground-color-on-accent: white;
  --text-selection-background-color: rgba(30, 144, 255, 0.5);
  --font-size: 14px;
  --ui-font: "Segoe UI", "Noto Sans", sans-serif;
}

/* Rules style a control TYPE: every Button in the app, unless code set that button's own colour. */
Button {
  border: 1px solid #15395c;
  border-radius: 4px;
}

Button:hover {
  background-color: var(--brand);
  color: white;
}

TextBox, ComboBox, NumericUpDown {
  background-color: #061120;
  border-color: var(--border-low-color);
}
```

Errors are reported with a line, a column, and what to write instead:

```text
error (12:3): Unknown property 'colour'. Did you mean 'color'? Supported properties: background-color, color, border, ...
error (20:1): 'TextBox:hover' is not supported: TextBox does not change appearance when hovered. ':hover' is available on: Button, LinkLabel, TrackBar.
```

## The language

A stylesheet has three kinds of statement. Whitespace and `/* comments */` are free.

### 1. `@theme` header (optional)

```css
@theme "Ocean" extends Dark;
```

- The name is required to **register** a theme (`Theme.RegisterThemeCss`); `Theme.LoadFromCss` and the
  Theme Studio accept a sheet without one.
- `extends` names the theme to start from: a built-in (`Light`, `Dark`, `Classic`, `Aero`,
  `PointOfSale`, `HotDog`) or any registered theme (CSS or XML). Applying the theme applies the base
  first, then this sheet. Without `extends`, the sheet layers onto whatever theme is current.
- Names may be quoted or bare identifiers. Only one `@theme` per file, and it is the only at-rule.

### 2. `:root` — theme tokens

```css
:root {
  --accent-color: #2a8ad0;
  --font-size: 14px;
  --ui-font: "Segoe UI", sans-serif;
}
```

`:root` accepts **only custom properties** (`--name`). A custom property is either:

- a **token** — one of the names in the table below, which sets the matching `Theme` property for the
  whole app the moment the sheet is applied; or
- **your own variable**, referenced later with `var(--name)`. A variable that is never referenced
  produces a warning (with a "did you mean" when it is close to a token name, since a typo'd token
  would otherwise silently do nothing).

Tokens are the kebab-case spelling of the `Theme` property: `AccentColor2` → `--accent-color-2`,
`UIFontBold` → `--ui-font-bold`.

### 3. Control rules

```css
Button { background-color: #333; color: white; border-radius: 4px; }
Button:hover { background-color: #444; }
TextBox, ComboBox { border: 1px solid #808080; }
```

- A selector is a **control type name** from the table below, optionally `:hover`, or a comma-separated
  list of those. Type names are matched case-insensitively.
- A rule sets the type's static default style (`Button.DefaultStyle`). Every instance that has not set
  the same property in code (`button.Style.BackgroundColor = ...`, or WinForms `BackColor`) picks it
  up; explicit per-control values still win, as in WinForms.
- `:hover` is available only on controls that repaint on hover (`Button`, `LinkLabel`, `TrackBar`).
  The hover style is layered on the normal one, so a `Button:hover` rule only needs the properties that
  change.
- Later declarations replace earlier ones, within a rule and across rules. There is no specificity,
  no cascade, and no `!important`.
- Derived controls without their own default style follow their base type's rule (`Panel` also styles
  `FlowLayoutPanel`, `TableLayoutPanel`, `TabPage`; `TextBox` also styles `DateTimePicker`; `ListBox`
  also styles `CheckedListBox`).

### Values

| Kind | Accepted forms | Notes |
|---|---|---|
| color | `#rgb` `#rgba` `#rrggbb` `#rrggbbaa` · `rgb(r, g, b)` `rgba(r, g, b, a)` · `rgb(r g b / a)` · `hsl(h, s%, l%)` `hsla(...)` · the 148 CSS colour names · `transparent` · `var(--x)` | Alpha is 0–1 (or a percentage). In hex, **alpha comes last** (`#rrggbbaa`) — the opposite of the `#AARRGGBB` order the XML format uses. |
| length | `14px` or `14` | Whole **pixels**, at 100% scaling. `pt`, `em`, `rem` and `%` are rejected, because every size in a Majorsilence.Forms style is a pixel value. |
| font family list | `"Segoe UI", "Noto Sans", sans-serif` | Quoted or bare names, comma-separated. The first family installed on the machine is used; a generic name (`sans-serif`, `serif`, `monospace`) is handed to the OS font matcher. |
| `var(--name)` | `var(--brand)` · `var(--brand, #333)` | Substitutes a `:root` variable, or a token. A token reference inside a control rule stays **live**: `Button:hover { background-color: var(--accent-color); }` follows later changes to `Theme.AccentColor`. |

### What is *not* supported, and what happens if you write it

Everything outside the subset is an **error with an explanation**, never silently ignored. The
offending declaration (or rule) is dropped and the rest of the sheet still applies.

<!-- BEGIN UNSUPPORTED -->
| You write | Why not | Write instead |
|---|---|---|
| `* { color: red; }` | There is no single style every control inherits from. | `:root { --foreground-color: red; }` for the theme-wide default. |
| `.primary { color: red; }` | Controls have no CSS classes or ids. | A type rule, and `button.Style.ForegroundColor` in code for one control. |
| `Panel Button { color: red; }` | A rule applies to every control of the type, wherever it sits. | `Button { color: red; }` |
| `Button:disabled { color: gray; }` | The only pseudo-class is `:hover`. | `:root { --foreground-disabled-color: gray; }` |
| `TextBox:hover { color: red; }` | `TextBox` does not repaint on hover. | Only `Button`, `LinkLabel`, `TrackBar` take `:hover`. |
| `Button { font-size: 12pt; }` | Sizes are pixels. | `font-size: 16px;` |
| `Button { color: red !important; }` | There is no cascade to override. | Put the declaration later in the file. |
| `@import url(base.css);` | No file inclusion. | Register the base theme, then `@theme "X" extends Base;`. |
| `@media (prefers-color-scheme: dark) { }` | No media queries. | Register two themes and pick one in code. |
| `Button { margin: 4px; }` | Layout is not themable; only colours, borders and fonts are. | Set `Padding`/`Margin` in code. |
| `Button { border: 1px dashed red; }` | Borders are always solid. | `border: 1px solid red;` |
| `Button { background: red; }` | Only the longhand is recognised (`background` would imply images/gradients). | `background-color: red;` |
| `Button { color: linear-gradient(red, blue); }` | Gradients and images are not colours. | A flat colour. |
| `:root { color: red; }` | `:root` takes tokens only. | `Form { color: red; }` or `--foreground-color`. |
| `Button { --pad: 4px; }` | Variables are declared in `:root` only. | Move the declaration to `:root`; use `var(--pad)` here. |
<!-- END UNSUPPORTED -->

## Reference

<!-- BEGIN GENERATED: reference (ThemeCssReference.ToMarkdown) -->
### Tokens (`:root` custom properties)

| Token | Value | Sets `Theme.` | What it is | Read by |
|---|---|---|---|---|
| `--accent-color` | color | `AccentColor` | The primary accent. | Button hover background, LinkLabel text, selected DataGridView cells, MonthCalendar selection, TrackBar thumb, the custom title bar. |
| `--accent-color-2` | color | `AccentColor2` | The secondary accent, used where the primary one needs a companion. | Button hover border, Form border, ProgressBar fill, the selected TabStrip tab underline, StatusStrip, NavigationPane, TrackBar. |
| `--background-color` | color | `BackgroundColor` | The window background and the default background of every control that does not pin its own. | Form, Panel and every ambient control; Menu, ToolBar and Ribbon items when the strip itself has no background rule. |
| `--border-low-color` | color | `BorderLowColor` | The everyday border colour. | Default control borders (TextBox, ListBox, ComboBox, GroupBox, NumericUpDown), DataGridView grid lines, ScrollBar outlines, ListView lines, Ribbon. |
| `--border-mid-color` | color | `BorderMidColor` | A stronger border colour. | DataGridView header separators, ListView. |
| `--border-high-color` | color | `BorderHighColor` | The strongest border colour. | DataGridView row headers, TrackBar ticks. |
| `--control-low-color` | color | `ControlLowColor` | The lightest control surface -- the 'paper' that lists and inputs sit on. | ListBox, DataGridView, PropertyGrid and TreeView backgrounds, the selected TabStrip tab, ScrollBar arrows and thumb, MenuDropDown, NavigationPane. |
| `--control-mid-color` | color | `ControlMidColor` | The default control surface. | Button, ComboBox and NumericUpDown faces, alternating DataGridView rows, ListBox items, TrackBar groove. |
| `--control-mid-high-color` | color | `ControlMidHighColor` | A slightly darker surface. | ScrollBar track (ScrollBar background), TrackBar. |
| `--control-high-color` | color | `ControlHighColor` | A dark control surface. Not read by the built-in renderers; available to custom controls and VisualStyleRenderer. | Custom controls. |
| `--control-very-high-color` | color | `ControlVeryHighColor` | The darkest control surface. Not read by the built-in renderers; available to custom controls. | Custom controls. |
| `--control-highlight-low-color` | color | `ControlHighlightLowColor` | The hover highlight for items inside a control. | Hovered Menu, ToolBar, Ribbon and MenuDropDown items, hovered ListBox/ListView rows, selected DataGridView rows, MonthCalendar hover. |
| `--control-highlight-mid-color` | color | `ControlHighlightMidColor` | The pressed / selected item highlight. | Selected Ribbon item, ScrollBar and NumericUpDown arrow glyphs, MonthCalendar. |
| `--control-highlight-high-color` | color | `ControlHighlightHighColor` | The strongest item highlight. Not read by the built-in renderers; available to custom controls. | Custom controls. |
| `--foreground-color` | color | `ForegroundColor` | The default text colour. | Every control's text unless a rule or the control sets its own; menu, toolbar, grid, tree and title bar text. |
| `--foreground-color-on-accent` | color | `ForegroundColorOnAccent` | Text drawn on top of an accent-coloured surface. Keep it readable against --accent-color. | Hovered Button text, the custom title bar caption, selected MonthCalendar day, DataGridView selection text. |
| `--foreground-disabled-color` | color | `ForegroundDisabledColor` | Text and glyphs of disabled controls and items. | Every renderer, when the control or item is disabled; the ProgressBar fill when disabled. |
| `--text-selection-background-color` | color | `TextSelectionBackgroundColor` | The highlight behind selected text. | TextBox, NumericUpDown and other text editors. |
| `--warning-highlight-color` | color | `WarningHighlightColor` | The colour of a destructive affordance. | The custom title bar's Close button when hovered; PictureBox error state. |
| `--font-size` | length (px) | `FontSize` | The pixel size of text drawn with the theme font. | Menu, ToolBar, StatusStrip, MenuDropDown, TreeView, NumericUpDown, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-size }` rule. |
| `--item-font-size` | length (px) | `ItemFontSize` | A smaller pixel size for dense item text. | DataGridView cells, ListView items, Ribbon items. |
| `--ui-font` | font family list | `UIFont` | The theme font family (regular weight). | Menu, ToolBar, StatusStrip, MenuDropDown, DataGridView, ListView, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-family }` rule. |
| `--ui-font-bold` | font family list | `UIFontBold` | The theme font family used where text is bold. Resolved at bold weight. | DataGridView column headers, MonthCalendar title, NavigationPane group headers. |

### Selectors (control type names)

| Selector | `:hover` | Also styles | Notes |
|---|---|---|---|
| `Button` | yes |  | Push buttons. Hovering applies the :hover rule on top of the normal one. |
| `CheckBox` | no |  | Check boxes: the text and the box glyph's surround. |
| `ComboBox` | no |  | Drop-down selectors (the closed box; the open list is a ListBox). |
| `DataGridView` | no |  | Data grids: the control background and border. Cells and headers follow the tokens (--control-low-color, --border-low-color, --accent-color). |
| `Form` | no |  | The window. background-color is the window background; border sets the window frame on platforms that draw their own; font-family / font-size / color here become the ambient defaults every child control inherits when it sets none of its own. |
| `GroupBox` | no |  | Titled group frames: the border colour is the frame, color is the caption. |
| `Label` | no |  | Static text. |
| `LinkLabel` | yes |  | Hyperlink text; color is the link colour. Supports :hover. |
| `ListBox` | no | `CheckedListBox` | Single-column lists (also the ComboBox drop-down list). Item highlight comes from --control-highlight-low-color. |
| `ListView` | no |  | Icon / detail lists. |
| `Menu` | no |  | The menu bar. Its items paint on the strip's background; the hovered item uses --control-highlight-low-color. |
| `MenuDropDown` | no |  | Drop-down and context menus. |
| `MonthCalendar` | no |  | The calendar grid; the selected day uses --accent-color. |
| `NavigationPane` | no |  | The Outlook-style side navigation bar. |
| `NumericUpDown` | no |  | Numeric spinners. |
| `Panel` | no | `FlowLayoutPanel`, `TableLayoutPanel`, `TabPage`, `SplitterPanel` | Plain containers, including layout panels and tab pages. |
| `PictureBox` | no |  | Image boxes. |
| `PropertyGrid` | no |  | Property editors. |
| `RadioButton` | no |  | Radio buttons. |
| `Ribbon` | no |  | The ribbon; items paint on its background and highlight with --control-highlight-low-color / --control-highlight-mid-color. |
| `ScrollBar` | no |  | Scroll bars: background-color is the track; the thumb and arrows follow --control-low-color and --border-low-color. |
| `SplitContainer` | no |  | Split containers (the splitter bar between the two panels). |
| `Splitter` | no |  | Stand-alone splitter bars. |
| `StatusBar` | no |  | The status bar along the bottom of a form. |
| `TabControl` | no |  | Tab controls: the frame around the pages (the tab headers are a TabStrip, the pages are Panels). |
| `TabStrip` | no |  | The row of tab headers. The selected tab uses --control-low-color with an --accent-color-2 underline. |
| `TextBox` | no | `DateTimePicker` | Text inputs. Selected text uses --text-selection-background-color. |
| `ToolBar` | no |  | Tool bars; items highlight with --control-highlight-low-color. |
| `TrackBar` | yes |  | Sliders. Supports :hover. |
| `TreeView` | no |  | Tree views. |

### Properties (inside a control rule)

| Property | Value | Effect |
|---|---|---|
| `background-color` | color | The control's background. |
| `color` | color | The control's text (foreground) colour. |
| `border` | [width] [solid \| none] [color] | Shorthand for border-width, border-style and border-color, in any order. Only solid borders are drawn; 'none' is width 0. |
| `border-width` | length | The width of all four border sides, in pixels. |
| `border-color` | color | The colour of all four border sides. |
| `border-radius` | length | The corner radius, in pixels, applied to all four corners. When it is greater than 0 all four sides are drawn with the same width and colour. |
| `border-top-width` | length | The width of one side. Also border-right-width, border-bottom-width, border-left-width. |
| `border-top-color` | color | The colour of one side. Also border-right-color, border-bottom-color, border-left-color. |
| `font-family` | family list | The typeface. The first family installed on the machine is used; generic names (sans-serif, serif, monospace) are passed to the OS font matcher. |
| `font-size` | length | The text size in pixels (not points). |
| `font-weight` | normal \| bold \| 100..900 | The weight. Without a font-family in the same rule, the default UI font family is used at that weight. |
| `font-style` | normal \| italic \| oblique | The slant. Without a font-family in the same rule, the default UI font family is used. |
<!-- END GENERATED: reference -->

Two things to know about **fonts**, because they are the one place the tokens and the rules meet:

- `--ui-font`, `--ui-font-bold`, `--font-size` and `--item-font-size` are the *theme font*, read directly
  by the renderers of composite controls (menus, tool bars, grids, list views, the title bar).
- Simple controls (`Button`, `Label`, `TextBox`, `CheckBox`, ...) draw with the WinForms-style
  **ambient** font: their own `Font` if set, else their parent's, ending at the window. So the way to
  change the app-wide text font is a `Form { font-family: ...; font-size: ...; }` rule, which every child
  inherits, or a rule on the specific type.

## The Light theme as CSS

`Theme.ExportCss ()` writes the current theme's tokens as a stylesheet. This is the built-in Light theme,
which is also what an app has before any theme is applied — copy it, delete the tokens you are happy
with, and edit the rest:

<!-- BEGIN GENERATED: light-theme (Theme.ExportCss) -->
```css
/* Majorsilence.Forms theme. Every token is documented in docs/theming.md.
   Delete a token to keep the base theme's value for it. */
@theme "MyTheme" extends Light;

:root {
  /* The primary accent. */
  --accent-color: #2a8ad0;
  /* The secondary accent, used where the primary one needs a companion. */
  --accent-color-2: #0078d4;
  /* The window background and the default background of every control that does not pin its own. */
  --background-color: #f0f0f0;
  /* The everyday border colour. */
  --border-low-color: #ababab;
  /* A stronger border colour. */
  --border-mid-color: #808080;
  /* The strongest border colour. */
  --border-high-color: #333333;
  /* The lightest control surface -- the 'paper' that lists and inputs sit on. */
  --control-low-color: #fbfbfb;
  /* The default control surface. */
  --control-mid-color: #f3f3f3;
  /* A slightly darker surface. */
  --control-mid-high-color: #e1e1e1;
  /* A dark control surface. Not read by the built-in renderers; available to custom controls and VisualStyleRenderer. */
  --control-high-color: #c2c3c9;
  /* The darkest control surface. Not read by the built-in renderers; available to custom controls. */
  --control-very-high-color: #686868;
  /* The hover highlight for items inside a control. */
  --control-highlight-low-color: #c6c6c6;
  /* The pressed / selected item highlight. */
  --control-highlight-mid-color: #b0b0b0;
  /* The strongest item highlight. Not read by the built-in renderers; available to custom controls. */
  --control-highlight-high-color: #808080;
  /* The default text colour. */
  --foreground-color: #000000;
  /* Text drawn on top of an accent-coloured surface. Keep it readable against --accent-color. */
  --foreground-color-on-accent: #ffffff;
  /* Text and glyphs of disabled controls and items. */
  --foreground-disabled-color: #aaaaaa;
  /* The highlight behind selected text. */
  --text-selection-background-color: #99c9ef;
  /* The colour of a destructive affordance. */
  --warning-highlight-color: #e81123;
  /* The pixel size of text drawn with the theme font. */
  --font-size: 14px;
  /* A smaller pixel size for dense item text. */
  --item-font-size: 12px;
  /* The theme font family (regular weight). */
  --ui-font: "Segoe UI", "Noto Sans", sans-serif;
  /* The theme font family used where text is bold. Resolved at bold weight. */
  --ui-font-bold: "Segoe UI Semibold", "Segoe UI", "Noto Sans", sans-serif;
}
```
<!-- END GENERATED: light-theme -->

## Theme Studio (live preview)

`samples/ThemeStudio` is a desktop app for writing themes: a CSS editor on the left, a preview of every
themable control on the right, and the parser's diagnostics underneath. Every keystroke re-applies the
sheet (a few hundred milliseconds after you stop typing), so you see the effect immediately — including
on the Studio's own window, which is themed by the same sheet.

```bash
dotnet run --project samples/ThemeStudio                       # start with the Light theme exported as CSS
dotnet run --project samples/ThemeStudio -- Themes/ocean.css   # open a file and watch it for changes
```

- **Open / Save** work on `.css` files. An opened file is **watched**: edit it in your own editor, or
  let a coding assistant edit it, and the preview updates when the file is saved.
- **New from…** replaces the editor with a built-in theme exported as CSS.
- **Copy reference for AI** puts `ThemeCssReference.ToMarkdown ()` plus a short instruction on the
  clipboard — paste it into a chat with your assistant along with what you want ("a warm, high-contrast
  light theme with rounded buttons") and paste the answer back into the editor.
- The **Tokens** tab shows every token's current value as a swatch, so you can see which one to change.
- `--render-headless out.png [theme.css]` renders the preview to a PNG without a display, for CI or for
  an assistant that wants to *look* at its theme.

## Prompting a coding assistant

Paste the reference (from the Studio's **Copy reference for AI**, or `ThemeCssReference.ToMarkdown ()`,
or this page) and ask for what you want. A prompt that works well:

> Write a Majorsilence.Forms CSS theme. Use only the tokens, selectors and properties in the reference
> below; nothing else is supported. Start with `@theme "Name" extends Light;` (or `Dark`). Set the
> `:root` tokens first, then add control rules only where the defaults are not what I want. Keep text
> readable: `--foreground-color` on `--background-color` and `--control-low-color`, and
> `--foreground-color-on-accent` on `--accent-color`. I want: *[describe the look]*.

Then load the result in the Studio; if the parser reports an error, paste the error text back to the
assistant — every message names the offending text and the supported alternative.

## How it works

- `ThemeStyleSheet.Parse` tokenizes and parses the text, resolves `var()` references, and compiles each
  valid declaration into a small action. It never throws for bad input; `Diagnostics` lists every
  problem with line and column. `Theme.RegisterThemeCss` / `Theme.LoadFromCss` throw a
  `ThemeCssException` carrying those diagnostics when there are errors; `Theme.ApplyStyleSheet` applies
  whatever parsed, which is what a live editor wants.
- Tokens set the same `Theme` properties as the XML format and as code (`Theme.AccentColor = ...`).
- Control rules attach to the type's static `DefaultStyle` / `DefaultStyleHover` (`ControlStyle`)
  as a layer that is re-applied after the type's declared defaults on every theme change, so the rule
  keeps winning when `Theme.AccentColor` later changes. Applying a stylesheet **replaces** all previously
  applied control rules; `Theme.SetBuiltInTheme` clears them along with the colours. Tokens, like XML
  elements, simply layer onto the current values.
- Base chains (`extends`) may mix CSS and XML themes; cycles are detected. `ThemeChanged` is raised once
  per apply.
