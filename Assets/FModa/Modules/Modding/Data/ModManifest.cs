using System;

namespace FModa.Modding
{
    /// <summary>
    /// 模组清单 —— 每个模组目录下 mod.json 的反序列化结构。
    ///
    /// 字段均为小写风格以匹配 JSON 惯例，XLua 侧的 ModAPI 也会读取这些信息。
    ///
    /// JSON 示例：
    /// <code>
    /// {
    ///   "id": "com.example.hellomod",
    ///   "name": "HelloMod",
    ///   "version": "1.0.0",
    ///   "author": "Me",
    ///   "description": "一个示例模组",
    ///   "entry": "main.lua"
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public class ModManifest
    {
        /// <summary> 模组唯一标识，如 "com.example.weaponpack" </summary>
        public string id;

        /// <summary> 显示名称 </summary>
        public string name;

        /// <summary> 语义版本号，如 "1.2.0" </summary>
        public string version = "1.0.0";

        /// <summary> 作者 </summary>
        public string author;

        /// <summary> 功能描述 </summary>
        public string description;

        /// <summary> 入口 Lua 文件名（相对于模组目录），默认 "main.lua" </summary>
        public string entry = "main.lua";

        /// <summary>
        /// 校验必要字段是否完整。
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(id)
                && !string.IsNullOrEmpty(name)
                && !string.IsNullOrEmpty(entry);
        }
    }
}
