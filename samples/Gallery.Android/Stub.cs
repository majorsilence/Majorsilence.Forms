namespace Gallery.Android
{
    // Present only so this project still compiles to a (tiny, empty, unreferenced) assembly when the
    // `android` workload is absent and EnableMobileHeads is off -- see the csproj header. MainActivity.cs
    // and App.cs, which hold the real entry point, are excluded from that stub build because they depend
    // on the workload's Android.* / Avalonia.Android assemblies.
    internal static class MobileHeadStub
    {
    }
}
