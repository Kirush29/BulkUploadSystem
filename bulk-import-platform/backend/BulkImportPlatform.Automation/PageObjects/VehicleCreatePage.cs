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

    /// <summary>
    /// Opens a fresh Add-Asset form. By default only waits until the form itself is rendered -
    /// every dropdown setter already waits for its own options to arrive, so waiting for the
    /// whole page's network to go idle (several seconds per vehicle) buys nothing there.
    /// Pass <paramref name="waitForAllData"/> when the dropdown lists themselves are being read
    /// (catalog sync): an option list read before its data loads would look empty.
    /// </summary>
    public async Task NavigateAsync(bool waitForAllData = false)
    {
        await _page.GotoAsync("https://live.irentosoft.com/Asset/Add-Asset",
            new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await _page.Locator("[id='mui-component-select-makeId']").WaitForAsync(new() { Timeout = 30000 });
        if (waitForAllData) await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public Task SetOwningLocationAsync(string text) => DropdownSelector.SelectMuiAsync(_page, "owningLocationId", text);

    public Task SetCurrentLocationAsync(string text) => DropdownSelector.SelectMuiAsync(_page, "locationId", text);

    /// <summary>
    /// Maps our Vehicle.VehicleNumber to irentosoft's field labeled "Code" (input[name='code'],
    /// confirmed live).
    ///
    /// irentosoft pre-fills Code with its own next running number, fetched asynchronously
    /// (GET /vehicles/code, observed ~1-5s after the form renders, sometimes twice). Anything
    /// typed before that lands is silently overwritten - which is how vehicles ended up saved
    /// as "57", "60", ... instead of their real numbers. So: wait for the pre-fill first, and
    /// call <see cref="EnsureCodeAsync"/> right before Save to catch a late second overwrite.
    /// </summary>
    public async Task SetCodeAsync(string vehicleNumber)
    {
        var code = _page.Locator("input[name='code']");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && string.IsNullOrEmpty(await code.InputValueAsync()))
        {
            await _page.WaitForTimeoutAsync(200);
        }
        await code.FillAsync(vehicleNumber);
    }

    /// <summary>
    /// Re-checks Code immediately before Save and re-types it if irentosoft's auto-number
    /// replaced it in the meantime. Throws rather than saving a vehicle under the wrong code.
    /// </summary>
    public async Task EnsureCodeAsync(string vehicleNumber)
    {
        var code = _page.Locator("input[name='code']");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await code.InputValueAsync() == vehicleNumber) return;
            await code.FillAsync(vehicleNumber);
            await _page.WaitForTimeoutAsync(500);
        }
        if (await code.InputValueAsync() != vehicleNumber)
            throw new InvalidOperationException(
                $"irentosoft kept replacing Code '{vehicleNumber}' with '{await code.InputValueAsync()}' - not saved, to avoid creating the vehicle under the wrong number.");
    }

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

    // Selecting a different make re-filters the Model list in place, so several makes'
    // models can be read on one page load (no navigation needed between them).

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
