namespace Azure.Sdk.Tools.PipelineWitness
{
    using System;
    using System.IO;
    using System.Threading;

    internal class DisposableTempFile : IDisposable
    {
        private readonly Lazy<string> path = new Lazy<string>(System.IO.Path.GetTempFileName, LazyThreadSafetyMode.ExecutionAndPublication);
        
        public string Path => path.Value;

        public void Dispose()
        {
            if (path.IsValueCreated && File.Exists(path.Value))
            {
                File.Delete(path.Value);
            }
        }
    }
}
