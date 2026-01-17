using BetterSettings.App.Models;

namespace BetterSettings.App;

public sealed class PlatformService
{
    public int BuildNumber { get; } = Environment.OSVersion.Version.Build;

    public bool IsAvailable(AvailabilityRule? rule)
    {
        if (rule == null)
        {
            return true;
        }

        if (rule.MinBuild.HasValue && BuildNumber < rule.MinBuild.Value)
        {
            return false;
        }

        return true;
    }
}
