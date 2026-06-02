using System;
using System.Threading;
using System.Threading.Tasks;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public interface IRealtimeSpeechSession : IAsyncDisposable
    {
        Task SendAudioFrameAsync(byte[] pcmBytes, int length, CancellationToken cancellationToken);
        Task CompleteAsync(CancellationToken cancellationToken);
    }
}
