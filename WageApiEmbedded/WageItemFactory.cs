using System;
using Il2Cpp;

namespace WageAPI;

/// <summary>
/// WageItemFactory 物品创建权威封装（2026-10-06，物品工厂统一封装候选）。
/// 统一物品创建入口：DirectoryMaster.Item(id, true)（触发目录懒加载——09-20 拆包实锤）+ null 兜底日志 + 可选 SetItemOwned。
/// 新代码一律走本入口；存量按 bug 驱动迁移（不盲迁 88 处）。
///
/// 【禁裸创建清单（已知坑/边界，别踩）】
/// ① CreateEmptyItem：创建后需 SetIdentifier/SetSpriteAndShape/SetShape 等复杂初始化（骰子/虚空珠/养蛊机产物/自定义储物箱/充电器/许可证）——B 类特殊创建，勿收敛到本入口
/// ② DirectoryMaster.Item(id, false)：=模板读取非创建实例（WagePowerPerk.SmartItem 价值查询用）——勿当创建用
/// ③ Tick 高频路径创建：养蛊机产物生成在 Tick 链——若每帧创建注意性能（本入口带日志，高频路径慎用或按需降日志）
/// ④ 创建后需特殊初始化的物品（WineHelper.InitBerry/WaterHelper.FillWithHighQualityWater 等）：本入口只创建+所有权，初始化由调用方在创建后继续（创建与初始化解耦）
/// </summary>
public static class WageItemFactory
{
    /// <summary>权威创建（DirectoryMaster.Item(id,true)——触发目录懒加载；null/异常→日志+return null）。</summary>
    public static GameItem Create(string id, bool setOwned = false)
    {
        try
        {
            GameItem it = DirectoryMaster.Item(id, true);
            if (it == null)
            {
                Core.Log?.Msg("[WageItemFactory] 创建失败：目录无 " + id + "（返回 null）");
                return null;
            }
            if (setOwned)
            {
                try { Il2Cpp.GeneralHelper.SetItemOwned(it, true); } catch (Exception ex) { Core.Log?.Msg("[WageItemFactory] SetItemOwned 异常 " + id + ": " + ex.Message); }
            }
            Core.Log?.Msg("[WageItemFactory] 创建成功：" + id);
            return it;
        }
        catch (Exception ex)
        {
            Core.Log?.Msg("[WageItemFactory] 创建异常 " + id + ": " + ex.Message);
            return null;
        }
    }
}
