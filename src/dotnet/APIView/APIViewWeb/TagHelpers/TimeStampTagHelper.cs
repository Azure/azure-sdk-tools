using Microsoft.AspNetCore.Razor.TagHelpers;
using System;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement("span", Attributes = "date")]
    public class TimeStampTagHelper : TagHelper
    {
        public DateTime Date { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var timeDifference = DateTime.Now.Subtract(Date);
            int secondsAgo = (int)timeDifference.TotalSeconds;
            int minutesAgo = (int)timeDifference.TotalMinutes;
            int hoursAgo = (int)(minutesAgo / 60);
            int daysAgo = (int)timeDifference.TotalDays;
            int weeksAgo = (int)(daysAgo / 7);

            if (minutesAgo == 0)
            {
                if (secondsAgo == 1)
                    output.Content.SetContent(secondsAgo + " second ago");
                else
                    output.Content.SetContent(secondsAgo + " seconds ago");
            }
            else if (hoursAgo == 0)
            {
                if (minutesAgo == 1)
                    output.Content.SetContent(minutesAgo + " minute ago");
                else
                    output.Content.SetContent(minutesAgo + " minutes ago");
            }
            else if (daysAgo == 0)
            {
                if (hoursAgo == 1)
                    output.Content.SetContent(hoursAgo + " hour ago");
                else
                    output.Content.SetContent(hoursAgo + " hours ago");
            }
            else if (weeksAgo == 0)
            {
                if (daysAgo == 1)
                    output.Content.SetContent(daysAgo + " day ago");
                else
                    output.Content.SetContent(daysAgo + " days ago");
            }
            else
            {
                if (weeksAgo == 1)
                    output.Content.SetContent(weeksAgo + " week ago");
                else
                    output.Content.SetContent(weeksAgo + " weeks ago");
            }
        }
    }
}
