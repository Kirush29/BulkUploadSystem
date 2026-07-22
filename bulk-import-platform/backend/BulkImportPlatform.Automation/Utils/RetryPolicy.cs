namespace BulkImportPlatform.Automation.Utils;

/// <summary>
/// Generic retry-with-delay wrapper, ported from IrentoQA's Utils/RetryHelper.cs. Used to wrap
/// login and Save() calls (tolerating a network hiccup) - deliberately NOT used to wrap dropdown
/// selection, since a genuine no-match should fail fast and loud rather than retry three times
/// against the same wrong option list.
/// </summary>
public static class RetryPolicy
{
    public static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxAttempts = 3, int delayMs = 1000)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(delayMs);
                }
            }
        }
        throw lastException!;
    }

    public static async Task RetryAsync(Func<Task> action, int maxAttempts = 3, int delayMs = 1000)
    {
        await RetryAsync(async () => { await action(); return true; }, maxAttempts, delayMs);
    }
}
