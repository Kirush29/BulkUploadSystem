using BulkImportPlatform.Automation.Utils;
using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.PageObjects;

/// <summary>
/// Ported from IrentoQA's Pages/Vehicle/VehicleCreatePage.cs, split into one setter per field
/// (instead of the original's grouped FillBasicInfo/FillAdditionalInfo/etc.) so RpaImportService
/// can call only the setters for fields it actually has mapped data for. Fields with no
/// corresponding data in our Vehicle entity - cylinders, numberOfdoors, trim, fuelLevel,
/// startOdometer, and the entire Ownership/Finance section - are deliberately NOT ported at
/// all: never fabricate a value for a real client's real vehicle.
///
/// All MUI dropdown selection goes through DropdownSelector, which throws
/// DropdownOptionNotFoundException on no match instead of IrentoQA's SelectMuiSelect silently
/// picking the first available option.
/// </summary>
public class VehicleCreatePage
{
    private const string CreatePathFragment = "Add-Asset";

    private readonly IPage _page;

    public VehicleCreatePage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync("https://live.irentosoft.com/Asset/Add-Asset");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public Task SetOwningLocationAsync(string text) => DropdownSelector.SelectMuiAsync(_page, "owningLocationId", text);

    public Task SetCurrentLocationAsync(string text) => DropdownSelector.SelectMuiAsync(_page, "locationId", text);

    /// <summary>
    /// Maps our Vehicle.VehicleNumber to irentosoft's field labeled "Code" (per the account
    /// owner - not documented in the IrentoQA catalog). Selector is a best-effort guess
    /// (input[name='code']) pending confirmation during the live verification pass.
    /// </summary>
    public Task SetCodeAsync(string vehicleNumber) => _page.FillAsync("input[name='code']", vehicleNumber);

    public Task SetVinAsync(string vin) => _page.FillAsync("input[name='vinNumber']", vin);

    public async Task SetMakeAsync(string make)
    {
        await DropdownSelector.SelectMuiAsync(_page, "makeId", make);
        // Model list depends on the selected make and needs time to repopulate before SetModel.
        await _page.WaitForTimeoutAsync(1500);
    }

    public Task SetModelAsync(string model) => DropdownSelector.SelectMuiAsync(_page, "modelId", model);

    /// <summary>
    /// Every make currently configured on this irentosoft account, read straight from the
    /// Add-Asset form's Make dropdown - the same list SetMakeAsync will later match against,
    /// so "exists" here means exactly "selectable when creating the vehicle".
    /// </summary>
    public Task<List<string>> ReadMakesAsync() => DropdownSelector.ReadMuiOptionsAsync(_page, "makeId");

    /// <summary>Models configured under <paramref name="make"/> (the Model list is filtered by the selected make).</summary>
    public async Task<List<string>> ReadModelsForMakeAsync(string make)
    {
        await SetMakeAsync(make);
        return await DropdownSelector.ReadMuiOptionsAsync(_page, "modelId");
    }

    public Task SetVehicleTypeAsync(string vehicleType) => DropdownSelector.SelectMuiAsync(_page, "vehicleTypeId", vehicleType);

    public Task SetLicenseNumberAsync(string licenseNo) => _page.FillAsync("input[name='licenseNo']", licenseNo);

    public Task SetTransmissionAsync(string transmission) => DropdownSelector.SelectMuiAsync(_page, "transmissionType", transmission);

    /// <summary>
    /// The Year field is an Autocomplete combobox, not a MUI Select - ported from IrentoQA as-is
    /// (located as the last combobox on the page, which is fragile/position-dependent) since
    /// there's no better selector documented yet. Throws if the typed year has no matching
    /// option, rather than silently doing nothing as the original did.
    /// </summary>
    public async Task SetYearAsync(string year)
    {
        var yearInput = _page.Locator("input[role='combobox']").Last;
        await yearInput.ClickAsync(new LocatorClickOptions { Force = true });
        await yearInput.FillAsync(year);

        var option = _page.Locator($"[role='option']:has-text('{year}')");
        try
        {
            await option.First.WaitForAsync(new() { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            throw new DropdownOptionNotFoundException("Year", year);
        }
        await option.First.ClickAsync();
    }

    public Task SetColorAsync(string color) => _page.FillAsync("input[name='color']", color);

    public Task SetFuelTypeAsync(string fuelType) => DropdownSelector.SelectMuiAsync(_page, "fuelType", fuelType);

    public Task SetTankSizeAsync(string tankSize) => _page.FillAsync("input[name='fuelTankSize']", tankSize);

    public Task SetOdometerAsync(string odometer) => _page.FillAsync("input[name='odometer']", odometer);

    public Task SetSpotNumberAsync(string spotNumber) => _page.FillAsync("input[name='spotNumber']", spotNumber);

    public async Task<FormValidationInspector.SaveOutcome> SaveAsync()
    {
        await _page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
        var saveBtn = _page.Locator("button[type='submit']:has-text('Save')");
        await saveBtn.ScrollIntoViewIfNeededAsync();
        await saveBtn.ClickAsync(new LocatorClickOptions { Force = true });

        return await FormValidationInspector.WaitForSaveOutcomeAsync(_page, CreatePathFragment);
    }
}
