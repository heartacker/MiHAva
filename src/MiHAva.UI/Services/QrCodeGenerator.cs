using System.IO;
using Avalonia.Media.Imaging;
using QRCoder;

namespace MiHAva.UI.Services;

public static class QrCodeGenerator
{
    public static Bitmap GenerateQrBitmap(string content, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var pngQr = new PngByteQRCode(qrData);
        byte[] bytes = pngQr.GetGraphic(pixelsPerModule);

        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }
}
