# ADR 0003：OAuth State 採 10 分鐘 TTL

- **狀態**：已採用

## 背景

OAuth2 授權需以 `state` 參數防止 CSRF。State 須在發起授權時產生並保存，於 callback 時比對。保存需設定存活時效，過短會讓正常授權逾時失敗，過長則擴大重放攻擊時間窗。

## 決策

- 以 `Guid.NewGuid().ToString("N")` 產生唯一 state。
- 透過 `OAuthStateStoreService` 存入 `IDistributedCache`，**TTL 設為 10 分鐘**。
- callback 時嚴格比對 state，不符即視為失敗。
- 流程結束（成功或任一失敗分支）皆呼叫 `RemoveAsync` 清除 state。

## 後果

- 兼顧安全與可用性：10 分鐘足以完成正常授權，又限制重放窗口。
- State 儲存依賴 `IDistributedCache`（目前為記憶體分散式快取）。

## 對應

- `source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs:52`（`TimeSpan.FromMinutes(10)`）及多處 `RemoveAsync`
- `source/SmartBodyAI/SmartBodyAI/Servicers/OAuthStateStoreService.cs`
- 約束：[`../../guidelines/constraints.md`](../../guidelines/constraints.md) C-6
