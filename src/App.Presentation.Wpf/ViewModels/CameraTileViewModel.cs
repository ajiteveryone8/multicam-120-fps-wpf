using System.Windows.Media;
using System.Windows.Media.Imaging;
using App.Domain;
using App.Presentation.Wpf.Imaging;
using App.Presentation.Wpf.Mvvm;

namespace App.Presentation.Wpf.ViewModels;

public sealed class CameraTileViewModel : ObservableObject
{
    private readonly BgraWriteableBitmap _bitmap = new();

    private string _title = "";
    private string _subtitle = "";
    private ImageSource? _preview;
    private string _fpsText = "—";
    private string _worstDeltaText = "—";
    private string _dropsText = "—";
    private string _healthText = "—";
    private Brush _healthBrush = Brushes.Gray;

    public CameraId CameraId { get; }

    public string Title { get => _title; set => Set(ref _title, value); }
    public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value); }
    public ImageSource? Preview { get => _preview; private set => Set(ref _preview, value); }

    public string FpsText { get => _fpsText; private set => Set(ref _fpsText, value); }
    public string WorstDeltaText { get => _worstDeltaText; private set => Set(ref _worstDeltaText, value); }
    public string DropsText { get => _dropsText; private set => Set(ref _dropsText, value); }

    public string HealthText { get => _healthText; private set => Set(ref _healthText, value); }
    public Brush HealthBrush { get => _healthBrush; private set => Set(ref _healthBrush, value); }

    public CameraTileViewModel(CameraId id, string subtitle)
    {
        CameraId = id;
        Title = id.ToString();
        Subtitle = subtitle;
    }

    public void UpdatePreview(FrameMetadata meta, ReadOnlySpan<byte> bgra, int strideBytes)
    {
        _bitmap.Ensure(meta.Width, meta.Height);
        _bitmap.Update(bgra, meta.Width, meta.Height, strideBytes);
        Preview = _bitmap.Source;
    }

    public void UpdateHealth(CameraHealth h)
    {
        FpsText = h.Fps <= 0 ? "—" : $"{h.Fps:0.0}";
        WorstDeltaText = h.WorstFrameDeltaMs <= 0 ? "—" : $"{h.WorstFrameDeltaMs:0.00} ms";
        DropsText = $"{h.DroppedFrames}";

        HealthText = h.State.ToString();
        HealthBrush = h.State switch
        {
            HealthState.Ok => Brushes.ForestGreen,
            HealthState.Degraded => Brushes.DarkOrange,
            HealthState.Faulted => Brushes.Firebrick,
            HealthState.Disconnected => Brushes.DimGray,
            _ => Brushes.Gray
        };
    }
}
