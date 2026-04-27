using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace lingualink_client.ViewModels
{
    internal static class AccountQrCodeImageBuilder
    {
        public static BitmapImage? Build(string? codeUrl)
        {
            if (string.IsNullOrWhiteSpace(codeUrl))
            {
                return null;
            }

            try
            {
                using var generator = new QRCodeGenerator();
                using var qrData = generator.CreateQrCode(codeUrl, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrData);
                var pngBytes = qrCode.GetGraphic(20);

                using var memoryStream = new MemoryStream(pngBytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memoryStream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
