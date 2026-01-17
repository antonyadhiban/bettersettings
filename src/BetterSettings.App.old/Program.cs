using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace BetterSettings;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log("Main start");
        BootstrapInitialize();
        Log("Bootstrap initialized");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Log("ComWrappers initialized");
        Application.Start(_ => new App());
    }

    private static void BootstrapInitialize()
    {
        var majorMinorVersion = global::Microsoft.WindowsAppSDK.Release.MajorMinor;
        var versionTag = global::Microsoft.WindowsAppSDK.Release.VersionTag;
        var minVersion = new PackageVersion(global::Microsoft.WindowsAppSDK.Runtime.Version.UInt64);

        if (!Bootstrap.TryInitialize(
                majorMinorVersion,
                versionTag,
                minVersion,
                Bootstrap.InitializeOptions.OnNoMatch_ShowUI,
                out var hr))
        {
            Log($"Bootstrap failed: 0x{hr:X8}");
            Environment.Exit(hr);
        }
    }

    private static void Log(string message)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "bootstrap.log");
            File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
