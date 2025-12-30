namespace App.Domain;

public enum PixelFormat
{
    Bgra32,
    Gray8,
}

public static class PixelFormatExtensions
{
    public static int BytesPerPixel(this PixelFormat fmt) => fmt switch
    {
        PixelFormat.Bgra32 => 4,
        PixelFormat.Gray8 => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(fmt), fmt, "Unknown pixel format")
    };
}
