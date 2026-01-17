using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace BetterSettings;

public sealed partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private AppShell? _appShell;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _serviceProvider = ConfigureServices();
        _appShell = _serviceProvider.GetRequiredService<AppShell>();
        _appShell.Initialize();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<PlatformService>();
        services.AddSingleton<CatalogLoader>();
        services.AddSingleton<IndexStore>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<LaunchService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<AppShell>();
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
