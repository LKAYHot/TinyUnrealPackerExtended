using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Services
{
    public class TexturePreviewService : ITexturePreviewService
    {
        private const int MAX_DIMENSION = 512;
        private readonly EGame _gameVersion;

        public TexturePreviewService(EGame gameVersion = EGame.GAME_UE4_LATEST)
        {
            _gameVersion = gameVersion;
        }

        public Task<BitmapImage> ExtractAsync(string uassetPath, CancellationToken ct = default)
          => ExtractInternalAsync(uassetPath, skipResize: true, ct);

        // 2) публичный для полного разрешения
        public Task<BitmapImage> ExtractFullResolutionAsync(string uassetPath, CancellationToken ct = default)
            => ExtractInternalAsync(uassetPath, skipResize: false, ct);

        // 3) общий приватный метод
        private async Task<BitmapImage> ExtractInternalAsync(
            string uassetPath,
            bool skipResize,
            CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(uassetPath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Файл .uasset не найден", fullPath);

                var directory = Path.GetDirectoryName(fullPath)!;
                var fileName = Path.GetFileName(fullPath);

                using var provider = new DefaultFileProvider(
                    directory,
                    SearchOption.TopDirectoryOnly,
                    new VersionContainer(_gameVersion));
                provider.Initialize();

                var key = provider.Files.Keys
                    .FirstOrDefault(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new FileNotFoundException("Не удалось найти пакет в провайдере", fileName);

                ct.ThrowIfCancellationRequested();

                var package = provider.LoadPackage(key);
                var textureExport = package.ExportsLazy
                    .Select(e => e.Value)
                    .OfType<UTexture2D>()
                    .FirstOrDefault()
                    ?? throw new InvalidDataException("Текстура не найдена в пакете");

                using var skBitmap = textureExport.Decode(ETexturePlatform.DesktopMobile)
                    ?? throw new InvalidOperationException("Не удалось декодировать текстуру");

                ct.ThrowIfCancellationRequested();

                SKBitmap output = skBitmap;
                if (skipResize
                    && (skBitmap.Width > MAX_DIMENSION || skBitmap.Height > MAX_DIMENSION))
                {
                    var scale = Math.Min(
                        (float)MAX_DIMENSION / skBitmap.Width,
                        (float)MAX_DIMENSION / skBitmap.Height);
                    var info = new SKImageInfo(
                        (int)(skBitmap.Width * scale),
                        (int)(skBitmap.Height * scale)
                    );
                    output = skBitmap.Resize(info, SKFilterQuality.Medium)
                             ?? skBitmap;
                }

                ct.ThrowIfCancellationRequested();

                using var imageData = output.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream(imageData.ToArray());

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }, ct);
        }
    }
}
