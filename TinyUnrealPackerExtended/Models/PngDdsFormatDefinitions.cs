using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Models
{
    public static class PngDdsFormatDefinitions
    {
        /// <summary>
        /// All supported DDS output formats.
        /// </summary>
        public static readonly IReadOnlyList<string> AvailableFormats = new List<string>
        {
            // BC1 / DXT1
            "BC1_UNORM",
            "BC1_UNORM_SRGB",

            // BC2 / DXT3
            "BC2_UNORM",
            "BC2_UNORM_SRGB",

            // BC3 / DXT5
            "BC3_UNORM",
            "BC3_UNORM_SRGB",

            // BC4 (single channel)
            "BC4_UNORM",
            "BC4_SNORM",

            // BC5 (two channels)
            "BC5_UNORM",
            "BC5_SNORM",

            // HDR: BC6H
            "BC6H_UF16",
            "BC6H_SF16",

            // Quality: BC7
            "BC7_UNORM",
            "BC7_UNORM_SRGB",

            // Uncompressed
            "R8G8B8A8_UNORM",
            "R8G8B8A8_UNORM_SRGB",

            // HDR
            "R16_FLOAT",
            "R16G16_FLOAT",
            "R16G16B16A16_FLOAT",

            // Full
            "R32_FLOAT",
            "R32G32_FLOAT",
            "R32G32B32A32_FLOAT"
        };

        public static readonly IReadOnlyDictionary<string, string[]> FormatMappings = new Dictionary<string, string[]>
        {
            { "PF_DXT1", new[] { "BC1_UNORM", "BC1_UNORM_SRGB" } },
            { "PF_DXT3", new[] { "BC2_UNORM", "BC2_UNORM_SRGB" } },
            { "PF_DXT5", new[] { "BC3_UNORM", "BC3_UNORM_SRGB" } },
            { "PF_BC4",  new[] { "BC4_UNORM", "BC4_SNORM" } },
            { "PF_BC5",  new[] { "BC5_UNORM", "BC5_SNORM" } },
            { "PF_BC6H", new[] { "BC6H_UF16", "BC6H_SF16" } },
            { "PF_BC7",  new[] { "BC7_UNORM", "BC7_UNORM_SRGB" } },
            { "PF_B8G8R8A8", new[] { "R8G8B8A8_UNORM", "R8G8B8A8_UNORM_SRGB" } },
            { "PF_R16F", new[] { "R16_FLOAT" } },
            { "PF_R16G16F", new[] { "R16G16_FLOAT" } },
            { "PF_R16G16B16A16F", new[] { "R16G16B16A16_FLOAT" } },
            { "PF_R32F", new[] { "R32_FLOAT" } },
            { "PF_R32G32F", new[] { "R32G32_FLOAT" } },
            { "PF_R32G32B32A32F", new[] { "R32G32B32A32_FLOAT" } }
        };

        /// <summary>
        /// Gets the CSV representation of mapped formats or null if none.
        /// </summary>
        public static string? GetMappedCsv(string pixelFormat)
        {
            if (FormatMappings.TryGetValue(pixelFormat, out var arr) && arr.Length > 0)
                return string.Join(", ", arr);
            return null;
        }

        /// <summary>
        /// Gets the allowed DDS formats for a given Unreal pixel format.
        /// </summary>
        public static IReadOnlyList<string> GetAllowedFormats(string pixelFormat)
        {
            return FormatMappings.TryGetValue(pixelFormat, out var arr)
                ? arr
                : new string[0];
        }
    }
}
