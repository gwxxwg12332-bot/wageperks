using System;
using Il2Cpp;

namespace WageAPI;

/// <summary>
/// WageTrade 交易预算权威封装（2026-10-06，交易预算统一入口候选）。
/// 统一 StoreClient 预算读/写口：负值 clamp（防第三方 CustomerCreditBoost 溢出负预算→永不买）+ try/catch + 日志。
///
/// 【已实施（本封装只是收敛，非新修）】
/// - 负预算兜底已存在：Patches.Trade.Budget.cs:55（鲁滨逊分支 budget<0→0）、WageGirlSystem.Budget.cs:29（budget<=0 return）、:83 clientCash<0→0
/// - CustomerCreditBoost 溢出根因（int 指数累乘溢出+原版 OverrideBudget 无 clamp）已拆包实锤（10-05），mod 侧兜底=读口夹 0
///
/// 【红线（禁止收敛/改动）】
/// - PatchRegistry.cs:39-60/99-103 蛙哥工厂注入（SetBudget(1109707341,100)）=活代码一字不动
/// - WageBrother.cs:105 收购扩展（SetBudget(1109707341,1000)+SELLNBUY）=用户已实施项，行为等价即可
/// - 收购价下限（普通水 unitValue=0 卖 0 块）=用户搁置项（"等我说"），本封装不含
/// </summary>
public static class WageTrade
{
    /// <summary>权威读预算（负值→0；防第三方溢出负预算导致 HasBudgetLeftToBuy 永不买）。</summary>
    public static int GetBudget(StoreClient client)
    {
        try
        {
            int b = client.GetBudget();
            return b < 0 ? 0 : b;
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTrade] GetBudget 异常: " + ex.Message); return 0; }
    }

    /// <summary>权威写预算（负值→0；SetBudget 直写字段；long 内部 clamp 后转 int）。</summary>
    public static void SetBudget(StoreClient client, long value)
    {
        try
        {
            if (value < 0) value = 0;
            if (value > int.MaxValue) value = int.MaxValue;
            client.SetBudget((int)value);
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTrade] SetBudget 异常: " + ex.Message); }
    }

    /// <summary>权威写预算倍率结果（负值→0；OverrideBudget 直写字段无 clamp——读口已夹 0 防溢出放大）。</summary>
    public static void OverrideBudget(StoreClient client, long value)
    {
        try
        {
            if (value < 0) value = 0;
            if (value > int.MaxValue) value = int.MaxValue;
            client.OverrideBudget((int)value);
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTrade] OverrideBudget 异常: " + ex.Message); }
    }
}
