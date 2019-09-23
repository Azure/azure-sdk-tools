using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public abstract class Handler<T>
    {
        public abstract Task HandleAsync(T payload, CancellationToken cancellationToken);
    }
}
