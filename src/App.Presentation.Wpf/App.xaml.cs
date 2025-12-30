using System.IO;
using System.Windows;
using App.Application;
using App.Common.Options;
using App.Infrastructure.Cameras;
using App.Infrastructure.Cameras.Abstractions;
using App.Infrastructure.Timing;
using App.Services.Diagnostics;
using App.Services.FramePipeline;
using App.Presentation.Wpf.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Presentation.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.Sources.Clear();
                cfg.SetBasePath(AppContext.BaseDirectory);

                // appsettings.json is copied to output and can be edited per machine.
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureLogging(log =>
            {
                log.ClearProviders();
                log.AddConsole();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<AppOptions>(ctx.Configuration.GetSection("App"));

                // Core infrastructure
                services.AddSingleton<IMonotonicClock, MonotonicClock>();
                services.AddSingleton<ICameraFactory, CameraFactory>();

                // Diagnostics
                services.AddSingleton<ICameraDiagnostics, CameraDiagnostics>();

                // Frame hub + pipeline factory (singleton hub shared across pipelines)
                services.AddSingleton<CameraPipelineFactory>();
                services.AddSingleton<ICameraPipelineFactory>(sp => sp.GetRequiredService<CameraPipelineFactory>());
                services.AddSingleton<IFrameHub>(sp => sp.GetRequiredService<CameraPipelineFactory>().Hub);

                // Application orchestration
                services.AddSingleton<ICameraSystem, CameraSystem>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>(sp =>
                {
                    var w = new MainWindow();
                    w.DataContext = sp.GetRequiredService<MainViewModel>();
                    return w;
                });
            })
            .Build();

        await _host.StartAsync().ConfigureAwait(false);

        var main = _host.Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                _host.Dispose();
            }
        }
        catch { }
        base.OnExit(e);
    }
}
