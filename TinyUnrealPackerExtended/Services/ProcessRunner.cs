using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Services
{
    public class ProcessRunner : IProcessRunner
    {
        public async Task<int> RunAsync(
            string exePath,
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.Start();
            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode;
        }
    }
}
