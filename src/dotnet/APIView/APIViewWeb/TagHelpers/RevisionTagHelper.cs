using Microsoft.AspNetCore.Razor.TagHelpers;
using System;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement(Attributes = "revision")]
    public class RevisionTagHelper : TagHelper
    {
        public ReviewRevisionModel Revision { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.Content.SetContent($"{GetDateLabel()}{GetUploaderLabel()}");
        }

        private string GetUploaderLabel() =>
            Revision.Uploader != null ? $" by {Revision.Uploader}" : "";

        private string GetDateLabel()
        {
            var timeDifference = DateTime.Now.Subtract(Revision.CreationDate);
            int secondsAgo = (int)timeDifference.TotalSeconds;
            int minutesAgo = (int)timeDifference.TotalMinutes;
            int hoursAgo = (int)(minutesAgo / 60);
            int daysAgo = (int)timeDifference.TotalDays;
            int weeksAgo = (int)(daysAgo / 7);
            string relativeDate = "";
            if (minutesAgo == 0)
            {
                if (secondsAgo > 0)
                {
                    relativeDate = GetLabel(secondsAgo, "second");
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
