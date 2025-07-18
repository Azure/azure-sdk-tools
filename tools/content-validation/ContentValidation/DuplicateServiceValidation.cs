using Microsoft.Playwright;

namespace UtilityLibraries;

public class DuplicateServiceValidation : IValidation
{
    private IPlaywright _playwright;

    public DuplicateServiceValidation(IPlaywright playwright)
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
        var set = new HashSet<string>();
        var errorList = new List<string>();

        //Get all service tags in the test page.
        var aElements = await page.Locator("li.has-three-text-columns-list-items.is-unstyled a[data-linktype='relative-path']").AllAsync();

        //Check if there are duplicate services.
        foreach (var element in aElements)
        {
            var text = await element.InnerTextAsync();

            //Store the names in the `HashSet`.
            //When `HashSet` returns false, duplicate service names are stored in another array.
            if (!set.Add(text))
            {
                errorList.Add(text);

                res.Result = false;
                res.ErrorLink = testLink;
                res.NumberOfOccurrences += 1;
            }

        }
        res.ErrorInfo = "Have Duplicate Service: " + string.Join(",", errorList);

        await browser.CloseAsync();

        return res;
    }
}



