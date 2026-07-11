-- ================================================================
--  main.lua  —— 热更新补丁示例
--  将此文件放到 persistentDataPath/lua/main.lua 即可被 LuaLoad 加载
--  也可直接在 StreamingAssets/lua/main.lua 中编辑调试
-- ================================================================

-- 先用局部变量引用常访问的类型，提升性能
local HotfixDemo = CS.ECMVC.Hotfix.Test.HotfixDemo
local GameObject  = CS.UnityEngine.GameObject
local Debug       = CS.UnityEngine.Debug

-- ================================================================
--  示例 1：xlua.hotfix —— 替换单个方法
-- ================================================================
xlua.hotfix(HotfixDemo, 'Add', function(self, a, b)
    return a * b + 1
end)

Debug.Log('[Lua] Add 方法已替换为 a*b+1')

-- ================================================================
--  示例 2：xlua.hotfix —— 一次替换整个类
-- ================================================================
xlua.hotfix(HotfixDemo, {
    Greet = function(self, name)
        return 'Hi, ' .. name .. ' (来自 Lua)'
    end;

    get_Title = function(self)
        return '【Lua】' .. self.Title
    end;

    set_Title = function(self, v)
        self.Title = '[Lua热更]' .. v
    end;

    GetVersion = function()
        return 'v2.0.0 (Lua 补丁)'
    end;

    Combine = function(self, a, b)
        if type(a) == 'string' and type(b) == 'string' then
            return b .. ' ← ' .. a
        else
            return tostring(a + b) .. ' (Lua)'
        end
    end;
})

Debug.Log('[Lua] HotfixDemo 类已批量打补丁')

-- ================================================================
--  示例 3：util.hotfix_ex —— 保留并扩展原始函数
-- ================================================================
local util = require 'xlua.util'
util.hotfix_ex(HotfixDemo, 'Hit', function(self)
    Debug.Log('[Lua] Hit 调用前')
    -- 在 hotfix_ex 中，用 util.hotfix_ex 替换后，
    -- 可以通过 self:Hit() 调用原始函数（不会递归）
    self:Hit()
    Debug.Log('[Lua] Hit 调用后，额外记录日志')
end)

Debug.Log('[Lua] main.lua 加载完成，所有补丁已就绪')
