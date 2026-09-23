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
    // 穷玩家对话（客户同情/看不起/想占便宜）
    private static readonly string[] PoorPlayerDialogues = {
        LangHelper.T("听说你这刚开张，手头紧吧？我给你个好价。", "Heard you just opened - cash-strapped, right? I'll give you a good price."),
        LangHelper.T("新店开张不容易，我这货便宜点给你。", "New shops have it rough - I'll cut you a deal on this."),
        LangHelper.T("看你这店空空的，是不是没钱进货？", "Your shop looks empty - out of money for stock?"),
        LangHelper.T("我这货不贵，你应该收得起。", "This isn't pricey - you should be able to afford it."),
        LangHelper.T("刚开店吧？我给你个机会，这货便宜卖你。", "Just opened? I'll give you a chance - cheap stock for you."),
        LangHelper.T("听说你这老板穷得叮当响，我来照顾照顾你生意。", "Heard you're flat broke, boss. Came to give you some business."),
        LangHelper.T("你这店能开下去吗？我看悬。", "Will this shop even survive? Looks doubtful."),
        LangHelper.T("我这货便宜，你收了吧，就当帮你撑场面。", "This is cheap - take it, call it helping you save face."),
        LangHelper.T("新店老板，给个实在价，我以后常来。", "New boss - give me a fair price and I'll be a regular."),
        LangHelper.T("看你这寒酸样，我这货就当送你了。", "Look at your shabby state - consider this a gift.")
    };
    // 富玩家对话（客户巴结/嫉妒/想卖高价）
    private static readonly string[] RichPlayerDialogues = {
        LangHelper.T("老板，听说你最近发了？我这有批好货。", "Boss, heard you struck it rich? I've got quality goods."),
        LangHelper.T("大老板，我这货可不便宜，你收得起吗？", "Big boss, this isn't cheap - can you afford it?"),
        LangHelper.T("听说你这生意越做越大，我来沾沾光。", "Heard your business is booming - came to soak up some of it."),
        LangHelper.T("有钱人就是不一样，你这店真气派。", "Rich folks are different - your shop looks grand."),
        LangHelper.T("老板，我这有批好货，专门给你留的。", "Boss, I saved a batch of good stock just for you."),
        LangHelper.T("听说你最近赚了不少，我这货给你个好价。", "Heard you've been raking it in - good price on this for you."),
        LangHelper.T("大老板，我这货你肯定看得上。", "Big boss, you'll definitely want this."),
        LangHelper.T("有钱人的店就是不一样，我来开开眼界。", "A rich man's shop is different - came to broaden my horizons."),
        LangHelper.T("老板，我这货只卖给识货的有钱人。", "Boss, I only sell this to wealthy men who know value."),
        LangHelper.T("听说你这老板出手大方，我来试试。", "Heard you're generous with your money. Giving you a try.")
    };
    // 新手玩家对话（客户试探/欺负/想占便宜）
    private static readonly string[] NewbiePlayerDialogues = {
        LangHelper.T("新来的吧？这行的规矩你懂吗？", "New around here? Know the rules of the trade?"),
        LangHelper.T("第一次开店？我给你上上课。", "First shop? Let me teach you a lesson or two."),
        LangHelper.T("新手老板，这货你知道值多少吗？", "New boss - do you even know what this is worth?"),
        LangHelper.T("看你这样子，是第一次做当铺生意吧？", "By the look of you - first time in the pawn business?"),
        LangHelper.T("新店开张，我来考考你眼力。", "New shop opening - let me test your eye."),
        LangHelper.T("新手就是新手，这价你也敢开？", "A rookie is a rookie - you dare quote that price?"),
        LangHelper.T("第一次收东西吧？我教你怎么看货。", "First time buying? I'll teach you how to appraise."),
        LangHelper.T("新店老板，别被人骗了，我这货是真的。", "New boss, don't get swindled - my goods are genuine."),
        LangHelper.T("看你生疏的样子，是刚入行吧？", "You look green - just got into the business?"),
        LangHelper.T("新手老板，给个实在价，别让人坑了。", "New boss, price it fair - don't let yourself get cheated.")
    };
    // 老手玩家对话（客户尊重/谨慎/不敢糊弄）
    private static readonly string[] VeteranPlayerDialogues = {
        LangHelper.T("老板，你这眼力我服，这货你肯定识。", "Boss, I respect your eye - you'll recognize this."),
        LangHelper.T("老行家了，我就不跟你绕弯子了。", "You're a veteran - I won't beat around the bush."),
        LangHelper.T("你这店开了有段时间了吧？我听说过你。", "Your shop's been around a while? I've heard of you."),
        LangHelper.T("老手就是不一样，这价你开得公道。", "A veteran's different - you price things fairly."),
        LangHelper.T("老板，你这名声在外，我不敢糊弄你。", "Boss, your reputation's out there - I wouldn't dare fool you."),
        LangHelper.T("行家一出手就知有没有，你看看这货。", "A pro knows at a glance - check this out."),
        LangHelper.T("你这老板我信得过，这货给你了。", "I trust you, boss - this is yours."),
        LangHelper.T("老生意人了，咱们直接谈价吧。", "Old hands at this - let's just talk price."),
        LangHelper.T("听说你这收东西最公道，我特意来的。", "Heard you're the fairest buyer - came on purpose."),
        LangHelper.T("你这店在这一片有名号，我放心。", "Your shop has a name in these parts - I'm at ease.")
    };
    // 坏名声玩家对话（客户警惕/不敢来/想卖高价）
    private static readonly string[] ShadyPlayerDialogues = {
        LangHelper.T("听说你这收过不少违禁品？我这有批货。", "Heard you've taken plenty of contraband? I've got a batch."),
        LangHelper.T("你这店名声不太好啊，我得小心点。", "Your shop's got a bad name - I need to be careful."),
        LangHelper.T("听说你这老板手黑，我这货你给个实在价。", "Heard you're ruthless, boss. Fair price on this."),
        LangHelper.T("你这店被查过不少次吧？我这货干净的。", "This shop's been raided a few times, right? My goods are clean."),
        LangHelper.T("听说你这什么都收，我这有批特别的货。", "Heard you'll take anything - I've got something special."),
        LangHelper.T("你这老板我听说过，手挺黑啊。", "I've heard of you, boss - you play rough."),
        LangHelper.T("你这店名声在外，不过是坏名声。", "Your shop's got a reputation - a bad one."),
        LangHelper.T("我这货干净的，你别给我弄成赃物。", "My goods are clean - don't turn them into stolen goods."),
        LangHelper.T("听说你这收过赃物？我这货可是正经来的。", "Heard you've taken stolen goods? Mine came legitimately."),
        LangHelper.T("你这老板我得防着点，别坑我。", "I need to watch out for you, boss - don't swindle me.")
    };
    // 好名声玩家对话（客户信任/慕名而来/愿意卖便宜）
    private static readonly string[] ReputablePlayerDialogues = {
        LangHelper.T("老板，你的名声我听说过，我信你。", "Boss, I've heard of your reputation - I trust you."),
        LangHelper.T("慕名而来，听说你这收东西最公道。", "Came for your name - heard you deal the fairest."),
        LangHelper.T("你的名声在道上响当当，我这货给你了。", "Your name carries weight on the street - this is yours."),
        LangHelper.T("听说你这老板实在，我特意来的。", "Heard you're an honest boss - came on purpose."),
        LangHelper.T("你的名声我信得过，这货你看着给。", "I trust your name - price it as you see fit."),
        LangHelper.T("道上的朋友都推荐你，我来看看。", "Everyone on the street recommends you - came to see."),
        LangHelper.T("你的名声在外，我不敢糊弄你。", "Your reputation precedes you - I wouldn't dare fool you."),
        LangHelper.T("听说你这从不坑人，我来试试。", "Heard you never cheat anyone - giving you a try."),
        LangHelper.T("你的名声我早就听说了，今天终于见到本人了。", "Heard of your name long ago - finally meeting you in person."),
        LangHelper.T("你这店口碑好，我放心把货卖给你。", "Your shop has a good name - I'm comfortable selling to you.")
    };
}
