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
    // 新手期对话（客户对玩家还不太熟悉）
    private static readonly string[] NewbieDialogues = {
        LangHelper.T("你就是新来的当铺老板？听说你这收东西，我来看看。", "You're the new pawnshop owner? Heard you take things in. Just looking around."),
        LangHelper.T("第一次来你这，希望你给个公道价。", "First time here - hope you give me a fair price."),
        LangHelper.T("这店刚开不久吧？我来试试水。", "Place just opened, huh? I'll give it a try."),
        LangHelper.T("听说这一片新开了家当铺，我来看看靠不靠谱。", "Heard a new pawnshop opened around here. Came to see if you're legit."),
        LangHelper.T("新手老板，你可得给我个好价钱，不然我以后不来了。", "New boss, you better give me a good price, or I'm not coming back.")
    };
    // 成长期对话（客户开始认识玩家）
    private static readonly string[] GrowthDialogues = {
        LangHelper.T("老板，又来光顾了，最近生意不错吧？", "Boss, back again. Business treating you well?"),
        LangHelper.T("我跟你说，你这店现在在我们圈子里有点名气了。", "Let me tell you, your shop's getting a name in our circles."),
        LangHelper.T("上次在你这卖的东西价格不错，这次又带了点货来。", "You gave me a good price last time, so I brought more goods."),
        LangHelper.T("你这老板挺实在的，我愿意常来。", "You're an honest boss. I'll come by often."),
        LangHelper.T("听说你这收东西公道，我特意过来的。", "Heard you deal fair, so I came specially.")
    };
    // 名声期对话（玩家名声在外）
    private static readonly string[] FamousDialogues = {
        LangHelper.T("终于见到你本人了，你的名号在道上可是响当当的。", "Finally meeting you in person - your name carries weight on the street."),
        LangHelper.T("能进你这店的都不是一般人，我这货也不是一般货。", "Only special people walk into your shop, and my goods aren't ordinary either."),
        LangHelper.T("我可是专程来找你的，别人给的价我都没卖。", "I came all the way to you - turned down other buyers' offers."),
        LangHelper.T("你的名声在外，我信得过你，开个价吧。", "Your reputation precedes you. I trust you - name a price."),
        LangHelper.T("听说你这什么都收，我来看看是不是真的。", "Heard you buy anything. Came to see if it's true.")
    };
}
