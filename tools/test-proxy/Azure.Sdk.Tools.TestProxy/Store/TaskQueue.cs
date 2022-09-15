using System.Threading.Tasks;
using System.Threading;
using System;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to control access to a directory. Within the GitProcessHandler, a queue is used per targeted git directory. This ensures
    /// that multiple Async requests hitting the asset store functionality will NEVER be able to stomp on each other.
    /// </summary>
    public class TaskQueue
    {
        private SemaphoreSlim semaphore;

        public TaskQueue()
        {
            semaphore = new SemaphoreSlim(1);
        }

        /// <summary>
        /// Used to await the running of a single block of code. Returns a value of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="incomingTask"></param>
        /// <returns></returns>
        public async Task<T> EnqueueAsync<T>(Func<Task<T>> incomingTask)
        {
            await semaphore.WaitAsync();
            try
            {
                return await incomingTask();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Used to await the running of a single block of code. No incoming arguments, no return types.
        /// </summary>
        /// <param name="incomingTask"></param>
        /// <returns></returns>
        public async Task EnqueueAsync(Func<Task> incomingTask)
        {
            await semaphore.WaitAsync();

            try
            {
                await incomingTask();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Used to invoke a block of code. No incoming arguments, no output arguments.
        /// </summary>
        /// <param name="incomingTask"></param>
        public void Enqueue(Action incomingTask)
        {
            semaphore.Wait();

            try
            {
                incomingTask();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
