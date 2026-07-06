using System.Threading;
using System.Threading.Tasks;
using Broiler.Media;

namespace Broiler.Media.Audio;

public interface IAudioOutput : IMediaOutput
{
    ValueTask WriteAsync(AudioBuffer buffer, CancellationToken cancellationToken = default);
}

