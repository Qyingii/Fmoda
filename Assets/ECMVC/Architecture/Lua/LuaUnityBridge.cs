using MoonSharp.Interpreter;
using UnityEngine;

// Lua 可用的 Unity 数学类型包装。
// 在 Lua 中使用：
//   local pos = Vector3(1, 2, 3)
//   local rot = Quaternion.Euler(0, 90, 0)
//   local col = Color(1, 0, 0, 1)

[MoonSharpUserData]
public class LuaVector3
{
    public float x, y, z;

    [MoonSharpHidden] public LuaVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    [MoonSharpHidden] public LuaVector3(float x, float y)          { this.x = x; this.y = y; }

    [MoonSharpUserDataMetamethod("__call")]
    public static LuaVector3 New(float x, float y, float z) => new LuaVector3(x, y, z);

    [MoonSharpUserDataMetamethod("__call")]
    public static LuaVector3 New(float x, float y) => new LuaVector3(x, y);

    [MoonSharpHidden] public Vector3 ToUnity() => new Vector3(x, y, z);

    public override string ToString() => $"({x}, {y}, {z})";
}

[MoonSharpUserData]
public class LuaQuaternion
{
    public float x, y, z, w;

    [MoonSharpHidden]
    public LuaQuaternion(float x, float y, float z, float w)
    { this.x = x; this.y = y; this.z = z; this.w = w; }

    [MoonSharpUserDataMetamethod("__call")]
    public static LuaQuaternion New(float x, float y, float z, float w) => new LuaQuaternion(x, y, z, w);

    // Lua 中常用：local rot = Quaternion.Euler(0, 90, 0)
    public static LuaQuaternion Euler(float x, float y, float z)
    {
        var q = Quaternion.Euler(x, y, z);
        return new LuaQuaternion(q.x, q.y, q.z, q.w);
    }

    [MoonSharpHidden] public Quaternion ToUnity() => new Quaternion(x, y, z, w);
}

[MoonSharpUserData]
public class LuaColor
{
    public float r, g, b, a;

    [MoonSharpHidden]
    public LuaColor(float r, float g, float b, float a)
    { this.r = r; this.g = g; this.b = b; this.a = a; }

    [MoonSharpUserDataMetamethod("__call")]
    public static LuaColor New(float r, float g, float b, float a) => new LuaColor(r, g, b, a);

    public static LuaColor New(float r, float g, float b) => new LuaColor(r, g, b, 1f);

    [MoonSharpHidden] public Color ToUnity() => new Color(r, g, b, a);
}
