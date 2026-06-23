# 開發流程（Contributing）

本文件說明 SmartBodyAI 的建置、測試、提交與文件同步流程。動手前請先讀 [`constraints.md`](constraints.md)。

---

## 一、需求環境

- .NET SDK 10.0
- Windows 開發環境
- 可用的 SMART on FHIR sandbox 或測試環境
- 可寫入的本機暫存目錄（預設 `C:\temp\SmartBodyAI\...`）
- 若要完整驗證 DICOM / AI 流程：需備妥外部 `CTMS` 相依專案（見 [`constraints.md` C-10](constraints.md)）

---

## 二、建置與執行

專案由兩個獨立應用組成，分別進入目錄執行：

```powershell
# 後端 API（先啟動）
cd source/SmartAgentApi/SmartAgentApi
dotnet run        # https://localhost:7274

# 前端主站（後啟動）
cd source/SmartBodyAI/SmartBodyAI
dotnet run        # https://localhost:7170
```

**啟動順序（重要）**：
1. 先啟動 `SmartAgentApi`
2. 再啟動 `SmartBodyAI`
3. 從 SMART launcher 或本機首頁開始授權流程
4. 完成 callback 後驗證病患資料讀取與 DICOM／AI 流程

---

## 三、測試

```powershell
dotnet test source/SmartBodyAI/SmartBodyAI.Tests/SmartBodyAI.Tests.csproj
```

測試專案（xUnit）涵蓋 SMART discovery、授權流程與 checklist 相關邏輯。新增功能時應補對應單元測試。

### 建議手動驗證流程
- SMART 啟動與授權流程可正常完成
- callback 後可成功取得 access token
- 可讀取病患資料與相關 FHIR 資源
- `/health-check` 可顯示 SMART readiness 狀態
- `/smart-checklist` 可提供人工檢查依據
- DICOM ZIP 可上傳並完成 AI 結果查詢與下載

---

## 四、文件編碼檢驗

提交文件前必須通過編碼檢驗（見 [`constraints.md` C-3](constraints.md)）：

```powershell
pwsh scripts/Test-DocsEncoding.ps1
```

所有 `docs/**/*.md` 須回報 `OK BOM=True ReplacementCharacter=False`。

---

## 五、提交（Commit）流程

1. 依 [`versioning.md`](versioning.md) 更新 `appsettings.json` 版本號（Patch +1）。
2. 同步更新受影響文件（`reference/`、`architecture/`、`changelog/CHANGELOG.md`）。
3. commit message 須說明：做了什麼 + 版本號變更。
4. 通過編碼檢驗與測試後再提交。
5. 機密（ClientSecret 等）不得進版控。

> 分支與 push 策略依團隊規範；不要在未確認下對 `main` 直接強推。

---

## 六、提交後的文件同步（CHANGELOG）

依使用者既定流程：**commit 之後**，再回頭同步更新 `changelog/CHANGELOG.md`，把本次異動補入對應版本區段。CHANGELOG 採 Keep a Changelog 格式，分類為「新增 / 變更 / 修正 / 回退」。
