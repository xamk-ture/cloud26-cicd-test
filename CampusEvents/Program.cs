using System.ComponentModel.DataAnnotations;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CampusEvents;
using CampusEvents.Data;
using CampusEvents.Models;
using CampusEvents.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Application Insights: only when the environment provides a connection string
// (Azure does, via Bicep). Locally and in tests the SDK is not registered at all.
if (builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] is not null)
{
    builder.Services.AddApplicationInsightsTelemetry();

    // The SDK listens to ASP.NET Core, HttpClient and SqlClient by default.
    // Npgsql publishes its own traces under the "Npgsql" source — subscribe to
    // it so every PostgreSQL command shows up as a dependency.
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource("Npgsql"));
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (connectionString is not null &&
    !connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
{
    // Passwordless: authenticate as this app's managed identity.
    // The token (~1 h lifetime) is fetched and refreshed automatically.
    var credential = new DefaultAzureCredential();
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UsePeriodicPasswordProvider(
        async (_, ct) => (await credential.GetTokenAsync(
            new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]), ct)).Token,
        TimeSpan.FromMinutes(45),  // refresh well before the ~60 min expiry
        TimeSpan.FromSeconds(10)); // on failure, retry quickly

    var dataSource = dataSourceBuilder.Build();
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));
}
else
{
    // Local development: docker-compose PostgreSQL with a password. Unchanged.
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<IEventStore, PostgresEventStore>();

var blobEndpoint = builder.Configuration["Storage:BlobEndpoint"];

if (blobEndpoint is not null)
{
    // Keyless: the container client authenticates as this app's managed identity
    // (Storage Blob Data Contributor on the account). Locally: your az login.
    builder.Services.AddSingleton(new BlobContainerClient(
        new Uri($"{blobEndpoint}posters"), new DefaultAzureCredential()));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (db.Database.IsRelational())
    {
        // PostgreSQL: apply every migration that is not yet in __EFMigrationsHistory.
        // A fresh database gets the whole schema; an existing one gets only the delta.
        db.Database.Migrate();
    }
    else
    {
        // Integration tests use the InMemory provider, which has no migrations.
        db.Database.EnsureCreated();
    }

    DbSeeder.SeedIfEmpty(db);
}

if (!app.Environment.IsEnvironment("Production"))
{
    app.UseHttpsRedirection();
}

// --- Ops / course helpers ---

app.MapGet("/", () => Results.Json(new
{
    application = "CampusEvents",
    message = "Campus Events API — register for workshops and campus meetups.",
    health = "/health",
    info = "/info",
    events = "/api/events"
}));

app.MapGet("/health", async (AppDbContext db) =>
{
    bool dbReachable = await db.Database.CanConnectAsync();

    return dbReachable
        ? Results.Ok(new { status = "Healthy", database = "reachable", utc = DateTime.UtcNow })
        : Results.Json(
            new { status = "Unhealthy", database = "unreachable", utc = DateTime.UtcNow },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/info", () => Results.Json(new
{
    application = "CampusEvents",
    version = "1.1.0", // ← new field
    environment = app.Environment.EnvironmentName,
    machine = Environment.MachineName,
    framework = Environment.Version.ToString(),
    timeUtc = DateTime.UtcNow
}));

// --- Events API ---

app.MapGet("/api/events", (IEventStore store) =>
{
    IReadOnlyList<CampusEvent> events = store.GetAll();
    return Results.Ok(events.Select(e => e.ToResponse()));
});

app.MapGet("/api/events/{id:guid}", (Guid id, IEventStore store) =>
{
    CampusEvent? campusEvent = store.GetById(id);
    return campusEvent is null
        ? Results.NotFound(new { error = "Event not found." })
        : Results.Ok(campusEvent.ToResponse());
});

app.MapPost("/api/events", (CreateEventRequest request, IEventStore store) =>
{
    if (!TryValidate(request, out Dictionary<string, string[]> errors))
    {
        return Results.ValidationProblem(errors);
    }

    if (request.StartsAtUtc <= DateTime.UtcNow)
    {
        return Results.BadRequest(new { error = "StartsAtUtc must be in the future." });
    }

    CampusEvent created = store.Create(request);
    return Results.Created($"/api/events/{created.Id}", created.ToResponse());
})
    .AddEndpointFilter<RequireApiKey>();

app.MapPut("/api/events/{id:guid}", (Guid id, UpdateEventRequest request, IEventStore store) =>
{
    if (!TryValidate(request, out Dictionary<string, string[]> errors))
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        CampusEvent? updated = store.Update(id, request);
        return updated is null
            ? Results.NotFound(new { error = "Event not found." })
            : Results.Ok(updated.ToResponse());
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .AddEndpointFilter<RequireApiKey>();

app.MapDelete("/api/events/{id:guid}", (Guid id, IEventStore store) =>
{
    bool deleted = store.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound(new { error = "Event not found." });
})
    .AddEndpointFilter<RequireApiKey>();

app.MapPost("/api/events/{id:guid}/registrations",
    (Guid id, RegisterRequest request, IEventStore store, ILogger<Program> logger) =>
{
    if (!TryValidate(request, out Dictionary<string, string[]> errors))
    {
        return Results.ValidationProblem(errors);
    }

    (RegistrationResponse? registration, string? error) = store.Register(id, request);

    if (error == "Event not found.")
    {
        logger.LogWarning("Registration rejected: event {EventId} not found", id);
        return Results.NotFound(new { error });
    }

    if (error is not null)
    {
        logger.LogWarning("Registration rejected for event {EventId}: {Reason}", id, error);
        return Results.Conflict(new { error });
    }

    logger.LogInformation("Registration {RegistrationId} created for event {EventId}",
        registration!.Id, id);

    return Results.Created($"/api/events/{id}/registrations/{registration.Id}", registration);
});

app.MapGet("/api/events/{id:guid}/registrations", (Guid id, IEventStore store) =>
{
    CampusEvent? campusEvent = store.GetById(id);
    if (campusEvent is null)
    {
        return Results.NotFound(new { error = "Event not found." });
    }

    var registrations = campusEvent.Registrations
        .OrderBy(r => r.RegisteredAtUtc)
        .Select(r => new RegistrationResponse(
            r.Id,
            campusEvent.Id,
            r.AttendeeName,
            r.AttendeeEmail,
            r.RegisteredAtUtc));

    return Results.Ok(registrations);
});

if (blobEndpoint is not null)
{
    app.MapPost("/api/events/{id:guid}/poster",
        async (Guid id, IFormFile file, BlobContainerClient posters, IEventStore store) =>
    {
        if (store.GetById(id) is null)
        {
            return Results.NotFound();
        }

        if (file.Length is 0 or > (2 * 1024 * 1024))
        {
            return Results.BadRequest(new { error = "Poster must be between 1 byte and 2 MB." });
        }

        if (file.ContentType is not ("image/jpeg" or "image/png"))
        {
            return Results.BadRequest(new { error = "Only JPEG and PNG posters are accepted." });
        }

        var blob = posters.GetBlobClient(id.ToString());
        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
        });

        return Results.NoContent();
    }).DisableAntiforgery();

    app.MapGet("/api/events/{id:guid}/poster", async (Guid id, BlobContainerClient posters) =>
    {
        var blob = posters.GetBlobClient(id.ToString());

        if (!await blob.ExistsAsync())
        {
            return Results.NotFound();
        }

        var download = await blob.DownloadStreamingAsync();
        return Results.Stream(download.Value.Content, download.Value.Details.ContentType);
    });
}

app.Run();

static bool TryValidate<T>(T model, out Dictionary<string, string[]> errors)
{
    ValidationContext context = new(model!);
    List<ValidationResult> results = [];
    bool valid = Validator.TryValidateObject(model!, context, results, validateAllProperties: true);

    errors = results
        .SelectMany(r =>
        {
            IEnumerable<string> members = r.MemberNames.Any() ? r.MemberNames : ["request"];
            return members.Select(m => (Member: m, Message: r.ErrorMessage ?? "Validation failed."));
        })
        .GroupBy(x => x.Member)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

    return valid;
}

// Needed later for integration tests / WebApplicationFactory
public partial class Program;
