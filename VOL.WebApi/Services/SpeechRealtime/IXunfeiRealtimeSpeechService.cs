using System;
using System.Threading;
using System.Threading.Tasks;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public interface IXunfeiRealtimeSpeechService
    {
        Task<IRealtimeSpeechSession> CreateSessionAsync(
            Func<string, Task> onResult,
            Func<string, Task> onLog,
            CancellationToken cancellationToken);
    }
}
