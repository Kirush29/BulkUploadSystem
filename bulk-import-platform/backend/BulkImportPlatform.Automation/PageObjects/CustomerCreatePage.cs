using BulkImportPlatform.Automation.Utils;
using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.PageObjects;

/// <summary>
/// Ported from IrentoQA's Pages/Customer/CustomerCreatePage.cs, split into per-field setters
/// (same rationale as VehicleCreatePage) so only fields with real mapped data get filled.
/// Fields with zero source data in our Customer entity - cell/business phone, ID/passport
/// number, employer info, license category/country/state/dates, insurance, notes - are
/// deliberately not ported. Card info (FillCard in the original) is excluded entirely: it's
/// PCI-scope data that has no business being reachable code in this automation at all.
/// </summary>
public class CustomerCreatePage
{
    private const string CreatePathFragment = "Add-Customer";

    private readonly IPage _page;

    public CustomerCreatePage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync("https://live.irentosoft.com/Customer/Add-Customer");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Most robust nav-wait in the ported project - keep verbatim.
        await _page.WaitForSelectorAsync("input[name='firstName']", new() { Timeout = 60000 });
    }

    public Task SetFirstNameAsync(string firstName) => _page.FillAsync("input[name='firstName']", firstName);

    public Task SetLastNameAsync(string lastName) => _page.FillAsync("input[name='lastName']", lastName);

    public Task SetEmailAsync(string email) => _page.FillAsync("input[name='email']", email);

    public Task SetPhoneAsync(string phone) => _page.FillAsync("input[name='phone']", phone);

    public Task SetDateOfBirthAsync(string dob) => _page.FillAsync("input[name='dateOfBirth']", dob);

    public Task SetDriverLicenseNumberAsync(string licenseNumber) => _page.FillAsync("input[name='licenseNumber']", licenseNumber);

    public Task SetCityAsync(string city) => _page.FillAsync("input[name='city']", city);

    /// <summary>
    /// Street autocomplete: types the address text and accepts the site's own first suggestion
    /// (ported as-is from IrentoQA) - this is the live app's own geo-autocomplete behavior, not
    /// data we're inventing, since the typed text itself is the real client-supplied address.
    /// </summary>
    public async Task SetStreetAsync(string street)
    {
        var input = _page.Locator("[name='street'] input[role='combobox']");
        await input.ClickAsync(new LocatorClickOptions { Force = true });
        await input.FillAsync(street);
        await _page.WaitForTimeoutAsync(1500);
        var options = _page.Locator("[role='option']");
        if (await options.CountAsync() > 0) await options.First.ClickAsync();
    }

    /// <summary>
    /// Country/state selection, hardened vs. IrentoQA's SelectByName: that helper would clear
    /// and retry on zero matches but then click whatever came back regardless of exact text -
    /// here we throw via DropdownSelector on no match instead of silently picking the wrong
    /// country/state for a real customer's real address.
    /// </summary>
    public Task SetCountryAsync(string country) => DropdownSelector.SelectAutocompleteAsync(_page, "[name='countryId']", "countryId", country);

    public Task SetStateAsync(string state) => DropdownSelector.SelectAutocompleteAsync(_page, "[name='stateId']", "stateId", state);

    public async Task<bool> IsDuplicateModalVisibleAsync() =>
        await _page.Locator("text=/Duplicate Customer/i").CountAsync() > 0;

    public async Task<FormValidationInspector.SaveOutcome> SaveAsync()
    {
        await _page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
        var saveBtn = _page.Locator("button[type='submit']:has-text('Save')");
        await saveBtn.ScrollIntoViewIfNeededAsync();
        await saveBtn.ClickAsync(new LocatorClickOptions { Force = true });

        // The duplicate-customer modal's selectors are explicitly unverified against the live
        // app (per IrentoQA's own SELECTOR-RISK notes) - production behavior on detecting it is
        // to fail the record for manual staff review, never auto-click Confirm/Close like the
        // QA test code does.
        return await FormValidationInspector.WaitForSaveOutcomeAsync(_page, CreatePathFragment, IsDuplicateModalVisibleAsync);
    }
}
