using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Records
{
    public interface IRecordHandler<T> where T: Record
    {
        Task<T> GetAsync(Uri recordUri);
        Task<T> PutAsync(string json);
    }
}
