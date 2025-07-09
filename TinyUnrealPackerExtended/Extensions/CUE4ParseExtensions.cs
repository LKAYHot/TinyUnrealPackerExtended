using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CUE4Parse.UE4.Assets;

namespace TinyUnrealPackerExtended.Extensions
{
    public static class CUE4ParseExtensions
    {
        public class LoadPackageResult
        {
            private const int PaginationThreshold = 5000;
            private const int MaxExportPerPage = 1;

            public IPackage Package;
            public int RequestedIndex;

            public bool IsPaginated => Package.ExportMapLength >= PaginationThreshold;

           
            public int InclusiveStart => Math.Max(0, RequestedIndex - RequestedIndex % MaxExportPerPage);

           
            public int ExclusiveEnd => IsPaginated
                ? Math.Min(InclusiveStart + MaxExportPerPage, Package.ExportMapLength)
                : Package.ExportMapLength;

            public int PageSize => ExclusiveEnd - InclusiveStart;

            public string TabTitleExtra => IsPaginated
                ? $"Exports {InclusiveStart}-{ExclusiveEnd - 1} of {Package.ExportMapLength - 1}"
                : null;

           
            public object GetDisplayData(bool save = false)
                => !save && IsPaginated
                    ? Package.GetExports(InclusiveStart, PageSize)
                    : Package.GetExports();
        }

        
        public static LoadPackageResult GetLoadPackageResult(this IFileProvider provider, GameFile file, string objectName = null)
        {
            var result = new LoadPackageResult { Package = provider.LoadPackage(file) };

            if (result.IsPaginated)
            {
                result.RequestedIndex = provider.LoadPackage(file).GetExportIndex(file.NameWithoutExtension);
                if (!string.IsNullOrEmpty(objectName))
                {
                    if (int.TryParse(objectName, out var idx))
                        result.RequestedIndex = idx;
                    else
                        result.RequestedIndex = provider.LoadPackage(file).GetExportIndex(objectName);
                }
            }

            return result;
        }
    }
}
