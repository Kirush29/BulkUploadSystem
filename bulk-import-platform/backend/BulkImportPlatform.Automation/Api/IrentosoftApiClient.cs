using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace BulkImportPlatform.Automation.Api;

public class IrentosoftApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public IrentosoftApiException(string message, HttpStatusCode? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }
}

public record ApiLocation(int LocationId, string? Name);

public record ApiVehicle(int AssetId, string? Code, string? LicenseNo, int VehicleTypeId, string? VehicleTypeName);

public record ApiRatePlan(
    int AssetRatePlanId, int AssetSpecTypeId, string? AssetSpecType, bool IsSeasonal, int Priority,
    int? AssetId, int? LocationId, bool? IsActive, decimal? Hourly, decimal? Daily, decimal? Weekly, decimal? Monthly);

/// <summary>
/// Calls irentosoft's own backend API - the same endpoints its React UI calls - instead of
/// filling in its web forms. A reservation then costs one HTTP round trip (well under a second)
/// rather than ~20 seconds of page loads and dropdown clicks, and calls can run in parallel.
///
/// Nothing about the API's location or auth is hard-coded: irentosoft's UI is split across
/// several microservices whose URLs are baked into its JS bundle at build time, so after a
/// normal browser login this opens the UI pages that use each service and records the URL and
/// bearer token of the request the UI itself makes.
/// </summary>
public sealed class IrentosoftApiClient : IDisposable
{
    private const string SiteUrl = "https://live.irentosoft.com";
    private const int PageSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly Func<string, Task<string?>> _reauthenticate;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string _token;

    public string BookingBaseUrl { get; }
    public string AssetBaseUrl { get; }
    public string RateBaseUrl { get; }
    public string LocationsUrl { get; }

    private IrentosoftApiClient(string token, string bookingBaseUrl, string assetBaseUrl, string rateBaseUrl, string locationsUrl, Func<string, Task<string?>> reauthenticate)
    {
        _token = token;
        BookingBaseUrl = bookingBaseUrl;
        AssetBaseUrl = assetBaseUrl;
        RateBaseUrl = rateBaseUrl;
        LocationsUrl = locationsUrl;
        _reauthenticate = reauthenticate;
    }

    /// <param name="page">A page already logged in to irentosoft.</param>
    /// <param name="reauthenticate">
    /// Called with the rejected token when the API answers 401 (token expired mid-run); returns
    /// a fresh token, or null if one can't be obtained.
    /// </param>
    public static async Task<IrentosoftApiClient> ConnectAsync(IPage page, Func<string, Task<string?>> reauthenticate)
    {
        var (bookingUrl, token) = await CaptureApiCallAsync(page, "/Reservation", "/vehicle-bookings");
        var (assetUrl, _) = await CaptureApiCallAsync(page, "/Asset", "/vehicles");
        var (rateUrl, _) = await CaptureApiCallAsync(page, "/Settings/Rate/RatePlan", "/vehicleRatePlans");
        var (locationUrl, _) = await CaptureApiCallAsync(page, "/Settings/Location", "/locations");

        return new IrentosoftApiClient(
            token,
            BaseBefore(bookingUrl, "/vehicle-bookings"),
            BaseBefore(assetUrl, "/vehicles"),
            BaseBefore(rateUrl, "/vehicleRatePlans"),
            // Locations are listed per client ("{base}clients/{clientId}/locations"), so keep the whole path.
            BaseBefore(locationUrl, "/locations") + "locations",
            reauthenticate);
    }

    /// <summary>Re-reads the bearer token the UI is currently sending (e.g. after a re-login).</summary>
    public static async Task<string> CaptureTokenAsync(IPage page) =>
        (await CaptureApiCallAsync(page, "/Reservation", "/vehicle-bookings")).Token;

    private static async Task<(string Url, string Token)> CaptureApiCallAsync(IPage page, string uiPath, string apiPathMarker)
    {
        IRequest request;
        try
        {
            request = await page.RunAndWaitForRequestAsync(
                () => page.GotoAsync(SiteUrl + uiPath),
                r => r.Method == "GET"
                     && (r.ResourceType == "fetch" || r.ResourceType == "xhr")
                     && new Uri(r.Url).AbsolutePath.Contains(apiPathMarker, StringComparison.Ordinal),
                new PageRunAndWaitForRequestOptions { Timeout = 30000 });
        }
        catch (TimeoutException)
        {
            throw new IrentosoftApiException(
                $"Opening irentosoft's {uiPath} page did not make the expected '{apiPathMarker}' API call within 30s - " +
                "the account may lack permission for that page, or irentosoft's UI has changed.");
        }

        var headers = await request.AllHeadersAsync();
        if (!headers.TryGetValue("authorization", out var auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new IrentosoftApiException($"irentosoft's '{apiPathMarker}' API call carried no bearer token.");

        return (request.Url, auth["Bearer ".Length..].Trim());
    }

    /// <summary>"https://api.x/booking/vehicle-bookings?a=1" + "/vehicle-bookings" -> "https://api.x/booking/".</summary>
    private static string BaseBefore(string url, string marker)
    {
        var withoutQuery = url.Split('?')[0];
        var index = withoutQuery.IndexOf(marker, StringComparison.Ordinal);
        return withoutQuery[..(index + 1)];
    }

    // ---------- Lookups ----------

    public Task<List<ApiLocation>> GetLocationsAsync() => GetAllPagesAsync<ApiLocation>(LocationsUrl, "active=true");

    public Task<List<ApiVehicle>> GetVehiclesAsync() => GetAllPagesAsync<ApiVehicle>(AssetBaseUrl + "vehicles", null);

    public Task<List<ApiRatePlan>> GetRatePlansAsync() => GetAllPagesAsync<ApiRatePlan>(RateBaseUrl + "vehicleRatePlans", "active=true");

    private async Task<List<T>> GetAllPagesAsync<T>(string url, string? extraQuery)
    {
        var all = new List<T>();
        for (var pageNumber = 1; pageNumber <= 200; pageNumber++)
        {
            var query = $"pageSize={PageSize}&pageNumber={pageNumber}" + (extraQuery == null ? "" : "&" + extraQuery);
            var body = await SendAsync(HttpMethod.Get, $"{url}?{query}", null);
            var page = JsonSerializer.Deserialize<ApiPage<T>>(body, JsonOptions);
            var records = page?.Records ?? new List<T>();
            all.AddRange(records);
            if (records.Count == 0 || pageNumber >= (page?.TotalPages ?? 1)) break;
        }
        return all;
    }

    private class ApiPage<T>
    {
        public int TotalPages { get; set; }
        public List<T>? Records { get; set; }
    }

    // ---------- Bookings ----------

    /// <summary>
    /// POST vehicle-bookings?action=CreateNew - what irentosoft's "Add Reservation" form calls.
    /// CreateNew (status New) is used rather than Reserve because Reserve also demands the
    /// customer's date of birth, licence number and licence expiry, which exports don't carry.
    /// </summary>
    public async Task<int> CreateBookingAsync(object request)
    {
        var body = await SendAsync(HttpMethod.Post, BookingBaseUrl + "vehicle-bookings?action=CreateNew", request);
        return int.TryParse(body.Trim().Trim('"'), out var id) && id > 0
            ? id
            : throw new IrentosoftApiException($"irentosoft accepted the booking but returned no booking id (response: {Truncate(body)}).");
    }

    public async Task<int?> GetBookingCustomerIdAsync(int bookingId)
    {
        var body = await SendAsync(HttpMethod.Get, $"{BookingBaseUrl}vehicle-bookings/{bookingId}", null);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("customerId", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null;
    }

    public async Task AddBookingNoteAsync(int bookingId, string note) =>
        await SendAsync(HttpMethod.Post, $"{BookingBaseUrl}bookings/{bookingId}/notes", new { note });

    // ---------- Transport ----------

    private async Task<string> SendAsync(HttpMethod method, string url, object? payload)
    {
        var tokenUsed = _token;
        using var response = await SendOnceAsync(method, url, payload, tokenUsed);

        // Only an auth rejection is retried: the request was refused before irentosoft did
        // anything, so resending can't create a duplicate booking (a timeout or 5xx could).
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var fresh = await RefreshTokenAsync(tokenUsed)
                ?? throw new IrentosoftApiException("irentosoft rejected the session token and logging in again failed.", HttpStatusCode.Unauthorized);
            using var retry = await SendOnceAsync(method, url, payload, fresh);
            return await ReadOrThrowAsync(retry);
        }

        return await ReadOrThrowAsync(response);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string url, object? payload, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (payload != null)
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        try
        {
            return await _http.SendAsync(request);
        }
        catch (TaskCanceledException)
        {
            throw new IrentosoftApiException($"irentosoft did not answer within {_http.Timeout.TotalSeconds:0}s. Check in irentosoft whether the booking was created before retrying this row.");
        }
    }

    private async Task<string?> RefreshTokenAsync(string rejectedToken)
    {
        await _tokenLock.WaitAsync();
        try
        {
            // Another parallel request may already have refreshed it.
            if (_token != rejectedToken) return _token;

            var fresh = await _reauthenticate(rejectedToken);
            if (fresh != null) _token = fresh;
            return fresh;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static async Task<string> ReadOrThrowAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode) return body;
        throw new IrentosoftApiException($"irentosoft returned {(int)response.StatusCode}: {ExtractError(body)}", response.StatusCode);
    }

    /// <summary>irentosoft errors are ASP.NET ProblemDetails: validation "errors", else "detail", else "title".</summary>
    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array
                            ? p.Value.EnumerateArray().Select(e => e.ToString())
                            : new[] { p.Value.ToString() })
                        .ToList();
                    if (messages.Count > 0) return string.Join(" ", messages);
                }
                if (root.TryGetProperty("detail", out var detail) && !string.IsNullOrWhiteSpace(detail.ToString())) return detail.ToString();
                if (root.TryGetProperty("title", out var title)) return title.ToString();
            }
        }
        catch (JsonException)
        {
        }
        return string.IsNullOrWhiteSpace(body) ? "(empty response)" : Truncate(body);
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300] + "...";

    public void Dispose()
    {
        _http.Dispose();
        _tokenLock.Dispose();
    }
}
