-- ============================================================
-- SampleMod 入口 —— 演示模组模块生命周期与 API 使用
-- ============================================================
--
-- 模组入口脚本需返回一个 table，包含以下可选钩子：
--   on_start(api)   模组启动时调用
--   on_update(api)  每逻辑帧调用
--   on_destroy(api) 模组卸载时调用
--
-- 通过 api 参数访问受控 API：
--   api:Log(msg)          普通日志
--   api:LogWarning(msg)   警告日志
--   api:LogError(msg)     错误日志
--   api:DataSet(k, v)     存储数据
--   api:DataGet(k)        读取数据
--   api:DataRemove(k)     删除数据
--   api:DataClear()       清空数据
--   api:DataHas(k)        检查键是否存在
-- ============================================================

local M = {}

-- 模组启动时调用
function M.on_start(api)
    api:Log("SampleMod 已启动！")

    -- 展示数据存储 API
    api:DataSet("greeting", "你好，模组世界！")
    api:DataSet("count", 0)

    local msg = api:DataGet("greeting")
    api:Log("读取存储: " .. msg)

    api:Log("模组信息: " .. api.Info.id .. " v" .. api.Info.version)
end

-- 每逻辑帧调用
function M.on_update(api)
    local count = api:DataGet("count")
    if count ~= nil then
        count = count + 1
        api:DataSet("count", count)
    end

    -- 每 60 帧打一次日志（避免刷屏）
    if count % 60 == 0 then
        api:Log("SampleMod 运行中… 帧计数: " .. count)
    end
end

-- 模组卸载时调用
function M.on_destroy(api)
    local count = api:DataGet("count") or 0
    api:Log("SampleMod 已卸载。共运行 " .. count .. " 帧，再见！")
end

return M
