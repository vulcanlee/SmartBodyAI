# 編碼與命名慣例（Coding Conventions）

本文件說明 SmartBodyAI 既有的命名、目錄與編碼慣例。新增程式碼時請**沿用既有風格**，不要引入不一致的命名或結構。

---

## 一、命名慣例

| 類型 | 後綴 / 規則 | 範例 | 位置 |
|------|------------|------|------|
| 業務邏輯服務 | `*Service`（介面以 `I` 開頭） | `SmartAuthorizationService`、`HealthCheckService`、`ISmartDiscoveryService` | `Services/` |
| 基礎設施服務 | `*Service`（放在 Servicers 目錄） | `OAuthStateStoreService`、`DicomService`、`SettingService`、`SmartAppSettingService` | `Servicers/` |
| 資料模型 | `*Model` | `SettingModel`、`SmartAppSettingModel`、`PatientInformationModel` | `Models/` |
| 結果 / 驗證物件 | `*Result` | `SmartCallbackValidationResult`、`HealthIndicatorResult`、`VitalSignsResult` | `Models/` |
| 靜態輔助類 | `*Helper` | `PkceHelper`、`MagicObjectHelper` | `Helpers/` |
| 完整路由頁面 | `*Page.razor` | `HealthCheckPage`、`SmartChecklistPage`、`PatientInformationPage` | `Components/Pages/` |
| 可復用視圖元件 | `*View.razor`（邏輯放 `.razor.cs`） | `LaunchView`、`PatientInformationView`、`AIResultView` | `Components/Views/` |

> 注意：`Services/`（業務邏輯）與 `Servicers/`（基礎設施服務）是兩個不同目錄，命名後綴皆為 `Service`，請依職責放對目錄。

---

## 二、目錄結構約束（SmartBodyAI 前端）

```
source/SmartBodyAI/SmartBodyAI/
├── Components/
│   ├── Pages/        # 完整路由頁面（*.razor），對應一個 URL
│   ├── Views/        # 可復用視圖元件（邏輯抽到 *.razor.cs）
│   ├── Commons/      # 通用對話框與複合元件
│   └── Layout/       # 版面配置（MainLayout、NavMenu 等）
├── Services/         # SMART、Health Check、Checklist 等業務邏輯
├── Servicers/        # OAuth state、DICOM、設定等基礎設施服務
├── Models/           # 資料模型與 DTO
├── Helpers/          # 靜態工具類
├── wwwroot/          # 靜態資產（含 page9.html 透明性頁）
└── appsettings.json  # 應用設定
```

- **Pages vs Views**：有路由（`@page`）的放 `Pages/`；被頁面組合、可重用、不直接對應 URL 的放 `Views/`，並把 C# 邏輯抽到 `*.razor.cs`（code-behind）。
- 新增服務時，先判斷屬「業務邏輯」還是「基礎設施」，放對應目錄並於 `Program.cs` 註冊 DI。

---

## 三、文件編碼慣例

- **所有文件**：UTF-8 繁體中文，不得有亂碼。
- **`docs/` 下的 `.md`**：必須**含 BOM**（見 [`constraints.md` C-3](constraints.md)）。
- 程式碼註解以繁體中文為主，既有英文 XML doc 註解可保留。

---

## 四、撰寫程式碼時的行為準則

依 `CLAUDE.md`，務必遵守：
1. **先思考再編碼**：不確定就先問，不靜默猜測。
2. **簡單優先**：只寫解決問題所需的最少程式碼，不做未要求的抽象或彈性。
3. **精準修改**：只動該動的，匹配既有風格，不順手「改善」無關程式碼。
4. **目標驅動**：把任務轉成可驗證的成功條件，迴圈到驗證通過。
