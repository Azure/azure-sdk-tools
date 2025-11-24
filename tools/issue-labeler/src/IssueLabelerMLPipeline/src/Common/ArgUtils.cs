// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Actions.Core.Services;

public class ArgUtils
{
    private ICoreService action;
    private Action<string?> showUsage;
    private Queue<string>? arguments { get; }

    /// <summary>
    /// Create an arguments utility class instance for a GitHub action, with input values retrieved from the GitHub action.
    /// </summary>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="showUsage">A method to show usage information for the application.</param>
    public ArgUtils(ICoreService action, Action<string?, ICoreService> showUsage)
    {
        this.action = action;
        this.showUsage = message => showUsage(message, action);
    }

    /// <summary>
    /// Create an arguments utility class instance for a GitHub action, with input values retrieved from a queue of command-line arguments.
    /// </summary>
    /// <param name="action">The GitHub action service.</param>
    /// <param name="showUsage">A method to show usage information for the application.</param>
    /// <param name="arguments">The queue of command-line arguments to extract argument values from.</param>
    public ArgUtils(ICoreService action, Action<string?, ICoreService> showUsage, Queue<string> arguments) : this(action, showUsage)
    {
        this.arguments = arguments;
    }

    /// <summary>
    /// Gets the input string for the specified input.
    /// </summary>
    /// <remarks>
    /// When running as a GitHub action, this method will retrieve the input value from the action's inputs.
    /// </remarks>
    /// <remarks>
    /// When using the constructor with a queue of command-line arguments, this method will dequeue the next argument from the queue.
    /// </remarks>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <returns>A nullable string containing the input value if retrieved, or <c>null</c> if there is no value specified.</returns>
    private string? GetInputString(string inputName)
    {
        string? input = null;
        
        if (arguments is not null)
        { 
            if (arguments.TryDequeue(out string? argValue))
            {
                input = argValue;
            }
        }
        else
        {
            input = action.GetInput(inputName);
        }
        
        return string.IsNullOrWhiteSpace(input) ? null : input;
    }

    /// <summary>
    /// Try to get a string input value, guarding against null values.
    /// </summary>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="value">The output string value if retrieved, or <c>null</c> if there is no value specified or it was empty.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetString(string inputName, [NotNullWhen(true)] out string? value)
    {
        value = GetInputString(inputName);
        return value is not null;
    }

    /// <summary>
    /// Determine if the specified flag is provided and set to <c>true</c>.
    /// </summary>
    /// <param name="inputName">The name of the flag to retrieve.</param>
    /// <param name="value"><c>true</c> if the flag is provided and set to <c>true</c>, <c>false</c> otherwise.</param>
    /// <returns>A boolean indicating if the flag was checked successfully, only returning <c>false</c> if specified as an invalid value.</returns>
    public bool TryGetFlag(string inputName, [NotNullWhen(true)] out bool? value)
    {
        string? input = GetInputString(inputName);

        if (input is null)
        {
            value = false;
            return true;
        }

        if (!bool.TryParse(input, out bool parsedValue))
        {
            showUsage($"Input '{inputName}' must be 'true', 'false', 'TRUE', or 'FALSE'.");
            value = null;
            return false;
        }

        value = parsedValue;
        return true;
    }

    /// <summary>
    /// Try to get the GitHub repository name from the input or environment variable.
    /// </summary>
    /// <remarks>
    /// Defaults to the GITHUB_REPOSITORY environment variable if the input is not specified.
    /// </remarks>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="org">The GitHub organization name, extracted from the specified {org}/{repo} value.</param>
    /// <param name="repo">The GitHub repository name, extracted from the specified {org}/{repo} value.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetRepo(string inputName, [NotNullWhen(true)] out string? org, [NotNullWhen(true)] out string? repo)
    {
        string? orgRepo = GetInputString(inputName) ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");

        if (orgRepo is null || !orgRepo.Contains('/'))
        {
            showUsage($$"""Input '{{inputName}}' has an empty value or is not in the format of '{org}/{repo}'. Value defaults to GITHUB_REPOSITORY environment variable if not specified.""");
            org = null;
            repo = null;
            return false;
        }

        string[] parts = orgRepo.Split('/');
        org = parts[0];
        repo = parts[1];
        return true;
    }

    /// <summary>
    /// Try to get the GitHub repository list from the input or environment variable.
    /// </summary>
    /// <remarks>
    /// Defaults to the GITHUB_REPOSITORY environment variable if the input is not specified.
    /// </remarks>
    /// <remarks>
    /// All repositories must be from the same organization.
    /// </remarks>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="org">The GitHub organization name, extracted from the specified {org}/{repo} value.</param>
    /// <param name="repos">The list of GitHub repository names, extracted from the specified {org}/{repo} value.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetRepoList(string inputName, [NotNullWhen(true)] out string? org, [NotNullWhen(true)] out List<string>? repos)
    {
        string? orgRepos = GetInputString(inputName) ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        org = null;
        repos = null;

        if (orgRepos is null)
        {
            showUsage($$"""Input '{{inputName}}' has an empty value or is not in the format of '{org}/{repo}': {{orgRepos}}""");
            return false;
        }

        foreach (var orgRepo in orgRepos.Split(',').Select(r => r.Trim()))
        {
            if (!orgRepo.Contains('/'))
            {
                showUsage($$"""Input '{{inputName}}' contains a value that is not in the format of '{org}/{repo}': {{orgRepo}}""");
                return false;
            }

            string[] parts = orgRepo.Split('/');

            if (org is not null && org != parts[0])
            {
                showUsage($"All '{inputName}' values must be from the same org.");
                return false;
            }

            org ??= parts[0];
            repos ??= [];
            repos.Add(parts[1]);
        }

        return (org is not null && repos is not null);
    }

    /// <summary>
    /// Try to get a file path from the input.
    /// </summary>
    /// <remarks>
    /// The file path is converted to an absolute path if it is not already absolute.
    /// </remarks>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="path">The output file path if retrieved, or <c>null</c> if there is no value specified.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetPath(string inputName, out string? path)
    {
        path = GetInputString(inputName);

        if (path is null)
        {
            return false;
        }

        if (!Path.IsPathRooted(path))
        {
            path = Path.GetFullPath(path);
        }

        return true;
    }

    /// <summary>
    /// Try to get a string array from the input.
    /// </summary>
    /// <remarks>
    /// The string array is split by commas and trimmed of whitespace.
    /// </remarks>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="values">The output string array if retrieved, or <c>null</c> if there is no value specified.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetStringArray(string inputName, [NotNullWhen(true)] out string[]? values)
    {
        string? input = GetInputString(inputName);

        if (input is null)
        {
            values = null;
            return false;
        }

        values = input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return true;
    }

    /// <summary>
    /// Try to get an integer from the input.
    /// </summary>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="value">The output integer value if retrieved, or <c>null</c> if there is no value specified.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetInt(string inputName, [NotNullWhen(true)] out int? value) =>
        TryParseInt(inputName, GetInputString(inputName), out value);

    /// <summary>
    /// Try to parse an integer from the input string.
    /// </summary>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="input">The input string to parse.</param>
    /// <param name="value">The output integer value if parsed successfully, or <c>null</c> if the input is invalid.</param>
    /// <returns><c>true</c> if the input value was parsed successfully, <c>false</c> otherwise.</returns>
    private bool TryParseInt(string inputName, string? input, [NotNullWhen(true)] out int? value)
    {
        if (input is null || !int.TryParse(input, out int parsedValue))
        {
            showUsage($"Input '{inputName}' must be an integer.");
            value = null;
            return false;
        }

        value = parsedValue;
        return true;
    }

    /// <summary>
    /// Try to get an integer array from the input.
    /// </summary>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="values">The output integer array if retrieved, or <c>null</c> if there is no value specified.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetIntArray(string inputName, [NotNullWhen(true)] out int[]? values)
    {
        string? input = GetInputString(inputName);

        if (input is not null)
        {
            string[] inputValues = input.Split(',');

            int[] parsedValues = inputValues.SelectMany(v => {
                if (!TryParseInt(inputName, v, out int? value))
                {
                    return new int[0];
                }

                return [value.Value];
            }).ToArray();

            if (parsedValues.Length == inputValues.Length)
            {
                values = parsedValues;
                return true;
            }
        }

        values = null;
        return false;
    }

    /// <summary>
    /// Try to get a float from the input.
    /// </summary>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="value">The output float value if retrieved, or <c>null</c> if there is no value specified.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetFloat(string inputName, [NotNullWhen(true)] out float? value)
    {
        string? input = GetInputString(inputName);

        if (input is null || !float.TryParse(input, out float parsedValue))
        {
            showUsage($"Input '{inputName}' must be a decimal value.");
            value = null;
            return false;
        }

        value = parsedValue;
        return true;
    }

    /// <summary>
    /// Try to get a list of number ranges from the input.
    /// </summary>
    /// <remarks>
    /// The input is a comma-separated list of numbers and/or dash-separated ranges.
    /// </remarks>
    /// <param name="inputName">The name of the input to retrieve.</param>
    /// <param name="values">The output list of ulong values if retrieved, or <c>null</c> if there is no value specified.</param>
    /// <returns><c>true</c> if the input value was retrieved successfully, <c>false</c> otherwise.</returns>
    public bool TryGetNumberRanges(string inputName, [NotNullWhen(true)] out List<ulong>? values)
    {
        string? input = GetInputString(inputName);

        if (input is not null)
        {
            var showUsageError = () => showUsage($"Input '{inputName}' must be comma-separated list of numbers and/or dash-separated ranges. Example: 1-3,5,7-9.");
            List<ulong> numbers = [];

            foreach (var range in input.Split(','))
            {
                var beginEnd = range.Split('-');

                if (beginEnd.Length == 1)
                {
                    if (!ulong.TryParse(beginEnd[0], out ulong number))
                    {
                        showUsageError();
                        values = null;
                        return false;
                    }

                    numbers.Add(number);
                }
                else if (beginEnd.Length == 2)
                {
                    if (!ulong.TryParse(beginEnd[0], out ulong begin))
                    {
                        showUsageError();
                        values = null;
                        return false;
                    }

                    if (!ulong.TryParse(beginEnd[1], out ulong end))
                    {
                        showUsageError();
                        values = null;
                        return false;
                    }

                    for (var number = begin; number <= end; number++)
                    {
                        numbers.Add(number);
                    }
                }
                else
                {
                    showUsageError();
                    values = null;
                    return false;
                }
            }

            values = numbers;
            return true;
        }

        values = null;
        return false;
    }
}
