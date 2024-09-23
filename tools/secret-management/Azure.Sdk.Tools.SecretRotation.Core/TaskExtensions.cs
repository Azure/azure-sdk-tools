namespace Azure.Sdk.Tools.SecretRotation.Core;

public static class TaskExtensions
{
    public static async Task<T[]> LimitConcurrencyAsync<T>(this IEnumerable<Task<T>> tasks, int concurrencyLimit = 1, CancellationToken cancellationToken = default)
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (pending.Count < concurrencyLimit) continue;

            var completed = await Task.WhenAny(pending);
            pending.Remove(completed);
            results.Add(await completed);
        }

        results.AddRange(await Task.WhenAll(pending));

        return results.ToArray();
    }

    public static Task<TResult[]> LimitConcurrencyAsync<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> taskFactory, int concurrencyLimit = 1, CancellationToken cancellationToken = default)
    {
        return LimitConcurrencyAsync(source.Select(taskFactory), concurrencyLimit, cancellationToken);
    }
}
