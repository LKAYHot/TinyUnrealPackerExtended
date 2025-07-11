using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.Extensions;
using TinyUnrealPackerExtended.Interfaces;
using CUE4Parse.FileProvider.Objects;

namespace TinyUnrealPackerExtended.Services
{
    public class Cue4ParseService : ICue4ParseService
    {
        private readonly EGame _gameVersion = EGame.GAME_UE4_LATEST;

        private async Task<string> ParseAsync<T>(
     string filePath,
     string rootDir,
     CancellationToken cancellationToken,
      Func<DefaultFileProvider, GameFile, T> extractor
 )
        {
            using var provider = ProviderFactory.Create(
                rootDir,
                SearchOption.AllDirectories,
                _gameVersion
            );
            provider.Initialize();

            var fileName = Path.GetFileName(filePath);
            var entry = provider.Files.Values
                .FirstOrDefault(e => e.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException($"Unable to find {fileName}", filePath);

            var data = extractor(provider, entry);

            return await Task.Run(
                () => JsonConvert.SerializeObject(data, Formatting.Indented),
                cancellationToken
            );
        }

        public Task<string> ParseAssetAsync(
            string uassetPath,
            string rootDir,
            CancellationToken cancellationToken = default
        ) => ParseAsync(
            uassetPath,
            rootDir,
            cancellationToken,
            (provider, entry) =>
            {
                var loadResult = provider.GetLoadPackageResult(entry);
                return loadResult.GetDisplayData(save: false);
            }
        );

        public Task<string> ParseLocresAsync(
            string locresPath,
            string rootDir,
            CancellationToken cancellationToken = default
        ) => ParseAsync(
            locresPath,
            rootDir,
            cancellationToken,
            (provider, entry) =>
            {
                if (!entry.TryCreateReader(out var archive))
                    throw new InvalidOperationException($"Unable to create reader for {entry.Path}");
                return new FTextLocalizationResource(archive);
            }
        );
    }
}
