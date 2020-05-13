using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Records
{
    public interface IRecordStore
    {
        Task<T> GetRecordAsync<T>(Uri recordUri) where T : Record, new();
        Task<T> PutRecordAsync<T>(string json) where T: Record, new();
    }
}
