using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;          // SKXamlCanvas, SKPaintSurfaceEventArgs
using Uno.UI.Hosting;

namespace UnoCanvasFocusRepro;

// Minimal, self-contained repro (NO third-party UI framework) of keyboard focus / text input not
// reaching a nested SKXamlCanvas on the Uno macOS Skia head.
//
//   Run on a desktop session:   dotnet run --project samples/UnoCanvasFocusRepro
//
// Layout: a native WinUI TextBox (works) above a custom SKXamlCanvas (the problem child), with a live
// on-screen log. Click the canvas and type:
//   • Expected (and what happens on Windows/X11 heads): canvas receives CharacterReceived / KeyDown.
//   • Actual on macOS Skia head: the system "unhandled key" BEEP fires per keystroke, no CharacterReceived
//     reaches the canvas, and FocusManager.GetFocusedElement(XamlRoot) reports <null> even though
//     SKXamlCanvas.Focus() returned true.
public static class Program
{
    [STAThread]
    public static void Main ()
    {
        var host = UnoPlatformHostBuilder.Create ()
            .App (() => new App ())
            .UseX11 ()
            .UseWin32 ()
            .UseMacOS ()
            .Build ();

        host.Run ();
    }
}

public sealed class App : Application
{
    private Window? _window;
    private TextBlock? _log;
    private SKXamlCanvas? _canvas;
    private readonly List<string> _lines = new ();

    public App ()
    {
        // Native WinUI controls need their default styles.
        Resources.MergedDictionaries.Add (new Microsoft.UI.Xaml.Controls.XamlControlsResources ());
    }

    protected override void OnLaunched (LaunchActivatedEventArgs args)
    {
        _window = new Window ();

        var root = new Grid ();
        root.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add (new RowDefinition { Height = new GridLength (160) });
        root.RowDefinitions.Add (new RowDefinition { Height = new GridLength (1, GridUnitType.Star) });

        // (1) Reference: a native WinUI TextBox. Focusing + typing here works on every head.
        var nativeBox = new TextBox { PlaceholderText = "Native WinUI TextBox — this works", Margin = new Thickness (8) };
        Grid.SetRow (nativeBox, 0);
        root.Children.Add (nativeBox);

        var instructions = new TextBlock {
            Margin = new Thickness (8, 0, 8, 4),
            TextWrapping = TextWrapping.Wrap,
            Text = "Click the dark SKXamlCanvas below and type. On the macOS Skia head this beeps per key and " +
                   "no CharacterReceived reaches the canvas; FocusManager reports <null> though Focus() returned true."
        };
        Grid.SetRow (instructions, 1);
        root.Children.Add (instructions);

        // (2) The custom surface under test.
        _canvas = new SKXamlCanvas { IsTabStop = true };
        _canvas.PaintSurface += OnPaint;
        _canvas.PointerPressed += (_, _) => {
            var ok = _canvas!.Focus (FocusState.Pointer);
            Log ($"canvas PointerPressed → Focus() returned {ok}");
            ReportFocus ("after canvas click");
        };
        _canvas.GotFocus += (_, _) => Log ("canvas GotFocus");
        _canvas.LostFocus += (_, _) => Log ("canvas LostFocus");
        _canvas.CharacterReceived += (_, e) => Log ($"canvas CharacterReceived '{e.Character}'  (does NOT fire on macOS)");
        _canvas.AddHandler (UIElement.KeyDownEvent,
            new KeyEventHandler ((_, e) => Log ($"canvas routed KeyDown {e.Key}")),
            handledEventsToo: true);
        _canvas.Loaded += (_, _) => {
            var ok = _canvas!.Focus (FocusState.Programmatic);
            Log ($"canvas Loaded → Focus() returned {ok}");
            ReportFocus ("after load (idle)");
        };
        Grid.SetRow (_canvas, 2);
        root.Children.Add (_canvas);

        // Live on-screen log (so the issue is visible without a console).
        var scroller = new ScrollViewer { Margin = new Thickness (8) };
        _log = new TextBlock { FontFamily = new FontFamily ("Consolas"), TextWrapping = TextWrapping.Wrap };
        scroller.Content = _log;
        Grid.SetRow (scroller, 3);
        root.Children.Add (scroller);

        // Trace where keyboard focus actually goes.
        try {
            FocusManager.GotFocus += (_, e) =>
                Log ($"FocusManager.GotFocus → {e.NewFocusedElement?.GetType ().Name ?? "<null>"}");
        } catch { }

        _window.Content = root;
        _window.Activate ();
    }

    private static void OnPaint (object? sender, SKPaintSurfaceEventArgs e)
    {
        var c = e.Surface.Canvas;
        c.Clear (new SKColor (0x20, 0x20, 0x28));
        using var font = new SKFont { Size = 16 };
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        c.DrawText ("Custom SKXamlCanvas — click here and type", 14, 32, font, paint);
    }

    private void ReportFocus (string when)
    {
        var f = FocusManager.GetFocusedElement (_canvas!.XamlRoot) as Microsoft.UI.Xaml.DependencyObject;
        Log ($"  FocusManager.GetFocusedElement ({when}) = {f?.GetType ().Name ?? "<null>"}");
    }

    private void Log (string msg)
    {
        Console.Error.WriteLine ("[repro] " + msg);
        _lines.Add (msg);
        if (_lines.Count > 200)
            _lines.RemoveAt (0);
        if (_log is not null)
            _log.Text = string.Join ("\n", _lines);
    }
}
