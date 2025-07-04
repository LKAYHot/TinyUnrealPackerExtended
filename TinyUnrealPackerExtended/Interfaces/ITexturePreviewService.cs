using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TinyUnrealPackerExtended.Interfaces
{
    public interface ITexturePreviewService
    {
        Task<BitmapImage> ExtractAsync(string uassetPath, CancellationToken ct = default);

        Task<BitmapImage> ExtractFullResolutionAsync(string uassetPath, CancellationToken ct = default);
    }

}
