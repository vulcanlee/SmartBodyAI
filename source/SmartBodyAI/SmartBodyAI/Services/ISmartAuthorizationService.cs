using SmartBodyAI.Models;

namespace SmartBodyAI.Services;

public interface ISmartAuthorizationService
{
    Task<SmartAuthorizationRequest> CreateAuthorizationRequestAsync(
        string authorizeUrl,
        SmartAuthorizationRequestContext context,
        CancellationToken cancellationToken = default);

    Task<SmartCallbackValidationResult> ValidateCallbackAsync(
        string? code,
        string? state,
        string? error,
        string? errorDescription,
        CancellationToken cancellationToken = default);

    Task<SmartTokenValidationResult> ExchangeCodeForTokenAsync(
        string tokenUrl,
        string code,
        string state,
        string clientId,
        string? clientSecret,
        string redirectUri,
        CancellationToken cancellationToken = default);
}
