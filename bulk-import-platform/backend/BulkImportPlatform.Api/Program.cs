using BulkImportPlatform.Automation;
using BulkImportPlatform.Core.Interfaces;
using BulkImportPlatform.Infrastructure.Data;
using BulkImportPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Customer and Vehicle Bulk Data Import Platform API",
        Version = "v1",
        Description = "Upload, validate, transform, review, and import Vehicle and Customer data in bulk."
    });
});

// Data Layer: EF Core against SQL Server, per the proposal's system architecture.
builder.Services.AddDbContext<BulkImportDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Business Layer (Import / Validation / Mapping / Reporting Engines)
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IMappingEngine, MappingEngine>();
builder.Services.AddScoped<IDefaultValueEngine, DefaultValueEngine>();
builder.Services.AddScoped<IValidationEngine, ValidationEngine>();
builder.Services.AddScoped<IImportService, ImportService>();

// Client account credentials (per-client irentosoft login used by the RPA push step) are
// encrypted at rest via Data Protection. Keys must be persisted to a fixed, backed-up path -
// the default ephemeral/user-profile key ring would silently invalidate every stored password
// on redeploy or a different service account.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"]
        ?? @"C:\BulkImportPlatform\keys"))
    .SetApplicationName("BulkImportPlatform");
builder.Services.AddScoped<IClientAccountCredentialProtector, ClientAccountCredentialProtector>();
builder.Services.AddScoped<IClientAccountService, ClientAccountService>();

// RPA push-to-irentosoft (Playwright browser automation, no API exists on irentosoft's side).
builder.Services.AddSingleton<BrowserSessionFactory>();
builder.Services.AddScoped<IRpaImportService, RpaImportService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

// Excel/Word uploads can be a few MB; raise the default Kestrel/form limits a bit.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

var app = builder.Build();

// ---------- Pipeline ----------

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bulk Import Platform API v1"));

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Apply pending EF Core migrations automatically on startup in Development so the
// database schema is always up to date when you run the API locally.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BulkImportDbContext>();
    db.Database.Migrate();
}

// Playwright's browser binaries are a separate `playwright install chromium` download, easy to
// forget on a fresh machine - fail loudly with an actionable message at startup instead of a
// cryptic native-library crash the first time an RPA run actually tries to launch a browser.
try
{
    using var rpaCheckScope = app.Services.CreateScope();
    var browserFactory = rpaCheckScope.ServiceProvider.GetRequiredService<BrowserSessionFactory>();
    await browserFactory.VerifyBrowserInstalledAsync();
}
catch (InvalidOperationException ex)
{
    app.Logger.LogWarning(ex, "RPA push-to-irentosoft will not work until Playwright's browser is installed.");
}

app.Run();
