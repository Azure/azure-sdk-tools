
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.NotificationConfiguration.Helpers
{
    public class CacheHelper<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> resultCache = new ConcurrentDictionary<TKey, TValue>(); 
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> keyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();


        public CacheHelper()
        {
            
        }

        public async Task<TValue> GetValueByKey(TKey key, Func<TKey, Task<TValue>> f)
        {
            TValue output;
            if (!resultCache.TryGetValue(key, out output))
            {
                output = await f(key);
                resultCache.TryAdd(key, output);
            }
            return output;
        }
    }
}
