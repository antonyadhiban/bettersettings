using System.Text.Json;
using Microsoft.Win32;

namespace BetterSettings.App;

public enum DisplayMode
{
    Compact,
    Detailed
}

/// <summary>
/// Manages application settings and startup preferences.
/// </summary>
public sealed class SettingsService
{
    private const string SettingsFileName = "settings.json";
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "BetterSettings";

    private readonly string _settingsPath;
    private AppSettings _settings;

    public SettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BetterSettings");
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, SettingsFileName);
        _settings = LoadSettings();
    }

    public DisplayMode DisplayMode
    {
        get => _settings.DisplayMode;
        set
        {
            _settings.DisplayMode = value;
            SaveSettings();
        }
    }

    public bool LoadOnStartup
    {
        get => IsInStartup();
        set
        {
            if (value)
            {
                AddToStartup();
            }
            else
            {
                RemoveFromStartup();
            }
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Return default settings on error
        }
        return new AppSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Best-effort save
        }
    }

    private bool IsInStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    private void AddToStartup()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true);
            key?.SetValue(AppName, $"\"{exePath}\"");
        }
        catch
        {
            // Ignore errors
        }
    }

    private void RemoveFromStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch
        {
            // Ignore errors
        }
    }

    private sealed class AppSettings
    {
        public DisplayMode DisplayMode { get; set; } = DisplayMode.Compact;
    }
}
