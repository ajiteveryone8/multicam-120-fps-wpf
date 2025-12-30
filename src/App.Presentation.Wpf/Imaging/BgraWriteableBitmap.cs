using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace App.Presentation.Wpf.Imaging;

public sealed class BgraWriteableBitmap
{
    private WriteableBitmap? _bmp;

    public BitmapSource? Source => _bmp;

    public void Ensure(int width, int height, double dpiX = 96, double dpiY = 96)
    {
        if (_bmp != null && _bmp.PixelWidth == width && _bmp.PixelHeight == height) return;
        _bmp = new WriteableBitmap(width, height, dpiX, dpiY, PixelFormats.Bgra32, null);
    }

    public unsafe void Update(ReadOnlySpan<byte> bgra, int width, int height, int strideBytes)
    {
        if (_bmp == null) return;

        _bmp.Lock();
        try
        {
            var backBuffer = (byte*)_bmp.BackBuffer;
            var backStride = _bmp.BackBufferStride;

            fixed (byte* src0 = bgra)
            {
                byte* src = src0;
                byte* dst = backBuffer;

                int rowBytes = Math.Min(strideBytes, backStride);
                for (int y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(src, dst, backStride, rowBytes);
                    src += strideBytes;
                    dst += backStride;
                }
            }

            _bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            _bmp.Unlock();
        }
    }
}
