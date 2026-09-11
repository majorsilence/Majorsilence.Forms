using Xunit;

// WinFormsCssTheme, Theme and ToolStripManager.Renderer are process-wide state; tests that apply a
// stylesheet cannot run alongside each other.
[assembly: CollectionBehavior (DisableTestParallelization = true)]
