using System;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    public static class BusinessDaysUtils
    {
        /// <summary>
        /// Calculate the number of business days between two dates, excluding weekends.
        /// This method counts Monday through Friday as business days.
        /// </summary>
        /// <param name="startDate">The starting date</param>
        /// <param name="endDate">The ending date</param>
        /// <returns>The number of business days between the two dates</returns>
        public static int CalculateBusinessDaysBetween(DateTime startDate, DateTime endDate)
        {
            if (startDate >= endDate)
                return 0;

            var businessDays = 0;
            var currentDate = startDate.Date;
            var targetDate = endDate.Date;

            while (currentDate < targetDate)
            {
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                currentDate = currentDate.AddDays(1);
            }

            return businessDays;
        }

        /// <summary>
        /// Calculate a date that is a specified number of business days ago from the current UTC time.
        /// This method excludes weekends (Saturday and Sunday).
        /// </summary>
        /// <param name="businessDays">Number of business days to go back</param>
        /// <returns>The calculated date that is the specified number of business days ago</returns>
        public static DateTime CalculateBusinessDaysAgo(int businessDays)
        {
            if (businessDays <= 0)
                return DateTime.UtcNow;

            var currentDate = DateTime.UtcNow.Date;
            var daysToSubtract = 0;
            var businessDaysFound = 0;

            while (businessDaysFound < businessDays)
            {
                daysToSubtract++;
                var testDate = currentDate.AddDays(-daysToSubtract);
                
                if (testDate.DayOfWeek != DayOfWeek.Saturday && testDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDaysFound++;
                }
            }

            return currentDate.AddDays(-daysToSubtract);
        }

        /// <summary>
        /// Check if the specified number of business days have elapsed since the given date.
        /// This method excludes weekends (Saturday and Sunday).
        /// </summary>
        /// <param name="sinceDate">The date to check from</param>
        /// <param name="businessDays">Number of business days to check</param>
        /// <returns>True if the specified number of business days have elapsed, false otherwise</returns>
        public static bool HasBusinessDaysElapsed(DateTime sinceDate, int businessDays)
        {
            var businessDaysElapsed = CalculateBusinessDaysBetween(sinceDate, DateTime.UtcNow);
            return businessDaysElapsed >= businessDays;
        }
    }
}
