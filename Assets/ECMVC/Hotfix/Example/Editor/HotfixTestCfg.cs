using System;
using System.Collections.Generic;
using XLua;

/// <summary>
/// 热更新类型配置 —— 官方推荐方式（v2）。
///
/// 原理：XLua 在注入阶段扫描所有带有 [Hotfix] 标记的静态字段或属性，
/// 将其值（Type 列表）注册为允许热更新的类型。
///
/// !!! 必须放在 Editor 目录下（高版本 Unity 要求）!!!
///
/// 使用步骤：
///   1. 在 Scripting Define Symbols 添加 HOTFIX_ENABLE
///   2. 菜单 XLua → Generate Code
///   3. 菜单 XLua → Hotfix Inject In Editor
///   4. 在 Lua 中调用 xlua.hotfix 即可替换 C# 方法
///
/// 对比方式一（直接在类上写 [Hotfix] 标签）:
///   方式一在高版本 Unity 已不支持，务必使用此方式。
/// </summary>
public static class HotfixTestCfg
{
    /// <summary>
    /// 方式 A：静态字段 —— 显式列出需要热更新的类型
    /// </summary>
    [Hotfix]
    public static List<Type> hotfixTypes = new List<Type>
    {
        typeof(ECMVC.Hotfix.Test.HotfixDemo),
        // 在这里追加需要热更新的其他类型：
        // typeof(SomeOtherClass),
        // typeof(AnotherClass),
    };

    /// <summary>
    /// 方式 B：静态属性 —— 通过反射按命名空间批量注册
    /// （仅作示例，此处未启用，可自行取消注释）
    /// </summary>
    // [Hotfix]
    // public static List<Type> hotfixByNamespace
    // {
    //     get
    //     {
    //         return new List<Type>(from type in System.Reflection.Assembly.Load("Assembly-CSharp").GetTypes()
    //                               where type.Namespace == "ECMVC.Hotfix.Test"
    //                               select type);
    //     }
    // }
}
