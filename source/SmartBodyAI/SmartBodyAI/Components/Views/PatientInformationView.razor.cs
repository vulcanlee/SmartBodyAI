using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Components;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SmartBodyAI.Components.Views;

public partial class PatientInformationView
{
    [Parameter]
    public string? Code { get; set; }
    [Parameter]
    public string? State { get; set; }
    [Inject]
    public SmartAppSettingService SmartAppSettingService { get; init; }
    [Inject]
    public OAuthStateStoreService OAuthStateStoreService { get; init; }
    [Inject]
    public NavigationManager NavigationManager { get; init; }

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

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (string.IsNullOrEmpty(Code) || string.IsNullOrEmpty(State))
            {
                StateHasChanged();
                await UpdateMessage("發現錯誤：Code 與 State 參數發現問題，無法繼續往下執行 !");
                StateHasChanged();
                await System.Threading.Tasks.Task.Delay(2000);
                NavigationManager.NavigateTo("/");
                return;
            }

            await UpdateMessage("更新取得的授權碼與狀態碼...");
            await SetAuthCodeAsync();
            await UpdateMessage("透過授權碼，取得 Access Token...");
            smartResponse = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(smartResponse.AccessToken))
            {
                await UpdateMessage("取得 Access Token 失敗，無法繼續往下執行 !");
                StateHasChanged();
                await System.Threading.Tasks.Task.Delay(2000);
                NavigationManager.NavigateTo("/");
                return;
            }
            isAccessTokenReady = true;
            await UpdateMessage($"已經取得 Access Token : {smartResponse.AccessToken}");
            //await GetLastYearOrdersAsync(smartResponse);
            StateHasChanged();
        }
    }

    async System.Threading.Tasks.Task OnChoicePatientNAsync(string n)
    {
        if (n == "1")
            patientId = "dc9335d0-bbdd-4120-8ae9-baa6604343b6";
        else if (n == "2")
            patientId = "3c7d3369-55b5-420b-83d3-c5dda319b9c9";
        else if (n == "3")
            patientId = "68cd1181-6359-4047-a77f-a165d7912480";
        else if (n == "4")
            patientId = "29d755a6-32fa-482e-8fdd-081300209df8";
        else if (n == "5")
            patientId = "fd9a5c15-8c54-4dad-9b69-9e0e5e904548";

        patientMrm = patientId;
    }

    async System.Threading.Tasks.Task OnQueryPatientAsync()
    {
        patientInformation.Reset();
        StateHasChanged();

        await UpdateMessage($"取得病患的基本資訊...");
        patientId = patientMrm;
        var found = await GetPatientAsync(smartResponse);
        if (!found)
        {
            return;
        }
        SubjectNo = patientInformation.Id;

        await UpdateMessage($"取得此病患的身高與體重...");
        await GetHeightAndWeightAsync(smartResponse);
        await UpdateMessage($"OK...");

    }

    /// <summary>
    /// 更新取得的授權碼與狀態碼
    /// </summary>
    /// <returns></returns>
    public async System.Threading.Tasks.Task SetAuthCodeAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        var SmartAppSettingModelItem = await OAuthStateStoreService.LoadAsync<SmartAppSettingModel>(State);

        if (SmartAppSettingModelItem == null)
        {
            NavigationManager.NavigateTo("/");
            return;
        }
        SmartAppSettingModelItem.AuthCode = Code;
        SmartAppSettingModelItem.State = State;

        SmartAppSettingService.UpdateSetting(SmartAppSettingModelItem);
        Console.WriteLine($"Retrive state: {SmartAppSettingService.Data.State}");
    }

    /// <summary>
    /// 透過授權碼，取得 Access Token
    /// </summary>
    public async System.Threading.Tasks.Task<SmartResponse> GetAccessTokenAsync()
    {
        SmartResponse smartResponse = new();
        Dictionary<string, string> requestValues = new Dictionary<string, string>()
            {
                { "grant_type", "authorization_code" },
                { "code", SmartAppSettingService.Data.AuthCode },
                { "redirect_uri", SmartAppSettingService.Data.RedirectUrl },
                { "launch", SmartAppSettingService.Data.Launch }
            };

        HttpRequestMessage request = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(SmartAppSettingService.Data.TokenUrl),
            Content = new FormUrlEncodedContent(requestValues),
        };

        HttpClient client = new HttpClient();

        HttpResponseMessage response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            smartResponse = new();
            return smartResponse;
        }

        string json = await response.Content.ReadAsStringAsync();

        System.Console.WriteLine($"----- Authorization Response -----");
        System.Console.WriteLine(json);
        System.Console.WriteLine($"----- Authorization Response -----");

        smartResponse = JsonSerializer.Deserialize<SmartResponse>(json);
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

    async System.Threading.Tasks.Task UpdateMessage(string message)
    {
        ProcessingMessage = message;
        await System.Threading.Tasks.Task.Delay(1000);
        StateHasChanged();
    }

    async System.Threading.Tasks.Task OnUploadDicomAsync(string filename)
    {
        if (filename != null)
        {
            imageVersion = DateTime.Now.Ticks.ToString();
            string imageFilename = Path.GetFileName(filename.Replace(".dicm", ".png"));
            image = Path.Combine(MagicObjectHelper.DicomWebPath, imageFilename);
        }
        ShowUploadDicomDialog = false;
        StateHasChanged();
    }

    async System.Threading.Tasks.Task OnShowUploadDicomDialogAsync()
    {
        ShowUploadDicomDialog = true;
        await System.Threading.Tasks.Task.Yield();
    }
}
