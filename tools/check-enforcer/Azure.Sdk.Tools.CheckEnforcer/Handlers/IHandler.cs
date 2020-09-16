using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public interface IHandler
    {
        string EventName { get; }
        Task HandleAsync(IEnumerable<string> payloads, CancellationToken cancellationToken);
    }
}
