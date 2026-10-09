# 最後紀元：第 5 賽季繁體中文與粉圓字型修補

這是 [aianlinb/LETraditionalChinese](https://github.com/aianlinb/LETraditionalChinese) 的非官方延伸版本，補上 Steam 第 5 賽季的繁體文字與字型修補。原作者的翻譯、工具與粉圓字型資產仍是本專案的基礎。

**已測試環境：Windows x64、Steam 遊戲版本 1.5.2（第 5 賽季）、Steam build 25764382、Unity 6000.4.8f1。** 其他遊戲版本尚未驗證；官方更新若更換資產結構、檔名或語系內容，需要重新檢查。

## 使用方式

1. 關閉遊戲，從 [Releases](https://github.com/PadeAdmin/LETraditionalChinese/releases) 下載本 fork 的發布包並完整解壓縮。
2. 保留 `LETraditionalChinese.exe` 旁的 `LETraditionalChinese` 資料夾；裡面有字典、字型與材質資料。不要把資料夾換成舊版同名 ZIP：新增的 bundle 修補需要資料夾形式。
3. 雙擊 EXE，自動尋找 Steam 遊戲位置；找不到時用 PowerShell 指定**遊戲安裝根目錄**：

```powershell
.\LETraditionalChinese.exe "E:\SteamLibrary\steamapps\common\Last Epoch"
```

4. 遊戲內選擇「簡體中文」語言，會顯示修補後的繁體文字。

**舊版升級：** 若遊戲曾套用尚未隔離聊天字型的舊版，先由 Steam 驗證遊戲檔案，再執行新版；舊版已清除部分原版字表，無法只靠重新執行補回。已使用新版隔離聊天字型的安裝，才可使用 `--refresh-fonts`。

目前既有的 2.1.1 發布包不包含此聊天隔離修正；本分支修正請依下方步驟編譯。

程式會直接替換遊戲資產。要還原，在 Steam 對遊戲執行「驗證遊戲檔案的完整性」。遊戲更新後先確認本版是否適用，再重新套用。

發布包不需要另外安裝 .NET。從原始碼編譯則需要 .NET 10 SDK。

### 唯讀檢查

```powershell
.\LETraditionalChinese.exe --check "E:\SteamLibrary\steamapps\common\Last Epoch"
```

檢查主資產的粉圓字型槽位，以及 `PermaLoad.bundle` 的 5 個粉圓字型槽位、29 個獨立原版聊天字型、聊天訊息引用及原版中文字表。這仍不等同完整畫面驗證。

已安裝包含聊天隔離修正的版本時，可只更新字型資產，不重跑文字轉換：

```powershell
.\LETraditionalChinese.exe --refresh-fonts "E:\SteamLibrary\steamapps\common\Last Epoch"
```

## 會修改哪些遊戲檔案？

以下路徑都相對於遊戲安裝根目錄。**使用者不需要手動編輯這些檔案**，由工具處理。

| 檔案 | 用途 |
| --- | --- |
| `Last Epoch_Data/StreamingAssets/aa/StandaloneWindows64/localization-string-tables-chinese(simplified)(zh)_assets_all.bundle` | 以字典替換簡體語系文字 |
| `Last Epoch_Data/StreamingAssets/aa/catalog.bin` | 處理語系 bundle 的 CRC 資訊 |
| `Last Epoch_Data/resources.assets` | 注入字型、字型圖集、材質與 fallback 設定 |
| `Last Epoch_Data/sharedassets0.assets` | 更新字型引用與字型 fallback |
| `Last Epoch_Data/StreamingAssets/LEAssetBundles/assets_a3ec63478769f648.bundle` | 替換介面使用的 NotoSansSC／NotoSansTC 動態字型資產 |
| `Last Epoch_Data/StreamingAssets/LEAssetBundles/PermaLoad.bundle` | 修補物品詳細說明、任務等常駐介面字型；保留獨立原版聊天字型並改接聊天訊息引用 |

`GameAssembly.dll` 與 IL2CPP metadata 僅供解析型別時讀取，不會修改；也不修改遊戲 EXE 或存檔。一般字型與 bundle 修補不建立還原備份；沿用的 catalog 程式在部分格式分支仍可能建立 `catalog.bin.bak`。還原方式以 Steam 驗證為準。

## 開發者：要修改哪些原始碼？

| 原始碼／資料 | 調整位置 |
| --- | --- |
| `Program.cs` | Steam 偵測、遊戲執行中檢查、語系與三階段字型修補、`--check` |
| `LETraditionalChinese/dictionary.json` | 翻譯對照表；修改用語時編輯此檔 |
| `LETraditionalChinese/fonts/`、`LETraditionalChinese/manifest.json` | 沿用原作者的字型與圖集資料；更換字型不能只換 TTF，需同步對應資料 |
| `tools/LEFontPatch/LEFontManager.cs` | `resources.assets`／`sharedassets0.assets`，字型來源更新、引用與 fallback 正規化 |
| `tools/LEFontPatch/BundleFontPatch.cs` | 指定介面 bundle 的動態字型、圖集與材質；版本變動時先查 bundle 檔名與資產 |
| `tools/LEFontPatch/PermaFontPatch.cs` | `PermaLoad.bundle` 中的字型、fallback、材質 padding／GradientScale，以及既有安裝字型來源更新；修改前複製原版聊天字型，重接 fallback／字重引用與訊息範本 |
| `tools/prepare-powder-font.py`、`tools/font-source/` | 從粉圓體原始資產與 Noto Sans TC 字型補齊中文字形，更新打包字型資料 |
| `tools/LEFontPatch/Program.cs` | 字型修補工具入口 |
| `tools/LELocalePatch/Program.cs`、`Catalog.cs` | 語系 bundle 的文字替換與 catalog CRC 處理 |
| `LETraditionalChinese.csproj`、`tools/*/*.csproj` | 建置與來源專案引用 |
| `deps/AssetsTools.NET` | 固定版 submodule；其 Cpp2IL 專案改用新版 LibCpp2IL 來源與 context API |
| `deps/Cpp2IL` | 固定 upstream commit 的 submodule，用於目前 IL2CPP metadata 解析 |

`tools/LEFontPatch` 與 `tools/LELocalePatch` 收錄各自原作者程式的修改版與授權。舊版預編譯 `lib/` DLL 已不參與建置。

### 取得與編譯

```powershell
git clone --recurse-submodules --branch season5-support https://github.com/PadeAdmin/LETraditionalChinese.git
cd LETraditionalChinese
dotnet publish .\LETraditionalChinese.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:TargetFrameworks=net10.0 -p:DebugType=embedded -o .\artifacts\publish
Copy-Item .\licenses .\artifacts\publish\licenses -Recurse -Force
Copy-Item .\README.md .\artifacts\publish\README.md
Compress-Archive -Path .\artifacts\publish\* -DestinationPath .\artifacts\LETraditionalChinese-Season5.zip -Force
```

已有 checkout 時先執行 `git submodule update --init --recursive`。請使用儲存庫固定的 submodule commit；不要直接更新到各依賴的最新分支。`TargetFrameworks=net10.0` 用於限制依賴的多目標建置。

### 翻譯與字型說明

- 原作者字典來自 Azure AI Translator，並經原作者程式及部分人工調整。本版保留此基礎，新增／整理第 5 賽季文字，使用 OpenCC `s2tw` 做繁體轉換；新增內容並非全面人工校對。
- 字典包含本次語系的對照與已轉換文字的自身映射。官方新增且未收進字典的文字，仍可能顯示簡體。
- 以原作者粉圓體（jf-openhuninn 2.1）資產為主，統一中文 fallback，修正原本仍使用襯線字的介面。
- 粉圓體保留原有字形；原字庫缺少的中文字形補入 Noto Sans TC，涵蓋該字型提供的 18,043 個中日韓字碼。此補字僅用於遊戲介面，不作為世界聊天的顯示方案。修改後字型內部名稱為 `LE Chinese Rounded`；遊戲資產名稱保留原有形式以維持工具相容。
- 世界聊天保留遊戲原版字型與字圖，與任務、物品介面的粉圓體分開：在修改任何常駐字型前複製 29 個原版 TMP 字型，重接內部 fallback／字重引用，讓聊天訊息範本引用獨立的原版 Caladea 與 Noto Sans SC。原版中文字表保留 5,046 筆；玩家訊息不經繁簡轉換。
- 字型圖集與材質沿用原版引用；新增聊天 TMP 字型時保留正確 script/type 索引，避免新增成無型別資訊的 MonoBehaviour。處理兩份內部同名的 bundle 必須使用不同 `AssetsManager`；整合版直接在修改前取得原版快照，避免載入快取混用。
- 執行 `python tools/prepare-powder-font.py` 可重建打包字型；需要 Python `fontTools`。一般 .NET 建置不需要執行此步驟。

### 已完成驗證

- 建置及 Windows x64 自包含發布。
- 本次 23 個語系表共 66,443 筆資料的輸出對照，確認條目 ID 保留。
- 修補 bundle 重新開啟，比對預期替換資產及未修改資產內容。
- `--check` 驗證主字型及常駐 bundle 字型槽位。
- 字型產生腳本確認粉圓體新增 8,631 個 Noto Sans TC 字形，CJK 覆蓋 18,043 個字碼。
- 原版資產修補後重新開啟 bundle，核對 35 個新增資產、預定替換資產、未修改資產雜湊，以及聊天引用；重新套用保留聊天資產。
- 使用者實機確認世界聊天、地圖、物品詳細說明及任務介面同時顯示正常。檔案檢查通過不代表畫面已驗證。

## 授權與來源

主程式與修補工具依原專案 MIT 授權；粉圓體與 Noto Sans TC 字形依 SIL Open Font License。各專案及字型授權見 `licenses/`，submodule 亦保留原始授權。發布包必須包含相關授權文件。

- [LETraditionalChinese](https://github.com/aianlinb/LETraditionalChinese)
- [LEFontPatch](https://github.com/aianlinb/LEFontPatch)
- [LELocalePatch](https://github.com/aianlinb/LELocalePatch)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)
- [粉圓體](https://github.com/justfont/open-huninn-font)
- [Noto Sans TC](https://github.com/google/fonts/tree/main/ofl/notosanstc)

本儲存庫與發布包不包含遊戲原始資產。問題請在本 fork 回報，避免將尚未合併的延伸版本問題直接報給原作者。

## 協作

Fork、push 與向原作者提出 PR 的步驟見 [CONTRIBUTING.md](CONTRIBUTING.md)。
