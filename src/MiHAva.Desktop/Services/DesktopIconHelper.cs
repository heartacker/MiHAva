using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using QRCoder;

namespace MiHAva.Desktop.Services;

public static class DesktopIconHelper
{
    private static WindowIcon? _cachedIcon;

    public static WindowIcon GetOrCreateAppIcon()
    {
        if (_cachedIcon != null) return _cachedIcon;

        // 动态生成一张 32x32 绿色品牌图标
        using var mem = new MemoryStream();
        // 简单 32x32 单色 PNG 二进制
        byte[] pngHeader = [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG Header
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR
            0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x20,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x73, 0x7A, 0x7A, 0xF4
        ];
        
        try
        {
            // 借助 QRCoder 渲染一个微型特征二维码作为图标位图
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode("MiHome", QRCodeGenerator.ECCLevel.L);
            using var pngQr = new PngByteQRCode(data);
            byte[] bytes = pngQr.GetGraphic(2);
            using var stream = new MemoryStream(bytes);
            _cachedIcon = new WindowIcon(stream);
        }
        catch
        {
            // 异常兜底
            _cachedIcon = new WindowIcon(new MemoryStream(pngHeader));
        }

        return _cachedIcon;
    }
}
