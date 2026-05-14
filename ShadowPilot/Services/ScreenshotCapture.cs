using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace ShadowPilot.Services;

public static class ScreenshotCapture
{
    public static byte[] Capture()
    {
        // Get the full virtual screen (all monitors)
        var left   = (int)SystemParameters.VirtualScreenLeft;
        var top    = (int)SystemParameters.VirtualScreenTop;
        var width  = (int)SystemParameters.VirtualScreenWidth;
        var height = (int)SystemParameters.VirtualScreenHeight;

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var gfx = Graphics.FromImage(bmp);
        gfx.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));

        using var ms = new MemoryStream();
        var encoder = GetJpegEncoder();
        var @params = new EncoderParameters(1);
        @params.Param[0] = new EncoderParameter(Encoder.Quality, 75L);
        bmp.Save(ms, encoder, @params);
        return ms.ToArray();
    }

    private static ImageCodecInfo GetJpegEncoder()
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
            if (codec.MimeType == "image/jpeg") return codec;
        throw new InvalidOperationException("JPEG encoder not found");
    }
}
