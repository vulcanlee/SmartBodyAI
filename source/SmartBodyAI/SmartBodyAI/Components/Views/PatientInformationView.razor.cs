using AntDesign;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Components;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartBodyAI.Components.Views;

public partial class PatientInformationView
{
    [Parameter]
    public string? Code { get; set; }
    [Parameter]
    public string? State { get; set; }
    [Parameter]
    public string? Error { get; set; }
    [Parameter]
    public string? ErrorDescription { get; set; }
    [Inject]
    public SmartAppSettingService SmartAppSettingService { get; init; }
    [Inject]
    public OAuthStateStoreService OAuthStateStoreService { get; init; }
    [Inject]
    public NavigationManager NavigationManager { get; init; }
    [Inject]
    public ILogger<PatientInformationView> logger { get; set; }
    [Inject]
    public INotificationService Notice { get; init; }
    [Inject]
    public SettingService SettingService { get; init; }

    Patient patient = new();
    SmartResponse smartResponse = new();
    string patientId = string.Empty;
    PatientInformationModel patientInformation = new PatientInformationModel();

    public string ProcessingMessage { get; set; }
    public string patientMrm { get; set; }
    bool isAccessTokenReady = false;

    bool ShowUploadDicomDialog = false;
    string SubjectNo = "";
    string image = "";
    string imageVersion = DateTime.Now.Ticks.ToString();
    string logMessage;
    ProcessModel processModel = new ProcessModel();

    string randomNumberKey = "";
    CancellationTokenSource cts = new CancellationTokenSource();
    bool hasValidAuthorizationResponse;

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            string requestUri = NavigationManager.Uri;
            logger.LogInformation($"PatientInformationView loaded with URI: {requestUri}");
            hasValidAuthorizationResponse = await SetAuthCodeAsync();

            if (!hasValidAuthorizationResponse)
            {
                StateHasChanged();
                await System.Threading.Tasks.Task.Delay(2000);
                //GoHome();
                return;
            }

            processModel.Reset();
            processModel.Build();

            logMessage = "更新取得的授權碼與狀態碼...";
            if (SmartAppSettingService.Data.IsDebug)
                await UpdateMessage(logMessage);
            logMessage = $"透過授權碼，取得 Access Token...";
            if (SmartAppSettingService.Data.IsDebug)
                await UpdateMessage(logMessage);
            smartResponse = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(smartResponse.AccessToken))
            {
                logMessage = "取得 Access Token 失敗，無法繼續往下執行 !";
                logger.LogWarning($"取得 Access Token 失敗，無法繼續往下執行 ! SmartResponse: {JsonSerializer.Serialize(smartResponse)}");
                await UpdateMessage(logMessage);
                StateHasChanged();
                await System.Threading.Tasks.Task.Delay(2000);
                GoHome();
                return;
            }
            //await OAuthStateStoreService.RemoveAsync(State!);
            isAccessTokenReady = true;
            logMessage = $"已經成功取得 Access Token。  {smartResponse.AccessToken}";
            logMessage = $"已經成功取得 Access Token。";
            logger.LogInformation($"已經成功取得 Access Token : {smartResponse.AccessToken}");
            await UpdateMessage(logMessage);
            //await GetLastYearOrdersAsync(smartResponse);
            StateHasChanged();

            processModel.ActiveClass[0] = MagicObjectHelper.ActiveClassName;
            processModel.Build();
            StateHasChanged();
        }
    }

    private void GoHome()
    {
        NavigationManager.NavigateTo($"/?iss={SmartAppSettingService.Data.FhirServerUrl}");
    }

    async System.Threading.Tasks.Task OnQueryPatientAsync()
    {
        patientInformation.Reset();
        StateHasChanged();

        logMessage = $"取得病患的基本資訊...";
        await UpdateMessage(logMessage);
        patientId = patientMrm;
        var found = await GetPatientAsync(smartResponse);
        if (!found)
        {
            logger.LogWarning($"病患的 {patientId} 找不到!");
            return;
        }
        SubjectNo = patientInformation.Id;

        StateHasChanged();

        logMessage = $"取得病患的基本資訊完成，取得此病患的身高與體重...";
        if (SmartAppSettingService.Data.IsDebug)
            await UpdateMessage(logMessage);
        await GetHeightAndWeightAsync(smartResponse);
        await UpdateMessage($"已經完成取得 取得病患的基本資訊 與 此病患的身高與體重...");

        processModel.ActiveClass[1] = MagicObjectHelper.ActiveClassName;
        processModel.Build();
    }

    /// <summary>
    /// 更新取得的授權碼與狀態碼
    /// </summary>
    /// <returns></returns>
    public async System.Threading.Tasks.Task<bool> SetAuthCodeAsync()
    {
        await System.Threading.Tasks.Task.Yield();

        if (string.IsNullOrWhiteSpace(State))
        {
            logMessage = $"發現錯誤：缺少 state 參數，無法繼續往下執行 ! State={State}";
            await UpdateMessage(logMessage, 10);
            logger.LogWarning(logMessage);
            return false;
        }

        var SmartAppSettingModelItem = await OAuthStateStoreService.LoadAsync<SmartAppSettingModel>(State);

        if (SmartAppSettingModelItem == null)
        {
            logMessage = $"無法找到對應 State 的設定資料，可能已過期或已被使用，State={State}";
            await UpdateMessage(logMessage, 10);
            logger.LogWarning(logMessage);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Error))
        {
            SmartAppSettingModelItem.AuthorizationError = Error;
            SmartAppSettingModelItem.AuthorizationErrorDescription = ErrorDescription ?? string.Empty;
            SmartAppSettingService.UpdateSetting(SmartAppSettingModelItem);

            logMessage = $"授權伺服器回傳錯誤：{Error} {ErrorDescription}";
            await UpdateMessage(logMessage, 10);
            logger.LogWarning(logMessage);
            await OAuthStateStoreService.RemoveAsync(State!);
            return false;
        }

        if (string.IsNullOrWhiteSpace(Code))
        {
            logMessage = $"發現錯誤：缺少 code 參數，無法繼續往下執行 ! Code={Code} , State={State}";
            await UpdateMessage(logMessage, 10);
            logger.LogWarning(logMessage);
            await OAuthStateStoreService.RemoveAsync(State!);
            return false;
        }

        SmartAppSettingModelItem.AuthCode = Code;
        SmartAppSettingModelItem.State = State;
        SmartAppSettingModelItem.AuthorizationError = string.Empty;
        SmartAppSettingModelItem.AuthorizationErrorDescription = string.Empty;

        SmartAppSettingService.UpdateSetting(SmartAppSettingModelItem);
        return true;
    }

    /// <summary>
    /// 透過授權碼，取得 Access Token
    /// </summary>
    public async System.Threading.Tasks.Task<SmartResponse> GetAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(SmartAppSettingService.Data.TokenUrl))
        {
            logger.LogWarning("TokenUrl 未設定，無法交換 Access Token。");
            await UpdateMessage($"TokenUrl 未設定，無法交換 Access Token。");
            return new SmartResponse();
        }

        SmartResponse smartResponse = new();
        Dictionary<string, string> requestValues = new Dictionary<string, string>()
            {
                { "grant_type", "authorization_code" },
                { "code", SmartAppSettingService.Data.AuthCode },
                { "redirect_uri", SmartAppSettingService.Data.RedirectUrl },
                { "client_id", SmartAppSettingService.Data.ClientId },
                { "code_verifier", SmartAppSettingService.Data.CodeVerifier }
            };

        string bodyDictionaryJsonContent = JsonSerializer.Serialize(requestValues);
        logger.LogInformation($"準備交換 Access Token，Token Endpoint: {SmartAppSettingService.Data.TokenUrl}, Request Body: {bodyDictionaryJsonContent}");

        if (!string.IsNullOrWhiteSpace(SmartAppSettingService.Data.Launch))
        {
            requestValues["launch"] = SmartAppSettingService.Data.Launch;
        }

        HttpRequestMessage request = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(SmartAppSettingService.Data.TokenUrl),
            Content = new FormUrlEncodedContent(requestValues),
        };

        if (!string.IsNullOrWhiteSpace(SmartAppSettingService.Data.ClientSecret))
        {
            var basicAuthValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{SmartAppSettingService.Data.ClientId}:{SmartAppSettingService.Data.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthValue);
            logger.LogInformation($"使用 Client Secret 進行 Basic Authentication，Client ID: {SmartAppSettingService.Data.ClientId} , Authorization Header : Basic {basicAuthValue}");
        }

        HttpClient client = new HttpClient();

        HttpResponseMessage response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            logger.LogWarning(
                "取得 Access Token 失敗，HTTP Status Code: {StatusCode}, Response: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            await OAuthStateStoreService.RemoveAsync(State!);
            await UpdateMessage($"取得 Access Token 失敗，HTTP Status Code: {(int)response.StatusCode}, Response: {responseBody}，無法繼續往下執行 !");
            smartResponse = new();
            return smartResponse;
        }

        string json = await response.Content.ReadAsStringAsync();

        logger.LogInformation($"已收到 Access Token 回應。 {json}");

        smartResponse = JsonSerializer.Deserialize<SmartResponse>(json) ?? new SmartResponse();
        var smartResponseJson = JsonSerializer.Serialize(smartResponse);
        logger.LogInformation($"反序列化 Access Token 回應成功。 SmartResponse: {smartResponseJson}");
        return smartResponse;
    }

    public async System.Threading.Tasks.Task<bool> GetPatientAsync(SmartResponse smartResponse)
    {
        bool isReadPatient = false;

        // 1. 先建立 HttpClient，預設好 Authorization header
        HttpClient httpClient = new HttpClient
        {
            BaseAddress = new Uri(SmartAppSettingService.Data.FhirServerUrl)
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", smartResponse.AccessToken);

        FhirClientSettings settings = new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        };

        FhirClient fhirClient = new FhirClient(SmartAppSettingService.Data.FhirServerUrl, httpClient, settings);


        // 依 MRN (identifier) 查病人
        var bundle = await fhirClient.SearchAsync<Patient>(
            new[] { $"identifier=http://hospital.smarthealthit.org|{patientMrm}" }
        );

        // 取第一筆病人
        patient = bundle.Entry
            .Select(e => e.Resource)
            .OfType<Patient>()
            .FirstOrDefault();

        if (patient == null)
        {
            await UpdateMessage($"病人的 {patientId} 找不到!");
            return false;
        }
        else
        {
            smartResponse.PatientId = patient.Id;
            patientInformation.Id = patient.Id;
            patientInformation.Identifier = patientId;
            patientInformation.Name = patient.Name[0].ToString();
            patientInformation.BirthDate = patient.BirthDate;
            patientInformation.Gender = patient.Gender.ToString();
            if (SmartAppSettingService.Data.IsDebug)
                await UpdateMessage($"發現到病人的 {patientId} {patient.Name[0].ToString()} {patient.BirthDate} {patient.Gender}");
            return true;
        }
    }

    private async System.Threading.Tasks.Task GetHeightAndWeightAsync(SmartResponse smartResponse)
    {
        HttpClient httpClient = new HttpClient
        {
            BaseAddress = new Uri(SmartAppSettingService.Data.FhirServerUrl)
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", smartResponse.AccessToken);

        FhirClientSettings settings = new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        };

        FhirClient fhirClient = new FhirClient(SmartAppSettingService.Data.FhirServerUrl, httpClient, settings);

        // 查詢該病人的 Observation（限制常見 vital-sign codes）
        SearchParams searchParams = new SearchParams()
            .Where($"patient={smartResponse.PatientId}")
            .Where("category=vital-signs")
            .Include("Observation:patient")
            .LimitTo(50);

        Bundle bundle = await fhirClient.SearchAsync<Observation>(searchParams);

        decimal? heightValue = null;
        string? heightUnit = null;
        decimal? weightValue = null;
        string? weightUnit = null;

        foreach (Bundle.EntryComponent entry in bundle.Entry)
        {
            if (entry.Resource is not Observation obs)
            {
                continue;
            }

            // Observation.code.coding[].code 比對 LOINC
            string? loincCode = obs.Code?.Coding?.FirstOrDefault()?.Code;

            if (loincCode is null)
            {
                continue;
            }

            Quantity? quantity = obs.Value as Quantity;
            if (quantity is null)
            {
                continue;
            }

            if (loincCode == "8302-2")
            {
                // 身高
                if (quantity.Value.HasValue)
                {
                    heightValue = (decimal)quantity.Value.Value;
                    heightUnit = quantity.Unit ?? quantity.Code;
                }
            }
            else if (loincCode == "29463-7")
            {
                // 體重
                if (quantity.Value.HasValue)
                {
                    weightValue = (decimal)quantity.Value.Value;
                    weightUnit = quantity.Unit ?? quantity.Code;
                }
            }
        }

        patientInformation.HeightValue = heightValue?.ToString();
        patientInformation.HeightUnit = heightUnit?.ToString();
        patientInformation.WeightValue = weightValue?.ToString();
        patientInformation.WeightUnit = weightUnit?.ToString();

        patientInformation.HeightValue = patientInformation.HeightValue.ToFloat().ToString("F2");
        patientInformation.WeightValue = patientInformation.WeightValue.ToFloat().ToString("F2");

        return;
    }

    /// <summary>
    /// 取得特定病患最近一年內，門診 / 急診 / 住院相關 Encounter 的醫令清單（含醫令代碼與意義）
    /// 這裡示範抓 MedicationRequest + ServiceRequest，實際可再依需求加入 Procedure / DiagnosticReport 等。
    /// </summary>
    private async System.Threading.Tasks.Task<List<OrderItemResult>> GetLastYearOrdersAsync(SmartResponse smartResponse)
    {
        DateTimeOffset today = DateTimeOffset.UtcNow;
        DateTimeOffset oneYearAgo = today.AddYears(-30);

        HttpClient httpClient = new HttpClient
        {
            BaseAddress = new Uri(SmartAppSettingService.Data.FhirServerUrl)
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", smartResponse.AccessToken);

        FhirClientSettings settings = new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json,
        };

        FhirClient fhirClient = new FhirClient(
            SmartAppSettingService.Data.FhirServerUrl,
            httpClient,
            settings);


        // 1. 先找出該病患最近一年內的 Encounter
        // date=ge{一年前}&date=le{今天}，再加 patient=xxx
        SearchParams encounterSearch = new SearchParams()
            .Where($"patient={smartResponse.PatientId}")
            //.Where($"date=ge{oneYearAgo:O}")
            //.Where($"date=le{today:O}")
            .LimitTo(100);

        var list = encounterSearch.ToUriParamList();
        var query = string.Join("&", list.Select(p => $"{p.Item1}={p.Item2}"));
        string fullUrl = $"{SmartAppSettingService.Data.FhirServerUrl}/Encounter?{query}";
        System.Console.WriteLine($"FHIR Search URL: {fullUrl}");

        Bundle encounterBundle = await fhirClient.SearchAsync<Encounter>(encounterSearch);

        List<Encounter> encounters = new List<Encounter>();

        foreach (Bundle.EntryComponent entry in encounterBundle.Entry)
        {
            if (entry.Resource is Encounter enc)
            {
                encounters.Add(enc);
            }
        }

        // 如果有分頁，可視需要把下一頁也抓回來 (這裡簡化未處理)

        // 2. 過濾門診 / 急診 / 住院
        //    Encounter.class.code / type.coding[].code 的 mapping 會依各家實作不同，你要依實際情況調整。
        List<Encounter> filteredEncounters = encounters
            //.Where(e => IsOpdErIpdEncounter(e))
            .ToList();

        List<OrderItemResult> results = new List<OrderItemResult>();

        foreach (Encounter encounter in filteredEncounters)
        {
            string encounterId = encounter.Id;

            string encounterType = GetEncounterType(encounter);

            DateTimeOffset? start = encounter.Period?.StartElement?.ToDateTimeOffset(TimeSpan.FromHours(8));

            // 3a. 找這個 Encounter 相關的 MedicationRequest（用 encounter=EncounterId）
            SearchParams mrSearch = new SearchParams()
                .Where($"encounter={encounterId}")
                .LimitTo(100);

            Bundle mrBundle = await fhirClient.SearchAsync<MedicationRequest>(mrSearch);
            foreach (Bundle.EntryComponent entry in mrBundle.Entry)
            {
                if (entry.Resource is not MedicationRequest mr)
                {
                    continue;
                }

                // medication 是 choice type：可能是 CodeableConcept 或 ResourceReference
                // 這裡先處理成 CodeableConcept 的情境
                CodeableConcept? medConcept = mr.Medication as CodeableConcept;

                if (medConcept == null)
                {
                    // 如果是 Reference (例如 Reference reference = mr.Medication as ResourceReference)
                    // 可以在這裡再決定要不要另外 Read<Medication> 把 code 抓回來
                    continue;
                }

                Coding? coding = medConcept.Coding?.FirstOrDefault();
                if (coding is null)
                {
                    continue;
                }

                results.Add(new OrderItemResult
                {
                    EncounterId = encounterId,
                    EncounterType = encounterType,
                    EncounterStart = start,
                    OrderResourceType = "MedicationRequest",
                    OrderId = mr.Id,
                    OrderCode = coding.Code,
                    OrderDisplay = coding.Display
                });
            }

            // 3b. 找這個 Encounter 相關的 ServiceRequest
            SearchParams srSearch = new SearchParams()
                .Where($"encounter={encounterId}")
                .LimitTo(100);

            Bundle srBundle = await fhirClient.SearchAsync<ServiceRequest>(srSearch);
            foreach (Bundle.EntryComponent entry in srBundle.Entry)
            {
                if (entry.Resource is not ServiceRequest sr)
                {
                    continue;
                }

                Coding? coding = sr.Code?.Coding?.FirstOrDefault();
                if (coding is null)
                {
                    continue;
                }

                results.Add(new OrderItemResult
                {
                    EncounterId = encounterId,
                    EncounterType = encounterType,
                    EncounterStart = start,
                    OrderResourceType = "ServiceRequest",
                    OrderId = sr.Id,
                    OrderCode = coding.Code,
                    OrderDisplay = coding.Display
                });
            }
        }

        // 依就醫日期排序，越新的在前
        List<OrderItemResult> orderedResults = results
            .OrderByDescending(r => r.EncounterStart)
            .ToList();

        foreach (OrderItemResult item in orderedResults)
        {
            System.Console.WriteLine(
                $"Encounter={item.EncounterId}({item.EncounterType}) " +
                $"OrderType={item.OrderResourceType} Code={item.OrderCode} Display={item.OrderDisplay}");
        }

        return orderedResults;
    }

    /// <summary>
    /// 判斷 Encounter 是否為門診 / 急診 / 住院。
    /// 真正的判斷規則要依實際 FHIR 伺服器的 coding 慣例調整。
    /// 這裡只示意用 Encounter.Class.Code 或 Type.Coding.Code 來區分。
    /// </summary>
    public bool IsOpdErIpdEncounter(Encounter encounter)
    {
        string? cls = encounter.Class?.Code;

        // 以下的 code 只是舉例，你需要依照實際 coding 規格（如 HL7 v2, local code set）調整
        if (string.Equals(cls, "AMB", StringComparison.OrdinalIgnoreCase))
        {
            // Ambulatory / 門診
            return true;
        }

        if (string.Equals(cls, "EMER", StringComparison.OrdinalIgnoreCase))
        {
            // Emergency / 急診
            return true;
        }

        if (string.Equals(cls, "IMP", StringComparison.OrdinalIgnoreCase))
        {
            // Inpatient / 住院
            return true;
        }

        // 也可以再看 Type.Coding 裡的 code 來判斷
        if (encounter.Type != null)
        {
            foreach (CodeableConcept type in encounter.Type)
            {
                Coding? coding = type.Coding?.FirstOrDefault();
                if (coding == null)
                {
                    continue;
                }

                string? code = coding.Code;

                // 依實際系統定義調整
                if (string.Equals(code, "OPD", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(code, "ER", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(code, "IPD", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 回傳簡化後的 Encounter 類型字串（OPD/ER/IPD/...）
    /// </summary>
    public string GetEncounterType(Encounter encounter)
    {
        string? cls = encounter.Class?.Code;

        if (string.Equals(cls, "AMB", StringComparison.OrdinalIgnoreCase))
        {
            return "OPD";
        }

        if (string.Equals(cls, "EMER", StringComparison.OrdinalIgnoreCase))
        {
            return "ER";
        }

        if (string.Equals(cls, "IMP", StringComparison.OrdinalIgnoreCase))
        {
            return "IPD";
        }

        // 也可以從 Type.Coding 推斷
        if (encounter.Type != null)
        {
            foreach (CodeableConcept type in encounter.Type)
            {
                Coding? coding = type.Coding?.FirstOrDefault();
                if (coding?.Code != null)
                {
                    return coding.Code;
                }
            }
        }

        return string.Empty;
    }

    async System.Threading.Tasks.Task UpdateMessage(string message, double duration = 1)
    {
        logger.LogInformation($"存取 FHIR 資源 - {message}");

        await Notice.Open(new NotificationConfig()
        {
            Message = "存取 FHIR 資源",
            Key = Guid.NewGuid().ToString(),
            Description = $"{message}",
            NotificationType = NotificationType.Info,
            Duration = duration
        });

    }

    async System.Threading.Tasks.Task UpdateMessageError(string message)
    {
        logger.LogError($"操作上發生問題 - {message}");

        await Notice.Open(new NotificationConfig()
        {
            Message = "操作上發生問題",
            Key = Guid.NewGuid().ToString(),
            Description = $"{message}",
            NotificationType = NotificationType.Error,
        });

    }

    async System.Threading.Tasks.Task OnUploadDicomAsync(string filename)
    {
        ShowUploadDicomDialog = false;
        if (filename != null)
        {
            // 隨機產生亂數，需要為10位數
            var random = new Random();
            randomNumberKey = random.NextInt64(1_000_000_000_000, 9_999_999_999_999).ToString() + random.NextInt64(1_000_000_000_000, 9_999_999_999_999).ToString();

            imageVersion = randomNumberKey;
            string imageOldFilename = Path.GetFileName(filename.Replace(".dicm", ".png"));
            string imageNewFilename = $"{randomNumberKey}.png";
            string sourceImagePath = Path.Combine(MagicObjectHelper.DicomImagePath, imageOldFilename);
            string targetImagePath = Path.Combine(MagicObjectHelper.DicomImagePath, imageNewFilename);
            File.Copy(sourceImagePath, targetImagePath, true);
            image = Path.Combine(MagicObjectHelper.DicomWebPath, imageNewFilename);
        }
        else
        {
            return;
        }

        processModel.ActiveClass[2] = MagicObjectHelper.ActiveClassName;
        processModel.Build();
        StateHasChanged();

    }

    async System.Threading.Tasks.Task OnShowUploadDicomDialogAsync()
    {

        if (processModel.ActiveClass[1] != MagicObjectHelper.ActiveClassName)
        {
            await UpdateMessageError($"尚未輸入完成病歷號與取得病患的年紀、性別、身高與體重資訊，所以無法進行上傳 DICOM，操作失敗");
            return;
        }

        ShowUploadDicomDialog = true;
        await System.Threading.Tasks.Task.Yield();
    }

    async System.Threading.Tasks.Task OnPatientSendAsync()
    {

        if (processModel.ActiveClass[0] != MagicObjectHelper.ActiveClassName)
        {
            await UpdateMessageError($"尚未取得 Access Token 存取權杖，操作失敗");
            return;
        }

        if (string.IsNullOrEmpty(patientMrm))
        {
            await UpdateMessageError($"病歷號必須要輸入");
            return;
        }
        patientId = patientMrm;
        await OnQueryPatientAsync();
    }

    void OnDefaultPatient()
    {
        patientMrm = "3c7d3369-55b5-420b-83d3-c5dda319b9c9";
    }
    async System.Threading.Tasks.Task OnViewResultAsync()
    {

        if (processModel.ActiveClass[3] != MagicObjectHelper.ActiveClassName)
        {
            await UpdateMessageError($"尚未完成 AI 推論作業，將無法進行 查看AI分析 功能，操作失敗");
            return;
        }

        NavigationManager.NavigateTo($"/AIResult/{randomNumberKey}");
    }

    async System.Threading.Tasks.Task OnAIInferenceAsync()
    {

        if (processModel.ActiveClass[2] != MagicObjectHelper.ActiveClassName)
        {
            await UpdateMessageError($"尚未完成上傳 DICOM 檔案，所以無法進行 AI 推論作業，操作失敗");
            return;
        }

        if (string.IsNullOrEmpty(patientInformation.HeightValue) || string.IsNullOrEmpty(patientInformation.WeightValue))
        {
            await UpdateMessageError($"尚未取得病患的身高與體重資訊，所以無法進行 AI 推論作業，操作失敗");
            return;
        }

        cts.Cancel();

        cts = new CancellationTokenSource();

        #region 產生要推論的壓縮檔案

        PatientDataModel patientDataModel = new PatientDataModel()
        {
            Age = patientInformation.GetAge(),
            Gender = patientInformation.Gender.ToLower() == "Male".ToLower() ? "M" : "F",
            Height = patientInformation.GetHeight(),
            Weight = patientInformation.GetWeight(),
            Code = randomNumberKey,
        };

        string patientDataJson = JsonSerializer.Serialize<PatientDataModel>(patientDataModel);
        string passApiDataPath = Path.Combine(MagicObjectHelper.UploadDicomTempPath, $"{randomNumberKey}");
        if (Directory.Exists(passApiDataPath))
        {
            Directory.Delete(passApiDataPath, true);
        }
        Directory.CreateDirectory(passApiDataPath);
        string patientDataFilename = Path.Combine(passApiDataPath, $"PatientData.json");
        await System.IO.File.WriteAllTextAsync(patientDataFilename, patientDataJson);

        string sourceDicomPath = Path.Combine(MagicObjectHelper.UploadDicomPath, $"{SubjectNo}.dicm");
        string targetDicomPath = Path.Combine(passApiDataPath, $"L3CT.dicm");

        File.Copy(sourceDicomPath, targetDicomPath, true);

        string zipFilename = Path.Combine(MagicObjectHelper.UploadDicomTempPath, $"{randomNumberKey}.zip");
        if (File.Exists(zipFilename))
        {
            File.Delete(zipFilename);
        }
        System.IO.Compression.ZipFile.CreateFromDirectory(passApiDataPath, zipFilename);
        #endregion

        #region 準備上傳
        string InferenceHostApi = SmartAppSettingService.Data.InferenceHostApi;
        string uploadUrl = $"{InferenceHostApi}/dicompack";

        logger.LogInformation($"準備上傳至: {uploadUrl}");
        logger.LogInformation($"Zip 檔案路徑: {zipFilename}");
        logger.LogInformation($"Zip 檔案大小: {new FileInfo(zipFilename).Length} bytes");

        HttpClientHandler handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        HttpClient httpClient = new HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromMinutes(5); // 增加超時時間

        MultipartFormDataContent form = new MultipartFormDataContent();
        byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipFilename);
        ByteArrayContent byteContent = new ByteArrayContent(zipBytes);
        form.Add(byteContent, "file", $"{randomNumberKey}.zip");

        logger.LogInformation($"開始上傳，檔案大小: {zipBytes.Length} bytes");
        HttpResponseMessage response = await httpClient.PostAsync(uploadUrl, form);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = string.Empty;
            try
            {
                errorContent = await response.Content.ReadAsStringAsync();
            }
            catch
            {
                errorContent = "無法讀取錯誤內容";
            }

            logger.LogError($"上傳失敗 - HTTP Status: {(int)response.StatusCode}, 錯誤內容: {errorContent}");
            await UpdateMessageError($"上傳失敗 (HTTP {(int)response.StatusCode})，無法進行 AI 推論作業。錯誤詳情: {errorContent}");
            return;
        }

        #endregion

        string message = "請稍後，資料與影像已經送至 AI 推論系統中";
        await Notice.Open(new NotificationConfig()
        {
            Message = "AI 推論",
            Key = Guid.NewGuid().ToString(),
            Description = $"{message}",
            NotificationType = NotificationType.Success,
            Duration = 1,
        });

        System.Threading.Tasks.Task task = System.Threading.Tasks.Task.Run(async () =>
        {
            string InferenceHostApi = SmartAppSettingService.Data.InferenceHostApi;
            string checkUrl = $"{InferenceHostApi}/dicompack/CheckResult/{randomNumberKey}";
            string downloadUrl = $"{InferenceHostApi}/dicompack/Download/{randomNumberKey}";
            HttpClient httpClient = new HttpClient();

            int totalRetry = 0;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if (cts.IsCancellationRequested == true) break;
                HttpResponseMessage response = await httpClient.GetAsync(checkUrl);

                if (!response.IsSuccessStatusCode)
                {
                    await InvokeAsync(async () =>
                    {
                        string cost = $"已經等待了 {stopwatch.Elapsed.TotalSeconds} 秒";
                        await Notice.Open(new NotificationConfig()
                        {
                            Message = "確認中",
                            Key = Guid.NewGuid().ToString(),
                            Description = $"正在輪詢 AI 推論服務，確認是否已經完成 AI 推論，已經嘗試了 {++totalRetry} 次，等待了 {stopwatch.Elapsed.TotalSeconds} 秒",
                            NotificationType = NotificationType.Warning,
                            Duration = 2.5,
                        });

                        StateHasChanged();
                    });

                    await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(3));
                    continue;
                }

                string json = await response.Content.ReadAsStringAsync();

                message = "AI 推論已經完成，正在下載推論結果";

                // 用 InvokeAsync 確保在主執行緒（同步 UI context）呼叫 Notification 與 StateHasChanged
                await InvokeAsync(async () =>
                {
                    await Notice.Open(new NotificationConfig()
                    {
                        Message = "AI 推論",
                        Key = Guid.NewGuid().ToString(),
                        Description = $"{message}",
                        NotificationType = NotificationType.Success,
                        Duration = 1,
                    });

                    StateHasChanged();
                });

                // 下載推論結果
                HttpResponseMessage downloadResponse = await httpClient.GetAsync(downloadUrl);

                if (!downloadResponse.IsSuccessStatusCode)
                {
                    string errorMsg = $"下載 AI 推論結果失敗 (HTTP {(int)downloadResponse.StatusCode})";

                    await InvokeAsync(async () =>
                    {
                        await Notice.Open(new NotificationConfig()
                        {
                            Message = "AI 推論",
                            Key = Guid.NewGuid().ToString(),
                            Description = errorMsg,
                            NotificationType = NotificationType.Error,
                        });
                    });

                    break;
                }

                // 讀取 zip 位元組
                byte[] resultZipBytes = await downloadResponse.Content.ReadAsByteArrayAsync();

                // 儲存 zip 檔：放在與上傳用 zip 同一個資料夾，檔名加一個後綴
                string resultZipFilename = Path.Combine(
                    MagicObjectHelper.UploadDicomTempPath,
                    $"{randomNumberKey}_result.zip");

                await System.IO.File.WriteAllBytesAsync(resultZipFilename, resultZipBytes);

                // 解壓縮到指定資料夾 (例如 {UploadDicomTempPath}\{randomNumberKey}_result)
                string resultExtractPath = Path.Combine(
                    MagicObjectHelper.UploadDicomTempPath,
                    $"{randomNumberKey}_result");

                if (Directory.Exists(resultExtractPath))
                {
                    Directory.Delete(resultExtractPath, true);
                }
                Directory.CreateDirectory(resultExtractPath);

                System.IO.Compression.ZipFile.ExtractToDirectory(
                    resultZipFilename,
                    resultExtractPath);

                // 下載＋解壓完成通知
                await InvokeAsync(async () =>
                {
                    await Notice.Open(new NotificationConfig()
                    {
                        Message = "AI 推論",
                        Key = Guid.NewGuid().ToString(),
                        Description = "AI 推論結果已下載並解壓完成，點選 [🔍 查看結果] 即可檢視。",
                        NotificationType = NotificationType.Success,
                        Duration = 2,
                    });

                    processModel.ActiveClass[3] = MagicObjectHelper.ActiveClassName;
                    processModel.Build();
                    StateHasChanged();
                });
                break;
            }
        }
        );
    }
}
