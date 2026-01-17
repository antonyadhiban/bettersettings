using System.Diagnostics;
using BetterSettings.Models;

namespace BetterSettings;

public sealed class LaunchService
{
    public Task<LaunchResult> LaunchAsync(SettingItem item)
    {
        try
        {
            if (!item.IsAvailable)
            {
                return Task.FromResult(new LaunchResult(false, "Setting not available on this Windows version."));
            }

            var startInfo = item.LaunchType switch
            {
                LaunchType.MsSettings => new ProcessStartInfo(item.LaunchTarget) { UseShellExecute = true },
                LaunchType.ControlPanelCanonical => new ProcessStartInfo("control.exe", $"/name {item.LaunchTarget}") { UseShellExecute = true },
                LaunchType.ControlPanelCpl => new ProcessStartInfo("control.exe", item.LaunchTarget) { UseShellExecute = true },
                LaunchType.Msc => new ProcessStartInfo(item.LaunchTarget) { UseShellExecute = true },
                LaunchType.Exe => new ProcessStartInfo(item.LaunchTarget) { UseShellExecute = true },
                _ => new ProcessStartInfo(item.LaunchTarget) { UseShellExecute = true }
            };

            Process.Start(startInfo);
            return Task.FromResult(new LaunchResult(true, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new LaunchResult(false, $"Launch failed: {ex.Message}"));
        }
    }
}

public sealed record LaunchResult(bool Succeeded, string? ErrorMessage);
