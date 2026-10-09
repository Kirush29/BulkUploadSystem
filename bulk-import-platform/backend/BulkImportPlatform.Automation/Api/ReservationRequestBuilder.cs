using System.Globalization;
using BulkImportPlatform.Core;

namespace BulkImportPlatform.Automation.Api;

/// <summary>
/// Turns a mapped reservation row into the body irentosoft's "Add Reservation" form posts to
/// vehicle-bookings (see BookingUIApp reservation/form/index.tsx), resolving the names in the
/// file (locations, vehicle number, vehicle group) to the account's ids. The account's
/// locations, vehicles and rate plans are fetched once per run, so building a request makes no
/// calls of its own.
/// </summary>
public class ReservationRequestBuilder
{
    private readonly List<ApiLocation> _locations;
    private readonly Dictionary<string, ApiLocation> _locationsByName;
    private readonly Dictionary<string, ApiVehicle> _vehiclesByCode;
    private readonly Dictionary<string, ApiVehicle> _vehiclesByLicense;
    private readonly Dictionary<string, (int Id, string Name)> _vehicleTypesByName;
    private readonly List<ApiRatePlan> _ratePlans;

    public int LocationCount => _locations.Count;
    public int VehicleCount => _vehiclesByCode.Count;
    public int RatePlanCount => _ratePlans.Count;

    private ReservationRequestBuilder(List<ApiLocation> locations, List<ApiVehicle> vehicles, List<ApiRatePlan> ratePlans)
    {
        _locations = locations;
        _locationsByName = ToLookup(locations.Where(l => !string.IsNullOrWhiteSpace(l.Name)), l => l.Name!);
        _vehiclesByCode = ToLookup(vehicles.Where(v => !string.IsNullOrWhiteSpace(v.Code)), v => v.Code!);
        _vehiclesByLicense = ToLookup(vehicles.Where(v => !string.IsNullOrWhiteSpace(v.LicenseNo)), v => v.LicenseNo!);
        _ratePlans = ratePlans.Where(p => p.IsActive != false).ToList();

        _vehicleTypesByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var v in vehicles.Where(v => v.VehicleTypeId > 0 && !string.IsNullOrWhiteSpace(v.VehicleTypeName)))
            _vehicleTypesByName.TryAdd(v.VehicleTypeName!.Trim(), (v.VehicleTypeId, v.VehicleTypeName.Trim()));
        foreach (var p in _ratePlans.Where(p => p.AssetSpecTypeId > 0 && !string.IsNullOrWhiteSpace(p.AssetSpecType)))
            _vehicleTypesByName.TryAdd(p.AssetSpecType!.Trim(), (p.AssetSpecTypeId, p.AssetSpecType.Trim()));
    }

    public static async Task<ReservationRequestBuilder> LoadAsync(IrentosoftApiClient api)
    {
        var locations = api.GetLocationsAsync();
        var vehicles = api.GetVehiclesAsync();
        var ratePlans = api.GetRatePlansAsync();
        await Task.WhenAll(locations, vehicles, ratePlans);
        return new ReservationRequestBuilder(locations.Result, vehicles.Result, ratePlans.Result);
    }

    public class BuildResult
    {
        public Dictionary<string, object?>? Request { get; set; }
        public string? Error { get; init; }
        // Non-fatal adjustments worth showing on the row (e.g. vehicle not found, booked by group).
        public List<string> Notes { get; } = new();
    }

    public BuildResult Build(Dictionary<string, string?> row, int? knownCustomerId)
    {
        var result = new BuildResult();

        var start = ImportValueParsers.TryParseDateTime(Get(row, "PickupDateTime"));
        var end = ImportValueParsers.TryParseDateTime(Get(row, "ReturnDateTime"));
        if (start == null || end == null)
            return new BuildResult { Error = "Pickup or return date/time is missing or invalid." };

        // ---- Locations ----
        var pickupName = Get(row, "PickupLocation");
        if (!TryFind(_locationsByName, pickupName, out var startLocation))
            return new BuildResult { Error = $"Pickup location '{pickupName}' was not found in irentosoft. Available: {Available(_locationsByName.Keys)}." };

        var returnName = Get(row, "ReturnLocation");
        var endLocation = startLocation;
        if (!string.IsNullOrWhiteSpace(returnName) && !TryFind(_locationsByName, returnName, out endLocation))
            return new BuildResult { Error = $"Return location '{returnName}' was not found in irentosoft. Available: {Available(_locationsByName.Keys)}." };

        // ---- Vehicle and vehicle type ----
        var vehicleNumber = Get(row, "VehicleNumber");
        var groupName = Get(row, "VehicleGroup");
        ApiVehicle? vehicle = null;
        if (!string.IsNullOrWhiteSpace(vehicleNumber)
            && !TryFind(_vehiclesByCode, vehicleNumber, out vehicle)
            && !TryFind(_vehiclesByLicense, vehicleNumber, out vehicle))
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return new BuildResult { Error = $"Vehicle '{vehicleNumber}' was not found in irentosoft (by Code or License No), and no vehicle group was given to book by instead." };
            result.Notes.Add($"vehicle '{vehicleNumber}' not found, booked by group '{groupName}' without a vehicle assigned");
        }

        int typeId;
        if (vehicle != null && vehicle.VehicleTypeId > 0)
        {
            typeId = vehicle.VehicleTypeId;
        }
        else if (TryFind(_vehicleTypesByName, groupName, out var type))
        {
            typeId = type.Id;
        }
        else
        {
            return new BuildResult { Error = $"Vehicle group '{groupName}' was not found in irentosoft. Available: {Available(_vehicleTypesByName.Keys)}." };
        }

        // ---- Rate ----
        var plan = PickRatePlan(typeId, vehicle?.AssetId, startLocation!.LocationId);
        if (plan == null)
            return new BuildResult { Error = $"No active rate plan exists in irentosoft for vehicle type #{typeId} - add one under Settings > Rate > Rate Plan." };

        // The file's vehicle price is spread over the rental days as the daily rate, so the
        // booking's rental charge matches the original. Without one, the plan's own rate applies.
        var days = Math.Max(1, (int)Math.Ceiling((end.Value - start.Value).TotalHours / 24));
        var vehiclePrice = ImportValueParsers.TryParseDecimal(Get(row, "VehiclePrice"));
        var daily = vehiclePrice is > 0
            ? Math.Round(vehiclePrice.Value / days, 2, MidpointRounding.AwayFromZero)
            : plan.Daily ?? 0m;

        // ---- Customer ----
        var (firstName, lastName) = ImportValueParsers.SplitName(Get(row, "CustomerName") ?? string.Empty);
        var phone = CleanPhone(Get(row, "CustomerPhone"));
        var email = Get(row, "CustomerEmail")?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) email = null;

        var deposit = ImportValueParsers.TryParseDecimal(Get(row, "SecurityDeposit"));

        result.Request = new Dictionary<string, object?>
        {
            ["customerId"] = knownCustomerId,
            ["firstName"] = Limit(firstName, 60),
            ["lastName"] = string.IsNullOrWhiteSpace(lastName) ? null : Limit(lastName, 60),
            ["phone"] = phone,
            ["email"] = email,
            ["assetSpecTypeId"] = typeId,
            ["assetId"] = vehicle?.AssetId,
            ["assetRatePlanId"] = plan.AssetRatePlanId,
            ["startDate"] = start.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            ["endDate"] = end.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            ["startLocationId"] = startLocation.LocationId,
            ["endLocationId"] = endLocation!.LocationId,
            ["bookingRate"] = new Dictionary<string, object?>
            {
                ["assetRatePlanId"] = plan.AssetRatePlanId,
                ["daily"] = daily
            },
            // Kept as the source system's number so the booking can be traced back to the file;
            // referenceId is left to irentosoft so it assigns its own number and default taxes.
            ["externalReferenceId"] = Get(row, "ReservationNumber")?.Trim(),
            ["depositAmount"] = deposit is > 0 ? deposit : null,
            // Never email the customer about a migrated booking.
            ["isSendMail"] = false,
            ["isActive"] = true
        };
        return result;
    }

    /// <summary>
    /// Standard (non-seasonal) plans first, then ones specific to this vehicle or location,
    /// then irentosoft's own priority.
    /// </summary>
    private ApiRatePlan? PickRatePlan(int typeId, int? assetId, int locationId) =>
        _ratePlans
            .Where(p => p.AssetSpecTypeId == typeId && (p.AssetId == null || p.AssetId == assetId))
            .Where(p => p.LocationId == null || p.LocationId == locationId)
            .OrderBy(p => p.IsSeasonal)
            .ThenByDescending(p => assetId != null && p.AssetId == assetId)
            .ThenByDescending(p => p.LocationId == locationId)
            .ThenByDescending(p => p.Priority)
            .FirstOrDefault();

    /// <summary>Phones arrive as text or as Excel numbers; irentosoft allows at most 20 characters.</summary>
    private static string? CleanPhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var phone = raw.Trim();
        return phone.Length > 20 ? phone[..20] : phone;
    }

    private static string Limit(string value, int max) => value.Length > max ? value[..max] : value;

    private static Dictionary<string, T> ToLookup<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var lookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items) lookup.TryAdd(key(item).Trim(), item);
        return lookup;
    }

    private static bool TryFind<T>(Dictionary<string, T> lookup, string? name, out T? value)
    {
        value = default;
        return !string.IsNullOrWhiteSpace(name) && lookup.TryGetValue(name.Trim(), out value);
    }

    private static string Available(IEnumerable<string> names)
    {
        var list = names.OrderBy(n => n).Take(15).ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }

    private static string? Get(Dictionary<string, string?> row, string field) =>
        row.TryGetValue(field, out var value) ? value : null;
}
