# Multilingual 多语言模块

## 用途

提供全局的文本翻译能力：

- **没有玩家手动选择时，自动跟随系统语言**（`Application.systemLanguage`）；
- 玩家手动切换后，用 `PlayerPrefs` 记住选择；没有手动选择过则每次启动重新按系统语言匹配；
- 内置语言包随包发布，自定义语言包可在运行时添加并持久化；
- 视图只通过 `IMultilingualManager` 取文本，不直接访问数据中心。

模块是纯逻辑模块（普通 C# 类），不需要挂到场景。

---

## 目录结构

```
Assets/FModa/Modules/Multilingual/
├── MultilingualManager.cs          ← 模块实现（IMultilingualManager + IModule）
├── Interface/
│   └── IMultilingualManager.cs     ← 对外契约
├── Data/
│   ├── LanguageData.cs             ← LanguageData / LanguagePackJson / LanguageInfo
│   └── MultilingualDataCenter.cs   ← 语种数据中心（code → 数据）
├── Resources/
│   └── Multilingual/
│       ├── en.json                 ← 内置语言包
│       ├── zh-CN.json
│       ├── zh-TW.json
│       ├── ja.json
│       └── ko.json
├── Example/
│   └── LocalizedText.cs            ← TMP 文本绑定示例
└── Doc/
    └── README.md
```

`MultilingualDataCenter` 已在 `Assets/FModa/Architecture/Data/DataManager.cs` 注册：

```csharp
public static MultilingualDataCenter Multilingual => _multilingual ??= new MultilingualDataCenter();
```

---

## 启动方式

由 Flow 创建并初始化。示例 `Assets/Example/Scripts/Flows/ExampleFlow.cs`：

```csharp
using FModa.Multilingual;

var multilingual = GF.GetModule<MultilingualManager>();
multilingual.Init();
```

- `GF.GetModule<MultilingualManager>()` 不存在时自动创建并注册。
- `Init()` 加载语言包、选择当前语种；重复调用幂等。
- 如果忘记调用 `Init()`，第一次访问 `GetText` / `CurrentLanguageCode` 等 API 时会懒初始化。
- 多语言是全局基础服务，通常**不随 Flow 退出而移除**；需要移除时调用 `GF.RemoveModule<MultilingualManager>()`。

如果要在初始化前改配置，必须在 `Init()` 之前设置：

```csharp
var multilingual = GF.GetModule<MultilingualManager>();
multilingual.AutoDetectSystemLanguage = false; // 关闭系统语言自动检测
multilingual.DefaultLanguageCode = "en";      // 兜底语种
multilingual.Init();
```

---

## 如何选择当前语种

`Init()` 里的选择顺序：

1. `PlayerPrefs` 中保存的玩家手动选择（key：`FModa.Multilingual.Language`，只有 `SetLanguage` 会写入）；
2. 若没有手动选择，且 `AutoDetectSystemLanguage == true`：按 `Application.systemLanguage` 匹配；
3. `DefaultLanguageCode`（默认 `en`）；
4. 第一个可用语种（按 code 排序）。

自动选择**不会**写入 `PlayerPrefs`，所以玩家没有手动切换过时，每次启动都会重新按系统语言计算；游戏更新新增语言包、或玩家修改系统语言后都能自动跟随。玩家一旦调用 `SetLanguage`，后续启动就以保存值为准。

系统语言映射示例：

| SystemLanguage | 语种 code |
|---|---|
| `ChineseSimplified` / `Chinese` | `zh-CN` |
| `ChineseTraditional` | `zh-TW` |
| `English` | `en` |
| `Japanese` | `ja` |
| `Korean` | `ko` |
| `French` / `German` / `Spanish` / `Portuguese` / `Russian` … | `fr` / `de` / `es` / `pt` / `ru` … |

只有真正存在的语言包才会被选中。例如系统是 `ja`，但项目里没有 `ja.json`，就会回退到 `en`。

### 语种代码归一化

`NormalizeCode` 会统一写法：

- 下划线转连字符：`zh_CN` → `zh-CN`；
- 语言小写、地区大写：`EN-us` → `en-US`；
- 中文特殊处理：
  - `zh` / `zh-Hans` / `zh-CN` / `zh-SG` → `zh-CN`
  - `zh-Hant` / `zh-TW` / `zh-HK` / `zh-MO` → `zh-TW`

匹配时先找完整 code，再找语言前缀（`en-US` → `en`）。

---

## 语言包 JSON 格式

```json
{
  "code": "en",
  "displayName": "English",
  "entries": [
    { "key": "common.yes", "value": "Yes" },
    { "key": "ui.start", "value": "Start Game" },
    { "key": "hud.score", "value": "Score: {0}" }
  ]
}
```

说明：

- `code` 必填；缺失时，内置包用文件名、自定义文件用文件名作为 code。
- `displayName` 缺失时使用 code。
- `entries` 是键值对数组。`JsonUtility` 不支持 `Dictionary`，所以不要写成对象。
- 不要写注释、尾逗号，`JsonUtility` 不接受。
- 带占位符的文本用 `string.Format` 风格，通过 `GetText(key, args)` 填充。

---

## 内置语言包

把 JSON 放到：

```
Assets/FModa/Modules/Multilingual/Resources/Multilingual/
```

`Init()` 使用 `Resources.LoadAll<TextAsset>("Multilingual")` 加载该目录下所有 JSON。

当前预置语种：`en`、`zh-CN`、`zh-TW`、`ja`、`ko`。

新增一门内置语言只需要加文件，例如 `ja.json`：

```json
{
  "code": "ja",
  "displayName": "日本語",
  "entries": [
    { "key": "common.yes", "value": "はい" },
    { "key": "ui.start", "value": "ゲーム開始" }
  ]
}
```

内置包在每次启动时重新加载，运行时对内置语言的修改不会写回 Resources。

---

## 自定义语言（运行时添加）

### 方式一：用字典添加

```csharp
using System.Collections.Generic;
using FModa.Multilingual;

var multilingual = GF.GetModule<MultilingualManager>();
multilingual.Init();

var entries = new Dictionary<string, string>
{
    ["common.yes"] = "Aye",
    ["ui.start"] = "Start Game",
};

// persist = true：写入 persistentDataPath/Multilingual/en-GB.json，下次启动自动加载
multilingual.AddLanguage("en-GB", "English (UK)", entries, persist: true);
multilingual.SetLanguage("en-GB");
```

### 方式二：用 JSON 添加

```csharp
var json = @"
{
  ""code"": ""fr"",
  ""displayName"": ""Français"",
  ""entries"": [
    { ""key"": ""common.yes"", ""value"": ""Oui"" },
    { ""key"": ""ui.start"", ""value"": ""Jouer"" }
  ]
}";

multilingual.AddLanguageFromJson(json, persist: true);
multilingual.SetLanguage("fr");
```

### 持久化位置

`persist: true` 时写入：

```
Application.persistentDataPath/Multilingual/<归一化后的 code>.json
```

`Init()` 会扫描该目录并加载所有 `*.json`。自定义包在内置包之后加载，**同 code、同 key 会覆盖内置条目**，未覆盖的条目保留。

> `persist: true` 写出的是该语种**累计的自定义条目**（运行时添加或覆盖的 key），不会把内置语言包的内容复制进存档。下次启动时先加载内置包，再叠加这些自定义条目。

### 覆盖与删除

- `AddLanguage` / `AddLanguageFromJson` 对已存在的 code 执行合并覆盖。
- `RemoveLanguage(code)` 只移除当前会话中的数据。
- 如果移除的是当前语种，且 `PlayerPrefs` 保存的正是它，保存键会被清除；下次启动重新自动选择。
- `RemoveLanguage(code, deletePersisted: true)` 同时删除 `persistentDataPath` 下的自定义文件。
- 内置语言的移除只在本会话有效，下次启动会重新加载。
- 不允许移除最后一个可用语种。

---

## 对外 API

接口：`FModa.Multilingual.IMultilingualManager`

| 成员 | 说明 |
|---|---|
| `event Action<string,string> OnLanguageChanged` | 当前语种变化：(旧 code, 新 code)。当前语种条目被覆盖时会以 `(code, code)` 触发一次，用于刷新文案 |
| `event Action<string> OnLanguageAdded` | 新增或合并语种后触发 |
| `event Action<string> OnLanguageRemoved` | 移除语种后触发 |
| `string CurrentLanguageCode` | 当前语种代码 |
| `string DefaultLanguageCode { get; set; }` | 兜底语种，默认 `en` |
| `bool AutoDetectSystemLanguage { get; set; }` | 没有手动选择时是否跟随系统语言 |
| `void Init()` | 加载语言包并选择语种 |
| `IReadOnlyList<LanguageInfo> GetAvailableLanguages()` | 设置界面用的语种列表 |
| `bool HasLanguage(string code)` | 是否存在该语种 |
| `bool SetLanguage(string code)` | 切换语种，成功返回 true |
| `string GetText(string key)` | 取文本，缺失时返回 key 并只警告一次 |
| `string GetText(string key, params object[] args)` | 取文本并 `string.Format` |
| `bool HasKey(string key)` | 当前或兜底语种是否有该 key |
| `bool AddLanguage(...)` | 添加/覆盖语种，可选持久化 |
| `bool AddLanguageFromJson(string json, bool persist = false)` | 从 JSON 添加 |
| `bool RemoveLanguage(string code, bool deletePersisted = false)` | 移除语种 |

### 文本回退链

```
当前语种[key] → DefaultLanguageCode[key] → key 本身
```

例如当前是 `en-GB`，但 `en-GB` 没有 `hud.hp`，会去 `en` 找；再找不到就返回 `"hud.hp"`，并输出一次警告。

---

## 视图使用

视图只拿模块，不碰 `DataManager`：

```csharp
using FModa.Multilingual;
using TMPro;
using UnityEngine;

public class StartButtonLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private IMultilingualManager multilingual;

    private void OnEnable()
    {
        multilingual = GF.GetModule<MultilingualManager>();
        multilingual.Init();
        multilingual.OnLanguageChanged += HandleLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        multilingual.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(string oldCode, string newCode)
    {
        Refresh();
    }

    private void Refresh()
    {
        label.text = multilingual.GetText("ui.start");
    }
}
```

- 订阅必须用命名方法，并在 `OnDisable` / `OnUnbind` 成对退订。
- 初始语种在 `OnEnable` / `OnBind` 里主动 `GetText` 同步一次；`OnLanguageChanged` 不会为初始选择补发（模块在 Flow 里已经初始化完成）。
- 设置界面可以用 `GetAvailableLanguages()` 生成列表，选中后调用 `SetLanguage(code)`。
- 完整示例见 `Example/LocalizedText.cs`。

---

## 注意事项

- **线程**：语言包加载、`PlayerPrefs`、与 UI 相关的调用都应在主线程执行。
- **同步加载**：`Init()` 同步读取 Resources 和 `persistentDataPath`。语言包很大时可能造成启动卡顿；需要异步/远程下发时，应在不破坏接口的前提下替换加载策略。
- **Key 缺失**：返回 key 本身并只警告一次。发布前建议做一次 key 完整性检查。
- **格式化**：`GetText(key, args)` 使用 `string.Format`。文本里的字面量花括号要写成 `{{` / `}}`。
- **不要缓存 `DataManager.Multilingual`**：`DataManager.PreWarm()` 会替换实例，模块内部每次都用属性取。
- **模块可移植性**：除框架的 `IModule` / `GF` / `DataManager` 外，本模块不引用其他业务模块。`Example/LocalizedText.cs` 只是示例，接入其他项目时可以删除。
- **保存键**：`PlayerPrefs` key 为 `FModa.Multilingual.Language`。只有调用 `SetLanguage` 才会写入；删除它可以恢复“每次启动自动跟随系统语言”。
- **语种 code**：会作为持久化文件名使用，不能包含路径分隔符或非法文件名字符；非法 code 会被 `AddLanguage` 拒绝。
- **自动选择 vs 手动选择**：没有保存键时，每次启动都会重新按系统语言选择；调用 `SetLanguage` 后以保存值为准。想恢复自动跟随，删除保存键。

---

## 验证

1. Unity 2022.3.38f1 打开项目，Console 无编译错误。
2. 进入 Play，确认日志中 `Multilingual` 没有“没有语言包”的错误。
3. 删除 `PlayerPrefs` 中的 `FModa.Multilingual.Language`，把系统语言切到中文/英文/日文，启动应选中对应语种；不支持的语种回退 `en`。再次启动仍应重新按系统语言选择。
4. 手动 `SetLanguage("zh-TW")` 后重启，应保持 `zh-TW`。
5. 调用 `AddLanguage(..., persist: true)`，确认 `persistentDataPath/Multilingual/<code>.json` 生成；重启后语种仍可用。
6. 用 `LocalizedText` 挂到 TMP 文本上，切换语种后文本应立即刷新。
7. 移除当前语种，应自动切到兜底/第一个可用语种，并且不会移除最后一个语种。
