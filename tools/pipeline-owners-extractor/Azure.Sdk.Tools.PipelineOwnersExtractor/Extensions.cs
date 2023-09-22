using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor
{
    public static class Extensions
    {
        public static async Task<T[]> LimitConcurrencyAsync<T>(this IEnumerable<Task<T>> tasks, int concurrencyLimit = 1)
        {
            if (concurrencyLimit == int.MaxValue) 
            { 
                return await Task.WhenAll(tasks);
            }

            var results = new List<T>();

            if (concurrencyLimit == 1)
            {
                foreach (var task in tasks)
                {
                    results.Add(await task);
                }

                return results.ToArray();
            }

            var pending = new List<Task<T>>();

            foreach (var task in tasks)
            {
                pending.Add(task);

                if (pending.Count < concurrencyLimit)
                {
                    continue; 
                }

                var completed = await Task.WhenAny(pending);
                pending.Remove(completed);
                results.Add(await completed);
            }

            results.AddRange(await Task.WhenAll(pending));

            return results.ToArray();
        }
    }
}
