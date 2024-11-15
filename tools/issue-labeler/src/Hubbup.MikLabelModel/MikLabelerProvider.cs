// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Hubbup.MikLabelModel
{
    public class MikLabelerProvider
    {
        private readonly ConcurrentDictionary<(string, string), MikLabelerModel> _mikLabelers = new ConcurrentDictionary<(string, string), MikLabelerModel>();
        private readonly ILogger<MikLabelerProvider> _logger;

        public MikLabelerProvider(ILogger<MikLabelerProvider> logger)
        {
            _logger = logger;
        }

        public MikLabelerModel GetMikLabeler(IMikLabelerPathProvider pathProvider)
        {
            var paths = pathProvider.GetModelPath();
            return _mikLabelers.GetOrAdd(
                paths,
                p =>
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var model = new MikLabelerModel(p);
                    stopwatch.Stop();
                    _logger.LogInformation("Creating new MikLabelerModel for paths {PATH} and {PR_PATH} in {TIME}ms", p.Item1, p.Item2, stopwatch.ElapsedMilliseconds);
                    return model;
                });
        }
    }
}
