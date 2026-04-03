using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartBodyAI.Models;
using SmartBodyAI.Services;
using SmartBodyAI.Servicers;
using Xunit;

namespace SmartBodyAI.Tests;

public class SmartAuthorizationServiceTests
{
    [Fact]
    public async Task CreateAuthorizationRequestAsync_BuildsStandaloneAuthorizeUrlWithPkce()
    {
        var stateStore = CreateStateStore();
        var service = new SmartAuthorizationService(() => new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException())), stateStore);

        var context = new SmartAuthorizationRequestContext
        {
            FhirServerUrl = "https://fhir.example/fhir",
            AuthorizeUrl = "https://auth.example/authorize",
            TokenUrl = "https://auth.example/token",
            ClientId = "smart-app",
            ClientSecret = "super-secret",
            RedirectUrl = "https://app.example/patient-information",
            Scope = "openid fhirUser profile launch/patient patient/*.read"
        };

        var request = await service.CreateAuthorizationRequestAsync(context.AuthorizeUrl, context);

        Assert.NotNull(request.State);
        Assert.NotNull(request.CodeVerifier);
        Assert.Contains("response_type=code", request.AuthorizationUrl);
        Assert.Contains("client_id=smart-app", request.AuthorizationUrl);
        Assert.Contains("aud=https%3A%2F%2Ffhir.example%2Ffhir", request.AuthorizationUrl);
        Assert.Contains("code_challenge=", request.AuthorizationUrl);
        Assert.Contains("code_challenge_method=S256", request.AuthorizationUrl);
        Assert.DoesNotContain("launch=", request.AuthorizationUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAuthorizationRequestAsync_PersistsEndpointsAndClientCredentialsInStateStore()
    {
        var stateStore = CreateStateStore();
        var service = new SmartAuthorizationService(() => new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException())), stateStore);

        var context = new SmartAuthorizationRequestContext
        {
            FhirServerUrl = "https://fhir.example/fhir",
            AuthorizeUrl = "https://auth.example/authorize",
            TokenUrl = "https://auth.example/token",
            ClientId = "smart-app",
            ClientSecret = "super-secret",
            RedirectUrl = "https://app.example/patient-information",
            Scope = "openid fhirUser profile launch/patient patient/*.read"
        };

        var request = await service.CreateAuthorizationRequestAsync(context.AuthorizeUrl, context);
        var storedState = await stateStore.LoadAsync<SmartAppSettingModel>(request.State);

        Assert.NotNull(storedState);
        Assert.Equal(context.AuthorizeUrl, storedState!.AuthorizeUrl);
        Assert.Equal(context.TokenUrl, storedState.TokenUrl);
        Assert.Equal(context.ClientId, storedState.ClientId);
        Assert.Equal(context.ClientSecret, storedState.ClientSecret);
        Assert.Equal(context.RedirectUrl, storedState.RedirectUrl);
    }

    [Fact]
    public async Task ValidateCallbackAsync_ReturnsError_WhenStateIsMissing()
    {
        var service = CreateService();

        var result = await service.ValidateCallbackAsync(code: "abc", state: null, error: null, errorDescription: null);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_RejectsMissingPatientContext()
    {
        var stateStore = CreateStateStore();
        var state = await SeedStateAsync(stateStore);
        var tokenJson = """
        {
          "token_type": "Bearer",
          "access_token": "access-token",
          "scope": "openid fhirUser profile patient/*.read",
          "id_token": "placeholder"
        }
        """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
        });

        var service = new SmartAuthorizationService(() => new HttpClient(handler), stateStore);

        var result = await service.ExchangeCodeForTokenAsync(
            "https://auth.example/token",
            code: "auth-code",
            state,
            "smart-app",
            "super-secret",
            "https://app.example/patient-information");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("patient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_ParsesOidcClaims_AndRemovesStateOnSuccess()
    {
        var stateStore = CreateStateStore();
        var state = await SeedStateAsync(stateStore);
        var idToken = BuildJwt(("fhirUser", "Practitioner/123"), ("sub", "user-1"), ("iss", "https://issuer.example"));
        var tokenJson = $$"""
        {
          "token_type": "Bearer",
          "access_token": "access-token",
          "scope": "openid fhirUser profile launch/patient patient/*.read",
          "patient": "patient-123",
          "id_token": "{{idToken}}"
        }
        """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
        });

        var service = new SmartAuthorizationService(() => new HttpClient(handler), stateStore);

        var result = await service.ExchangeCodeForTokenAsync(
            "https://auth.example/token",
            code: "auth-code",
            state,
            "smart-app",
            "super-secret",
            "https://app.example/patient-information");

        Assert.True(result.IsValid);
        Assert.Equal("patient-123", result.PatientId);
        Assert.Equal("Practitioner/123", result.FhirUser);
        Assert.Null(await stateStore.LoadAsync<SmartAppSettingModel>(state));
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_RejectsMalformedIdToken()
    {
        var stateStore = CreateStateStore();
        var state = await SeedStateAsync(stateStore);
        var tokenJson = """
        {
          "token_type": "Bearer",
          "access_token": "access-token",
          "scope": "openid fhirUser profile launch/patient patient/*.read",
          "patient": "patient-123",
          "id_token": "not-a-jwt"
        }
        """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
        });

        var service = new SmartAuthorizationService(() => new HttpClient(handler), stateStore);

        var result = await service.ExchangeCodeForTokenAsync(
            "https://auth.example/token",
            code: "auth-code",
            state,
            "smart-app",
            "super-secret",
            "https://app.example/patient-information");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("id_token", StringComparison.OrdinalIgnoreCase));
    }

    private static SmartAuthorizationService CreateService()
    {
        var stateStore = CreateStateStore();
        return new SmartAuthorizationService(() => new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException())), stateStore);
    }

    private static OAuthStateStoreService CreateStateStore()
    {
        var cache = new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));

        return new OAuthStateStoreService(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<OAuthStateStoreService>(),
            cache);
    }

    private static async Task<string> SeedStateAsync(OAuthStateStoreService stateStore)
    {
        var state = "state-123";
        await stateStore.SaveAsync(state, new SmartAppSettingModel
        {
            State = state,
            CodeVerifier = "code-verifier",
            CodeChallengeMethod = "S256"
        }, TimeSpan.FromMinutes(10));
        return state;
    }

    private static string BuildJwt(params (string Type, string Value)[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("12345678901234567890123456789012"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "https://issuer.example",
            audience: "smart-app",
            claims: claims.Select(x => new Claim(x.Type, x.Value)),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
