using System;

namespace Broiler.Media.Video.MediaFoundation;

internal interface IMediaFoundationMediaEngine : IDisposable
{
    event EventHandler<MediaFoundationMediaEngineEvent>? EventReceived;

    TimeSpan Position { get; }

    void SetSource(string sourceUri);

    void Load();

    void Play();

    void Pause();

    void Seek(TimeSpan position);

    VideoStreamInfo GetStreamInfo();

    void OnTargetChanged(MediaFoundationBorrowedHwndVideoOutput target);

    void Shutdown();
}
