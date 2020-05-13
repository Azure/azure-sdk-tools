using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Records
{
    public abstract class RecordHandler<T> : IRecordHandler<T> where T: Record
    {
        public Task<T> GetAsync(Uri recordUri)
        {
            throw new NotImplementedException();
        }

        public Task<T> PutAsync(string json)
        {
            throw new NotImplementedException();
        }
    }
}
