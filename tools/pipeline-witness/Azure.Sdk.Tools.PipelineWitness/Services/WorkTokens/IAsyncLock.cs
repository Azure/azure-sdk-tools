using System;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens
{
    public interface IAsyncLock : IAsyncDisposable
    {
        Task<bool> TryRenewAsync(CancellationToken cancellationToken);
    }
}
