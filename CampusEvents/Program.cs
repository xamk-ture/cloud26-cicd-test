using System.ComponentModel.DataAnnotations;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CampusEvents.Data;
using CampusEvents.Models;
using CampusEvents.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEventStore, PostgresEventStore>();

var storageConnectionString = builder.Configuration.GetConnectionString("Storage");

if (storageConnectionString is not null)
{
    builder.Services.AddSingleton(new BlobContainerClient(storageConnectionString, "posters"));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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
});

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
});

app.MapDelete("/api/events/{id:guid}", (Guid id, IEventStore store) =>
{
    bool deleted = store.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound(new { error = "Event not found." });
});

app.MapPost("/api/events/{id:guid}/registrations", (Guid id, RegisterRequest request, IEventStore store) =>
{
    if (!TryValidate(request, out Dictionary<string, string[]> errors))
    {
        return Results.ValidationProblem(errors);
    }

    (RegistrationResponse? registration, string? error) = store.Register(id, request);

    if (error == "Event not found.")
    {
        return Results.NotFound(new { error });
    }

    if (error is not null)
    {
        return Results.Conflict(new { error });
    }

    return Results.Created($"/api/events/{id}/registrations/{registration!.Id}", registration);
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

if (storageConnectionString is not null)
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
