using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace UtilityLibraries;

public class ErrorDisplayValidation : IValidation
{
    private IBrowser _browser;

    public ErrorDisplayValidation(IBrowser browser)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
    }

    public async Task<TResult> Validate(string testLink)
    {
        //Create a new page from the shared browser instance.
        var page = await _browser.NewPageAsync();
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

        await page.CloseAsync();

        return res;
    }
}



