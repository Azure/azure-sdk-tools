using System.Collections.Generic;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineCodeownerExtractor
{
    public static class Extensions
    {
        public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> tasks)
        {
            return Task.WhenAll(tasks);
        }

        public static Task WhenAll(this IEnumerable<Task> tasks)
        {
            return Task.WhenAll(tasks);
        }


        public static async Task LimitConcurrencyAsync(this IEnumerable<Task> tasks, int concurrencyLimit = 1)
        {
            if (concurrencyLimit == int.MaxValue)
            {
                await Task.WhenAll(tasks);
                return;
            }

            if (concurrencyLimit == 1)
            {
                foreach (var task in tasks)
                {
                    await task;
                }

                return;
            }

            var pending = new List<Task>();

            foreach (var task in tasks)
            {
                pending.Add(task);

                if (pending.Count < concurrencyLimit)
                {
                    continue;
                }

                var completed = await Task.WhenAny(pending);

                pending.Remove(completed);
            }

            await Task.WhenAll(pending);
        }

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
