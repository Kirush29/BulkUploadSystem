using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.Utils;

/// <summary>
/// Detects whether a create-form Save attempt actually succeeded, ported from IrentoQA's
/// Utils/ValidationHelper.cs with NUnit/Allure assertions and attachment logging stripped -
/// only the generic error-detection primitives remain, since RpaImportService needs a
/// specific, actionable message for RpaAttempt.ErrorMessage rather than a bare exception.
/// </summary>
public static class FormValidationInspector
{
    public static async Task<string?> GetInlineErrorAsync(IPage page)
    {
        var error = page.Locator("p.Mui-error, .MuiFormHelperText-root.Mui-error").First;
        if (await error.CountAsync() > 0 && await error.IsVisibleAsync())
        {
            return (await error.TextContentAsync())?.Trim();
        }
        return null;
    }

    public static async Task<string?> GetToastMessageAsync(IPage page)
    {
        var toast = page.Locator(".MuiAlert-message, .MuiSnackbar-root, [role='alert'], .Toastify__toast-body").First;
        if (await toast.CountAsync() > 0 && await toast.IsVisibleAsync())
        {
            return (await toast.TextContentAsync())?.Trim();
        }
        return null;
    }

    /// <summary>True if the page is still sitting on a create-form URL (didn't navigate away after Save).</summary>
    public static bool StillOnCreatePage(IPage page, string createPathFragment) =>
        page.Url.Contains(createPathFragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Polls for a bounded time after Save for one of: navigation away from the create page,
    /// an inline field error, a toast/alert message, or (caller-supplied) a modal appearing -
    /// replacing IrentoQA's flat WaitForTimeoutAsync(5000)/(3000) fixed sleeps, which are exactly
    /// the kind of flakiness a production bot should not carry forward.
    /// </summary>
    public static async Task<SaveOutcome> WaitForSaveOutcomeAsync(
        IPage page,
        string createPathFragment,
        Func<Task<bool>>? modalCheck = null,
        int timeoutMs = 15000,
        int pollIntervalMs = 300)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!StillOnCreatePage(page, createPathFragment))
            {
                return new SaveOutcome(true, null);
            }

            if (modalCheck != null && await modalCheck())
            {
                return new SaveOutcome(false, "DUPLICATE_MODAL");
            }

            var inlineError = await GetInlineErrorAsync(page);
            if (inlineError != null)
            {
                return new SaveOutcome(false, inlineError);
            }

            var toast = await GetToastMessageAsync(page);
            if (toast != null)
            {
                return new SaveOutcome(false, toast);
            }

            await page.WaitForTimeoutAsync(pollIntervalMs);
        }

        return new SaveOutcome(false, "Timed out waiting for a save confirmation, error, or navigation.");
    }

    public record SaveOutcome(bool Success, string? Message);
}
