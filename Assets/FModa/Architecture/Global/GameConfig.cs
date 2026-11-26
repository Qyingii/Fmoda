using UnityEngine;

/// <summary>
/// 全局静态数据（配置 / 常量 / 只读表 / 默认值）的唯一位置。
/// 新增字段时必须用注释或 [Tooltip] 写明：这是什么、谁在用、单位或取值范围。
/// 运行时通过 GF.config.xxx 读取；不要再新建配置类、常量类或 public static 数据类。
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    
}
