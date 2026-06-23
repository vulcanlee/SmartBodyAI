# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.


* 每次產生出一個建置內容後，appsettings.json 內的版本編號，都要把**最後一碼（Patch）加 1**（例：`0.4.0 → 0.4.1`，不進位、不分異動性質），並且在 commit message 中說明版本編號的變更。

* 所有文件都要採用 UTF-8 繁體中文編碼，並且不能夠有亂碼存在（`docs/` 下 `.md` 須**含 BOM**，CI 以 `scripts/Test-DocsEncoding.ps1` 遞迴強制）

* 每次有異動後，要確認相關文件也要進行更新

---

## 專案文件導引（動手前必讀）

本專案的完整文件體系位於 `docs/`。**新增需求或修正錯誤前，請先閱讀文件，依其約束收斂做法，不要做出違反文件的變更。**

建議閱讀順序：

1. [`docs/README.md`](docs/README.md) — 文件總索引，依任務類型導引該讀哪些文件。
2. [`docs/guidelines/constraints.md`](docs/guidelines/constraints.md) — **不可違反的硬性約束（C-1～C-10）**，最優先。
3. [`docs/guidelines/`](docs/guidelines/) 其餘：`coding-conventions`（命名/目錄/編碼慣例）、`versioning`（版本號規則）、`contributing`（建置/測試/提交流程）。
4. 視任務參閱 [`docs/prd/`](docs/prd/)（需求）、[`docs/architecture/`](docs/architecture/)（架構與 ADR）、[`docs/reference/`](docs/reference/)（設定/API/術語）、[`docs/operations/`](docs/operations/)（部署/測試）。
5. 變更後依 [`docs/guidelines/contributing.md`](docs/guidelines/contributing.md) 同步更新 [`docs/changelog/CHANGELOG.md`](docs/changelog/CHANGELOG.md)。

若文件與程式碼矛盾，以程式碼與設定檔為事實來源，並回報修正文件。

