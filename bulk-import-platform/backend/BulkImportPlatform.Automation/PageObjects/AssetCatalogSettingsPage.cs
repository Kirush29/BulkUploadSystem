using BulkImportPlatform.Automation.Utils;
using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.PageObjects;

/// <summary>
/// irentosoft's Settings &gt; Vehicles &gt; Makes / Models screens, used to add a Make or Model
/// that the Add-Asset form's dropdowns don't offer yet (typically a brand-new client account).
///
/// Both screens (inspected live) are a paginated list on the left plus an always-visible
/// "Add New ..." side panel holding a plain &lt;form&gt;: Makes has just a Name field
/// (input[name='name']); Models adds a "Makes *" MUI Select (mui-component-select-makeId)
/// before the "Vehicle Model Name *" field (also input[name='name']). Both submit via a
/// "Save" button.
///
/// This page object only CREATES entries - it never decides whether one is needed. Existence
/// is checked by the caller against VehicleCreatePage's Make/Model dropdowns (one complete,
/// unpaginated list, unlike these screens' tables), and every create is re-verified there
/// afterwards, which is the real confirmation that the entry is usable.
/// </summary>
public class AssetCatalogSettingsPage
{
    private const string MakeUrl = "https://live.irentosoft.com/Settings/Asset/AssetMake";
    private const string ModelUrl = "https://live.irentosoft.com/Settings/Asset/AssetModel";

    private readonly IPage _page;

    public AssetCatalogSettingsPage(IPage page)
    {
        _page = page;
    }

    public async Task CreateMakeAsync(string make)
    {
        var form = await OpenAsync(MakeUrl);
        await form.Locator("input[name='name']").FillAsync(make);
        await SaveAsync(form, $"make '{make}'");
    }

    public async Task CreateModelAsync(string make, string model)
    {
        var form = await OpenAsync(ModelUrl);
        await DropdownSelector.SelectMuiAsync(_page, "makeId", make);
        await form.Locator("input[name='name']").FillAsync(model);
        await SaveAsync(form, $"model '{model}' (make '{make}')");
    }

    // ---------- Internals ----------

    /// <summary>
    /// Returns the screen's "Add New" form (the search box sits outside any form). The form
    /// resets itself after each save, so consecutive creates on the same screen reuse the
    /// already-loaded page instead of reloading it every time.
    /// </summary>
    private async Task<ILocator> OpenAsync(string url)
    {
        if (!_page.Url.StartsWith(url, StringComparison.OrdinalIgnoreCase))
        {
            await _page.GotoAsync(url);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        var form = _page.Locator("form").Filter(new() { Has = _page.Locator("input[name='name']") }).First;
        await form.WaitForAsync(new() { Timeout = 10000 });
        return form;
    }

    /// <summary>
    /// The side panel stays on the same URL after saving, so success is detected as the form
    /// resetting (Name field emptied) with no inline field error. Any inline error or toast
    /// (e.g. "already exists") is reported verbatim so the run log says exactly what
    /// irentosoft objected to.
    /// </summary>
    private async Task SaveAsync(ILocator form, string what)
    {
        var nameInput = form.Locator("input[name='name']");
        await form.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var inlineError = await FormValidationInspector.GetInlineErrorAsync(_page);
            if (inlineError != null)
                throw new InvalidOperationException($"irentosoft rejected {what}: {inlineError}");

            if (string.IsNullOrEmpty(await nameInput.InputValueAsync()))
                return;

            await _page.WaitForTimeoutAsync(300);
        }

        var toast = await FormValidationInspector.GetToastMessageAsync(_page);
        throw new InvalidOperationException($"Timed out saving {what}{(toast != null ? $": {toast}" : ".")}");
    }
}
