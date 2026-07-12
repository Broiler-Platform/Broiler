using Broiler.Media;
using Broiler.Media.Audio;
using Broiler.Media.Video;

namespace Broiler.Playback;

/// <summary>
/// Opens media sources into <see cref="IMediaPlaybackSession"/> state machines by probing a
/// <see cref="MediaCodecCatalog"/>, and answers capability queries (<see cref="CanPlayType"/>).
/// This is the application/HTML-layer entry point; the HTML media element binding will drive it.
/// </summary>
public sealed class MediaPlayer
{
    private readonly MediaCodecCatalog _catalog;

    public MediaPlayer(MediaCodecCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Answers <c>HTMLMediaElement.canPlayType(type)</c> from the registered codecs' declared
    /// MIME types. A bare container match is "maybe"; a match with an explicit <c>codecs=</c>
    /// parameter is "probably". Unknown types are "no".
    /// </summary>
    public MediaCanPlayResult CanPlayType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return MediaCanPlayResult.No;

        string trimmed = mimeType.Trim();
        string baseMime = trimmed.Split(';')[0].Trim();
        if (baseMime.Length == 0)
            return MediaCanPlayResult.No;

        foreach (MediaCodec codec in _catalog.Codecs)
        {
            foreach (MediaFormatDescriptor format in codec.Descriptor.Formats)
            {
                if (format.MimeTypes.Contains(baseMime, StringComparer.OrdinalIgnoreCase))
                {
                    return trimmed.Contains("codecs", StringComparison.OrdinalIgnoreCase)
                        ? MediaCanPlayResult.Probably
                        : MediaCanPlayResult.Maybe;
                }
            }
        }

        return MediaCanPlayResult.No;
    }

    /// <summary>
    /// Probes the source and opens the matching audio or video playback session. The
    /// <paramref name="inputFactory"/> must produce a fresh, readable (ideally seekable) stream
    /// on each call — it is invoked separately for probing, metadata, and decoding.
    /// </summary>
    /// <exception cref="MediaException">
    /// Thrown with <see cref="MediaErrorCode.UnsupportedFormat"/> when no registered codec can
    /// play the source — the deterministic capability error the HTML layer surfaces.
    /// </exception>
    public async ValueTask<IMediaPlaybackSession> OpenAsync(
        Func<MediaInput> inputFactory,
        IMediaOutputProvider outputs,
        MediaPlaybackOpenOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputFactory);
        ArgumentNullException.ThrowIfNull(outputs);
        cancellationToken.ThrowIfCancellationRequested();
        MediaPlaybackOpenOptions opts = options ?? new MediaPlaybackOpenOptions();

        if (await SelectAsync(MediaKind.Audio, inputFactory, cancellationToken).ConfigureAwait(false) is AudioCodec audioCodec)
            return await OpenAudioAsync(audioCodec, inputFactory, outputs, opts, cancellationToken).ConfigureAwait(false);

        if (await SelectAsync(MediaKind.Video, inputFactory, cancellationToken).ConfigureAwait(false) is VideoCodec videoCodec)
            return await OpenVideoAsync(videoCodec, inputFactory, outputs, opts, cancellationToken).ConfigureAwait(false);

        throw new MediaException(new MediaError(
            MediaErrorCode.UnsupportedFormat,
            "No registered audio or video codec can play the supplied media source."));
    }

    private async ValueTask<MediaCodec?> SelectAsync(MediaKind kind, Func<MediaInput> inputFactory, CancellationToken cancellationToken)
    {
        using MediaInput input = inputFactory();
        MediaCodecMatch? match = await _catalog.SelectAsync(kind, input, cancellationToken: cancellationToken).ConfigureAwait(false);
        return match?.Codec;
    }

    private static async ValueTask<IMediaPlaybackSession> OpenAudioAsync(
        AudioCodec codec,
        Func<MediaInput> inputFactory,
        IMediaOutputProvider outputs,
        MediaPlaybackOpenOptions opts,
        CancellationToken cancellationToken)
    {
        AudioStreamInfo info;
        using (MediaInput infoInput = inputFactory())
            info = await codec.GetInfoAsync(infoInput, opts.AudioDecodeOptions, cancellationToken).ConfigureAwait(false);

        IAudioOutput output = outputs.CreateAudioOutput();

        // Decode end-to-end into the real output sink. The session then plays that buffered PCM.
        using (MediaInput decodeInput = inputFactory())
            await codec.DecodeAsync(decodeInput, output, opts.AudioDecodeOptions, cancellationToken).ConfigureAwait(false);

        TimeSpan duration = ResolveDuration(info, output);
        return new AudioPlaybackSession(info, output, duration);
    }

    private static TimeSpan ResolveDuration(AudioStreamInfo info, IAudioOutput output)
    {
        if (info.Duration is { } declared)
            return declared;
        if (info.TotalFrames is long frames && info.SampleRate > 0)
            return TimeSpan.FromSeconds((double)frames / info.SampleRate);
        return output is BufferedAudioOutput buffered ? buffered.BufferedDuration : TimeSpan.Zero;
    }

    private static async ValueTask<IMediaPlaybackSession> OpenVideoAsync(
        VideoCodec codec,
        Func<MediaInput> inputFactory,
        IMediaOutputProvider outputs,
        MediaPlaybackOpenOptions opts,
        CancellationToken cancellationToken)
    {
        IVideoOutput output = outputs.CreateVideoOutput();
        MediaInput input = inputFactory();
        IVideoSession session = await codec
            .OpenSessionAsync(input, output, opts.VideoSessionOptions, cancellationToken)
            .ConfigureAwait(false);
        return new VideoPlaybackSession(session);
    }
}
