using System.Net;
using System.Net.Http;
using System.Text;
using SmartBodyAI.Services;
using Xunit;

namespace SmartBodyAI.Tests;

public class SmartDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_UsesWellKnownConfiguration_WhenEndpointsAndCapabilitiesArePresent()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/.well-known/smart-configuration", StringComparison.Ordinal) == true)
            {
                return CreateJsonResponse("""
                {
                  "authorization_endpoint": "https://auth.example/authorize",
                  "token_endpoint": "https://auth.example/token",
                  "capabilities": [
                    "launch-standalone",
                    "context-standalone-patient",
                    "permission-patient",
                    "sso-openid-connect"
                  ]
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new SmartDiscoveryService(() => new HttpClient(handler));

        var result = await service.DiscoverAsync("https://fhir.example/fhir");

        Assert.True(result.IsValid);
        Assert.Equal("https://auth.example/authorize", result.AuthorizeUrl);
        Assert.Equal("https://auth.example/token", result.TokenUrl);
        Assert.Equal(".well-known/smart-configuration", result.MetadataSource);
        Assert.Contains("launch-standalone", result.Capabilities);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public async Task DiscoverAsync_FallsBackToMetadata_WhenWellKnownIsUnavailable()
    {
        const string metadataJson = """
        {
          "resourceType": "CapabilityStatement",
          "rest": [
            {
              "mode": "server",
              "security": {
                "extension": [
                  {
                    "url": "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris",
                    "extension": [
                      { "url": "authorize", "valueUri": "https://auth.example/authorize" },
                      { "url": "token", "valueUri": "https://auth.example/token" }
                    ]
                  }
                ],
                "service": [
                  {
                    "coding": [
                      {
                        "code": "SMART-on-FHIR",
                        "display": "SMART-on-FHIR"
                      }
                    ]
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/.well-known/smart-configuration", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/metadata", StringComparison.Ordinal) == true)
            {
                return CreateJsonResponse(metadataJson);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new SmartDiscoveryService(() => new HttpClient(handler));

        var result = await service.DiscoverAsync(
            "https://fhir.example/fhir",
            capabilitiesFromMetadata: new[]
            {
                "launch-standalone",
                "context-standalone-patient",
                "permission-patient",
                "sso-openid-connect"
            });

        Assert.True(result.IsValid);
        Assert.Equal("/metadata", result.MetadataSource);
        Assert.Equal("https://auth.example/token", result.TokenUrl);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsValidationError_WhenStandaloneCapabilityIsMissing()
    {
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse("""
        {
          "authorization_endpoint": "https://auth.example/authorize",
          "token_endpoint": "https://auth.example/token",
          "capabilities": [
            "context-standalone-patient",
            "permission-patient",
            "sso-openid-connect"
          ]
        }
        """));

        var service = new SmartDiscoveryService(() => new HttpClient(handler));

        var result = await service.DiscoverAsync("https://fhir.example/fhir");

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, error => error.Contains("launch-standalone", StringComparison.Ordinal));
    }

    private static HttpResponseMessage CreateJsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
