using Microsoft.Win32;
using BetterSettings.App.Models;

namespace BetterSettings.App;

/// <summary>
/// Discovers Windows settings and Control Panel items from the registry.
/// This expands the search index beyond the shipped catalog.
/// </summary>
public sealed class SettingsDiscoveryService
{
    private const string ControlPanelSettingsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Settings";
    private const string ControlPanelExplorerKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ControlPanel\NameSpace";
    private const string ControlPanelClsidKey = @"SOFTWARE\Classes\CLSID";

    /// <summary>
    /// Discovers additional settings from the Windows registry.
    /// </summary>
    public IReadOnlyList<SettingItem> DiscoverFromRegistry()
    {
        var discovered = new List<SettingItem>();

        // Discover from Control Panel Settings
        discovered.AddRange(DiscoverControlPanelSettings());

        // Discover from Control Panel Explorer namespace
        discovered.AddRange(DiscoverControlPanelNamespace());

        return discovered;
    }

    private IEnumerable<SettingItem> DiscoverControlPanelSettings()
    {
        var items = new List<SettingItem>();

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ControlPanelSettingsKey);
            if (key == null) return items;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName") as string;
                    var keywords = subKey.GetValue("Keywords") as string;
                    var pageUri = subKey.GetValue("PageUri") as string ?? subKey.GetValue("SettingsPageUri") as string;

                    if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(pageUri))
                        continue;

                    // Resolve display name if it's a resource reference
                    displayName = ResolveResourceString(displayName) ?? displayName;

                    var synonyms = new List<string>();
                    if (!string.IsNullOrEmpty(keywords))
                    {
                        synonyms.AddRange(keywords.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(k => ResolveResourceString(k) ?? k)
                            .Where(k => !string.IsNullOrWhiteSpace(k)));
                    }

                    items.Add(new SettingItem
                    {
                        Id = $"registry.settings.{subKeyName.ToLowerInvariant()}",
                        DisplayName = displayName,
                        Synonyms = synonyms.ToArray(),
                        Category = "Settings",
                        Breadcrumbs = $"Settings > {GetCategoryFromUri(pageUri)}",
                        Description = $"Discovered from registry",
                        LaunchType = LaunchType.MsSettings,
                        LaunchTarget = pageUri,
                        IsAvailable = true
                    });
                }
                catch
                {
                    // Skip items that fail to parse
                }
            }
        }
        catch
        {
            // Registry access may fail
        }

        return items;
    }

    private IEnumerable<SettingItem> DiscoverControlPanelNamespace()
    {
        var items = new List<SettingItem>();

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ControlPanelExplorerKey);
            if (key == null) return items;

            foreach (var clsid in key.GetSubKeyNames())
            {
                try
                {
                    // Look up the CLSID in the Classes registry
                    using var clsidKey = Registry.LocalMachine.OpenSubKey($@"{ControlPanelClsidKey}\{clsid}");
                    if (clsidKey == null) continue;

                    var displayName = clsidKey.GetValue(null) as string;
                    var infoTip = clsidKey.GetValue("InfoTip") as string;
                    var localizedString = clsidKey.GetValue("LocalizedString") as string;

                    // Try to get the canonical name for launching
                    string? canonicalName = null;
                    using var shellFolderKey = clsidKey.OpenSubKey("ShellFolder");
                    if (shellFolderKey != null)
                    {
                        // Check for System.ApplicationName
                        using var sysKey = clsidKey.OpenSubKey("System.ApplicationName");
                        canonicalName = sysKey?.GetValue(null) as string;
                    }

                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = localizedString;
                    }

                    if (string.IsNullOrEmpty(displayName))
                        continue;

                    // Resolve resource strings
                    displayName = ResolveResourceString(displayName) ?? displayName;
                    var description = ResolveResourceString(infoTip) ?? infoTip ?? string.Empty;

                    // Determine launch target
                    string launchTarget;
                    LaunchType launchType;

                    if (!string.IsNullOrEmpty(canonicalName))
                    {
                        launchTarget = canonicalName;
                        launchType = LaunchType.ControlPanelCanonical;
                    }
                    else
                    {
                        // Use shell CLSID launch
                        launchTarget = $"shell:::{clsid}";
                        launchType = LaunchType.Exe;
                    }

                    items.Add(new SettingItem
                    {
                        Id = $"registry.controlpanel.{clsid.ToLowerInvariant()}",
                        DisplayName = displayName,
                        Synonyms = Array.Empty<string>(),
                        Category = "Control Panel",
                        Breadcrumbs = "Control Panel",
                        Description = description,
                        LaunchType = launchType,
                        LaunchTarget = launchTarget,
                        IsAvailable = true
                    });
                }
                catch
                {
                    // Skip items that fail to parse
                }
            }
        }
        catch
        {
            // Registry access may fail
        }

        return items;
    }

    private static string GetCategoryFromUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return "System";

        // Parse ms-settings: URIs to get category
        if (uri.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.Substring("ms-settings:".Length);
            var parts = path.Split('-', '_');
            if (parts.Length > 0)
            {
                return char.ToUpper(parts[0][0]) + parts[0].Substring(1);
            }
        }

        return "System";
    }

    private static string? ResolveResourceString(string? resourceRef)
    {
        if (string.IsNullOrEmpty(resourceRef))
            return null;

        // If it doesn't look like a resource reference, return as-is
        if (!resourceRef.StartsWith("@"))
            return resourceRef;

        // For now, we skip resolving complex resource strings
        // This would require P/Invoke to SHLoadIndirectString
        // Return null to indicate we couldn't resolve it
        return null;
    }
}
