using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AIUsageWidget.App.Services;

/// <summary>Dock visibility is an application policy on macOS, not a per-window taskbar flag.</summary>
[SupportedOSPlatform("macos")]
internal static class MacApplicationService
{
    public static void SetShowInDock(bool visible)
    {
        var application = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        SetPolicy(application, sel_registerName("setActivationPolicy:"), visible ? 0 : 1);
    }

    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
    [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern byte SetPolicy(IntPtr receiver, IntPtr selector, nint policy);
}
