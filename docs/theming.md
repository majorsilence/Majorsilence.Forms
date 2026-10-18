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

Pieces *inside* a control -- grid headers, the selected tab, a hovered menu item, the scroll bar thumb --
are **parts**, addressed as pseudo-elements on the control's selector:

```css
DataGridView::header    { background-color: #2c2c30; color: #e8e8ea; font-weight: bold; }
DataGridView::selection { background-color: var(--accent-color); color: var(--foreground-color-on-accent); }
TabStrip::selected      { border-bottom-color: #f2a93b; border-bottom-width: 2px; }
Menu::item:hover        { background-color: #34343a; }
ScrollBar::thumb        { background-color: #55555c; border-radius: 4px; }
```

Errors are reported with a line, a column, and what to write instead:

```text
error (12:3): Unknown property 'colour'. Did you mean 'color'? Supported properties: background-color, color, border, ...
error (20:1): 'TextBox:hover' is not supported: TextBox does not change appearance for that state. ':hover' is available on: Button, LinkLabel, TrackBar.
```

## The language

A stylesheet has three kinds of statement -- a header, tokens, and control rules (which may target a
part of a control). Whitespace and `/* comments */` are free.

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

- A selector is a **control type name** from the table below, optionally one pseudo-class (`:hover`,
  `:active`, `:disabled`, `:focus` or `:focus-visible`), or a comma-separated list of those. Type names
  are matched case-insensitively.
- A rule sets the type's static default style (`Button.DefaultStyle`). Every instance that has not set
  the same property in code (`button.Style.BackgroundColor = ...`, or WinForms `BackColor`) picks it
  up; explicit per-control values still win, as in WinForms.
- Each pseudo-class is available only on the controls whose **:hover** / **:active** / **:disabled** /
  **:focus** / **:focus-visible** column in the reference says `yes` -- currently `Button`, `LinkLabel`
  and `TrackBar` take all five, `TextBox` takes `:focus` and `:focus-visible`; everything else takes
  none. A pseudo-class's style is layered on the normal rule, so e.g. a `Button:active` rule only needs
  the properties that change, and exactly one state wins when more than one applies at once (disabled,
  then hover, then active/pressed, then focus -- see `Control.CurrentStyle`).
- `:focus-visible` is focus the user reached with the keyboard: the control is focused **and** its
  window shows focus cues, which WinForms turns on once Tab is pressed -- the same idea as a browser's
  heuristic. It layers on `:focus`, so `Button:focus-visible { outline: ... }` adds a focus ring to the
  focus style only for keyboard users. A focused `TextBox` always counts, as a text field does in a
  browser.
- Later declarations replace earlier ones, within a rule and across rules. There is no specificity,
  no cascade, and no `!important`.
- Derived controls without their own default style follow their base type's rule (`Panel` also styles
  `FlowLayoutPanel`, `TableLayoutPanel`, `TabPage`; `TextBox` also styles `DateTimePicker`; `ListBox`
  also styles `CheckedListBox`).
- `box-shadow` is a control-rule property, not a pseudo-class, and applies to any selector: shadows
  painted with the control's own shape -- CSS's full form, `[inset] <x> <y> [<blur> [<spread>]] <color>`,
  as a comma-separated list or `none` (see [Effects](#7-effects)). The simplest is a hard, offset,
  no-blur shadow, e.g. `box-shadow: 4px 4px #2b1b4d;`, and pairing it with a pressed `:active` rule (a
  smaller or zero offset) is how a "hard
  shadow that collapses on press" look is built entirely in CSS:

```css
Button {
  box-shadow: 4px 4px #2b1b4d;
}

Button:active {
  box-shadow: 0px 0px #2b1b4d;
}

Button:disabled {
  box-shadow: 4px 4px #9aa0ab;
}
```

- `border-radius` takes one to four lengths, CSS-style (top-left, top-right, bottom-right, bottom-left;
  missing values are filled in as in CSS), and each corner has a longhand
  (`border-top-left-radius`, ...). A later `border-radius` resets corners set earlier; a later corner
  longhand wins over an earlier `border-radius`. `border-style` is `solid` (the default) or `dashed`
  (dash and gap are each three border-widths long); `dotted`, `double` and the rest are errors. Both are
  control-rule properties only -- a part such as `ScrollBar::thumb` still takes a single `border-radius`:

```css
Button {
  border: 2px dashed #2b1b4d;
  border-radius: 12px 12px 0 0;   /* rounded top, square bottom */
}

Panel {
  border-style: dashed;
  border-top-left-radius: 0;
}
```

### 4. Parts -- `Type::part`

```css
DataGridView::header        { background-color: #2c2c30; color: #e8e8ea; border-bottom-color: #55555c; }
DataGridView::alternating-row { background-color: #26262a; }
TabStrip::item:hover        { background-color: #2c2c30; }
TabStrip::selected          { color: white; border-bottom-color: var(--accent-color); }
Menu::item, ToolBar::item   { color: #e8e8ea; }
Menu::item:hover, ToolBar::item:hover, MenuDropDown::item:hover { background-color: var(--accent-color); color: var(--foreground-color-on-accent); }
ListBox::selection, ListView::selection, TreeView::selection    { background-color: #3a4352; color: white; }
ScrollBar::thumb            { background-color: #55555c; border-color: transparent; border-radius: 4px; }
ScrollBar::arrow            { background-color: #2c2c30; color: #9aa0ab; }
```

- A part is a piece a control paints inside itself that has its own type-level style: the grid's
  column headers are `DataGridView.DefaultColumnHeaderStyle`, the thumb is `ScrollBar.DefaultThumbStyle`,
  and so on. Its defaults come from the tokens (a theme that sets only tokens still recolours every part),
  and a `Type::part` rule overrides exactly that piece.
- The **Parts** table in the reference lists every part and the properties it **accepts**. A part only
  honours what its renderer reads; a property outside that list is an error naming the accepted ones,
  never a silent no-op.
- `:hover` after a part (`Menu::item:hover`) means "that part when hovered" and is only accepted where
  the renderer tracks hover: `Menu::item`, `ToolBar::item`, `MenuDropDown::item`, `TabStrip::item`.
  `ToolBar::item:hover` also covers a checked (toggled) item. The hover style layers on the part's own,
  so `Menu::item { color }` carries into the hovered item.
- A tab inside a `TabControl` paints `TabStrip::item:hover` only while `TabControl.HotTrack` is `true`;
  it defaults to `false`, as in WinForms, so a theme's hover rule shows only in tab controls that opt in.
- Per-control values still win over a part rule where the control exposes one (`grid.ColumnHeadersDefaultCellStyle`,
  `tree.Style.SelectedItemBackgroundColor`), the same way `button.Style.BackgroundColor` wins over a `Button` rule.
- Selectors without parts (`Button`, `TextBox`, ...) reject `::`; the error lists the controls that have them.

### 5. Transitions

A `transition` animates the change into a state instead of switching at once: hover a button and its
background fades to the hover colour.

```css
Button {
  background-color: var(--control-mid-color);
  transition: background-color 150ms ease-in-out, border-color 150ms, box-shadow .2s cubic-bezier(.22, 1, .36, 1);
}

Button:hover {
  background-color: var(--accent-color);
}

Button:active {
  box-shadow: 0px 0px #2b1b4d;
  transition-duration: 60ms;
}
```

- `transition` is the shorthand for `transition-property`, `transition-duration`,
  `transition-timing-function` and `transition-delay`. Each comma-separated entry is
  `[property] [duration] [timing-function] [delay]` in any order; the first time is the duration, the
  second the delay. What an entry leaves out is CSS's default: `all`, `0s`, `ease`, `0s`.
- The longhands take comma lists that line up by index -- the n-th property uses the n-th duration,
  timing function and delay -- and a shorter list repeats, as in CSS.
- As in CSS, the transition that runs is the one on the **style being entered**. A `Button` rule's
  transition covers every state change, because every state style inherits from it; a state rule can
  override part of it. Above, `Button:active` changes only the duration, so pressing is quick while the
  properties and easing still come from the `Button` rule. Each longhand is inherited separately.
- **Animatable:** `background-color`, `color`, the border colours, the corner radii (`border-radius`
  and its per-corner forms) and `box-shadow`, or `all` of them. `border-width` is rejected: a border's
  width decides where the control's client area starts, so animating it would move the children every
  frame. Fonts and `border-style` have no in-between values, so they are rejected too.
- **Timing functions:** `linear`, `ease`, `ease-in`, `ease-out`, `ease-in-out`, `step-start`,
  `step-end`, `cubic-bezier(x1, y1, x2, y2)` (x1 and x2 between 0 and 1; the y values may overshoot) and
  `steps(n[, jump-start | jump-end | jump-none | jump-both | start | end])`.
- A change cut short reverses from what is on screen: leaving a button half way through its hover fade
  fades back from the half-way colour.
- Transitions run on the controls that change state -- the selectors with a `yes` in a pseudo-class
  column of the reference (`Button`, `LinkLabel`, `TextBox`, `TrackBar`) -- and are rejected elsewhere.
  Parts do not take them.
- When the user has asked the system for **reduced motion**, every transition is an instant change, as a
  browser does under `prefers-reduced-motion`.
- In code, the same thing is `ControlStyle.Transition` (a `ControlTransition`), and the curves are
  `Majorsilence.Forms.Animation.Easing.CubicBezier`, `Steps`, `Ease` and friends.

### 6. Modern values

The value syntax follows current CSS, so values copied from a design system or a web stylesheet work.

```css
:root {
  --brand: oklch(60% 0.24 240);
  --brand-tint: color-mix(in oklch, white 80%, var(--brand));
  --accent-color: light-dark(#0b5cad, #5aa9ff);
}

Button {
  --button-bg: var(--brand);
  background-color: var(--button-bg);
  border-radius: calc(0.375rem + 1px);
  color: white;
}

Button:hover {
  --button-bg: oklch(from var(--brand) calc(l * .9) calc(c * 1.15) h);
}
```

- **Units and arithmetic.** `rem` is `--root-font-size` (16px unless a theme sets it, a browser's root
  font size), so a `0.375rem` radius copied from a web stylesheet keeps its size. `calc()`, `min()`,
  `max()`, `clamp()` and parentheses combine lengths, times, numbers and angles; a length token inside
  one stays live (`calc(var(--font-size) / 2)`). Put spaces around `+` and `-`, as CSS requires.
- **Colour functions.** `hwb()`, `lab()`, `lch()`, `oklab()`, `oklch()` and `color()` (`srgb`,
  `srgb-linear`, `display-p3`, `xyz`, `xyz-d50`, `xyz-d65`) in their space-separated form, with `none`,
  `calc()` and `/ alpha`. A colour outside sRGB is brought in by CSS's gamut-mapping algorithm, which
  lowers chroma rather than clipping, so it keeps its hue.
- **`color-mix(in <space> [<method> hue], a [p%], b [p%])`** mixes in any of those spaces (plus `hsl`
  and `hwb`), premultiplied by alpha; percentages that add up to less than 100% leave the result that
  much transparent. An achromatic colour (white, grey) takes the other colour's hue, so
  `color-mix(in oklch, white 80%, var(--brand))` is a tint of the brand colour.
- **Relative colours** -- `oklch(from <color> l c h)`, `rgb(from <color> r g b / 50%)`, and the same for
  every function -- name the origin's channels, which `calc()` can change. The origin can be a token,
  and the result follows it.
- **`light-dark(light, dark)`** picks by the theme in effect: dark when its `--background-color` is
  darker than mid-grey. One sheet then works whether it `extends Light` or `extends Dark`.
- **Variables inside a rule.** A control rule can declare its own custom properties. They are visible to
  every rule for the same control type and part, and a state rule that sets one changes every
  declaration of the base rule that reads it: above, `Button:hover` only sets `--button-bg`, and the
  hovered button's background follows. (CSS's cascade does exactly this; host appliers see the result as
  ordinary `:hover` declarations.) A `:root` variable means what it means on `:root`, whatever rule reads
  it, and a rule cannot set a theme token -- those are theme-wide.

### 7. Effects

```css
Button {
  background-color: #0d6efd;
  background-image: linear-gradient(to bottom, rgb(255 255 255 / 15%), transparent);
  border-radius: 6px;
  box-shadow: 0 1px 2px rgb(0 0 0 / 30%), inset 0 1px 0 rgb(255 255 255 / 15%);
  transition: box-shadow 150ms, opacity 150ms;
}

Button:focus-visible {
  outline: 2px solid #86b7fe;
  outline-offset: 2px;
}

Button:disabled {
  opacity: .65;
}
```

- **`opacity`** (0 to 1, or a percentage) fades the whole control, children included, as its parent
  composites it. It does not change hit-testing. A window's opacity is `Form.Opacity`, so windows reject
  it.
- **`box-shadow`** takes CSS's whole form: `[inset] <x> <y> [<blur> [<spread>]] <color>`, a
  comma-separated list (the first on top), or `none`. A blur softens the edge; a spread grows (or, if
  negative, shrinks) the shadow first; an `inset` shadow is cast inside the face, over the background.
  The colour is required -- CSS's default is `currentcolor`, which a type rule has no element for.
  **A control cannot paint outside itself**, so an outer shadow takes room inside the control's bounds:
  the face shrinks by however far the shadows reach past it on each side (offset, spread and blur
  together). Size a control with a shadow to leave that room.
- **`outline`** (`outline-width`, `outline-style`, `outline-color`, `outline-offset`) is a ring drawn
  around the control by its parent, outside the control: it takes no room and moves nothing, which makes
  it the usual focus ring. It follows the corner radii. Its style is `none` until a rule says `solid`,
  `dashed` or `auto` -- as in CSS, an outline with only a width and a colour draws nothing. Its colour is
  the text colour when no rule sets one.
- **`background-image`** takes gradients: `linear-gradient()` (an angle or `to <side or corner>`),
  `radial-gradient()` (`circle` or `ellipse`, a size keyword or lengths, `at <position>`),
  `conic-gradient()` (`from <angle>`, `at <position>`) and their `repeating-` forms, layered over the
  background colour inside the face's rounded shape. Stops take lengths and percentages (angles for a
  conic) and two positions for a hard band; colours interpolate premultiplied, so fading to
  `transparent` fades the colour out instead of through grey. Image URLs, transition hints and
  `in <colorspace>` are rejected. `background`, the shorthand, stays an error.
- `opacity`, the outline's colour, width and offset, and every part of a shadow list can be
  transitioned. Shadow lists move pair by pair, the shorter padded with transparent shadows.

### 8. Media queries

```css
:root { --page: #ffffff; }

@media (prefers-color-scheme: dark) {
  :root { --page: #1e1e1e; --accent-color: #5aa9ff; }
}

@media (prefers-reduced-motion: reduce) {
  Button { transition: none; }
}

Form { background-color: var(--page); }
```

- `@media` takes the two user preferences a desktop theme can know: `prefers-color-scheme` (`light` or
  `dark`, the operating system's appearance) and `prefers-reduced-motion` (`reduce` or
  `no-preference`; `(prefers-reduced-motion)` alone means `reduce`). Queries combine with `and`, `or`,
  `not` and commas, after an optional `all`, `screen` or `print` media type (`print` never matches: a
  theme is not used for printing). Blocks may nest.
- Anything can go inside -- `:root` tokens and variables, control rules, rule variables -- and it means
  what it would in CSS: above, every `var(--page)` follows the dark override.
- The theme applies the sheet again when either preference changes, so a running application follows
  the system; host appliers follow too. `ThemeStyleSheet.UsesMediaQueries` says whether a sheet depends
  on them, and its `Rules` and `Tokens` describe what applies under the current settings (each rule's
  `Media` says which query it sits in).
- Size, input-device and the other media features are errors that say why: there is no one viewport
  for a theme that styles every window, and the backends do not report the rest.
- The operating system's appearance is read by the Avalonia backend (`SystemInformation.PrefersDarkColorScheme`,
  which `Application.SystemColorMode` now reports as well); a backend that cannot tell answers light. A
  `ThemeScope` evaluates its sheet's queries when it is applied, but does not re-apply on a change.

### 9. Keyframe animations

```css
@keyframes pulse {
  from { opacity: 1; }
  50%  { opacity: .55; animation-timing-function: ease-in; }
  to   { opacity: 1; }
}

@keyframes attention {
  to { box-shadow: 0 0 0 4px rgb(220 53 69 / 50%); }
}

ProgressBar {
  animation: pulse 1.2s ease-in-out infinite;
}

Button:focus-visible {
  animation: attention 300ms ease-out forwards;
}
```

- `@keyframes <name> { ... }` lists keyframes selected by `from`, `to` or percentages (a comma list
  makes one keyframe at each). A keyframe sets any property a transition can move, and may set its own
  `animation-timing-function` for the stretch that starts at it. A property a keyframe list leaves out
  of `from` or `to` animates from or to the control's own style -- above, `attention` grows a shadow
  out of whatever the button has.
- `animation` is the shorthand for `animation-name`, `-duration`, `-timing-function`, `-delay`,
  `-iteration-count` (a number, fractions allowed, or `infinite`), `-direction` (`normal`, `reverse`,
  `alternate`, `alternate-reverse`), `-fill-mode` (`none`, `forwards`, `backwards`, `both`) and
  `-play-state` (`running`, `paused`). Lists line up by index, as for transitions, and each longhand is
  inherited separately, so `Button:disabled { animation-play-state: paused; }` pauses the button's
  animation.
- An animation starts when the control first shows the rule's style or enters it; the same name across
  a state change keeps running, a new name starts again, and a sheet without it stops it. Its values win
  over the style's own and over a transition's, as in CSS.
- The keyframes must be in the same sheet as the rule that names them, at the top level (not inside
  `@media`: choose the animation in `@media` with `animation-name` instead).
- An infinite animation stops asking for frames while its control is off screen and resumes when it is
  painted again. When the user prefers reduced motion no animation runs; a sheet can also say what to
  do instead with `@media (prefers-reduced-motion: reduce)`.
- Animations run on any control the parent composites -- not on windows, which do not animate their own
  style, and not on parts. In code, the same thing is `ControlStyle.Animation` (a `ControlAnimation`).

### Values

| Kind | Accepted forms | Notes |
|---|---|---|
| color | `#rgb` `#rgba` `#rrggbb` `#rrggbbaa` · `rgb(r, g, b)` `rgba(r, g, b, a)` · `rgb(r g b / a)` · `hsl(h, s%, l%)` `hsla(...)` · `hwb()` `lab()` `lch()` `oklab()` `oklch()` · `color(display-p3 1 0 0)` · `color-mix(in oklch, a 30%, b)` · relative colours `oklch(from var(--x) l c h)` · `light-dark(a, b)` · the 148 CSS colour names · `transparent` · `var(--x)` | Alpha is 0–1 (or a percentage). In hex, **alpha comes last** (`#rrggbbaa`) — the opposite of the `#AARRGGBB` order the XML format uses. See [Modern values](#6-modern-values). |
| length | `14px` or `14` · `0.875rem` · `calc(1rem - 2px)` · `min()` `max()` `clamp()` | **Pixels**, at 100% scaling, rounded to whole pixels. `1rem` is `--root-font-size` (16px by default). `pt`, `em` and `%` are rejected: every size in a style is a pixel value, and `em` would depend on an inherited font size a type rule cannot know. |
| font family list | `"Segoe UI", "Noto Sans", sans-serif` | Quoted or bare names, comma-separated. The first family the machine has is used, and a font registered with `PrivateFontCollection` counts as one it has; a generic name (`sans-serif`, `serif`, `monospace`) is handed to the OS font matcher. |
| time | `150ms` · `.15s` | A transition duration or delay. The unit is required, as in CSS. |
| timing function | `ease` · `cubic-bezier(.22, 1, .36, 1)` · `steps(4, jump-start)` | The easing of a transition. |
| `var(--name)` | `var(--brand)` · `var(--brand, #333)` | Substitutes a variable -- the rule's own, then `:root`'s -- or a token. A token reference inside a control rule stays **live**: `Button:hover { background-color: var(--accent-color); }` follows later changes to `Theme.AccentColor`. |

### What is *not* supported, and what happens if you write it

Everything outside the subset is an **error with an explanation**, never silently ignored. The
offending declaration (or rule) is dropped and the rest of the sheet still applies.

<!-- BEGIN UNSUPPORTED -->
| You write | Why not | Write instead |
|---|---|---|
| `* { color: red; }` | There is no single style every control inherits from. | `:root { --foreground-color: red; }` for the theme-wide default. |
| `.primary { color: red; }` | Controls have no CSS classes or ids. | A type rule, and `button.Style.ForegroundColor` in code for one control. |
| `Panel Button { color: red; }` | A rule applies to every control of the type, wherever it sits. | `Button { color: red; }` |
| `TextBox:disabled { color: gray; }` | `TextBox` does not change appearance for that state; only `Button`, `LinkLabel`, `TrackBar` take `:disabled`. | `:root { --foreground-disabled-color: gray; }`, which every disabled control's text already follows. |
| `Button:oops { color: gray; }` | The pseudo-classes are `:hover`, `:active`, `:disabled` and `:focus`. | Pick one of those four. |
| `Button { box-shadow: 4px 4px; }` | A shadow needs a colour: CSS's default is `currentcolor`, which a type rule has no element for. | `box-shadow: 4px 4px rgb(0 0 0 / 25%);` |
| `Button::icon { color: red; }` | `Button` has no separately styleable parts; parts exist only where a renderer paints a distinct piece. | `Button { color: red; }`; the reference lists the controls with parts. |
| `DataGridView::header:hover { color: red; }` | Headers do not react to hover. | `:hover` on parts: `Menu::item`, `ToolBar::item`, `MenuDropDown::item`, `TabStrip::item`. |
| `ScrollBar::thumb { font-size: 12px; }` | A part accepts only the properties its renderer reads. | See the part's **Accepts** column; the thumb takes `background-color`, `border-*`, `border-radius`. |
| `TextBox:hover { color: red; }` | `TextBox` does not repaint on hover. | Only `Button`, `LinkLabel`, `TrackBar` take `:hover`. |
| `Button { font-size: 12pt; }` | Sizes are pixels. | `font-size: 16px;` or `font-size: 1rem;` |
| `Button { font-size: 1.2em; }` | `em` is relative to an inherited font size, which a type rule cannot know. | `rem` (relative to `--root-font-size`) or `px`. |
| `Button { color: currentcolor; }` | A type rule has no element whose colour it could follow. | `var(--foreground-color)`. |
| `Button { color: red !important; }` | There is no cascade to override. | Put the declaration later in the file. |
| `@import url(base.css);` | No file inclusion. | Register the base theme, then `@theme "X" extends Base;`. |
| `@media (min-width: 600px) { }` | A theme applies to every window at once, so there is no one viewport size to test. | `@media` takes `prefers-color-scheme` and `prefers-reduced-motion`; size-dependent appearance belongs in layout code. |
| `Panel { background-image: url(a.png); }` | A theme has no file to load an image from. | A gradient, or `Control.BackgroundImage` in code. |
| `Form { opacity: .9; }` | Nothing composites a window onto another surface. | `Form.Opacity` in code. |
| `@keyframes grow { to { font-size: 20px; } }` | Keyframes animate what a transition can: paint-only values, not layout or fonts. | Animate `opacity`, colours, `box-shadow`, the outline or the radii. |
| `Panel { animation: spin 1s; }` | No `@keyframes spin` in the sheet. | Declare the keyframes in the same sheet. |
| `Button { margin: 4px; }` | Layout is not themable; only colours, borders and fonts are. | Set `Padding`/`Margin` in code. |
| `Button { border: 1px dotted red; }` | Borders are solid or dashed. | `border: 1px dashed red;` |
| `Button { transition: border-width 1s; }` | A border's width moves the client area, and with it the children, every frame. | Transition `border-color`, or a `box-shadow`. |
| `Button { transition: color 1s allow-discrete; }` | `transition-behavior` only matters for properties that change in one step, and no themable property does. | Leave it out. |
| `Label { transition: color 1s; }` | `Label` never changes state, so a transition would never run. | Transitions run on `Button`, `LinkLabel`, `TextBox`, `TrackBar`. |
| `Button { background: red; }` | Only the longhand is recognised (`background` would imply images/gradients). | `background-color: red;` |
| `Button { color: linear-gradient(red, blue); }` | Gradients and images are not colours. | A flat colour. |
| `:root { color: red; }` | `:root` takes tokens only. | `Form { color: red; }` or `--foreground-color`. |
| `Button { --accent-color: red; }` | A theme token is theme-wide; a rule cannot override it for one control type. | Set it in `:root`, or declare a variable of your own: `Button { --button-accent: red; }`. |
<!-- END UNSUPPORTED -->

## Reference

<!-- BEGIN GENERATED: reference (ThemeCssReference.ToMarkdown) -->
### Tokens (`:root` custom properties)

| Token | Value | Sets `Theme.` | What it is | Read by |
|---|---|---|---|---|
| `--accent-color` | color | `AccentColor` | The primary accent. | Button hover background, LinkLabel text, selected DataGridView cells, MonthCalendar selection, TrackBar thumb, the custom title bar. Telerik: RadGridView, RadToggleSwitch. |
| `--accent-color-2` | color | `AccentColor2` | The secondary accent, used where the primary one needs a companion. | Button hover border, Form border, ProgressBar fill, the selected TabStrip tab underline, StatusStrip, NavigationPane, TrackBar. |
| `--background-color` | color | `BackgroundColor` | The window background and the default background of every control that does not pin its own. | Form, Panel and every ambient control; Menu, ToolBar and Ribbon items when the strip itself has no background rule. Telerik: the dock windows (ToolWindow, DocumentWindow, DockWindow). |
| `--border-low-color` | color | `BorderLowColor` | The everyday border colour. | Default control borders (TextBox, ListBox, ComboBox, GroupBox, NumericUpDown), DataGridView grid lines, ScrollBar outlines, ListView lines, Ribbon. Telerik: RadGridView, RadScheduler. |
| `--border-mid-color` | color | `BorderMidColor` | A stronger border colour. | DataGridView header separators, ListView. Telerik: RadGridView. |
| `--border-high-color` | color | `BorderHighColor` | The strongest border colour. | DataGridView row headers, TrackBar ticks. Telerik: RadGridView. |
| `--control-low-color` | color | `ControlLowColor` | The lightest control surface -- the 'paper' that lists and inputs sit on. | ListBox, DataGridView, PropertyGrid and TreeView backgrounds, the selected TabStrip tab, ScrollBar arrows and thumb, MenuDropDown, NavigationPane. Telerik: RadGridView. |
| `--control-mid-color` | color | `ControlMidColor` | The default control surface. | Button, ComboBox and NumericUpDown faces, alternating DataGridView rows, ListBox items, TrackBar groove. Telerik: RadGridView, RadToggleSwitch, RadScheduler agenda day headers, RadDock tab strips. |
| `--control-mid-high-color` | color | `ControlMidHighColor` | A slightly darker surface. | ScrollBar track (ScrollBar background), TrackBar. Telerik: RadGridView. |
| `--control-high-color` | color | `ControlHighColor` | A dark control surface. Not read by the built-in renderers; available to custom controls and VisualStyleRenderer. | Custom controls. Telerik: RadToggleSwitch. |
| `--control-very-high-color` | color | `ControlVeryHighColor` | The darkest control surface. Not read by the built-in renderers; available to custom controls. | Custom controls. Telerik: RadGridView. |
| `--control-highlight-low-color` | color | `ControlHighlightLowColor` | The hover highlight for items inside a control. | Hovered Menu, ToolBar, Ribbon and MenuDropDown items, hovered TabStrip tabs (TabControl.HotTrack), hovered ListBox/ListView rows, selected DataGridView rows, MonthCalendar hover. |
| `--control-highlight-mid-color` | color | `ControlHighlightMidColor` | The pressed / selected item highlight. | Selected Ribbon item, ScrollBar and NumericUpDown arrow glyphs, MonthCalendar. |
| `--control-highlight-high-color` | color | `ControlHighlightHighColor` | The strongest item highlight. Not read by the built-in renderers; available to custom controls. | Custom controls. |
| `--foreground-color` | color | `ForegroundColor` | The default text colour. | Every control's text unless a rule or the control sets its own; menu, toolbar, grid, tree and title bar text. Telerik: RadGridView, RadToggleSwitch, RadPageView. |
| `--foreground-color-on-accent` | color | `ForegroundColorOnAccent` | Text drawn on top of an accent-coloured surface. Keep it readable against --accent-color. | Hovered Button text, the custom title bar caption, selected MonthCalendar day, DataGridView selection text. Telerik: RadGridView, RadToggleSwitch. |
| `--foreground-disabled-color` | color | `ForegroundDisabledColor` | Text and glyphs of disabled controls and items. | Every renderer, when the control or item is disabled; the ProgressBar fill when disabled. Telerik: RadGridView, RadToggleSwitch, RadDock tab strips. |
| `--text-selection-background-color` | color | `TextSelectionBackgroundColor` | The highlight behind selected text. | TextBox, NumericUpDown and other text editors. |
| `--warning-highlight-color` | color | `WarningHighlightColor` | The colour of a destructive affordance. | The custom title bar's Close button when hovered; PictureBox error state. |
| `--font-size` | length (px) | `FontSize` | The pixel size of text drawn with the theme font. | Menu, ToolBar, StatusStrip, MenuDropDown, TreeView, NumericUpDown, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-size }` rule. |
| `--item-font-size` | length (px) | `ItemFontSize` | A smaller pixel size for dense item text. | DataGridView cells, ListView items, Ribbon items. Telerik: RadGridView. |
| `--root-font-size` | length (px) | `RootFontSize` | What one 'rem' is, in pixels: 16px by default, a browser's root font size, so a rem copied from a web stylesheet keeps its size. Changes no font by itself. | Every rem in the theme: lengths, calc() and font sizes written in rem. |
| `--ui-font` | font family list | `UIFont` | The theme font family (regular weight). | Menu, ToolBar, StatusStrip, MenuDropDown, DataGridView, ListView, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-family }` rule. Telerik: RadGridView. |
| `--ui-font-bold` | font family list | `UIFontBold` | The theme font family used where text is bold. Resolved at bold weight. | DataGridView column headers, MonthCalendar title, NavigationPane group headers. Telerik: RadGridView. |

### Selectors (control type names)

| Selector | `:hover` | `:active` | `:disabled` | `:focus` | `:focus-visible` | Also styles | Notes |
|---|---|---|---|---|---|---|---|
| `Button` | yes | yes | yes | yes | yes | `RadButton`, `RadDropDownButton` | Push buttons. Hovering applies the :hover rule on top of the normal one. Supports :active, :disabled, :focus and :focus-visible. |
| `CheckBox` | no | no | no | no | no | `RadCheckBox`, `RadToggleSwitch` | Check boxes: the text and the box glyph's surround. |
| `ComboBox` | no | no | no | no | no | `CompatComboBox`, `DataGridViewComboBoxEditingControl`, `RadCheckedDropDownList`, `RadDropDownList` | Drop-down selectors (the closed box; the open list is a ListBox). |
| `DataGridView` | no | no | no | no | no | `RadGridView` | Data grids: the control background and border. Cells follow the tokens (--control-low-color, --border-low-color); headers, selection and alternating rows are parts. |
| `DateTimePicker` | no | no | no | no | no | `RadDateTimePicker` | Date pickers: background-color is behind the date text; the drop button and the check box follow the tokens. |
| `Form` | no | no | no | no | no | `ColorDialog`, `FontDialog`, `MessageBoxForm`, `PageSetupDialog`, `PrintDialog`, `PrintPreviewDialog`, `RadDesktopAlertPopup`, `RadForm`, `RadRibbonForm`, `RadTabbedForm`, `SchedulerPrintSettingsDialog`, `ThreadExceptionDialog` | The window. background-color is the window background; border sets the window frame on platforms that draw their own; font-family / font-size / color here become the ambient defaults every child control inherits when it sets none of its own. |
| `FormTitleBar` | no | no | no | no | no |  | The title bar a window draws for itself (every platform but macOS, which uses the system's). background-color is the bar, color the caption text. On macOS's merged title bar it blends with the window background instead. |
| `GroupBox` | no | no | no | no | no | `RadGroupBox` | Titled group frames: the border colour is the frame, color is the caption. |
| `HostedSurface` | no | no | no | no | no |  | A Majorsilence.Forms surface embedded in an Avalonia or Uno host. Transparent by default so the host shows through; a background-color rule makes it opaque. |
| `Label` | no | no | no | no | no | `RadLabel` | Static text. |
| `LinkLabel` | yes | yes | yes | yes | yes | `RadLinkLabel` | Hyperlink text; color is the link colour. Supports :hover, :active, :disabled, :focus and :focus-visible. |
| `ListBox` | no | no | no | no | no | `CheckedListBox`, `RadListControl`, `RichListBox` | Single-column lists (also the ComboBox drop-down list). The selected item is the ::selection part. |
| `ListView` | no | no | no | no | no |  | Icon / detail lists. The selected item is the ::selection part. |
| `MdiClient` | no | no | no | no | no |  | The workspace of an MDI parent form, behind its child windows: background-color is the workspace colour. |
| `Menu` | no | no | no | no | no | `MainMenu`, `MenuStrip`, `MenuStripClickThrough`, `RadMenu` | The menu bar. Items are the ::item part; they paint on the strip's background unless ::item sets one. |
| `MenuDropDown` | no | no | no | no | no | `ContextMenu`, `ContextMenuStrip`, `ToolStripDropDown`, `ToolStripDropDownMenu`, `ToolStripOverflow` | Drop-down and context menus. Items are the ::item part. |
| `MonthCalendar` | no | no | no | no | no | `RadCalendar` | The calendar grid; the selected day uses --accent-color. |
| `NavigationPane` | no | no | no | no | no |  | The Outlook-style side navigation bar. |
| `NumericUpDown` | no | no | no | no | no |  | Numeric spinners. |
| `Panel` | no | no | no | no | no | `Card`, `ContainerControl`, `DataGrid`, `DocumentContainer`, `DocumentTabStrip`, `DomainUpDown`, `FlowLayoutPanel`, `LayoutControlGroup`, `LayoutControlItem`, `NavigationHost`, `RadCollapsiblePanel`, `RadDock`, `RadLayoutControl`, `RadPageViewPage`, `RadPanel`, `RadScrollablePanel`, `RadScrollablePanelContainer`, `SplitPanel`, `SplitterPanel`, `StackPanel`, `TabPage`, `TableLayoutPanel`, `ToolStripContainer`, `ToolStripContentPanel`, `ToolStripPanel`, `ToolTabStrip`, `UserControl` | Plain containers, including layout panels and tab pages. |
| `PictureBox` | no | no | no | no | no |  | Image boxes. |
| `PopupWindow` | no | no | no | no | no |  | Floating popup windows -- a combo box's list, a tool tip, a menu host: background-color is the popup behind its content. |
| `PrintPreviewControl` | no | no | no | no | no |  | Print previews: background-color is the surround the pages sit on; the pages themselves are paper and stay white. |
| `ProgressBar` | no | no | no | no | no | `RadWaitingBar` | Progress bars: background-color is the track and border its frame; the fill is --accent-color-2. |
| `PropertyGrid` | no | no | no | no | no | `RadPropertyGrid` | Property editors. |
| `RadioButton` | no | no | no | no | no | `RadRadioButton` | Radio buttons. |
| `Ribbon` | no | no | no | no | no |  | The ribbon; items paint on its background and highlight with --control-highlight-low-color / --control-highlight-mid-color. |
| `ScrollBar` | no | no | no | no | no | `HScrollBar`, `HorizontalScrollBar`, `VScrollBar`, `VerticalScrollBar` | Scroll bars: background-color is the track; the grip and the arrow buttons are the ::thumb and ::arrow parts. |
| `SplitContainer` | no | no | no | no | no | `RadSplitContainer` | Split containers (the splitter bar between the two panels). |
| `Splitter` | no | no | no | no | no |  | Stand-alone splitter bars. |
| `StatusBar` | no | no | no | no | no |  | The status bar along the bottom of a form. |
| `StatusStrip` | no | no | no | no | no |  | Status strips (the ToolStrip-based status bar): background-color is the strip, border-top its seam with the form above. |
| `TabControl` | no | no | no | no | no | `RadPageView` | Tab controls: the frame around the pages (the tab headers are a TabStrip, the pages are Panels). |
| `TabStrip` | no | no | no | no | no |  | The row of tab headers. Tabs are the ::item part (with :hover) and the current one the ::selected part. |
| `TextBox` | no | no | no | yes | yes | `DataGridViewTextBoxEditingControl`, `MaskedTextBox`, `RadTextBox`, `RadTextBoxControl`, `RadTimePicker`, `RichTextBox`, `TimePicker` | Text inputs. Selected text uses --text-selection-background-color. Supports :focus and :focus-visible, for a focus ring (a text box always counts as :focus-visible when focused, as in a browser). |
| `ToolBar` | no | no | no | no | no | `BindingNavigator`, `ToolStrip`, `ToolStripClickThrough` | Tool bars. Items are the ::item part; :hover also covers a checked (toggled) item. |
| `TrackBar` | yes | yes | yes | yes | yes |  | Sliders. Supports :hover, :active, :disabled, :focus and :focus-visible. |
| `TreeView` | no | no | no | no | no | `RadTreeView` | Tree views. The selected node is the ::selection part. |
| `DockWindowBase` | no | no | no | no | no | `DockWindow`, `DocumentWindow`, `ToolWindow` | Telerik dock windows (tool, document and plain dock windows): background-color is the window behind its content, which is --background-color by default rather than the form's. |
| `RadCommandBar` | no | no | no | no | no |  | Telerik command bars: background-color is the bar behind its strips. |
| `RadPdfViewerNavigator` | no | no | no | no | no |  | The Telerik PDF viewer's navigation toolbar. |
| `RadRibbonBar` | no | no | no | no | no |  | Telerik ribbon bars: background-color is the ribbon behind its tabs and groups. |
| `RadScheduler` | no | no | no | no | no |  | The Telerik scheduler's agenda view: background-color is behind the appointment list. |
| `RadSchedulerNavigator` | no | no | no | no | no |  | The Telerik scheduler's navigation bar. |
| `RadStatusStrip` | no | no | no | no | no |  | Telerik status strips along the bottom of a form. |
| `RichTextEditorRibbonBar` | no | no | no | no | no |  | The Telerik rich text editor's ribbon bar. |

### Telerik controls (`Majorsilence.Forms.Telerik`)

Each `Rad*` control follows the selector named here -- its own, or the core control it is built on. "Tokens only" means no control rule reaches it, and it follows the `:root` tokens alone.

| Control | Follows |
|---|---|
| `DockWindow` | `DockWindowBase` |
| `DocumentContainer` | `Panel` |
| `DocumentTabStrip` | `Panel` |
| `DocumentWindow` | `DockWindowBase` |
| `LayoutControlGroup` | `Panel` |
| `LayoutControlItem` | `Panel` |
| `RadButton` | `Button` |
| `RadCalendar` | `MonthCalendar` |
| `RadCheckBox` | `CheckBox` |
| `RadCheckedDropDownList` | `ComboBox` |
| `RadCollapsiblePanel` | `Panel` |
| `RadCommandBar` | `RadCommandBar` |
| `RadDateTimePicker` | `DateTimePicker` |
| `RadDesktopAlertPopup` | `Form` |
| `RadDock` | `Panel` |
| `RadDropDownButton` | `Button` |
| `RadDropDownList` | `ComboBox` |
| `RadForm` | `Form` |
| `RadGridView` | `DataGridView` |
| `RadGroupBox` | `GroupBox` |
| `RadLabel` | `Label` |
| `RadLayoutControl` | `Panel` |
| `RadLinkLabel` | `LinkLabel` |
| `RadListControl` | `ListBox` |
| `RadMenu` | `Menu` |
| `RadPageView` | `TabControl` |
| `RadPageViewPage` | `Panel` |
| `RadPanel` | `Panel` |
| `RadPdfViewer` | tokens only (frame only) |
| `RadPdfViewerNavigator` | `RadPdfViewerNavigator` |
| `RadPropertyGrid` | `PropertyGrid` |
| `RadRadioButton` | `RadioButton` |
| `RadRibbonBar` | `RadRibbonBar` |
| `RadRibbonForm` | `Form` |
| `RadRichTextEditor` | tokens only (frame only) |
| `RadScheduler` | `RadScheduler` |
| `RadSchedulerNavigator` | `RadSchedulerNavigator` |
| `RadScrollablePanel` | `Panel` |
| `RadScrollablePanelContainer` | `Panel` |
| `RadSplitContainer` | `SplitContainer` |
| `RadStatusStrip` | `RadStatusStrip` |
| `RadTabbedForm` | `Form` |
| `RadTextBox` | `TextBox` |
| `RadTextBoxControl` | `TextBox` |
| `RadTimePicker` | `TextBox` |
| `RadToggleSwitch` | `CheckBox` |
| `RadTreeView` | `TreeView` |
| `RadWaitingBar` | `ProgressBar` |
| `RichTextEditorRibbonBar` | `RichTextEditorRibbonBar` |
| `SchedulerPrintSettingsDialog` | `Form` |
| `SplitPanel` | `Panel` |
| `ToolTabStrip` | `Panel` |
| `ToolWindow` | `DockWindowBase` |

### Frame-only controls

A rule for the selector these follow styles their background and border; the content is not the library's to paint.

| Control | Why |
|---|---|
| `NativeControlHost` | a native platform widget draws the content |
| `SKControl` | the application paints the content |
| `SKGLControl` | the application paints the content |
| `WebBrowser` | a native browser widget draws the content |
| `RadPdfViewer` | the page area is the browser's own PDF renderer |
| `RadRichTextEditor` | the document is web-view content |

### Parts (`Selector::part` pseudo-elements)

| Part | `:hover` | Accepts | What it is |
|---|---|---|---|
| `DataGridView::header` | no | `background-color`, `color`, `border-color`, `border-bottom-color`, `font-family`, `font-size`, `font-weight`, `font-style` | Column headers: background, text colour, font; border-color is the separator between headers, border-bottom-color the line under the header row. |
| `DataGridView::row-header` | no | `background-color`, `color`, `border-color` | Row headers: background, the current-row indicator (color) and the separator (border-color). |
| `DataGridView::selection` | no | `background-color`, `color`, `border-color`, `border-width` | The selected row's background and text colour, and the outline of the selected cell in cell-select mode (border-color, border-width). |
| `DataGridView::alternating-row` | no | `background-color` | The background of every second row. Unset by default (a shade derived from the grid background). |
| `ListBox::selection` | no | `background-color`, `color` | The selected item's background and, when set, its text colour. |
| `ListView::selection` | no | `background-color`, `color` | The selected item's background and, when set, its text colour. |
| `Menu::item` | yes | `background-color`, `color` | A menu bar item: text colour and optional background; :hover is the hovered or open item. |
| `MenuDropDown::item` | yes | `background-color`, `color` | A drop-down item: background and text colour; :hover is the hovered or open item. |
| `ScrollBar::thumb` | no | `background-color`, `border-color`, `border-width`, `border-radius` | The draggable grip: fill, outline (border-color, border-width) and corner radius. |
| `ScrollBar::arrow` | no | `background-color`, `border-color`, `color` | The two arrow buttons: fill, outline and the arrow glyph colour (color). |
| `TabStrip::item` | yes | `background-color`, `color` | A tab: optional background and the caption colour; :hover is the hovered tab, painted only when TabControl.HotTrack is set (default --control-highlight-low-color). |
| `TabStrip::selected` | no | `background-color`, `color`, `border-bottom-color`, `border-bottom-width` | The selected tab: optional background, caption colour, and the accent underline (border-bottom-color, border-bottom-width; default --accent-color-2, 3px). |
| `ToolBar::item` | yes | `background-color`, `color` | A tool bar item: text colour and optional background; :hover is the hovered, open or checked item. |
| `TreeView::selection` | no | `background-color`, `color` | The selected node's background and, when set, its text colour. |

### Properties (inside a control rule)

| Property | Value | Effect |
|---|---|---|
| `background-color` | color | The control's background. |
| `color` | color | The control's text (foreground) colour. |
| `border` | [width] [solid | dashed | none] [color] | Shorthand for border-width, border-style and border-color, in any order. Borders are solid or dashed; 'none' is width 0. |
| `border-style` | solid | dashed | How the border line is drawn: a continuous line or dashes (each dash and gap three times the border width). dotted, double, groove, ridge, inset and outset are rejected. |
| `border-width` | length | The width of all four border sides, in pixels. |
| `border-color` | color | The colour of all four border sides. |
| `border-radius` | length{1,4} | The corner radius, in pixels. One value applies to all four corners; two to top-left/bottom-right and top-right/bottom-left; three to top-left, top-right/bottom-left and bottom-right; four to top-left, top-right, bottom-right, bottom-left. When any corner is greater than 0 all four sides are drawn with the same width and colour. Elliptical radii ('/') are not supported. |
| `border-top-left-radius` | length | The radius of one corner. Also border-top-right-radius, border-bottom-right-radius, border-bottom-left-radius. Wins over an earlier border-radius; a later border-radius resets it. |
| `border-top-width` | length | The width of one side. Also border-right-width, border-bottom-width, border-left-width. |
| `border-top-color` | color | The colour of one side. Also border-right-color, border-bottom-color, border-left-color. |
| `font-family` | family list | The typeface. The first family the machine has is used, and a font registered with PrivateFontCollection counts as one it has; generic names (sans-serif, serif, monospace) are passed to the OS font matcher. |
| `font-size` | length | The text size in pixels (not points). |
| `font-weight` | normal | bold | 100..900 | The weight. Without a font-family in the same rule, the default UI font family is used at that weight. |
| `font-style` | normal | italic | oblique | The slant. Without a font-family in the same rule, the default UI font family is used. |
| `box-shadow` | none | [inset] <offset-x> <offset-y> [<blur> [<spread>]] <color># | Shadows with the control's own shape, the first on top: an outer one behind the face, an inset one inside it over the background. e.g. 'box-shadow: 4px 4px #2b1b4d;' (hard and offset) or '0 1px 3px rgb(0 0 0 / 20%), inset 0 0 0 1px #fff'. The colour is required. A control cannot paint outside itself, so the face shrinks to leave room for outer shadows on each side they reach. |
| `background-image` | none | <gradient># | Gradient layers over the background colour, inside the control's rounded face, the first on top: linear-gradient(), radial-gradient(), conic-gradient() and their repeating- forms, e.g. 'linear-gradient(to bottom, #ffffff, #e6e6e6)'. Stops take lengths, percentages (angles for conic) and two positions for a hard band; url() images and transition hints are not supported. 'background' (the shorthand) stays an error. |
| `outline` | [width] [solid | dashed | auto | none] [color] | A ring drawn around the control, outside it, by its parent: it takes no room and moves nothing -- the usual focus ring, e.g. 'Button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }'. Shorthand for outline-width (thin 1px, medium 3px, thick 5px), outline-style and outline-color (the text colour when omitted). It follows the corner radii. Not on windows or parts. |
| `outline-offset` | length | How far outside the control's edge the outline sits; negative draws it inside. |
| `opacity` | <number> | <percentage> | How opaque the control is, 0 to 1 (or 0% to 100%), children included: its parent composites it translucent, e.g. 'Button:disabled { opacity: .65; }'. Hit-testing is unchanged. Not on windows (Form, PopupWindow, HostedSurface): a window's own opacity is Form.Opacity. |
| `transition` | [<property> || <duration> || <timing-function> || <delay>]# | none | Animates the change into this rule's state: shorthand for the four transition-* longhands, one comma-separated entry per property, e.g. 'transition: background-color 150ms ease-out, box-shadow 200ms;'. In an entry the first time is the duration and the second the delay; a missing property is 'all', a missing duration 0s, a missing timing function 'ease'. Runs on controls with pseudo-classes, and not at all when the user prefers reduced motion. |
| `transition-property` | none | all | <property># | Which properties animate. Animatable: background-color, color, border-color, border-top-color, border-right-color, border-bottom-color, border-left-color, border-radius, border-top-left-radius, border-top-right-radius, border-bottom-right-radius, border-bottom-left-radius, box-shadow, opacity, outline-color, outline-width, outline-offset. Border widths are rejected (they move the client area, and with it the children); fonts and border-style have no in-between values. |
| `transition-duration` | <time># | How long each transition takes: '150ms' or '.15s'. The n-th duration goes with the n-th property; a shorter list repeats. |
| `transition-timing-function` | <timing-function># | The easing: linear, ease (the default), ease-in, ease-out, ease-in-out, step-start, step-end, cubic-bezier(x1, y1, x2, y2), steps(n[, position]). linear() stop lists are not supported. |
| `transition-delay` | <time># | How long each transition waits before it starts; a negative delay starts it part-way through. |
| `animation` | [<name> || <duration> || <timing-function> || <delay> || <iteration-count> || <direction> || <fill-mode> || <play-state>]# | none | Plays '@keyframes' while the control is in this rule's state, e.g. 'animation: pulse 1.2s ease-in-out infinite alternate;'. Shorthand for the eight animation-* longhands; the first time is the duration, the second the delay. The keyframes must be in the same sheet. Animates the same properties as transition, wins over it, and does not run when the user prefers reduced motion. Not on windows or parts. |
| `animation-name` | none | <keyframes-name># | Which '@keyframes' play. Changing the name restarts the animation; keeping it lets it run on across a state change. |
| `animation-duration` | <time># | How long one iteration takes. |
| `animation-timing-function` | <timing-function># | The easing between keyframes; a keyframe can set its own with animation-timing-function inside it. |
| `animation-delay` | <time># | How long it waits before starting; negative starts part-way through. |
| `animation-iteration-count` | <number> | infinite | How many times it runs; fractions stop part-way. An infinite one pauses while its control is off screen. |
| `animation-direction` | normal | reverse | alternate | alternate-reverse | Which way the iterations run. |
| `animation-fill-mode` | none | forwards | backwards | both | Whether the first keyframe applies during the delay (backwards) and the last stays after the end (forwards). |
| `animation-play-state` | running | paused | A paused animation holds its starting frame. |
<!-- END GENERATED: reference -->

Two things to know about **fonts**, because they are the one place the tokens and the rules meet:

- `--ui-font`, `--ui-font-bold`, `--font-size` and `--item-font-size` are the *theme font*, read directly
  by the renderers of composite controls (menus, tool bars, grids, list views, the title bar).
- Simple controls (`Button`, `Label`, `TextBox`, `CheckBox`, ...) draw with the WinForms-style
  **ambient** font: their own `Font` if set, else their parent's, ending at the window. So the way to
  change the app-wide text font is a `Form { font-family: ...; font-size: ...; }` rule, which every child
  inherits, or a rule on the specific type.

### Bundling a font

An app that ships its own typeface registers it with `PrivateFontCollection` and then names the family in
CSS like any installed one. The bytes can come from anywhere; an **embedded resource** needs no platform
API, so the same code runs on every head:

```csharp
// Once, at start-up and before the theme is loaded. Keep the collection for the life of the app:
// disposing it unregisters its fonts.
using var stream = typeof (Program).Assembly.GetManifestResourceStream ("MyApp.Fonts.MyFont-Regular.ttf")!;
var bytes = new byte[stream.Length];
stream.ReadExactly (bytes);

fonts = new PrivateFontCollection ();
fonts.AddMemoryFont (bytes);

Theme.LoadFromCss ("Form { font-family: \"My Font\", sans-serif; font-size: 16px; }");
```

Name the family the way the font file names it (`fonts.Families[0].Name` shows it). A private family is
found ahead of the system font manager for the same name, and in a list it counts as one the machine has,
so `"My Font", sans-serif` uses the bundled font and only falls back if it was never registered. A
family the system really has, listed *before* a private one, still wins.

What has been run: an embedded resource with `AddMemoryFont` on the Headless backend (Linux) and on an
Android emulator, and `AddFontFile` on Headless. Anything else that yields the file's bytes should
work the same way (an Android asset opened with `Assets.Open`, a file from an iOS bundle read with
`File.ReadAllBytes`), but those routes, iOS as a whole, and a real device have not been tried, so
check them in your own app.

### Colour emoji

A character followed by VARIATION SELECTOR-16 (U+FE0F, "️") asks for its **emoji presentation** — a
coloured pictograph rather than a plain monochrome glyph. Some base characters (WARNING SIGN, U+26A0, is
the common one: most text fonts already include a plain triangle for it) are covered by the UI font
itself, so nothing in the text ever looked *missing* — it just drew as a grayscale outline instead of the
coloured triangle a chat app or a phone's own text field would show for the same string.

This is a font-mapping limitation, not a glyph one: RichTextKit resolves one typeface per `Style` (a run),
not per character, so it has no way to notice that one specific codepoint inside a run is asking for a
different presentation. `TextMeasurer.CreateTextBlock` now looks for a trailing VS16 itself, splits that
base character (and the selector) into their own run, and resolves that run's face with
`SKFontManager.MatchCharacter`, hinted with the `und-Zsye` (emoji) BCP-47 tag, so the font manager prefers
an emoji-capable face over whichever plain one already happens to cover the base character. VARIATION
SELECTOR-15 (U+FE0E, explicit **text** presentation) is recognised the same way but left alone — it still
needs its own run (the selector must not be measured as a stray glyph), just not a different face.

No app code is needed for this: it applies to every string measured or drawn through `TextMeasurer`,
`Label`, `DrawString`, and anywhere else in the library that lays out text. What it needs is a colour
emoji face actually installed for the font manager to find — Windows and macOS ship one; a Linux desktop
or CI image needs `fonts-noto-color-emoji` (the same way CJK fallback needs `fonts-noto-cjk`, see
`.github/workflows/dotnet.yml`); Android and iOS ship their own system emoji font. Without one installed,
`MatchCharacter` finds nothing and the base character falls back exactly as it would with no selector at
all — a plain glyph, never a missing one.

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
  /* What one 'rem' is, in pixels: 16px by default, a browser's root font size, so a rem copied from a web stylesheet keeps its size. Changes no font by itself. */
  --root-font-size: 16px;
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
- The **Tokens** tab shows every token's current value as a swatch, and below them every part
  (`Selector::part`) with its current background and text colour, so you can see which one to change.
- `--render-headless out.png [theme.css] [--tab N]` renders the preview to a PNG without a display (tabs:
  0 inputs, 1 lists and grids, 2 menus and chrome, 3 token swatches) and exits non-zero if the theme has
  errors — for CI, or for an assistant that wants to *look* at its theme.
- `samples/ThemeStudio/Themes/` ships six example themes to start from: `light.css` / `dark.css` (a
  matched pair with one accent and identical control rules, so an app can switch modes without anything
  moving), `ocean.css`, `graphite.css`, `paper.css` and `parchment.css`.

## Scoped themes

`Theme` is application-wide. A `ThemeScope` themes one window, or one control and everything inside it,
while the rest of the application keeps `Theme` -- for a component embedded in a host app (a report
designer inside a WPF app) or two documents side by side in different themes.

```csharp
var scope = new ThemeScope ();
scope.LoadFromCss (File.ReadAllText ("Themes/ocean.css"));   // or scope.SetBuiltInTheme (BuiltInTheme.Dark)
designerControl.ThemeScope = scope;                           // or form.ThemeScope = scope

scope.AccentColor = SKColors.Orange;                          // one token; the controls repaint
scope.Reset (nameof (Theme.AccentColor));                     // follow Theme again
```

- **What it covers.** The control (or window) and its children. A dialog with no scope of its own uses
  its `Owner`'s (or the window it was shown over with `ShowDialog (owner)`), an MDI child its parent's,
  and a drop-down, context menu or tool tip the scope of the control it opened for -- so a combo box in a
  scoped panel of an unscoped window gets a scoped list. Failing all of those, a window created while a
  scope was current uses it: a dialog or message box opened from a scoped control's click, menu or key
  handler looks like that control even when its owner is an unscoped host window (or there is none, as
  when the control is hosted in another toolkit).
- **What it overrides.** Only the tokens it sets; `SetBuiltInTheme` and `LoadFromCss`/`ApplyStyleSheet`
  set all of them. Every other token follows `Theme`, including later changes. The application's CSS
  control rules apply inside a scope until the scope has a theme of its own (`SetBuiltInTheme` or a
  stylesheet), whose rules then replace them there. A scoped stylesheet may `extends` a built-in theme or
  a CSS theme registered with `Theme.RegisterThemeCss`, not an XML one.
- **What it leaves alone.** `Theme`'s values, `Theme.ThemeChanged`, `Theme.CurrentStyleSheets` and the
  WinForms/Avalonia bridges. A scope raises its own `Changed` event (once per `BeginUpdate`/`EndUpdate`).
- **Custom painting.** While Majorsilence.Forms paints, lays out or delivers input to a control, its
  scope is `ThemeScope.Current` and every `Theme` property reads through it, so `OnPaint` code reading
  `Theme.AccentColor` follows the scope unchanged; so do `Button.DefaultStyle` and the other type-level
  styles. Code running outside those paths -- a timer, a background thread -- sees the application theme;
  wrap it in `using (scope.Enter ()) { ... }` to read the scope's values.
- **Cost.** An application that never creates a scope pays one static read per paint, layout and input
  event.

## Real System.Windows.Forms apps

The same stylesheet can restyle **real WinForms controls** in a mixed migration app through
`Majorsilence.Forms.Theming.WinForms` (`WinFormsCssTheme.Apply` / `Watch` / `Track`), as far as WinForms
allows — with every gap reported as a diagnostic, never silently ignored. The property × control support
matrix and the Windows-only Theme Studio head are in [theming-winforms.md](theming-winforms.md).

## Native Avalonia apps

The same stylesheet can restyle **native Avalonia controls** (Fluent theme) through
`Majorsilence.Forms.Theming.Avalonia` (`AvaloniaCssTheme.Apply` / `Watch`): colours become the Fluent
theme resources the control themes read, geometry and fonts become generated styles, and the sheet's
tokens and author variables are published as named resources for your own views. The support matrix is
in [theming-avalonia.md](theming-avalonia.md).

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

### Reading a theme from other code (host bridges)

A parsed sheet is also available as a **host-neutral model**, for code that mirrors the theme onto
another toolkit (real `System.Windows.Forms`, Avalonia) without re-parsing the CSS:

```csharp
var sheet = ThemeStyleSheet.Parse (css);

foreach (var token in sheet.Tokens)              // ThemeCssTokenValue: Token, Value, TokenReference
    Console.WriteLine ($"{token.Token.Name} = {token.Value}");

foreach (var rule in sheet.Rules)                // ThemeCssRule: Selector, Hover, Declarations
    foreach (var d in rule.Declarations)         // ThemeCssDeclaration: Property, Value, TokenReference
        Apply (rule.Selector.Name, rule.Hover, d.Property, d.Value);   // ThemeCssValue: Argb / Pixels / FontFamilies / FontWeight / FontStyle

Theme.StyleSheetApplied += (_, e) => Mirror (e.StyleSheet, e.Chain);  // after ThemeChanged, once per apply
var current = Theme.CurrentStyleSheets;                                // the installed chain, base first
```

- Values are plain data — colours as `0xAARRGGBB`, lengths as pixels, fonts as family lists, weights as
  100–900, styles as the keyword — with no SkiaSharp types. `border` is already expanded to
  `border-width` / `border-color`; the three `font-*` properties stay separate.
- Author variables (`:root` custom properties that are not tokens) are in `sheet.Variables`
  (`ThemeCssVariable`: `Name`, a typed `Value` — colour, length or font list, or null — and
  `TokenReference`), for a bridge that publishes them as named resources.
- A declaration written as `var(--token)` keeps `TokenReference`, and its `Value` is read **live** from the
  current theme, so a bridge that re-applies on `ThemeChanged` follows later token edits exactly as the
  built-in renderers do. Author variables (`--brand`) are substituted at parse time and look like literals.
- The model is built from the same compiled declarations the renderers apply; a test keeps the two in
  agreement.
