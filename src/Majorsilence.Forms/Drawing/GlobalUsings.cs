// Makes the cross-platform Majorsilence.Forms.Drawing types (Image, Bitmap, Font, Pen, Brush, Icon, Region,
// StringFormat, etc.) visible by their bare names throughout the Majorsilence.Forms assembly, the same
// way `using System.Drawing;` exposed the Windows-only GDI+ types it replaces. The value types
// (Color, Point, Size, Rectangle) still come from System.Drawing.Primitives, which is cross-platform.
global using Majorsilence.Forms.Drawing;
