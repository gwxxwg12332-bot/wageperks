using System;

using System.Collections.Generic;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using MelonLoader;

using UnityEngine;

namespace JacksonPerks
{
    // ============================================================

    // 命运骰子 - 无内部空间版

    // 直接"吃掉"物品：拖物品到命运骰子上 → 销毁物品 + 累计价值

    // 每满400价值触发1个随机事件

    // ============================================================

    public static partial class DestinyDice

    {



        // ===== 自造事件：下层区酒荒（进通用事件池 normalEventBlueprints，所有玩家随机遇到） =====

        private static bool _moddedEventsRegistered = false;






        // 复合事件联动：活跃事件 → 安排客户/发放奖励（v1.2.1）

        private static bool _legacyGranted = false;




        // 幂等注册：OnDayStart 时确保蓝图已注入（normalEventBlueprints 此时已初始化）







        // 事件蓝图：下层区酒荒（用户样板文案）




        // 事件蓝图：安保部换装巡逻（便衣盯梢，武器/违禁品风险升）




        // 事件蓝图：营养果新品发布会（食品/零食热销）




        // 事件蓝图：垃圾场大清理（材料供应充足降价）




        // 事件蓝图：黑市码头大扫荡（违禁品价格飙升）




        // 事件蓝图：上层区奢华展（奢侈品价格走高）




        // 复合事件：黑市军火流入（赃物流入，武器/违禁品降价）




        // 复合事件：上层区拍卖周（奢侈品涨 + 上层客户到访）




        // 复合事件：营养果危机囤积（食品跌 + 违禁品涨）




        // 复合事件：火车劫案余波（补给箱跌 + 门禁卡涨）




        // 复合事件：流浪商人遗赠（奖励：治安战利品箱送上柜台）




        // 复合事件：酒鬼闹事（酒跌 + 医疗涨）




}

}
