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
using TinyUnrealPackerExtended.Extensions;
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

        public Task<BitmapImage> ExtractFullResolutionAsync(string uassetPath, CancellationToken ct = default)
            => ExtractInternalAsync(uassetPath, skipResize: false, ct);

        public async Task<BitmapImage?> ExtractInternalAsync(
        string uassetPath,
        bool skipResize,
        CancellationToken ct = default)
        {
            return await Task.Run(() => PerformExtraction(uassetPath, skipResize, ct), ct);
        }

        private BitmapImage PerformExtraction(
            string uassetPath,
            bool skipResize,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string fullPath = ResolveAndValidatePath(uassetPath);
            string directory = Path.GetDirectoryName(fullPath)!;
            string fileName = Path.GetFileName(fullPath);

            using var provider = ProviderFactory.Create(
    directory,
    SearchOption.TopDirectoryOnly,
    _gameVersion);

            ct.ThrowIfCancellationRequested();
            string key = FindPackageKey(provider, fileName);

            ct.ThrowIfCancellationRequested();
            var texture = LoadFirstTexture(provider, key);

            ct.ThrowIfCancellationRequested();
            using SKBitmap skBitmap = DecodeTexture(texture);

            ct.ThrowIfCancellationRequested();
            SKBitmap finalBitmap = skipResize
                ? ResizeIfNeeded(skBitmap)
                : skBitmap;

            ct.ThrowIfCancellationRequested();
            return ConvertToBitmapImage(finalBitmap);
        }

        private string ResolveAndValidatePath(string uassetPath)
        {
            string fullPath = Path.GetFullPath(uassetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(".uasset file not found", fullPath);

            if (!Path.GetExtension(fullPath)
                    .Equals(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"File extension '{Path.GetExtension(fullPath)}' is not supported.");
            }

            return fullPath;
        }

        private string FindPackageKey(DefaultFileProvider provider, string fileName)
        {
            return provider.Files.Keys
                .FirstOrDefault(k => k.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException(
                    "Package not found in provider.", fileName);
        }

        private UTexture2D LoadFirstTexture(DefaultFileProvider provider, string key)
        {
            var package = provider.LoadPackage(key);
            return package.ExportsLazy
                .Select(e => e.Value)
                .OfType<UTexture2D>()
                .FirstOrDefault()
                ?? throw new NotSupportedException(
                    "No UTexture2D found in package.");
        }

        private SKBitmap DecodeTexture(UTexture2D texture)
        {
            return texture.Decode(ETexturePlatform.DesktopMobile)
                ?? throw new InvalidOperationException(
                    "Failed to decode texture from package.");
        }

        private SKBitmap ResizeIfNeeded(SKBitmap source)
        {
            if (source.Width <= MAX_DIMENSION && source.Height <= MAX_DIMENSION)
                return source;

            float scale = Math.Min(
                (float)MAX_DIMENSION / source.Width,
                (float)MAX_DIMENSION / source.Height
            );
            var info = new SKImageInfo(
                (int)(source.Width * scale),
                (int)(source.Height * scale)
            );
            return source.Resize(info, SKFilterQuality.Medium) ?? source;
        }

        private BitmapImage ConvertToBitmapImage(SKBitmap bitmap)
        {
            using var imageData = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(imageData.ToArray());

            var bmpImage = new BitmapImage();
            bmpImage.BeginInit();
            bmpImage.StreamSource = ms;
            bmpImage.CacheOption = BitmapCacheOption.OnLoad;
            bmpImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bmpImage.EndInit();
            bmpImage.Freeze();

            return bmpImage;
        }
    }
}
