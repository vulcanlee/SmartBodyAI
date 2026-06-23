# 第二階段測試準備清單

本專案已支援從 ASP.NET Core User Secrets 或環境變數讀取測試用 `ClientSecret`，不要把主辦單位提供的 secret 寫進 Git 追蹤檔案。

## 1. 設定測試用憑證

在專案目錄 `source/SmartBodyAI/SmartBodyAI` 執行：

```powershell
dotnet user-secrets set "SmartAppSetting:ClientId" "<主辦提供的 client_id>"
dotnet user-secrets set "SmartAppSetting:ClientSecret" "<主辦提供或重新申請的 client_secret>"
```

也可以改用環境變數：

```powershell
$env:SmartAppSetting__ClientId="<主辦提供的 client_id>"
$env:SmartAppSetting__ClientSecret="<主辦提供或重新申請的 client_secret>"
```

## 2. 測試前要向主辦確認

- SMART 授權端點 `authorize endpoint`
- Token 端點 `token endpoint`
- 採用 `Standalone Launch` 或 `EHR Launch`
- `https://localhost:7170/patient-information` 是否已加入 redirect URI 白名單

## 3. 本機演練流程

1. 啟動 `SmartBodyAI`
2. 從啟動頁發出 SMART 授權請求
3. Callback 收到 `code` 與 `state`
4. 以授權碼交換 access token
5. 使用 access token 讀取病患與觀察資料
6. 走完 DICOM 上傳與 AI 推論展示流程

## 4. 安全檢查

- 不要把 `client_secret` 寫進 `appsettings.json`
- 不要把 `client_secret` 放進簡報、截圖、Email 轉寄鏈或聊天室
- Access token 與 refresh token 不要寫進 log
- 若 secret 已外流到協作工具，建議向主辦申請更換測試憑證
