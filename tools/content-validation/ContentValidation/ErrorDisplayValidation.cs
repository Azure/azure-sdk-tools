using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace UtilityLibraries;

public class ErrorDisplayValidation : IValidation
{
    private IPlaywright _playwright;

    public ErrorDisplayValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    public async Task<TResult> Validate(string testLink)
    {
        //Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var errorList = new List<string>();

        var htmlContent = await page.ContentAsync();

        // Define the regex pattern to match the Markdown table format
        string pattern = @"^\|[- ]+\|[- ]+\|$";

        // Use Regex.Matches to find all matches in the HTML content
        var matches = Regex.Matches(htmlContent, pattern, RegexOptions.Multiline);

        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    errorList.Add(match.Value);
                }
            }

            var formattedList = errorList
                .GroupBy(item => item)
                .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times ,  {group.Key}")
                .ToList();

            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = errorList.Count;
            res.ErrorInfo = "There are errors displayed here: markdown table format found";
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }
}



