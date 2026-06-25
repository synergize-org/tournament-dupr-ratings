using System.Net;
using System.Net.Http;
using TournamentDuprRatings.Services;

namespace TournamentDuprRatings.Tests;

public class DuprServiceTests
{
    // The DUPR API returns `id` as a JSON number (e.g. 123456789), but
    // DuprPlayerHit.Id is typed as string?.  System.Text.Json is strict about
    // type mismatches, so deserialization throws before the fix lands.

    private static HttpClient ClientReturning(string json)
    {
        var handler = new FakeHttpMessageHandler(json);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task SearchAsync_NumericId_DeserializesSuccessfully()
    {
        // Arrange – response where $.result.hits[0].id is a JSON number
        const string json = """
            {
              "result": {
                "hits": [
                  {
                    "id": 123456789,
                    "fullName": "John Doe",
                    "duprId": "ABC-123"
                  }
                ]
              }
            }
            """;

        var service = new DuprService(ClientReturning(json));

        // Act – must not throw; numeric id should coerce to its string form
        var hits = await service.SearchAsync("John Doe", 0.0, 0.0, "fake-token");

        Assert.Single(hits);
        Assert.Equal("123456789", hits[0].Id);
    }

    [Fact]
    public async Task SearchAsync_NumericId_PreservesOtherHitFields()
    {
        // Arrange – same bug scenario; verify FullName and DuprId survive
        const string json = """
            {
              "result": {
                "hits": [
                  {
                    "id": 987654321,
                    "fullName": "Jane Smith",
                    "duprId": "XYZ-456"
                  }
                ]
              }
            }
            """;

        var service = new DuprService(ClientReturning(json));

        // Act
        var hits = await service.SearchAsync("Jane Smith", 0.0, 0.0, "fake-token");

        // Assert adjacent fields are not lost alongside the id conversion
        Assert.Single(hits);
        Assert.Equal("Jane Smith", hits[0].FullName);
        Assert.Equal("XYZ-456", hits[0].DuprId);
    }

    private sealed class FakeHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
