using Microsoft.Playwright;

namespace UtilityLibraries;

public class InvalidTagsValidation : IValidation
{
    private IPlaywright _playwright;

    public InvalidTagsValidation(IPlaywright playwright)
    {
        _playwright = playwright;
    }
    public async Task<TResult> Validate(string testLink)
    {
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var allTags = await page.QuerySelectorAllAsync("body *");

        List<string> invalidTags = new List<string>();
        List<string> allTagNames = new List<string>();
        List<string> errorList = new List<string>();

        HashSet<string> validHtmlTags = new HashSet<string>
        {
            // List of valid HTML tags
                "a", "abbr", "acronym", "address", "applet", "area", "article", "aside", "audio", "b", "base", "basefont", "bdi", "bdo", "big", "blockquote", "body", "br", "button", "canvas", "caption", "center", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del", "details", "dfn", "dialog", "dir", "div", "dl", "dt", "em", "embed", "fieldset", "figcaption", "figure", "font", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd", "keygen", "label", "legend", "li", "link", "main", "map", "mark", "menu", "menuitem", "meta", "meter", "nav", "noframes", "noscript", "object", "ol", "optgroup", "option", "output", "p", "param", "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "script", "section", "select", "small", "source", "span", "strike", "strong", "style", "sub", "summary", "sup", "table", "tbody", "td", "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track", "tt", "u", "ul", "var", "video", "wbr", "rect", "svg", "path", "panel-controller", "search-expander", "overflow-menu", "panel-controller", "bread-crumbs", "circle", "line", "local-time"
        };

        var tagNames = await page.EvaluateAsync<string[]>("() => Array.from(document.querySelectorAll('body *'), el => el.tagName.toLowerCase())");

        foreach (var tagName in tagNames)
        {
            allTagNames.Add(tagName);
            if (!validHtmlTags.Contains(tagName))
            {
                try{
                    var context = await page.EvaluateAsync<string>($"() => {{ const element = document.querySelector('body {tagName}'); return element ? element.parentElement.innerHTML : ''; }}");
                    if (context.Length > 100)
                    {
                        errorList.Add(tagName);
                    }
                    else if (context.Length > 0 && context != "")
                    {
                        errorList.Add(context);
                    }
                    else
                    {
                        errorList.Add(tagName);
                    }
                }
                catch
                {
                    errorList.Add(tagName);
                }
                invalidTags.Add(tagName);

            }
        }

        var formattedList = errorList
            .GroupBy(item => item)
            .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times ,  {group.Key}")
            .ToList();

        if (invalidTags.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = invalidTags.Count;
            res.ErrorInfo = "Invalid tags found: " + string.Join(",", invalidTags);
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }
}