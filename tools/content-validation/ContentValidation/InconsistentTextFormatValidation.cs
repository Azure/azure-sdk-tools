using Microsoft.Playwright;

namespace UtilityLibraries;

public class InconsistentTextFormatValidation : IValidation
{
    private IPlaywright _playwright;

    public InconsistentTextFormatValidation(IPlaywright playwright)
    {
        _playwright = playwright;
    }

    public async Task<TResult> Validate(string testLink)
    {
        //Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var errorList = new List<string>();
        var errorLocation = new List<string>();

        var hTagsInTd = await page.QuerySelectorAllAsync("td h1, td h2, td h3, td h4, td h5, td h6");

        if (hTagsInTd.Count > 0)
        {
            foreach (var element in hTagsInTd)
            {
                var text = await element.InnerTextAsync();

                string headerId = null;
                try
                {
                    headerId = await element.GetAttributeAsync("id");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("There no 'id' attribute. " + ex.Message);
                }

                var anchorLink = string.IsNullOrEmpty(headerId) ? $"{testLink}" : $"{testLink}#{headerId}";
                errorList.Add(text);
                errorLocation.Add(anchorLink);
            }

            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = hTagsInTd.Count;
            res.ErrorInfo = "Inconsistent Text Format: " + string.Join(",", errorList);
            res.LocationsOfErrors = errorLocation;
        }

        await browser.CloseAsync();

        return res;
    }
}