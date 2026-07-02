-- ExampleMod / main.lua
-- 将此文件夹复制到 <ProjectName>_Data/Mods/ExampleMod/ 即可在运行时自动加载。
-- 编辑器下对应路径为：Assets/Mods/ExampleMod/
--
-- 在 LuaExampleEntity 的 Inspector 填写组件名称即可挂载：
--   Lua Components[0] = "Ticker"
--   Lua Components[1] = "VisibilityLogger"


-- ─────────────────────────────────────────────────────────────
-- 组件 1：Ticker
-- 每隔 interval 帧向控制台打印一次日志，演示 self 状态隔离。
-- 不同实体各自持有独立的 self.frame 计数，互不干扰。
-- ─────────────────────────────────────────────────────────────
local Ticker = {}

Ticker.Start = function(self)
    self.frame    = 0
    self.interval = 60          -- 每 60 帧输出一次，可在 Lua 中热改
    Game.Log("Ticker 已启动，间隔 = " .. self.interval .. " 帧")
end

Ticker.Update = function(self)
    self.frame = self.frame + 1
    if self.frame >= self.interval then
        self.frame = 0
        Game.Log("Ticker：已运行 " .. self.interval .. " 帧")
    end
end

Ticker.OnDestroy = function(self)
    Game.Log("Ticker 已销毁")
end

Game.AddComponent("Ticker", Ticker)


-- ─────────────────────────────────────────────────────────────
-- 组件 2：VisibilityLogger
-- 监听 OnEnable / OnDisable，演示无状态组件的写法。
-- ─────────────────────────────────────────────────────────────
local VisibilityLogger = {}

VisibilityLogger.OnEnable  = function(self) Game.Log("实体已启用") end
VisibilityLogger.OnDisable = function(self) Game.Log("实体已禁用") end

Game.AddComponent("VisibilityLogger", VisibilityLogger)
