using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.Utils;

/// <summary>
/// Thrown when a dropdown/autocomplete has no option matching the requested text. This is the
/// deliberate replacement for IrentoQA's SelectMuiSelect helper, which silently falls back to
/// picking the first available option on a mismatch - acceptable for a QA test asserting on
/// its own synthetic data, but not for a bot entering a real client's real vehicle/customer
/// data: picking the wrong Make/Model because the text didn't match exactly is worse than the
/// row failing loudly.
/// </summary>
public class DropdownOptionNotFoundException : Exception
{
    public string FieldName { get; }
    public string RequestedValue { get; }

    public DropdownOptionNotFoundException(string fieldName, string requestedValue)
        : base($"Dropdown '{fieldName}' has no option matching '{requestedValue}'.")
    {
        FieldName = fieldName;
        RequestedValue = requestedValue;
    }
}

/// <summary>
/// Selects an option from a Material UI Select or Autocomplete control, based on the more
/// robust wait-then-match pattern from IrentoQA's PlayWrightHelper.SelectDropdown (which waits
/// properly for options to render and throws on no match) rather than SelectMuiSelect's fixed
/// timeout + silent-fallback-to-first-option behavior.
/// </summary>
public static class DropdownSelector
{
    /// <summary>Static MUI Select menu (click opens a role="option" listbox) - e.g. Vehicle form fields.</summary>
    public static async Task SelectMuiAsync(IPage page, string muiComponentName, string text, int timeoutMs = 10000)
    {
        var trigger = page.Locator($"[id='mui-component-select-{muiComponentName}']");
        await trigger.ScrollIntoViewIfNeededAsync();
        await trigger.ClickAsync();

        await SelectMatchingOptionAsync(page, muiComponentName, text, timeoutMs);
    }

    /// <summary>Autocomplete control (click, type to filter, then match) - e.g. Customer form fields.</summary>
    public static async Task SelectAutocompleteAsync(IPage page, string fieldSelector, string fieldName, string text, int timeoutMs = 10000)
    {
        var input = page.Locator($"{fieldSelector} input[role='combobox']");
        await input.ClickAsync(new LocatorClickOptions { Force = true });
        await input.FillAsync(text);

        await SelectMatchingOptionAsync(page, fieldName, text, timeoutMs);
    }

    private static async Task SelectMatchingOptionAsync(IPage page, string fieldName, string text, int timeoutMs)
    {
        var options = page.Locator("[role='option']");
        try
        {
            await options.First.WaitForAsync(new() { Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            throw new DropdownOptionNotFoundException(fieldName, text);
        }

        // Prefer an exact (case-insensitive) match so e.g. "Ford" picks "Ford" rather than
        // whichever of "Ford" / "Ford Trucks" happens to render first; fall back to the
        // original substring match so existing mappings keep working.
        var exact = options.Filter(new() { HasTextRegex = ExactTextRegex(text) });
        if (await exact.CountAsync() > 0)
        {
            await exact.First.ClickAsync();
            return;
        }

        var match = page.Locator($"[role='option']:has-text('{text}')");
        if (await match.CountAsync() == 0)
        {
            throw new DropdownOptionNotFoundException(fieldName, text);
        }

        await match.First.ClickAsync();
    }

    /// <summary>
    /// Opens a MUI Select, returns the text of every option it lists, then closes it again
    /// without choosing anything. An empty list (e.g. a make with no models yet) is a valid
    /// result, not an error - the menu simply never renders any role="option" rows.
    /// </summary>
    public static async Task<List<string>> ReadMuiOptionsAsync(IPage page, string muiComponentName, int timeoutMs = 5000)
    {
        var trigger = page.Locator($"[id='mui-component-select-{muiComponentName}']");
        await trigger.ScrollIntoViewIfNeededAsync();
        await trigger.ClickAsync();

        var options = page.Locator("[role='option']");
        try
        {
            await options.First.WaitForAsync(new() { Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            await page.Keyboard.PressAsync("Escape");
            return new List<string>();
        }

        var texts = (await options.AllInnerTextsAsync())
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        await page.Keyboard.PressAsync("Escape");
        try
        {
            await options.First.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            // Menu close animation lingering is harmless - the next dropdown click reopens its own.
        }
        return texts;
    }

    private static System.Text.RegularExpressions.Regex ExactTextRegex(string text) =>
        new($"^\\s*{System.Text.RegularExpressions.Regex.Escape(text.Trim())}\\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
