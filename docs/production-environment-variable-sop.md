# SmartBodyAI 正式主機環境變數設定 SOP

本文件說明如何在正式主機上為 SmartBodyAI 設定 `ClientId` 與 `ClientSecret`，並避免將敏感資訊寫入 Git、`appsettings.json` 或部署檔案。

## 適用情境

- Windows Server
- IIS 部署 ASP.NET Core
- Windows Service 啟動 ASP.NET Core
- 直接以 `dotnet SmartBodyAI.dll` 啟動程式

## 一、必要設定名稱

SmartBodyAI 目前使用 ASP.NET Core 設定繫結，`SmartAppSetting` 節點對應的正式環境變數名稱如下：

- `SmartAppSetting__ClientId`
- `SmartAppSetting__ClientSecret`

說明：

- `__` 代表 ASP.NET Core 組態中的巢狀節點
- 這兩個環境變數會覆蓋 [appsettings.json](C:/Vulcan/Github/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/appsettings.json) 中的同名欄位
- `ClientSecret` 不可寫入 Git 追蹤檔案

## 二、正式主機標準作業流程

### 步驟 1：登入正式主機

- 使用具管理員權限帳號登入 Windows Server
- 確認本次要部署的主機名稱、站台名稱、應用程式集區名稱與服務名稱

### 步驟 2：設定系統層級環境變數

以系統管理員身分開啟 PowerShell，執行：

```powershell
[System.Environment]::SetEnvironmentVariable("SmartAppSetting__ClientId", "正式ClientId", "Machine")
[System.Environment]::SetEnvironmentVariable("SmartAppSetting__ClientSecret", "正式ClientSecret", "Machine")
```

說明：

- `"Machine"` 代表寫入系統層級環境變數
- 正式環境建議使用 Machine scope，不建議用 User scope
- 請直接貼入主辦或正式環境提供的值，不要保留引號內容為樣板文字

### 步驟 3：確認是否設定成功

在 PowerShell 執行：

```powershell
[System.Environment]::GetEnvironmentVariable("SmartAppSetting__ClientId", "Machine")
[System.Environment]::GetEnvironmentVariable("SmartAppSetting__ClientSecret", "Machine")
```

預期結果：

- `ClientId` 應顯示正確值
- `ClientSecret` 應顯示正確值

注意：

- 驗證完成後不要截圖或複製 `ClientSecret` 到聊天工具
- 若有旁人協作，建議只由授權管理者操作

### 步驟 4：重新啟動應用程式宿主

環境變數更新後，必須重啟宿主程序才會生效。

## 三、IIS 部署 SOP

若 SmartBodyAI 透過 IIS 部署，請依下列步驟操作。

### 1. 設定完環境變數後重新載入 IIS

可使用以下其中一種方式。

方式 A：重啟整個 IIS

```powershell
iisreset
```

方式 B：只回收指定 Application Pool

```powershell
Import-Module WebAdministration
Restart-WebAppPool -Name "SmartBodyAIAppPool"
```

若你們有固定站台名稱，也可以手動在 IIS Manager 執行：

- 開啟 IIS Manager
- 找到對應的 Application Pool
- 點選 `Recycle`

### 2. 驗證站台是否正常啟動

- 開啟站台首頁
- 進入 SMART App 啟動流程
- 確認應用程式沒有因設定缺失而啟動失敗
- 檢查 Windows Event Viewer 或應用程式 log 是否有設定讀取錯誤

### 3. 驗證是否讀到正式設定

建議驗證方式：

- 不要輸出 `ClientSecret`
- 可透過實際 SMART OAuth 流程確認 token exchange 是否成功
- 如需除錯，只記錄 `ClientId` 或 token endpoint 是否成功呼叫，不記錄 secret 與 access token

## 四、Windows Service 部署 SOP

若 SmartBodyAI 是包成 Windows Service 執行：

### 1. 設定系統環境變數

仍使用前述 `Machine` scope 設定方式：

```powershell
[System.Environment]::SetEnvironmentVariable("SmartAppSetting__ClientId", "正式ClientId", "Machine")
[System.Environment]::SetEnvironmentVariable("SmartAppSetting__ClientSecret", "正式ClientSecret", "Machine")
```

### 2. 重新啟動服務

```powershell
Restart-Service "SmartBodyAI"
```

若不確定服務名稱，可先查詢：

```powershell
Get-Service | Where-Object { $_.DisplayName -like "*SmartBodyAI*" -or $_.Name -like "*SmartBodyAI*" }
```

### 3. 驗證服務狀態

```powershell
Get-Service "SmartBodyAI"
```

預期狀態：

- `Status` 為 `Running`

## 五、直接用 dotnet 啟動的 SOP

如果正式環境是用排程、批次檔或人工方式直接執行：

```powershell
dotnet SmartBodyAI.dll
```

請注意：

- 環境變數必須先設定在系統層級，或在啟動同一個 Shell session 中先設好
- 更新環境變數後，原本已啟動的程序不會自動吃到新值，必須關掉重開

同一個 PowerShell Session 臨時測試可用：

```powershell
$env:SmartAppSetting__ClientId="正式ClientId"
$env:SmartAppSetting__ClientSecret="正式ClientSecret"
dotnet SmartBodyAI.dll
```

注意：

- 這種方式只適合臨時測試
- 關閉 PowerShell 視窗後會失效
- 不建議作為正式長期部署方法

## 六、圖形介面操作 SOP

若不使用 PowerShell，可透過 Windows 介面設定。

### 1. 開啟環境變數設定畫面

- 按 `Win + R`
- 輸入 `SystemPropertiesAdvanced`
- 點選 `環境變數`

### 2. 新增系統變數

在「系統變數」區塊新增：

- 變數名稱：`SmartAppSetting__ClientId`
- 變數值：正式 `ClientId`

再新增：

- 變數名稱：`SmartAppSetting__ClientSecret`
- 變數值：正式 `ClientSecret`

### 3. 重新啟動宿主

- IIS：回收 App Pool 或 `iisreset`
- Windows Service：重新啟動服務
- 直接執行：關掉原程序再重開

## 七、驗證 SOP

建議依下列順序驗證。

### 1. 設定層驗證

- 確認環境變數已存在
- 確認 `ClientId` 值正確
- 確認沒有把 `ClientSecret` 寫進 `appsettings.json`

### 2. 應用程式層驗證

- 啟動 SmartBodyAI
- 進入 SMART Launch 流程
- 確認 callback 可回到 `/patient-information`
- 確認 token exchange 成功

### 3. 功能層驗證

- 確認可使用 access token 查詢 FHIR 病患資料
- 確認可繼續執行既有 AI 流程

## 八、維運注意事項

- 不要把 `ClientSecret` 存入 Git、`appsettings.Production.json`、`web.config` 或簡報檔
- 不要把 `ClientSecret` 寫進 log
- 更換 secret 時，先更新環境變數，再重啟 IIS 或服務
- 若為多台正式主機，需逐台同步設定
- 若有藍綠部署或備援主機，兩邊都要同步更新
- 若 secret 疑似外流，應立即向提供方申請新的 secret，並完成主機更新

## 九、建議的正式變更紀錄格式

每次更新憑證時，請至少留下以下資訊於內部變更紀錄：

- 變更日期時間
- 操作者
- 主機名稱
- 站台名稱或服務名稱
- 是否已完成重啟
- 是否已完成驗證

注意：

- 變更紀錄不要寫入 `ClientSecret` 明文

## 十、快速指令彙整

### 設定

```powershell
[System.Environment]::SetEnvironmentVariable("SmartAppSetting__ClientId", "正式ClientId", "Machine")
[System.Environment]::SetEnvironmentVariable("SmartAppSetting__ClientSecret", "正式ClientSecret", "Machine")
```

### 查詢

```powershell
[System.Environment]::GetEnvironmentVariable("SmartAppSetting__ClientId", "Machine")
[System.Environment]::GetEnvironmentVariable("SmartAppSetting__ClientSecret", "Machine")
```

### IIS 重啟

```powershell
iisreset
```

或

```powershell
Import-Module WebAdministration
Restart-WebAppPool -Name "SmartBodyAIAppPool"
```

### Windows Service 重啟

```powershell
Restart-Service "SmartBodyAI"
```
