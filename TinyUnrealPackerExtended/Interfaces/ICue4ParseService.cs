using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface ICue4ParseService
    {
        Task<string> ParseAssetAsync(string uassetPath, string rootDir, CancellationToken cancellationToken = default);
        Task<string> ParseLocresAsync(string locresPath, string rootDir, CancellationToken cancellationToken = default);
    }

}
