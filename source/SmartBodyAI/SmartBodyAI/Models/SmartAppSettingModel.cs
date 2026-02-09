namespace SmartBodyAI.Models;

/// <summary>
/// Smart On FHIR 設定模型，用於儲存與 FHIR 伺服器互動所需的各項設定值。
/// </summary>
public class SmartAppSettingModel
{
    public string AuthorizationScope { get; set; }
    public int ProcessDelayTimeInMilliSeconds { get; set; }

    /// <summary>
    /// FHIR 伺服器的基底網址。
    /// </summary>
    public string FhirServerUrl { get; set; }
    public string InferenceHostApi { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the client application.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// 授權完成後 Smart App 的重新導向網址。
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// 從授權伺服器取得的授權碼 (Authorization Code)。
    /// </summary>
    public string AuthCode { get; set; }

    /// <summary>
    /// 用於驗證請求完整性的客戶端狀態字串。
    /// </summary>
    public string ClientState { get; set; } = "local_state";

    /// <summary>
    /// 交換 Access Token 的授權伺服器 Token Endpoint 位址。
    /// </summary>
    public string TokenUrl { get; set; }
    /// <summary>
    /// 更新 Access Token 需要用到的 Endpoint 位置
    /// </summary>
    public string RefreshTokenUrl { get; set; }
    /// <summary>
    /// 用於與外部提供者啟動授權流程的 URL。
    /// </summary>
    public string AuthorizeUrl { get; set; }
    /// <summary>
    /// 可用來是否透過 EHR Launch 來啟動授權流程的參數。
    /// </summary>
    public string Iss { get; set; }
    public string Launch { get; set; }
    public string State { get; set; }
}
