# 與原作者協作

## Fork、push 與 Pull Request 的差別

- Fork 是自己帳號底下的專案副本，你可以自行開分支與 push。
- Push 把 commit 上傳到自己的 fork；原作者的專案不會因此改變。
- Pull Request（PR）把分支的差異送給原作者審查；原作者決定是否合併。公開專案通常不需要先取得 collaborator 權限。

本次版本位於 `PadeAdmin/LETraditionalChinese` 的 `season5-support` 分支。尚未向原作者建立 PR。

## 本機日常更新

在本儲存庫目錄執行：

```powershell
git status
git add README.md Program.cs
# 依實際修改內容選擇檔案，避免把遊戲資產、暫存檔與建置輸出加入
git commit -m "Describe the actual change"
git push origin season5-support
```

`origin` 指向自己的 fork；`upstream` 指向原作者。初次 clone 沒有 upstream 時可加入：

```powershell
git remote add upstream https://github.com/aianlinb/LETraditionalChinese.git
git fetch upstream
```

合併 upstream 更新前先確認工作目錄乾淨。依賴是固定 commit 的 submodule；修改依賴需先在依賴儲存庫 commit、push，再回到主儲存庫提交新的 submodule 指標，否則其他人無法取得相同版本。

## 送給原作者審查

1. 到自己的 GitHub fork，選擇 `season5-support` 分支。
2. 選「Contribute → Open pull request」。
3. 確認 base 是 `aianlinb/LETraditionalChinese` 的 `main`，head 是 `PadeAdmin/LETraditionalChinese` 的 `season5-support`。
4. 說明適用的遊戲版本、修改原因、哪些檔案受影響、編譯方式及實機測試結果。
5. PR 建立後，同一分支後續 push 會更新這個 PR。

本版同時涉及原作者的 LEFontPatch 與 LELocalePatch，並調整依賴及目錄結構。可以先開 Draft PR 說明整體修正，請作者確認希望採用整合版本，或拆成各工具儲存庫的獨立 PR。若拆開，必須另外 fork 各工具專案並保留對應修改，不是直接把主專案的全部變更送到工具專案。

若只是想先交流，可以在原作者開啟的 Issues／Discussions 貼自己的分支連結與測試結果。PR 本身也能作為通知與討論入口，不必同時重複開 Issue。

### 可用的 PR 說明起稿

> 此修改更新 Steam 第 5 賽季 1.5.2 的繁體中文與粉圓字型支援。除了語系字典，也修正 sharedassets0、介面動態字型 bundle 及 PermaLoad 常駐字型，處理物品說明與任務介面混用字型的問題。
>
> 已完成 .NET 10 Windows x64 自包含建置、資產內容比對及唯讀字型檢查，並在實機確認地圖、物品說明及任務介面。依賴版本與原始碼修改位置列在 README。
>
> 此分支目前將兩個修補工具的來源收進 tools，解析函式庫使用固定 commit 的 submodule。如果您偏好維持原本的獨立工具架構，我可以將修改拆成對應儲存庫的 PR。

