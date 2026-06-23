# 版本號規則（Versioning）

本文件說明 SmartBodyAI 的版本號格式與遞增規則。這是 [`constraints.md` C-2](constraints.md) 的詳細版。

---

## 一、版本號格式

```
主版號.次版號.修補號 (日期)
例：1.2.36 (2026/04/06)
```

- 版本值定義於 `source/SmartBodyAI/SmartBodyAI/appsettings.json` 的
  `SmartAppSetting:SystemInformation:SystemVersion`。
- 此版本會顯示於系統資訊與健康檢查頁。

---

## 二、遞增規則

- **每次產生一個建置內容，修補號（Patch，最後一碼）加 1**。
  - 例：`1.2.36 → 1.2.37 → 1.2.38`。
  - **不進位**（37、38…一直累加，不因 +1 而往次版號進位）。
  - **不分異動性質**：無論是新功能、修正或回退，都一律 Patch +1（回退版本號為例外情況，見下）。
- 同時在 **commit message** 中說明版本編號的變更。
- **日期**更新為建置當日。

> 例外：歷史上曾有「回退 SystemVersion」的 commit（見 `changelog/CHANGELOG.md`）。若因故需回退版本，須在 commit message 與 CHANGELOG 明確標註原因。

---

## 三、與其他項目的對應

| 項目 | 關係 |
|------|------|
| `appsettings.json` 的 `SystemVersion` | 版本號的**唯一事實來源** |
| `changelog/CHANGELOG.md` | 每個版本一個區段，版本號與日期須與 appsettings 一致 |
| commit message | 須說明本次版本號變更（例：`... 版本號 1.2.36 → 1.2.37`） |
| 健康檢查頁 / 系統資訊 | 讀取 `SystemVersion` 顯示 |

---

## 四、發版檢查清單

提交建置前確認：

- [ ] `appsettings.json` 的 `SystemVersion` Patch 已 +1、日期已更新
- [ ] `changelog/CHANGELOG.md` 已新增對應版本區段
- [ ] commit message 已說明版本變更
- [ ] 受影響文件已同步（見 [`constraints.md` C-4](constraints.md)）
- [ ] `pwsh scripts/Test-DocsEncoding.ps1` 通過
