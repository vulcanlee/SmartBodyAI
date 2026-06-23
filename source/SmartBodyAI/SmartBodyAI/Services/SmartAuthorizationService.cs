using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;

namespace SmartBodyAI.Services;

public class SmartAuthorizationService : ISmartAuthorizationService
{
    private readonly Func<HttpClient> clientFactory;
    private readonly OAuthStateStoreService stateStore;

    public SmartAuthorizationService(IHttpClientFactory httpClientFactory, OAuthStateStoreService stateStore)
        : this(httpClientFactory.CreateClient, stateStore)
    {
    }

    public SmartAuthorizationService(Func<HttpClient> clientFactory, OAuthStateStoreService stateStore)
    {
        this.clientFactory = clientFactory;
        this.stateStore = stateStore;
    }

    public async Task<SmartAuthorizationRequest> CreateAuthorizationRequestAsync(
        string authorizeUrl,
        SmartAuthorizationRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.GenerateCodeChallenge(codeVerifier);

        var persistedState = new SmartAppSettingModel
        {
            State = state,
            CodeVerifier = codeVerifier,
            CodeChallengeMethod = SmartAppSettingModel.DefaultCodeChallengeMethod,
            FhirServerUrl = context.FhirServerUrl,
            AuthorizeUrl = context.AuthorizeUrl,
            TokenUrl = context.TokenUrl,
            ClientId = context.ClientId,
            ClientSecret = context.ClientSecret,
            RedirectUrl = context.RedirectUrl,
            AuthorizationScope = BuildScope(context.Scope, context.Launch),
            Launch = context.Launch
        };

        await stateStore.SaveAsync(state, persistedState, TimeSpan.FromMinutes(10), cancellationToken);

        var scope = persistedState.AuthorizationScope;
        var url = QueryHelpers.AddQueryString(
            authorizeUrl,
            new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = context.ClientId,
                ["redirect_uri"] = context.RedirectUrl,
                ["scope"] = scope,
                ["state"] = state,
                ["aud"] = context.FhirServerUrl,
                ["launch"] = context.Launch,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = SmartAppSettingModel.DefaultCodeChallengeMethod
            }
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value));

        return new SmartAuthorizationRequest
        {
            AuthorizationUrl = url,
            State = state,
            CodeVerifier = codeVerifier
        };
    }

    public async Task<SmartCallbackValidationResult> ValidateCallbackAsync(
        string? code,
        string? state,
        string? error,
        string? errorDescription,
        CancellationToken cancellationToken = default)
    {
        SmartCallbackValidationResult result = new()
        {
            Code = code,
            State = state
        };

        if (string.IsNullOrWhiteSpace(state))
        {
            result.Errors.Add("回呼缺少 state。");
            return result;
        }

        var storedState = await stateStore.LoadAsync<SmartAppSettingModel>(state, cancellationToken);
        if (storedState is null)
        {
            result.Errors.Add("回呼的 state 不存在或已過期。");
            return result;
        }

        result.StoredState = storedState;

        if (!string.IsNullOrWhiteSpace(error))
        {
            result.Errors.Add($"授權失敗：{error}。{errorDescription}".Trim());
            await stateStore.RemoveAsync(state, cancellationToken);
            return result;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            result.Errors.Add("回呼缺少授權碼。");
            await stateStore.RemoveAsync(state, cancellationToken);
        }

        return result;
    }

    public async Task<SmartTokenValidationResult> ExchangeCodeForTokenAsync(
        string tokenUrl,
        string code,
        string state,
        string clientId,
        string? clientSecret,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        SmartTokenValidationResult result = new();

        if (string.IsNullOrWhiteSpace(tokenUrl))
        {
            result.Errors.Add("必須提供 Token 端點。");
            return result;
        }

        var storedState = await stateStore.LoadAsync<SmartAppSettingModel>(state, cancellationToken);
        if (storedState is null)
        {
            result.Errors.Add("回呼的 state 不存在或已過期。");
            return result;
        }

        using var client = clientFactory();
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = storedState.CodeVerifier
            })
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await stateStore.RemoveAsync(state, cancellationToken);
            result.Errors.Add($"Token 交換失敗。HTTP {(int)response.StatusCode}：{json}");
            return result;
        }

        var smartResponse = JsonSerializer.Deserialize<SmartResponse>(json) ?? new SmartResponse();
        ValidateTokenResponse(smartResponse, result);

        if (result.Errors.Count == 0)
        {
            await stateStore.RemoveAsync(state, cancellationToken);
        }
        else
        {
            await stateStore.RemoveAsync(state, cancellationToken);
        }

        return result;
    }

    private static void ValidateTokenResponse(SmartResponse response, SmartTokenValidationResult result)
    {
        if (!string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Token 回應必須使用 Bearer token_type。");
        }

        if (string.IsNullOrWhiteSpace(response.AccessToken))
        {
            result.Errors.Add("Token 回應缺少 access_token。");
        }

        if (string.IsNullOrWhiteSpace(response.PatientId))
        {
            result.Errors.Add("Token 回應缺少病人內容資訊。");
        }

        if (!ContainsScope(response.Scopes, "patient/*.read"))
        {
            result.Errors.Add("Token response is missing required scope `patient/*.read`.");
        }

        if (!ContainsScope(response.Scopes, "launch/patient") && !ContainsScope(response.Scopes, "launch"))
        {
            result.Errors.Add("Token response is missing patient launch context scope `launch/patient` or `launch`.");
        }

        if (!ContainsScope(response.Scopes, "openid")
            || !ContainsScope(response.Scopes, "fhirUser")
            || !ContainsScope(response.Scopes, "profile"))
        {
            result.Errors.Add("Token 回應缺少必要的 OIDC scopes。");
        }

        if (string.IsNullOrWhiteSpace(response.IdToken))
        {
            result.Errors.Add("Token 回應缺少 id_token。");
        }
        else
        {
            try
            {
                var token = new JwtSecurityTokenHandler().ReadJwtToken(response.IdToken);
                var fhirUser = token.Claims.FirstOrDefault(x => string.Equals(x.Type, "fhirUser", StringComparison.Ordinal))?.Value;
                if (string.IsNullOrWhiteSpace(fhirUser))
                {
                    result.Errors.Add("id_token 缺少 fhirUser 宣告。");
                }
                else
                {
                    result.FhirUser = fhirUser;
                }

                if (!token.Claims.Any(x => string.Equals(x.Type, "sub", StringComparison.Ordinal)))
                {
                    result.Errors.Add("id_token 缺少 sub 宣告。");
                }

                if (!token.Claims.Any(x => string.Equals(x.Type, "iss", StringComparison.Ordinal)))
                {
                    result.Errors.Add("id_token 缺少 iss 宣告。");
                }
            }
            catch (Exception)
            {
                result.Errors.Add("id_token 不是有效的 JWT。");
            }
        }

        if (result.Errors.Count == 0)
        {
            result.AccessToken = response.AccessToken;
            result.PatientId = response.PatientId;
            result.Scopes = response.Scopes;
            result.IdToken = response.IdToken;
        }
    }

    private static bool ContainsScope(string scopes, string targetScope)
    {
        return scopes
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(targetScope, StringComparer.Ordinal);
    }

    private static string BuildScope(string scope, string? launch)
    {
        if (string.IsNullOrWhiteSpace(launch) || ContainsScope(scope, "launch"))
        {
            return scope;
        }

        return $"{scope} launch";
    }
}
