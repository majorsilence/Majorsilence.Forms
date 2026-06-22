using Avalonia;

namespace EmbeddingAvalonia;

// A host-owned Avalonia desktop app that embeds Majorsilence.Forms content via MajorsilenceFormsPresenter.
// Run on a desktop session: `dotnet run --project samples/EmbeddingAvalonia`.
public static class Program
{
    [System.STAThread]
    public static void Main (string[] args)
        => BuildAvaloniaApp ().StartWithClassicDesktopLifetime (args);

    public static AppBuilder BuildAvaloniaApp ()
        => AppBuilder.Configure<App> ()
            .UsePlatformDetect ()
            .WithInterFont ()
            .UseSkia ()
            .LogToTrace ();
}
