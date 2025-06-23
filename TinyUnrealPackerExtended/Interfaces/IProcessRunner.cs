using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface IProcessRunner
    {
        Task<int> RunAsync(
            string exePath,
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default);
    }
}
