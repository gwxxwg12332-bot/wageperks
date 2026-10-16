// 10-16 闪退修复8：剧情挂点 Host 轻量化
// 实锤（11:11 主线程版日志）：崩 #165 GameItemElement.ApplyAnimationFrame——
//   剧情 6 条（Host=StoryPlotSystem 重类，含 Dialogue/GameOverUIManager 等 Il2Cpp 引用）插入数组 79-84，
//   Harmony 挂载时解析 Host 类型会递归遍历其成员引用类型 → 单条解析深度暴涨，
//   使后续条目（GameItemElement #165）在解析链更深处栈溢出（1.3.6 无剧情 Host，204 条全程过=用户实测能进）。
// 修法：新建轻量 Host（本类，仅 6 个 static 方法，方法体 try/catch 委托 StoryPlotSystem/StoryNewsSystem）
//   → Harmony 只解析本类（无 Il2Cpp 字段/成员引用）→ 解析深度 ≈ 普通条目。
//   配套：PatchRegistryTable 中 6 条剧情挂点 Host=typeof(StoryPlotHooks) 并移至数组末尾（前段顺序对齐 1.3.6）。
using System;

namespace WagePerks
{
    /// <summary>剧情 mod 挂载专用轻量 Host：只做 try/catch 委托，避免 Harmony 解析重类型。</summary>
    public static class StoryPlotHooks
    {
        public static void PostfixStoryNewsDayStart()
        {
            try { StoryNewsSystem.PostfixStoryNewsDayStart(); }
            catch (Exception ex) { Core.LogMsg("[StoryHooks] 新闻日更异常: " + ex.Message); }
        }

        public static void PostfixCheckStoryLineEleanor()
        {
            try { StoryPlotSystem.PostfixCheckStoryLineEleanor(); }
            catch (Exception ex) { Core.LogMsg("[StoryHooks] Eleanor 调度异常: " + ex.Message); }
        }

        public static void PostfixCheckStoryLineOfficerLun()
        {
            try { StoryPlotSystem.PostfixCheckStoryLineOfficerLun(); }
            catch (Exception ex) { Core.LogMsg("[StoryHooks] OfficerLun 调度异常: " + ex.Message); }
        }

        public static void PostfixCheckStoryLineGeorgio()
        {
            try { StoryPlotSystem.PostfixCheckStoryLineGeorgio(); }
            catch (Exception ex) { Core.LogMsg("[StoryHooks] Georgio 调度异常: " + ex.Message); }
        }

        public static void PostfixCheckStoryLineNellis()
        {
            try { StoryPlotSystem.PostfixCheckStoryLineNellis(); }
            catch (Exception ex) { Core.LogMsg("[StoryHooks] Nellis 调度异常: " + ex.Message); }
        }

        public static bool PrefixExecuteGameOver(string ending)
        {
            try { return StoryPlotSystem.PrefixExecuteGameOver(ending); }
            catch (Exception ex) { Core.LogMsg("[StoryHooks] 真结局异常: " + ex.Message); return true; }
        }
    }
}
