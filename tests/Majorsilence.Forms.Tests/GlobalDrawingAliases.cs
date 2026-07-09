// The test project references the real System.Drawing.Common (to exercise
// ComponentResourceManager.NormalizeDeserialized's Windows live-object path), which makes drawing
// type names ambiguous against Majorsilence.Forms.Drawing in any file importing both namespaces.
// These aliases pin the unqualified names to the fork's types -- the semantic every existing test
// already assumed. Tests that genuinely need the System.Drawing.Common type spell it out fully.
global using Bitmap = Majorsilence.Forms.Drawing.Bitmap;
global using Font = Majorsilence.Forms.Drawing.Font;
global using FontStyle = Majorsilence.Forms.Drawing.FontStyle;
global using Icon = Majorsilence.Forms.Drawing.Icon;
global using Image = Majorsilence.Forms.Drawing.Image;
