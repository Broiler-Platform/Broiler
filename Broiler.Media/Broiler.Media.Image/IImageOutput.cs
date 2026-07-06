using System.Threading;
using System.Threading.Tasks;
using Broiler.Media;

namespace Broiler.Media.Image;

public interface IImageOutput : IMediaOutput
{
    ValueTask WriteAsync(ImageFrame frame, CancellationToken cancellationToken = default);
}

