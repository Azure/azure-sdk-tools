// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Actions.Core.Markdown;
using Actions.Core.Services;

/// <summary>
/// This class contains methods to run tasks and handle exceptions.
/// </summary>
public static class App
{
    /// <summary>
    /// Runs a list of tasks, catching and handling exceptions by logging them to the action's output and summary.
    /// </summary>
    /// <remarks>Upon completion, the persistent summary is written.</remarks>
    /// <param name="tasks">The list of tasks to run, waiting for all tasks to complete.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <returns>A boolean indicating whether all tasks were completed successfully.</returns>
    public async static Task<bool> RunTasks(List<Task> tasks, ICoreService action)
    {
        var allTasks = Task.WhenAll(tasks);
        var success = await RunTasks(allTasks, action);

        return success;
    }

    /// <summary>
    /// Runs a list of tasks, catching and handling exceptions by logging them to the action's output and summary.
    /// </summary>
    /// <typeparam name="TResult">The Task result type.</typeparam>
    /// <param name="tasks">The list of tasks to run, waiting for all tasks to complete.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <returns>A tuple containing the results of the tasks and a boolean indicating whether all tasks were completed successfully.</returns>
    public async static Task<(TResult[], bool)> RunTasks<TResult>(List<Task<TResult>> tasks, ICoreService action)
    {
        var allTasks = Task.WhenAll(tasks);
        var success = await RunTasks(allTasks, action);

        return (allTasks.Result, success);
    }

    /// <summary>
    /// Runs a single task, catching and handling exceptions by logging them to the action's output and summary.
    /// </summary>
    /// <param name="task">The task to run, waiting for it to complete.</param>
    /// <param name="action">The GitHub action service.</param>
    /// <returns>A boolean indicating whether the task was completed successfully.</returns>
    private async static Task<bool> RunTasks(Task task, ICoreService action)
    {
        var success = false;

        try
        {
            task.Wait();
            success = true;
        }
        catch (AggregateException ex)
        {
            action.WriteError($"Exception occurred: {ex.Message}");

            action.Summary.AddPersistent(summary =>
            {
                summary.AddAlert("Exception occurred", AlertType.Caution);
                summary.AddNewLine();
                summary.AddNewLine();
                summary.AddMarkdownCodeBlock(ex.Message);
            });
        }

        await action.Summary.WritePersistentAsync();
        return success;
    }
}
