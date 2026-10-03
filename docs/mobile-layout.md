# Laying out a phone-style screen

Three recipes for the screens a phone app is mostly made of: text that wraps to the width it is given, content grouped on
cards, and a list whose rows are more than one line. The first needs nothing new; the other two are `Card` and `RichListBox`.

## Text that wraps to the available width

Use the ordinary controls. All of these wrap, and `Label.GetPreferredSize` reports the wrapped height, so there is nothing to
measure by hand.

```csharp
// 1. Fixed width: the text wraps inside it. Ask what height that needs.
var label = new Label { Text = longText, AutoSize = false, Width = 300 };
label.Height = label.GetPreferredSize (new Size (300, 0)).Height;

// 2. Let the label size itself, capped at a width. It wraps at the cap once the form has laid it out.
var note = new Label { Text = longText, AutoSize = true, MaximumSize = new Size (300, 0) };

// 3. A column of labels that each take the panel's width and wrap: no WrapContents, top to bottom.
var column = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, AutoScroll = true };
column.Controls.Add (new Label { Text = title, AutoSize = true });
column.Controls.Add (new Label { Text = body, AutoSize = true });
```

`GetPreferredSize` can report a width a few pixels over the one you proposed (a word that does not fit), so size a wrapping label
from the **height** it returns.

## Cards

`Card` is a `Panel` drawn as a rounded, bordered surface with padding. It takes its colours from the theme, so it follows a
light or dark theme, and everything it sets is ordinary `Style`, so a theme rule or code can restyle it.

```csharp
var card = new Card { Dock = DockStyle.Top, Height = 120 };   // CornerRadius defaults to 12, Padding to 12
card.Controls.Add (new Label { Text = "Reminders", Dock = DockStyle.Top });
card.CornerRadius = 20;
```

Put a `FlowLayoutPanel` or `TableLayoutPanel` inside it when the card holds more than one thing.

## A list with multi-line rows

`RichListBox` is a `ListBox` whose rows come from a template, so a list of posts, messages or results can show a title, a date and
an excerpt per row. It keeps selection, keyboard, mouse wheel and touch scrolling, and only the rows on screen are drawn.

```csharp
var list = new RichListBox { Dock = DockStyle.Fill };
list.ItemTemplate = item => {
    var post = (Post) item;
    return new[] {
        new ListItemLine (post.Title) { Bold = true, FontSize = 16 },
        new ListItemLine (post.Date) { Muted = true, FontSize = 12 },
        new ListItemLine (post.Excerpt) { MaxLines = 3 },          // cut off with an ellipsis after three lines
    };
};
list.Items.AddRange (posts.ToArray ());
list.SelectedIndexChanged += (_, _) => Show ((Post?) list.SelectedItem);
```

- Each `ListItemLine` wraps at the row's width and can set its own `FontSize`, `Bold`, `Italic`, `Muted` and `MaxLines`.
- Rows are as tall as their wrapped text needs. Heights are measured when items are added and again when the list is resized.
  If an item's text changes without the item count changing, call `RefreshItems()`.
- `ItemPadding`, `ItemSpacing` and `ItemCornerRadius` shape the cards.
- Without an `ItemTemplate` each item's text is shown on one row.
