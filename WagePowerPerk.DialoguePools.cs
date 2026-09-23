using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class WagePowerPerk : CustomStartingPerk
{

    // ============================================================
    // 剧情系统 - 根据游戏天数和玩家行为变化对话
    // ============================================================

    // 获取剧情阶段
    private static string GetStoryPhase()
    {
        if (_gameDay <= 7) return "新手期";
        if (_gameDay <= 21) return "成长期";
        return "名声期";
    }




    // ============================================================
    // 故事性对话库 - 让每个客户都有独特的开场白
    // ============================================================





    // ============================================================

    // ============================================================
    // 退休枪匠专属对话（老手艺/军需品/军旅背景，赛博朋克底层）
    // ============================================================
    internal static readonly string[] GunsmithDialogues = {
        LangHelper.T("在上边干了三十年枪匠，如今下来也闲不住，手里还有几样压箱底的家伙事儿。", "Worked thirty years up top as a gunsmith. Can't stay idle down here - still got a few pieces stashed away."),
        LangHelper.T("这年头能玩枪的都当大爷了，也就我这把老骨头还愿意跟你们做点实在买卖。", "These days anyone who can handle a gun acts like a big shot. Just me and my old bones doing honest trade with you."),
        LangHelper.T("枪不是纸糊的，是拿命换来的手艺。你识货，我就给你好货。", "Guns aren't made of paper - they're a craft earned with blood. You know quality, I'll give you quality."),
        LangHelper.T("上面那些少爷兵，枪擦得锃亮，一梭子打不准。我这儿的东西，经得起折腾。", "Those rich-kid soldiers up top polish their guns shiny but can't hit a thing. My stuff survives real abuse."),
        LangHelper.T("退休了反倒更忙，隔三差五就得来你这儿换点零件，看看有没有顺手的家伙。", "Retired but busier than ever - come here every few days to trade parts, see if there's anything handy."),
        LangHelper.T("我这辈子就信两样：好枪和好价。别拿次品糊弄我，也别想占我便宜。", "I believe in two things in life: good guns and good prices. Don't fob off junk on me, and don't try to rip me off."),
        LangHelper.T("军需库清出来的老货，有些比那些新兵蛋子还老。懂行的人自然知道价值。", "Old stock cleared from the armory - some of it older than those fresh recruits. A man who knows his trade sees the value."),
        LangHelper.T("当年教新兵擦枪，一教就是三十年。现在教不动了，卖点好枪给你们这些识货的。", "Spent thirty years teaching recruits how to clean guns. Can't teach anymore, so I'll sell good guns to those who know."),
    };
    // 阵营对话库 - 普通客户按阵营说话（进店开场白）
    // 9阵营：scav/lower/upper/security/tourist/rev/blackmarket/cartel/church
    // 每阵营12条，共108条。语气对齐原版 cynical/黑色幽默/短句/潜台词
    // ============================================================
























    // ============================================================
    // 按玩家状态分类的对话 - 让对话根据玩家状态动态变化
    // ============================================================






}
