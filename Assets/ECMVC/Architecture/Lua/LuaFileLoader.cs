using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using UnityEngine;

// MoonSharp 脚本加载器：
//   - script.DoFile(absolutePath) 传入绝对路径，直接读取
//   - Lua 内 require("module") 传入相对路径，从 Mods 文件夹查找
public class LuaFileLoader : ScriptLoaderBase
{
    public override object LoadFile(string file, Table globalContext)
    {
        if (Path.IsPathRooted(file) && File.Exists(file))
            return File.ReadAllText(file);

        string path = Path.Combine(Application.dataPath, "Mods", file);
        if (File.Exists(path))
            return File.ReadAllText(path);

        throw new FileNotFoundException($"Lua file not found: {file}");
    }

    public override bool ScriptFileExists(string name)
    {
        if (Path.IsPathRooted(name))
            return File.Exists(name);

        return File.Exists(Path.Combine(Application.dataPath, "Mods", name + ".lua"));
    }
}
