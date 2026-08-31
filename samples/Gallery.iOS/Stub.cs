namespace Gallery.iOS
{
    // Present only so this project still compiles to a (tiny, empty, unreferenced) assembly when it is
    // not being built as a real iOS app -- see the csproj header. Main.cs and AppDelegate.cs, which hold
    // the real entry point, are excluded from that stub build because they depend on the workload's
    // ObjCRuntime / UIKit / Avalonia.iOS assemblies.
    internal static class MobileHeadStub
    {
    }
}
