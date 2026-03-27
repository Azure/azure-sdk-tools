// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.SLA;

/// <summary>
/// Calculates business days (weekdays only, excluding Saturday and Sunday).
/// Holidays are not excluded in this initial implementation.
/// </summary>
public static class BusinessDayCalculator
{
    /// <summary>
    /// Counts the number of business days between two dates (exclusive of end date).
    /// </summary>
    public static int CountBusinessDays(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            return 0;
        }

        var current = start.Date;
        var endDate = end.Date;
        int count = 0;

        while (current < endDate)
        {
            if (current.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                count++;
            }
            current = current.AddDays(1);
        }

        return count;
    }

    /// <summary>
    /// Adds business days to a date and returns the resulting date.
    /// </summary>
    public static DateTimeOffset AddBusinessDays(DateTimeOffset start, int businessDays)
    {
        var current = start;
        int added = 0;

        while (added < businessDays)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                added++;
            }
        }

        return current;
    }
}
