using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Records
{
    public class RecordStore : IRecordStore
    {
        public RecordStore(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        private IServiceProvider serviceProvider;

        public async Task<T> GetRecordAsync<T>(Uri recordUri) where T: Record, new()
        {
            return new T();
        }

        public async Task<T> PutRecordAsync<T>(string json) where T: Record, new()
        {
            var handler = serviceProvider.GetRequiredService<IRecordHandler<T>>();
            
            return new T();
        }
    }
}
