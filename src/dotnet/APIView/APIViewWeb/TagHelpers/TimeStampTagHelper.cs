using Microsoft.AspNetCore.Razor.TagHelpers;
using System;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement(Attributes = "date")]
    public class TimeStampTagHelper : TagHelper
    {
        public DateTime Date { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.Content.SetContent(GetDateLabel());
        }

        private string GetDateLabel()
        {
            var timeDifference = DateTime.Now.Subtract(Date);
            int secondsAgo = (int)timeDifference.TotalSeconds;
            int minutesAgo = (int)timeDifference.TotalMinutes;
            int hoursAgo = (int)(minutesAgo / 60);
            int daysAgo = (int)timeDifference.TotalDays;
            int weeksAgo = (int)(daysAgo / 7);
            string relativeDate;
            if (minutesAgo == 0)
            {
                if (secondsAgo > 0)
                {
                    relativeDate = GetLabel(secondsAgo, "second");
                }
                else
                {
                    relativeDate = "just now";
                }
            }
            else if (hoursAgo == 0)
            {
                relativeDate = GetLabel(minutesAgo, "minute");
            }
            else if (daysAgo == 0)
            {
                relativeDate = GetLabel(hoursAgo, "hour");
            }
            else if (weeksAgo == 0)
            {
                relativeDate = GetLabel(daysAgo, "day");
            }
            else
            {
                relativeDate = GetLabel(weeksAgo, "week");
            }
            return relativeDate;

            static string GetLabel(int amount, string unit) =>
            $"{amount} {unit}{(amount > 1 ? "s" : "")} ago";
        }
    }
}
