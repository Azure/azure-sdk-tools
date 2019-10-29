using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Locking
{
    public interface IDistributedLockProvider
    {
        DistributedLock Create(string identifier);
    }
}
