using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Records
{
    public class RunStateChangedEventRecordHandler : RecordHandler<RunStateChangedEventRecord>
    {
        public RunStateChangedEventRecordHandler(IMemoryCache cache)
        {
            this.cache = cache;
        }

        private IMemoryCache cache;

        public async Task<RunStateChangedEventRecord> GetAsync(Uri recordUri)
        {
            var cache.Get(recordUri);
        }

        public async Task<RunStateChangedEventRecord> PutAsync(string json)
        {
        }
    }
}
