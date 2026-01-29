// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/// <summary>
/// This class contains methods to run tasks and handle exceptions.
/// </summary>
public static class App
{
    /// <summary>
    /// Runs a list of tasks, catching and handling exceptions by logging them to the console.
    /// </summary>
    /// <param name="tasks">The list of tasks to run, waiting for all tasks to complete.</param>
    /// <returns>A boolean indicating whether all tasks were completed successfully.</returns>
    public async static Task<bool> RunTasks(List<Task> tasks)
    {
        var allTasks = Task.WhenAll(tasks);
        var success = await RunTasks(allTasks);

        return success;
    }

    /// <summary>
    /// Runs a list of tasks, catching and handling exceptions by logging them to the console.
    /// </summary>
    /// <typeparam name="TResult">The Task result type.</typeparam>
    /// <param name="tasks">The list of tasks to run, waiting for all tasks to complete.</param>
    /// <returns>A tuple containing the results of the tasks and a boolean indicating whether all tasks were completed successfully.</returns>
    public async static Task<(TResult[], bool)> RunTasks<TResult>(List<Task<TResult>> tasks)
    {
        var allTasks = Task.WhenAll(tasks);
        var success = await RunTasks(allTasks);

        return (allTasks.Result, success);
    }

    /// <summary>
    /// Runs a single task, catching and handling exceptions by logging them to the console.
    /// </summary>
    /// <param name="task">The task to run, waiting for it to complete.</param>
    /// <returns>A boolean indicating whether the task was completed successfully.</returns>
    private async static Task<bool> RunTasks(Task task)
    {
        var success = false;

        try
        {
            await task;
            success = true;
        }
        catch (AggregateException ex)
        {
            Console.WriteLine($"ERROR: Exception occurred: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return success;
    }
}
