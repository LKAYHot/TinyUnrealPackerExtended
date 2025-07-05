using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Versions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.Interfaces;
using TinyUnrealPackerExtended.Models;

namespace TinyUnrealPackerExtended.Services
{
    public class UassetInspectorService : IUassetInspectorService
    {
        public async Task<(string pixelFormat, string mappedCsv)> InspectAsync(string uassetPath, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                var fullPath = Path.GetFullPath(uassetPath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("UAsset file not found", fullPath);

                using var provider = new DefaultFileProvider(
                     Path.GetDirectoryName(fullPath)!,
                     SearchOption.TopDirectoryOnly,
                     new VersionContainer(EGame.GAME_UE4_27)
                 );
                provider.Initialize();

                var key = provider.Files.Keys
                    .FirstOrDefault(k => k.EndsWith(Path.GetFileName(fullPath), StringComparison.OrdinalIgnoreCase));
                if (key == null)
                    throw new InvalidOperationException("Package not found in provider");

                var pkg = provider.LoadPackage(key);
                var tex = pkg.ExportsLazy
                             .Select(e => e.Value)
                             .OfType<UTexture2D>()
                             .FirstOrDefault();
                if (tex == null)
                    throw new InvalidOperationException("No Texture2D export inside the UAsset");

                var pf = tex.PlatformData.PixelFormat.ToString();
                var mapped = PngDdsFormatDefinitions.GetMappedCsv(pf)
                             ?? throw new InvalidOperationException($"No mapping defined for format {pf}");

                return (pf, mapped);
            }, ct);
        }
    }
}
