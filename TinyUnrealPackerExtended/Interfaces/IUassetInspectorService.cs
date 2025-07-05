using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IUassetInspectorService
    {
        Task<(string pixelFormat, string mappedCsv)> InspectAsync(string uassetPath, CancellationToken ct);
    }
}
