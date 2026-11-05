using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CampusEvents.Tests;

/// <summary>
/// Exercise 3: the lock on the admin endpoints is a security control —
/// without a test it is one refactor away from silently disappearing.
/// </summary>
public class AdminApiKeyTests : IClassFixture<TestAppFactory>
{
    private const string IntroToAzureId = "11111111-1111-1111-1111-111111111111";
    private readonly HttpClient _client;

    public AdminApiKeyTests(TestAppFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static object NewEvent() => new
    {
        title = "Locked-door test",
        description = "Created by the integration tests",
        startsAtUtc = DateTime.UtcNow.AddDays(30),
        location = "D113",
        capacity = 5
    };

    [Fact]
    public async Task CreateEvent_WithoutApiKey_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithWrongApiKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events")
        {
            Content = JsonContent.Create(NewEvent())
        };
        request.Headers.Add("X-Api-Key", "not-the-key");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithApiKey_ReturnsCreated()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events")
        {
            Content = JsonContent.Create(NewEvent())
        };
        request.Headers.Add("X-Api-Key", TestAppFactory.AdminApiKey);

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutApiKey_StillWorks()
    {
        // The public product must stay open — this test fails if someone over-locks.
        var registration = new { attendeeName = "Open Door", attendeeEmail = "open.door@example.com" };

        HttpResponseMessage response =
            await _client.PostAsJsonAsync($"/api/events/{IntroToAzureId}/registrations", registration);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
