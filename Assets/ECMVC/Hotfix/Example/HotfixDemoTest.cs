using UnityEngine;
using XLua;

namespace ECMVC.Hotfix.Test
{
    /// <summary>
    /// 热更新演示入口 —— 展示 Lua 补丁替换 C# 方法的前后对比。
    ///
    /// 用法：挂载到场景任意 GameObject 上，运行后看 Console 输出。
    ///
    /// 前置条件（否则热更不生效，但仍能看到原始 C# 行为）：
    ///   1. Scripting Define Symbols 添加 HOTFIX_ENABLE
    ///   2. XLua → Generate Code
    ///   3. XLua → Hotfix Inject In Editor
    /// </summary>
    public class HotfixDemoTest : MonoBehaviour
    {
        void Start()
        {
            var demo = new HotfixDemo();

            // ==========================================================
            //  第一步：原始 C# 行为
            // ==========================================================
            Debug.Log("══════ 热更前（原始 C#）══════");
            Debug.Log($"Add(2, 3)          = {demo.Add(2, 3)}           （期望: 5）");
            Debug.Log($"Greet(\"World\")    = {demo.Greet("World")}      （期望: Hello, World）");
            Debug.Log($"GetVersion()       = {HotfixDemo.GetVersion()}  （期望: v1.0.0）");
            Debug.Log($"Combine(\"A\",\"B\") = {demo.Combine("A", "B")}   （期望: AB）");
            Debug.Log($"Combine(2, 3)      = {demo.Combine(2, 3)}       （期望: 5）");
            Debug.Log($"Title              = {demo.Title}               （期望: 原始标题）");

            int a = 1, b = 99;
            demo.Swap(ref a, ref b);
            Debug.Log($"Swap(1,99)         = a={a}, b={b}               （期望: a=99, b=1）");

            // ==========================================================
            //  第二步：注入 Lua 补丁
            // ==========================================================
            Debug.Log("\n══════ 注入 Lua 补丁 ══════");

            LuaEnv luaEnv = new LuaEnv();
            luaEnv.DoString(@"
                -- 1) 单方法形式：只替换 Add
                xlua.hotfix(CS.ECMVC.Hotfix.Test.HotfixDemo, 'Add', function(self, a, b)
                    return a * b + 1      -- 原本 a+b，改为 a*b+1
                end)

                -- 2) 批量形式：用一个 table 替换多个成员
                xlua.hotfix(CS.ECMVC.Hotfix.Test.HotfixDemo, {
                    Greet = function(self, name)
                        return 'Hi, ' .. name .. ' (来自 Lua)'
                    end;

                    -- 属性 getter：注意不能读 self.Title（会触发递归）
                    -- 必须直接访问 backing field _title
                    get_Title = function(self)
                        return '【Lua 修改】' .. self._title
                    end;

                    -- 属性 setter：同理必须访问 backing field
                    set_Title = function(self, v)
                        self._title = '[Lua]' .. v
                    end;

                    -- 静态方法：无 self 参数
                    GetVersion = function()
                        return 'v2.0.0 (Lua 热更版本)'
                    end;

                    -- 重载方法：所有重载映射到同一个 Lua 函数
                    Combine = function(self, a, b)
                        if type(a) == 'string' and type(b) == 'string' then
                            return b .. ' > ' .. a       -- 反转拼接
                        else
                            return tostring(a + b) .. ' (Lua 重载)'
                        end
                    end;
                })

                -- 3) ref 参数：修改后通过额外返回值写回
                xlua.hotfix(CS.ECMVC.Hotfix.Test.HotfixDemo, 'Swap', function(self, a, b)
                    local temp = a
                    a = b + 10
                    b = temp - 5
                    return a, b   -- ref 参数作为额外返回值
                end)
            ");

            // ==========================================================
            //  第三步：热更后的行为
            // ==========================================================
            Debug.Log("\n══════ 热更后（Lua 补丁）══════");

            var patched = new HotfixDemo();

            Debug.Log($"Add(2, 3)          = {patched.Add(2, 3)}         （期望: 2*3+1=7）");
            Debug.Log($"Greet(\"World\")    = {patched.Greet("World")}    （期望: Hi, World (来自 Lua)）");
            Debug.Log($"GetVersion()       = {HotfixDemo.GetVersion()}   （期望: v2.0.0）");
            Debug.Log($"Combine(\"A\",\"B\") = {patched.Combine("A", "B")} （期望: B > A）");
            Debug.Log($"Combine(2, 3)      = {patched.Combine(2, 3)}     （期望: 5 (Lua 重载)）");

            // setter/getter 都经过了 Lua 补丁
            patched.Title = "测试标题";
            Debug.Log($"Title (set+get)    = {patched.Title}             （期望: 【Lua 修改】[Lua]测试标题）");

            int x = 10, y = 20;
            Debug.Log($"Swap(10,20) before = x={x}, y={y}");
            patched.Swap(ref x, ref y);
            Debug.Log($"Swap(10,20) after  = x={x}, y={y}               （期望: x=30(y+10), y=5(x-5)）");

            Debug.Log("\n══════ 测试完成 ══════");

            // ==========================================================
            //  清理：先清除补丁，再释放 LuaEnv
            // ==========================================================
            luaEnv.DoString(@"
                local T = CS.ECMVC.Hotfix.Test.HotfixDemo
                xlua.hotfix(T, 'Add', nil)
                xlua.hotfix(T, 'Greet', nil)
                xlua.hotfix(T, 'get_Title', nil)
                xlua.hotfix(T, 'set_Title', nil)
                xlua.hotfix(T, 'GetVersion', nil)
                xlua.hotfix(T, 'Combine', nil)
                xlua.hotfix(T, 'Swap', nil)
            ");
            luaEnv.FullGc();

            try
            {
                luaEnv.Dispose();
                Debug.Log("══════ LuaEnv 已释放 ══════");
            }
            catch (System.InvalidOperationException e)
            {
                Debug.LogWarning($"LuaEnv 释放提示（可忽略）: {e.Message}");
            }

            Debug.Log("\n💡 属性热更要点：");
            Debug.Log("   在 get_Title/set_Title 补丁中，必须访问 backing field（如 _title）");
            Debug.Log("   而非属性本身（self.Title），否则会触发无限递归导致堆栈溢出");
        }
    }
}
