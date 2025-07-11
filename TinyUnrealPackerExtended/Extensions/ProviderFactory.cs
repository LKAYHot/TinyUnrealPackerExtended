using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Extensions
{
    public static class ProviderFactory
    {
        public static DefaultFileProvider Create(string rootDir, SearchOption searchOption, EGame gameVersion)
        {
            var provider = new DefaultFileProvider(
                rootDir,
                searchOption,
                new VersionContainer(gameVersion),
                StringComparer.OrdinalIgnoreCase
            );
            provider.Initialize();
            return provider;
        }
    }
}
