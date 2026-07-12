using UnityEngine;

namespace FModa.Hotfix.Test
{
    /// <summary>
    /// 热更新演示类 —— 展示可以被 Lua 补丁替换的 C# 成员。
    /// </summary>
    public class HotfixDemo
    {
        // ── 字段 ──
        public int hitCount;

        // ── 属性（手工 backing field，便于 Lua 绕过属性直接访问字段）──
        private string _title = "原始标题";
        public string Title
        {
            get => _title;
            set => _title = value;
        }

        // ── 实例方法 ──
        public int Add(int a, int b) => a + b;

        public string Greet(string name) => $"Hello, {name}";

        public void Hit()
        {
            hitCount++;
            Debug.Log($"[C#] Hit! 总次数: {hitCount}");
        }

        // ── 静态方法 ──
        public static string GetVersion() => "v1.0.0 (C# 原始版本)";

        // ── 重载方法 ──
        public string Combine(string a, string b) => a + b;

        public string Combine(int a, int b) => (a + b).ToString();

        // ── ref 参数 ──
        public void Swap(ref int a, ref int b) => (a, b) = (b, a);

        public bool TryParse(string input, out int result)
            => int.TryParse(input, out result);
    }
}
