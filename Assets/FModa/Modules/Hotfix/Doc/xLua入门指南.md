# xLua 入门指南

> 面向 Unity 开发者的 xLua 快速上手文档，帮助你理解如何在 ECMVC 框架中使用 xLua 进行 Lua 编程和热更新。

---

## 目录

1. [xLua 是什么](#1-xlua-是什么)
2. [Lua 语言速览（对比 C#）](#2-lua-语言速览对比-c)
3. [环境配置](#3-环境配置)
4. [Lua 文件加载方式](#4-lua-文件加载方式)
5. [C# 访问 Lua](#5-c-访问-lua)
6. [Lua 调用 C#](#6-lua-调用-c)
7. [热更新（Hotfix）](#7-热更新hotfix)
8. [配置系统](#8-配置系统)
9. [常见问题与陷阱](#9-常见问题与陷阱)
10. [项目实战：ECMVC 中的热更新流程](#10-项目实战ecmvc-中的热更新流程)

---

## 1. xLua 是什么

**xLua** 是腾讯开源的一个 Unity 下 Lua 编程解决方案，它让 Lua 和 C# 之间可以**双向无缝调用**。

### 核心能力

| 能力 | 说明 |
|------|------|
| **Lua 调用 C#** | 在 Lua 代码中创建 C# 对象、调用方法和属性 |
| **C# 调用 Lua** | 在 C# 中获取 Lua 变量、调用 Lua 函数 |
| **热更新 (Hotfix)** | 用 Lua 函数替换已编译的 C# 方法，无需重新打包 |
| **代码生成** | 生成适配代码获得高性能，也支持反射降级方案 |

### 在本项目中的角色

ECMVC 使用 xLua 实现**游戏逻辑热更新**。当需要修复线上 bug 或调整数值时，只需编写 Lua 补丁脚本下发到客户端，无需通过应用商店重新发布版本。

---

## 2. Lua 语言速览（对比 C#）

如果熟悉 C#，Lua 上手非常快。以下是核心差异速览。

### 2.1 基础语法对比

| 特性 | C# | Lua |
|------|-----|-----|
| 变量声明 | `int x = 10;` | `local x = 10` |
| 字符串 | `"hello"` | `'hello'` 或 `"hello"` |
| 字符串拼接 | `"a" + "b"` | `'a' .. 'b'` |
| 布尔值 | `true` / `false` | `true` / `false` |
| 条件判断 | `if (x > 0) { }` | `if x > 0 then end` |
| 循环 | `for (int i = 0; i < n; i++)` | `for i = 0, n-1 do end` |
| 函数 | `int Add(int a, int b) { ... }` | `function add(a, b) return a + b end` |
| 注释 | `// 单行` / `/* 多行 */` | `-- 单行` / `--[[ 多行 ]]` |
| 无值 | `null` | `nil` |
| 不等 | `!=` | `~=` |
| 与或非 | `&&` / `\|\|` / `!` | `and` / `or` / `not` |

### 2.2 变量

**默认是全局变量**，用 `local` 声明局部变量：

```lua
-- 全局变量（不推荐滥用）
score = 100

-- 局部变量（推荐）
local name = 'ECMVC'
local hp = 200
```

### 2.3 表（Table）—— Lua 唯一的数据结构

Table 既是数组也是字典，是 Lua 最核心的概念：

```lua
-- 作为数组使用（索引从 1 开始！）
local arr = {10, 20, 30}
print(arr[1])  --> 10（不是 arr[0]）

-- 作为字典使用
local player = {
    name = 'Alice',
    hp = 100,
    level = 5
}
print(player.name)   --> Alice
print(player['hp'])  --> 100（等价写法）

-- 混合
local mixed = {
    'first',           -- [1] = 'first'
    'second',          -- [2] = 'second'
    key = 'value',     -- 字符串键
    [100] = 'hundred'  -- 数字键
}
```

> ⚠️ **给 C# 开发者**：Lua 数组从 `1` 开始索引，不是 `0`。

### 2.4 函数

函数是一等公民，可以赋值给变量、作为参数传递：

```lua
-- 基本函数
function greet(name)
    return 'Hello, ' .. name
end

-- 匿名函数
local add = function(a, b)
    return a + b
end

-- 多返回值
function divide(a, b)
    return a // b, a % b  -- 返回商和余数
end
local q, r = divide(10, 3)  -- q=3, r=1
```

### 2.5 控制流

```lua
-- if 语句（注意 then 和 end）
if score >= 100 then
    print('Excellent!')
elseif score >= 60 then
    print('Pass')
else
    print('Fail')
end

-- for 循环
for i = 1, 10 do
    print(i)
end

-- 遍历 table
for k, v in pairs(player) do
    print(k, v)
end

-- 遍历数组
for i, v in ipairs(arr) do
    print(i, v)  -- i 从 1 开始
end

-- while 循环
local i = 1
while i <= 10 do
    print(i)
    i = i + 1
end
```

---

## 3. 环境配置

### 3.1 添加宏定义

在 Unity 的 `File → Build Settings → Player Settings → Scripting Define Symbols` 中添加：

```
HOTFIX_ENABLE
```

> 💡 建议开发时**平时不打开**此宏，只在需要构建手机版本或调试热更补丁时再开启。

### 3.2 生成代码和注入

| 步骤 | 菜单 | 说明 |
|------|------|------|
| 1 | `XLua → Generate Code` | 生成 C# ↔ Lua 桥接代码 |
| 2 | `XLua → Hotfix Inject In Editor` | 注入 hotfix 桩到 C# 程序集 |

每次修改了被 `[Hotfix]` 标记的类后，都需要重新执行第 2 步。成功后控制台会打印 `hotfix inject finish!`。

### 3.3 全局 LuaEnv 管理

本项目通过 `LuaLoad` 类管理 `LuaEnv` 生命周期：

- `LuaLoad` 在 `HotfixManager.Awake()` 中被创建
- 在 `HotfixManager.OnDestroy()` 中被释放
- `SetupLuaEnvironment()` 注册自定义加载器并加载 `main.lua`

---

## 4. Lua 文件加载方式

### 4.1 执行字符串（不推荐用于生产）

```csharp
luaenv.DoString("print('hello world')");
```

### 4.2 使用 require 加载文件（推荐）

```csharp
luaenv.DoString("require 'main'");
```

然后在 `main.lua` 中加载其他模块。**整个程序只应有一个 DoString**，类似 lua 命令行的 `lua main.lua`。

### 4.3 自定义 Loader（支持热更）

本项目通过 `LuaLoad.SetupLuaEnvironment()` 注册了自定义加载器，它将 `require('foo.bar')` 映射到文件系统中 `lua/foo/bar.lua`：

```csharp
luaEnv.AddLoader((ref string filepath) =>
{
    string fileName = filepath.Replace('.', '/') + ".lua";
    string localPath = Path.Combine(persistentPath, "lua", fileName);
    return File.Exists(localPath) ? File.ReadAllBytes(localPath) : null;
});
```

这意味着你可以从服务器下载 Lua 文件到 `Application.persistentDataPath/lua/`，然后通过 `require` 加载。

---

## 5. C# 访问 Lua

通过 `LuaEnv.Global.Get<T>()` 获取 Lua 全局变量。

### 5.1 获取基本类型

```csharp
int a = luaenv.Global.Get<int>("a");
string b = luaenv.Global.Get<string>("b");
bool c = luaenv.Global.Get<bool>("c");
```

### 5.2 获取 Table

有四种方式将一个 Lua table 映射到 C#：

| 方式 | 性能 | 是否需要生成代码 | 特点 |
|------|------|:---:|------|
| **① 映射到 class/struct** | ⭐⭐⭐ | 否 | 值拷贝，修改不同步 |
| **② 映射到 interface** | ⭐⭐⭐⭐ | **是** | 引用方式，读写即同步 |
| **③ 映射到 Dictionary/List** | ⭐⭐ | 否 | 轻量，key/value 类型需一致 |
| **④ 映射到 LuaTable** | ⭐ | 否 | 最灵活但最慢，无类型检查 |

**推荐**：常规场景用 **①**，需要频繁交互的高性能场景用 **②**。

```csharp
// 方式①：映射到 class
public class PlayerData
{
    public string name;
    public int level;
}

PlayerData p = luaenv.Global.Get<PlayerData>("player");
Debug.Log(p.name);
```

```csharp
// 方式②：映射到 interface（需要 [CSharpCallLua] 配置）
[CSharpCallLua]
public interface IPlayer
{
    string name { get; set; }
    int level { get; set; }
    void Attack(); // 可以映射 Lua 函数
}

IPlayer p = luaenv.Global.Get<IPlayer>("player");
p.Attack(); // 调用 Lua 侧的函数
```

### 5.3 获取 Lua 函数

```csharp
// 方式①：映射到 delegate（推荐，高性能，类型安全）
public delegate int AddDelegate(int a, int b);

// 注意：该 delegate 需要配置 CSharpCallLua 并生成代码
AddDelegate add = luaenv.Global.Get<AddDelegate>("add");
int result = add(1, 2);
```

```csharp
// 方式②：映射到 LuaFunction（灵活但慢）
LuaFunction fn = luaenv.Global.Get<LuaFunction>("add");
object[] results = fn.Call(1, 2);
int result = (int)results[0];
```

> 💡 **建议**：尽量在初始化时一次性获取并缓存 delegate/interface，避免频繁访问全局变量。

---

## 6. Lua 调用 C#

这是 xLua 最常用的功能——在 Lua 中操作 C# 对象。

### 6.1 核心约定

```
CS.命名空间.类名
```

- 所有 C# 相关内容都放在 `CS` 命名空间下
- Lua 中**没有 `new` 关键字**，直接调用构造函数
- 成员方法使用**冒号 `:` 语法糖**（自动传 self）

### 6.2 创建 C# 对象

```lua
-- C#: var go = new GameObject("player");
local go = CS.UnityEngine.GameObject('player')

-- 带多个参数
local v3 = CS.UnityEngine.Vector3(1, 2, 3)
```

### 6.3 访问静态成员

```lua
-- 静态属性
local dt = CS.UnityEngine.Time.deltaTime

-- 写静态属性
CS.UnityEngine.Time.timeScale = 0.5

-- 静态方法
local go = CS.UnityEngine.GameObject.Find('player')

-- 枚举值
local layer = CS.UnityEngine.LayerMask.NameToLayer('UI')

-- 技巧：频繁访问的类用局部变量缓存，提升性能
local GameObject = CS.UnityEngine.GameObject
local go = GameObject.Find('player')
```

### 6.4 访问实例成员

```lua
-- 读属性
print(transform.position)

-- 写属性
transform.position = CS.UnityEngine.Vector3(0, 10, 0)

-- 调用成员方法（使用冒号语法）
gameObject:SetActive(false)
transform:Translate(1, 0, 0)

-- 注意：成员方法的第一个参数是 self
-- 以下两种写法等价：
transform:Translate(1, 0, 0)
transform.Translate(transform, 1, 0, 0)  -- 不推荐
```

### 6.5 重载方法

Lua 中按参数类型调用，xLua 自动匹配重载：

```lua
-- C#: void Foo(int), void Foo(string)
testobj:Foo(100)    -- 调用 int 版本
testobj:Foo('hello') -- 调用 string 版本
```

> ⚠️ 注意：C# 的 `int`、`float`、`double` 在 Lua 中都对应 `number`，如果三个重载都存在，只能调用到生成代码中排最前面的那个。这种情况下建议用扩展方法封装。

### 6.6 out / ref 参数

```lua
-- C#: bool TryParse(string input, out int result)
local success, result = testobj:TryParse('123')

-- C#: void Swap(ref int a, ref int b)
local newA, newB = testobj:Swap(10, 20)
-- ref 参数：输入时传值，返回值中取修改后的值
```

规则：**返回值 + out 参数 + ref 参数 = Lua 的多返回值**。

### 6.7 操作符

支持的操作符：`+`、`-`、`*`、`/`、`==`、一元 `-`、`<`、`<=`、`%`、`[]`

```lua
local v1 = CS.UnityEngine.Vector3(1, 1, 1)
local v2 = CS.UnityEngine.Vector3(2, 2, 2)
local v3 = v1 + v2  -- C# 操作符重载
print(v3.x)         --> 3
```

### 6.8 重要工具函数

```lua
-- typeof —— 相当于 C# 的 typeof
typeof(CS.UnityEngine.ParticleSystem)

-- 枚举转换
CS.Tutorial.TestEnum.__CastFrom(1)       -- 数字转枚举
CS.Tutorial.TestEnum.__CastFrom('E1')    -- 字符串转枚举

-- private_accessible —— 访问私有成员
xlua.private_accessible(CS.UnityEngine.GameObject)

-- cast —— 用指定代码生成类型访问（用于 interface/抽象类）
cast(calc, typeof(CS.Tutorial.Calc))

-- 泛型方法获取
local foo_generic = xlua.get_generic_method(CS.GetGenericMethodTest, 'Foo')
local foo = foo_generic(CS.System.Int32, CS.System.Double)
local ret = foo(obj, 1, 2)
```

---

## 7. 热更新（Hotfix）

这是 xLua 的杀手级特性：**用 Lua 函数替换已编译的 C# 方法**。

### 7.1 核心 API

```lua
xlua.hotfix(class, method_name, fix)
```

| 参数 | 说明 |
|------|------|
| `class` | C# 类：`CS.Namespace.TypeName` 或字符串 `"Namespace.TypeName"` |
| `method_name` | 方法名，可选 |
| `fix` | 单个方法时传 function；批量替换时传 table |

### 7.2 替换单个方法

```lua
xlua.hotfix(CS.MyGame.Calc, 'Add', function(self, a, b)
    return a * b + 1  -- 原逻辑是 a + b，改为 a * b + 1
end)
```

**注意**：实例方法的第一个参数是 `self`（对应 C# 的 `this`），静态方法没有 `self`。

### 7.3 批量替换整个类

```lua
xlua.hotfix(CS.MyGame.Calc, {
    Add = function(self, a, b)
        return a + b + 100
    end;

    get_Result = function(self)
        return 'Result: ' .. self._result
    end;

    set_Result = function(self, v)
        self._result = v
    end;
})
```

### 7.4 属性热更方法名对照

| C# 成员 | method_name |
|---------|-------------|
| 构造函数 | `.ctor` |
| 析构函数 | `Finalize` |
| 属性 getter | `get_属性名` |
| 属性 setter | `set_属性名` |
| `[]` 索引器 get | `get_Item` |
| `[]` 索引器 set | `set_Item` |
| `+` 运算符 | `op_Addition` |
| 事件 `+=` | `add_事件名` |
| 事件 `-=` | `remove_事件名` |

### 7.5 属性热更的陷阱【重要】

**绝不能**在 getter/setter 补丁中访问属性本身，否则会导致无限递归和堆栈溢出！

```lua
-- ❌ 错误：self.Title 会再次触发 hotfix 的 get_Title → 死循环
get_Title = function(self)
    return self.Title
end

-- ✅ 正确：直接访问 backing field
get_Title = function(self)
    return self._title
end

set_Title = function(self, v)
    self._title = v
end
```

### 7.6 保留原始逻辑（util.hotfix_ex）

如果希望在原逻辑前后追加行为，使用 `util.hotfix_ex`：

```lua
local util = require 'xlua.util'

util.hotfix_ex(CS.MyGame.Player, 'Hit', function(self)
    print('Hit 调用前 - 额外逻辑')
    self:Hit()          -- 调用原始 C# 逻辑（不会递归）
    print('Hit 调用后 - 额外逻辑')
end)
```

### 7.7 替换协程

使用 `util.cs_generator` 模拟 C# 的 `yield return`：

```lua
local util = require 'xlua.util'

xlua.hotfix(CS.MyGame.HotFixSubClass, {
    Start = function(self)
        return util.cs_generator(function()
            while true do
                coroutine.yield(CS.UnityEngine.WaitForSeconds(3))
                print('Wait for 3 seconds')
            end
        end)
    end;
})
```

### 7.8 泛型类型的热更

```lua
-- 对 GenericClass<double> 打补丁
xlua.hotfix(CS.GenericClass(CS.System.Double), {
    Func1 = function(obj)
        print('GenericClass<double>.Func1', obj)
    end;
})
```

---

## 8. 配置系统

xLua 用白名单机制来决定哪些类型生成适配代码。有三种配置方式：

### 8.1 三种配置方式

| 方式 | 说明 | 建议 |
|------|------|:----:|
| **打标签** `[LuaCallCSharp]` | 直接写在类上 | ❌ 不推荐，il2cpp 下会增加代码量 |
| **静态列表** | 静态类中的 `List<Type>` 字段 | ✅ **推荐** |
| **动态列表** | 静态属性，getter 中用反射批量获取 | ✅ 适合按命名空间批量配置 |

### 8.2 四个核心标签

| 标签 | 用途 |
|------|------|
| `[LuaCallCSharp]` | Lua 中调用的 C# 类型（生成适配代码） |
| `[CSharpCallLua]` | C# 中调用的 Lua delegate/interface |
| `[Hotfix]` | 需要热更的 C# 类型 |
| `[GCOptimize]` | 纯值类型，优化 GC |
| `[ReflectionUse]` | 阻止 il2cpp 代码剪裁（替代 LuaCallCSharp 的轻量方案） |

### 8.3 本项目配置示例

见 `Assets/ECMVC/Hotfix/Example/Editor/HotfixTestCfg.cs`：

```csharp
public static class HotfixTestCfg
{
    [Hotfix]
    public static List<Type> hotfixTypes = new List<Type>
    {
        typeof(ECMVC.Hotfix.Test.HotfixDemo),
        // 在这里追加需要热更新的其他类型
    };
}
```

> ⚠️ **配置必须放在 `Editor` 目录下**（高版本 Unity 要求）。

### 8.4 判断使用哪个标签

一个简单规则：**看调用者是谁**。

```
调用者是 C#，被调用者是 Lua 函数 → [CSharpCallLua]
调用者是 Lua，被调用者是 C# 类型 → [LuaCallCSharp]
```

例如 `List<int>.Find(Predicate<int> match)`：
- `List<int>` → `[LuaCallCSharp]`
- `Predicate<int>` → `[CSharpCallLua]`（因为调用者是 C# 的 Find，被调用的是 Lua 函数）

更无脑的方法：看到报错 `This delegate/interface must add to CSharpCallLua : XXX`，就把 `XXX` 加到 `CSharpCallLua`。

---

## 9. 常见问题与陷阱

### Q: 报错 `xlua.access, no field __Hitfix0_Update`
**原因**：未正确注入 Hotfix。  
**解决**：执行 `XLua → Hotfix Inject In Editor`，确认打印 `hotfix inject finish!`。

### Q: iOS 上报 `attempt to call a nil value`
**原因**：il2cpp 代码剪裁掉了未引用的 API。  
**解决**：给相关类型加上 `[LuaCallCSharp]` 或 `[ReflectionUse]`，重新生成代码。

### Q: 属性热更导致堆栈溢出
**原因**：getter/setter 补丁中访问了属性自身，形成递归。  
**解决**：始终访问 backing field，而非属性名。

### Q: 已经 Destroy 的 GameObject，在 Lua 中却不为 nil
**原因**：UnityEngine.Object 重写了 `==` 运算符，destroy 后 C# 对象不为 null。  
**解决**：写一个扩展方法做 null 判断：

```csharp
public static bool IsNull(this UnityEngine.Object o) => o == null;
```

```lua
print(go:GetComponent('Animator'):IsNull())
```

### Q: 无法通过 `dic['abc']` 访问 Dictionary
**原因**：xLua 中 `[]` 操作符会与成员方法名冲突。  
**解决**：使用 `get_Item` / `set_Item` 替代：

```lua
dic:set_Item('a', 1)
print(dic:get_Item('a'))  --> 1
```

### Q: 调用 LuaEnv.Dispose() 时报错 `try to dispose a LuaEnv with C# callback!`
**原因**：C# 侧仍有引用指向 Lua 函数的 delegate。  
**解决**：释放前清除所有引用：

```lua
-- 清除 hotfix
xlua.hotfix(CS.MyClass, 'MyMethod', nil)

-- 或查找哪些函数被引用
local util = require 'xlua.util'
util.print_func_ref_by_csharp()
```

---

## 10. 项目实战：ECMVC 中的热更新流程

### 10.1 整体架构

```
服务器 (hotfix-server)
    │ version.json, lua/*.lua, AssetBundles
    ▼
HotfixManager (MonoBehaviour 单例)
    │ 版本检查 → 下载 → 加载 Lua
    ▼
LuaLoad (LuaEnv 管理)
    │ 自定义 Loader → require('main')
    ▼
main.lua (热更入口)
    │ xlua.hotfix(...) 替换 C# 方法
    ▼
游戏逻辑运行补丁版本
```

### 10.2 编写第一个 Lua 补丁

参考项目中的 `Assets/ECMVC/Hotfix/Example/lua/main.lua`：

```lua
-- 1. 用局部变量缓存常访问的类型
local HotfixDemo = CS.ECMVC.Hotfix.Test.HotfixDemo
local Debug = CS.UnityEngine.Debug

-- 2. 替换单个方法
xlua.hotfix(HotfixDemo, 'Add', function(self, a, b)
    return a * b + 1
end)

-- 3. 批量替换
xlua.hotfix(HotfixDemo, {
    Greet = function(self, name)
        return 'Hi, ' .. name .. ' (来自 Lua)'
    end;

    -- 属性务必访问 backing field！
    get_Title = function(self)
        return '【Lua】' .. self._title
    end;

    set_Title = function(self, v)
        self._title = '[Lua热更]' .. v
    end;

    GetVersion = function()
        return 'v2.0.0 (Lua 补丁)'
    end;
})

-- 4. 保留原始逻辑
local util = require 'xlua.util'
util.hotfix_ex(HotfixDemo, 'Hit', function(self)
    Debug.Log('[Lua] Hit 调用前')
    self:Hit()  -- 调用原始 C# 逻辑
    Debug.Log('[Lua] Hit 调用后')
end)

Debug.Log('main.lua 加载完成！')
```

### 10.3 完整调试步骤

```
① 添加 HOTFIX_ENABLE 宏
② 菜单 XLua → Generate Code
③ 在 HotfixTestCfg.cs 注册需要热更的类型
④ 菜单 XLua → Hotfix Inject In Editor（确认打印注入成功）
⑤ 修改 lua 文件（如 Example/lua/main.lua）
⑥ 运行场景，观察 Console 输出
```

> 💡 在编辑器开发阶段，修改 Lua 文件后只需重新运行场景即可，**不需要重新生成代码或注入**。

---

## 参考资源

- [xLua GitHub 仓库](https://github.com/Tencent/xLua)
- [本机 xLua 文档](..\..\..\XLua\Doc\XLua教程.md)
- [本机 xLua API 参考](..\..\..\XLua\Doc\XLua_API.md)
- [热更新完整指南](..\..\..\XLua\Doc\hotfix.md)
- [FAQ 常见问题](..\..\..\XLua\Doc\faq.md)
- [ECMVC 热更新系统使用文档](./README.md)
