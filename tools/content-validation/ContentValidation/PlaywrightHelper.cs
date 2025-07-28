using Microsoft.Playwright;
public static class PlaywrightHelper
{
    public static async Task<IPage> GotoageWithRetriesAsync(IPage page, string url, int retryCount = 3)
    {
        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 30000 });

                return page;
            }
            catch
            {
                Console.WriteLine($"Navigating to {url} (attempt {i + 1}/{retryCount})");
                if (i == retryCount - 1)
                {
                    throw;
                }
            }
        }
        throw new InvalidOperationException("This code should not be executed.");
    }
}
