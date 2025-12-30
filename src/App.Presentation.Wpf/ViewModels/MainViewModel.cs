using System.Collections.ObjectModel;
using System.Windows.Threading;
using App.Application;
using App.Common.Options;
using App.Domain;
using App.Presentation.Wpf.Mvvm;
using App.Services.Diagnostics;
using App.Services.FramePipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Presentation.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICameraSystem _system;
    private readonly IFrameHub _frames;
    private readonly ICameraDiagnostics _diag;
    private readonly AppOptions _opts;
    private readonly ILogger _log;

    private readonly DispatcherTimer _timer;

    public ObservableCollection<CameraTileViewModel> Cameras { get; } = new();

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    private bool _isRunning;

    public MainViewModel(
        ICameraSystem system,
        IFrameHub frames,
        ICameraDiagnostics diag,
        IOptions<AppOptions> options,
        ILogger<MainViewModel> log)
    {
        _system = system;
        _frames = frames;
        _diag = diag;
        _opts = options.Value;
        _log = log;

        StartCommand = new RelayCommand(Start, () => !_isRunning);
        StopCommand = new RelayCommand(Stop, () => _isRunning);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, _opts.Diagnostics.UiUpdateHz))
        };
        _timer.Tick += (_, __) => Tick();
    }

    private async void Start()
    {
        try
        {
            if (_isRunning) return;
            _isRunning = true;
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();

            Cameras.Clear();

            foreach (var cam in _opts.CameraSystem.Cameras)
            {
                var id = CameraId.From(cam.CameraId);
                Cameras.Add(new CameraTileViewModel(id, $"{cam.Provider} • {cam.Width}×{cam.Height} @ {cam.TargetFps} fps"));
            }

            await _system.StartAsync(CancellationToken.None).ConfigureAwait(false);
            _timer.Start();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Start failed");
            _isRunning = false;
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    private async void Stop()
    {
        try
        {
            _timer.Stop();
            await _system.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Stop failed");
        }
        finally
        {
            _isRunning = false;
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    private void Tick()
    {
        foreach (var vm in Cameras)
        {
            if (_frames.TryGetLatest(vm.CameraId, out var latest))
            {
                // LatestFrame memory is owned by hub; treat as read-only.
                vm.UpdatePreview(latest.Metadata, latest.Buffer.Span, latest.StrideBytes);
            }

            var health = _diag.GetSnapshot(vm.CameraId);
            vm.UpdateHealth(health);
        }
    }
}
