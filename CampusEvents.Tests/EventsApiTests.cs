using System.Net;
using System.Net.Http.Json;
using CampusEvents.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CampusEvents.Tests;

public class EventsApiTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    // Seeded event ids (see DbSeeder)
    private static readonly Guid IntroToAzureId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CoffeeHourId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public EventsApiTests(TestAppFactory factory)
    {
        // https base address: Program.cs redirects http → https outside Production
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_ReturnsSeededEvents()
    {
        List<EventResponse>? events = await _client.GetFromJsonAsync<List<EventResponse>>("/api/events");

        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public async Task Register_ReturnsCreated_AndTakesASeat()
    {
        var request = new RegisterRequest("Test Person", "test.person@example.com");

        HttpResponseMessage response =
            await _client.PostAsJsonAsync($"/api/events/{IntroToAzureId}/registrations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        EventResponse? updated =
            await _client.GetFromJsonAsync<EventResponse>($"/api/events/{IntroToAzureId}");
        Assert.NotNull(updated);
        Assert.Equal(1, updated.SeatsTaken);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        // alex@example.com is seeded onto the Coffee Hour event
        var request = new RegisterRequest("Alex Again", "alex@example.com");

        HttpResponseMessage response =
            await _client.PostAsJsonAsync($"/api/events/{CoffeeHourId}/registrations", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
