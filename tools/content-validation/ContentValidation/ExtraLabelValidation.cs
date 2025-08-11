using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace UtilityLibraries;

public class ExtraLabelValidation : IValidation
{
    private IPlaywright _playwright;

    public ExtraLabelValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }
    public async Task<TResult> Validate(string testLink)
    {
        var res = new TResult();
        var errorList = new List<string>();

        var ignoreList = IgnoreData.GetIgnoreList("ExtraLabelValidation", "contains");

        // Define a list (labelList) containing various HTML tags and entities.
        var labelList = new List<string> {
            "/p>",
            "<br",
            "<span",
            "<div",
            "<table",
            "<img",
            "<code",
            "<xref",
            "&amp;",
            "&lt",
            "&gt",
            "&quot",
            "&apos",
        };

        // Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        // Get all text content of the current html.
        var htmlText = await page.Locator("html").InnerTextAsync();

        // Count the number of all errors on the current page.
        int sum = 0;
        string errorInfo = "Extra label found: ";

        // Iterate through labelList and check if the page content contains any of the tags. If any tags are found, add them to the errorList.
        foreach (var label in labelList)
        {
            int index = 0;
            // Count the number of all errors for the current label.
            int count = 0;
            while ((index = htmlText.IndexOf(label, index)) != -1)
            {
                // Check if the label is followed by a self-closing tag pattern
                string selfClosingPattern = $@"{label}\s(.*)?\/>";
                if (Regex.IsMatch(htmlText.Substring(index), selfClosingPattern))
                {
                    // This is a self-closing tag, skip it
                    index += label.Length + 2;
                    continue;
                }

                if (ignoreList.Any(ignore => htmlText.Contains(ignore.IgnoreText)))
                {
                    // If the label is in the ignore list, skip it
                    continue;
                }

                count++;
                sum++;
                index += label.Length;
            }

            if (count > 0)
            {
                errorInfo += label;
                errorList.Add($"{errorList.Count + 1}. Appears {count} times , label : {label} ");
            }
        }

        if (sum > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.ErrorInfo = errorInfo;
            res.NumberOfOccurrences = sum;
            res.LocationsOfErrors = errorList;
        }

        await browser.CloseAsync();

        return res;
    }
}