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

    // ============================================================
    // 故事性对话库 - 让每个客户都有独特的开场白
    // ============================================================

    // 卖家专属对话（来卖东西的客户）
    // 加上开场白和过渡词，让对话更自然连贯
    private static readonly string[] SellerDialogues = {
        LangHelper.T("老板，我可算找到你了，跑了大半个城。这一片就你这收东西最公道，你看看这货。", "Boss, finally found you - crossed half the city. You're the fairest buyer around. Take a look at this."),
        LangHelper.T("哎，老板，跟你说个事。这货我藏了大半年，一般人我可不拿出来，听说你是识货的，你给看看。", "Hey boss, listen. I've been hiding this for half a year - wouldn't show it to just anyone. Heard you know your stuff. Take a look."),
        LangHelper.T("老板，实在不好意思，我是急用钱，没办法才来的。你看着给，别让我太亏就行。", "Boss, sorry about this. I need cash urgently, no other choice. Just don't screw me over too badly."),
        LangHelper.T("老板你看，这是我在废墟里淘了三天才弄到的，差点把命搭进去。你看看值多少？", "Boss, I dug this out of the ruins over three days, nearly died for it. What's it worth?"),
        LangHelper.T("那个……老板，这东西来路你别问，懂的都懂。给个好价，以后有好货我还来你这。", "Uh... boss, don't ask where this came from. You know how it is. Give me a good price and I'll bring more good stuff."),
        LangHelper.T("老板，这是我部队退伍带出来的，放我这也没用，占地方。你收了吧，给个实在价。", "Boss, brought this out of the army when I retired. No use to me now, just takes up space. Take it - fair price."),
        LangHelper.T("哎，老板，你看这个，实验室淘汰的，还能用，没坏。我寻思你这应该用得上，你收不收？", "Hey boss, look at this - lab surplus, still works, nothing broken. Figured you could use it. Buying?"),
        LangHelper.T("老板，刚到手的热乎货，你赶紧收了，别问哪来的，问就是捡的。你给个痛快价。", "Boss, fresh goods, hot off the hand. Take it quick, don't ask where - I found it. Just give me a clean price."),
        LangHelper.T("我跟你说啊老板，这东西我本来想自己留着的，要不是最近手头紧……唉，你看着给吧。", "Boss, I was gonna keep this for myself, but money's tight lately... ugh. Whatever you think is fair."),
        LangHelper.T("老板，行家一出手就知有没有，我就不跟你绕弯子了。你看看这货怎么样，给个价。", "Boss, a pro can spot quality at a glance, so I won't beat around the bush. Check this out and price it."),
        LangHelper.T("老板，我可是专程来找你的，别人给的价我都没卖，就信你这公道。你看看给多少？", "Boss, I came all the way to you - refused everyone else. I trust your fairness. What'll you give?"),
        LangHelper.T("老板你看，这批货来之不易，我跑了好几个地方才凑齐。你可得好好看看，别给低了。", "Boss, this batch was hard to get - hit up several places to gather it. Look closely, don't lowball me."),
        LangHelper.T("听说你这收东西最公道，我特意从老远过来的，路上还差点被安保查了。你看看这货。", "Heard you're the fairest buyer, so I came from far away - almost got stopped by security on the way. Check this out."),
        LangHelper.T("老板，好货不等人，你要是不收我可就找下家了啊。不过说实话，我还是想卖给你。", "Boss, good goods don't wait. If you don't take it, I'll find another buyer. But honestly, I'd rather sell to you."),
        LangHelper.T("能进你这店的都不是一般人，我这货也不是一般货。老板你是识货的，给个价吧。", "Only special people walk into your shop, and my goods aren't ordinary either. You know quality - name a price."),
        LangHelper.T("老板，先喝口水，不急。我跟你说，这货我可是挑了又挑才拿来的，你仔细看看。", "Boss, have some water first, no rush. I picked through everything to bring you this. Look it over carefully."),
        LangHelper.T("哎，老板，好久没来了，最近生意不错吧？我这有点好货，你给看看，给个实在价。", "Hey boss, long time no see. Business good? Got some quality goods here - take a look, fair price."),
        LangHelper.T("老板，我跟你说，这东西我本来想留着传家的，但是最近实在是缺钱……你收了吧。", "Boss, this was meant to be a family heirloom, but I'm really short on cash... take it."),
        LangHelper.T("老板，你看这货，成色不错吧？我可是费了好大劲才弄到手的。你给个价，合适我就卖。", "Boss, look at this - great condition, right? Took real effort to get. Name a price, if it's fair I'll sell."),
        LangHelper.T("行了老板，咱们都是痛快人，我也不跟你绕弯子了。这货你开个价，合适我就留下。", "Alright boss, we're both straight shooters, so I'll cut to it. Price this, if it's fair I'm staying.")
    };

    // 买家专属对话（来买东西的客户）
    // 加上开场白和过渡词，让对话更自然连贯
    private static readonly string[] BuyerDialogues = {
        LangHelper.T("老板，好久不见啊。最近手头宽裕，想来你这淘点好东西，钱不是问题。", "Boss, long time no see. Money's good lately, came to hunt for something nice. Cash is no problem."),
        LangHelper.T("哎，老板，跟你打听个事。我最近需要点实用的东西，你这有没有什么靠谱的推荐？", "Hey boss, quick question. I need something practical lately. Got any reliable recommendations?"),
        LangHelper.T("老板你好啊，我又来了。这次我想多扫点货，价钱合适的话我全包了。", "Boss, I'm back again. Want to stock up this time - if the price is right, I'll take it all."),
        LangHelper.T("嗨，第一次来你这店，先随便看看。对了，有什么好东西推荐吗？我预算不多，别太贵。", "Hi, first time here, just browsing. Got anything good to recommend? Small budget, nothing too pricey."),
        LangHelper.T("老板，又来光顾你生意了。最近有没有进什么新货？我来看看有没有合眼缘的。", "Boss, back for more business. Any new stock lately? Looking for something that catches my eye."),
        LangHelper.T("那个……老板，我想找个特定的东西，跑了好几家店都没找着，你这有没有？", "Uh... boss, I'm looking for something specific. Tried several shops, nothing. You got it?"),
        LangHelper.T("老板，别忙了，先招呼我。把你这最好的东西拿出来看看，价钱随便开，我不还价。", "Boss, stop what you're doing and serve me. Show me your best - price it however, I won't haggle."),
        LangHelper.T("哟，老板，还记得我不？我可是你的老主顾了，这次给个优惠价怎么样？", "Hey boss, remember me? I'm one of your regulars. How about a discount this time?"),
        LangHelper.T("老板你好，我听朋友说你这什么都有，特意过来看看，是不是真的像传说中那么全。", "Hello boss, friends said you've got everything. Came to see if the legend's true."),
        LangHelper.T("嗨，老板，是朋友推荐我来的，说你这东西最全、价最实。我来看看有没有我需要的。", "Hi boss, a friend recommended you - best selection, best prices. Seeing if you've got what I need."),
        LangHelper.T("老板，跟你商量个事。我想给家里添点东西，你这有什么合适的？给我推荐推荐。", "Boss, got a question. Want to get something for the home - what fits? Give me some recommendations."),
        LangHelper.T("老板，今天我可带够钱了，就等你这有好货。赶紧拿出来让我开开眼。", "Boss, I brought plenty of cash today, waiting for the good stuff. Show me something impressive."),
        LangHelper.T("哎，老板，我跟你说，我收藏缺了几样，跑了大半个城都没找齐，看看你这能不能补齐。", "Hey boss, my collection's missing a few pieces. Tried half the city - can you fill the gaps?"),
        LangHelper.T("老板，我跟你说句掏心窝子的话。听说你这有黑市货？你放心，我懂规矩的。", "Boss, I'll be straight with you. Heard you got black market goods? Relax, I know the rules."),
        LangHelper.T("行了老板，别藏着掖着了，咱们都是明白人。把压箱底的好东西拿出来吧，我等不及了。", "Alright boss, stop hiding things - we're both smart people. Bring out your best stuff, I can't wait."),
        LangHelper.T("老板，先别急着做生意，喝口水。对了，我这次来是想看看你这有没有什么稀罕物件。", "Boss, no rush on business, have some water. I came to see if you've got any rare finds."),
        LangHelper.T("哟，老板，你这店越开越红火了啊。我来凑个热闹，看看有没有什么值得买的。", "Hey boss, your shop's booming! Thought I'd join the crowd and see what's worth buying."),
        LangHelper.T("老板，我跟你说，我最近手头有点紧，但还是想来你这看看。有没有便宜点的好货？", "Boss, money's a bit tight lately, but I still wanted to come by. Got any good deals?"),
        LangHelper.T("嗨，老板，好久没来你这了，怪想你的。顺便来看看有没有什么新东西，给我介绍介绍。", "Hi boss, haven't been here in a while, missed you. Came to see what's new - give me a tour."),
        LangHelper.T("老板，我就直说了吧。我需要一批货，量不小，你这能不能一次性给我凑齐？", "Boss, I'll be direct. I need a batch of goods, decent amount. Can you fill it all at once?")
    };

    // 通用对话（所有客户都可能用）
    private static readonly string[] GeneralDialogues = {
        LangHelper.T("终于见到你本人了，你的名号可是如雷贯耳啊。", "Finally meeting you in person - your name rings loud and clear."),
        LangHelper.T("最近生意怎么样？我给你带了点好东西。", "How's business lately? Brought you something good."),
        LangHelper.T("有人让我来找你，说你这什么都能收。", "Someone sent me your way, said you'll buy anything."),
        LangHelper.T("废话不多说，看货吧。", "Enough talk, just look at the goods."),
        LangHelper.T("能进你这店的都不是一般人。", "Only special people walk into your shop."),
        LangHelper.T("我可是慕名而来，希望你别让我失望。", "I came for your reputation - hope you don't disappoint."),
        LangHelper.T("这地方不好找啊，我绕了三圈才找到。", "This place is hard to find - circled three times before I got here."),
        LangHelper.T("听说你这的规矩是现金交易，我带够了。", "Heard you only do cash. Brought plenty."),
        LangHelper.T("第一次来，希望我们合作愉快。", "First time here - hope we do good business."),
        LangHelper.T("你的名声在外，我信得过你。", "Your reputation precedes you. I trust you.")
    };

    // 特殊客户对话（博士等老朋友）
    private static readonly string[] SpecialDialogues = {
        LangHelper.T("老朋友，我又给你带好东西来了。", "Old friend, brought you something good again."),
        LangHelper.T("这次的货可不一般，你肯定感兴趣。", "This batch is special - you'll definitely be interested."),
        LangHelper.T("我可是冒着风险来的，你得给个好价。", "I came here at my own risk - you better give me a good price."),
        LangHelper.T("咱们之间就不用客套了，直接看货。", "No need for pleasantries between us - straight to the goods.")
    };

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

    // 拾荒者 Scav（卖为主 · 粗粝/直白/底层生存直觉/防人之心）
    private static readonly string[] ScavDialogues = {
        // 曾经是退伍军人，现在沦落到捡垃圾
        LangHelper.T("刚从倾倒区上来，一身灰，别嫌弃。以前在部队的时候，哪能想到自己会沦落到捡垃圾。", "Just came up from the dumping grounds, covered in dust, don't mind me. Back in the army I never thought I'd end up scrapping."),
        LangHelper.T("捡到点破烂，看看你收不收。别小看这些，以前我在部队管军械的时候，这些都是好东西。", "Found some junk, see if you'll take it. Don't look down on it - back when I ran army armories, this was all good stuff."),
        LangHelper.T("听说你这给信用点，给钱痛快不？退伍费早就花完了，不捡垃圾就得饿死。", "Heard you pay in credits - you pay up quick? My pension ran out ages ago. If I don't scrap, I starve."),
        LangHelper.T("好东西早被安保收走了，剩下的都是我从垃圾堆里翻出来的。以前在部队，这种东西我看都不看。", "Security already grabbed all the good stuff. What's left I dug out of the trash. Back in the army, I wouldn't even look at this."),
        LangHelper.T("你眼神行不行啊？我这可都是从倾倒区深处翻出来的好货。以前在部队练出来的眼神，差不了。", "Got good eyes? This is prime stuff dug from deep in the dumping grounds. Eyes trained in the army - they don't lie."),
        LangHelper.T("别压太狠，这趟我差点把命搭在倾倒区。以前在边境执行任务都没这么险，现在为了点破烂拼命。", "Don't squeeze me too hard - I nearly died in the dumping grounds this trip. Border missions weren't this dangerous. Now I risk my life for scraps."),
        // 曾经是工程师，现在沦落到捡垃圾
        LangHelper.T("今天倾倒区有个废弃的工厂，我从里面翻出点东西。以前我就是在这种工厂上班的，现在只能来捡垃圾。", "There's an abandoned factory in the dumping grounds today - pulled some stuff out of it. I used to work in a place like that. Now I just scrap."),
        LangHelper.T("这玩意儿我留着没用，以前我还能修修，现在手都生了，你看着给个价。", "No use for this now. Used to be I could fix it, but my hands have gone rusty. Whatever you think it's worth."),
        LangHelper.T("我一般不去店里卖，店里的都吃人。你是例外，看你像个实在人。以前在公司上班的时候，也是这么实在，结果被裁了。", "I don't usually sell to shops - they eat people alive. You're the exception, seem like a straight shooter. I was straight with the company too, and they laid me off."),
        LangHelper.T("上趟捡的破烂，换点信用点，得活命啊。以前是工程师，现在是拾荒客，世道变了。", "Scraps from last trip, trading them for credits to stay alive. Used to be an engineer, now a scavenger. Times change."),
        LangHelper.T("垃圾场翻出点能用的，你给看看值多少。这些东西以前都是我设计的，现在只能从垃圾堆里捡。", "Found some usable stuff in the dump - see what it's worth. I used to design these things. Now I pick them out of trash."),
        // 曾经是工人，现在沦落到捡垃圾
        LangHelper.T("听说你这不坑人，我才敢进门的。以前在工厂上班的时候，被老板坑过，现在对谁都提防着。", "Only came in because I heard you don't cheat people. Got screwed by my boss back in the factory - now I trust nobody."),
        LangHelper.T("这东西是从三楼掉下来的，还热乎着呢。以前我在三楼上班，现在只能在楼下捡垃圾。", "This fell off the third floor - still warm. I used to work on that floor. Now I pick garbage below it."),
        LangHelper.T("安保刚搜过我身，啥也没搜着。你懂的，以前在工厂的时候，就学会怎么藏东西了。", "Security just frisked me - found nothing. You know how it is. Learned how to hide things back at the factory."),
        LangHelper.T("这趟走得远，从旧城区背回来的。以前在旧城区的工厂上班，现在工厂倒闭了，只能去捡垃圾。", "Long trip this time - carried it back from Old Town. Used to work at the factory there. It shut down, so now I scrap."),
        // 通用拾荒客（提到捡垃圾、倾倒区）
        LangHelper.T("别问哪来的，问就是捡的。拾荒客的东西，哪来的不重要，能用就行。", "Don't ask where it came from - if you ask, I found it. A scavenger's stuff: where it's from doesn't matter, only that it works."),
        LangHelper.T("这玩意儿我藏了三天，就等个识货的。从倾倒区翻出来的，不容易。", "Hid this for three days, waiting for someone who knows value. Dug it out of the dumping grounds - wasn't easy."),
        LangHelper.T("倾倒区今天有酸雨，我是冒雨去的。这些东西都是雨里抢出来的，你给个实在价。", "Acid rain over the dumping grounds today - went out in it anyway. Dragged these out through the rain. Give me a real price."),
        LangHelper.T("这东西本来是给我自己留的，实在没钱了。拾荒客能有什么值钱东西，你看着给吧。", "Was keeping this for myself, but I'm out of money. What does a scavenger own that's worth anything? You decide."),
        LangHelper.T("你要是不收，我就得去下一家碰运气了。拾荒客不容易，到处碰壁。", "If you won't take it, I'll have to try my luck elsewhere. Life's hard for a scavenger - doors slam everywhere."),
        LangHelper.T("刚从倾倒区回来，累得腰都直不起来。这些是今天的收获，你给看看。", "Just got back from the dumping grounds, can barely stand straight. Today's haul - take a look."),
        LangHelper.T("以前觉得拾荒客丢人，现在自己干了这行，才知道不丢人，能活着就不错了。", "Used to think scrapping was shameful. Now that I do it, I know it isn't - just staying alive is enough."),
        LangHelper.T("倾倒区的东西，都是上层人扔的。他们扔的破烂，就是我们的宝贝。世道就是这样。", "Everything in the dumping grounds was thrown away by the uppers. Their trash is our treasure. That's just how the world works."),
        LangHelper.T("干拾荒这行，得眼神好，手脚快，还得不怕脏不怕累。我以前干别的，现在都学会了。", "Scrapping takes sharp eyes, quick hands, and a stomach for filth. I did other work before - learned all of it now."),
        // 出生就在下层区，从头到尾就是下层人的拾荒客
        LangHelper.T("从小就在倾倒区捡垃圾，干了二十年了。这些东西，我一眼就知道值多少钱。", "Been scrapping in the dumping grounds since I was a kid - twenty years now. I can tell what this is worth at a glance."),
        LangHelper.T("我家三代都是拾荒客，我爷爷捡，我爹捡，现在轮到我捡。这就是命。", "Three generations of scavengers - my grandpa picked, my dad picked, now it's my turn. That's fate."),
        LangHelper.T("从来没上过楼，也不知道上层区是什么样。就在这下层区捡垃圾，能活着就行。", "Never been up a level, don't know what the uppers look like. Just scrapping in the lower level - surviving is enough."),
        LangHelper.T("从小就没了爹妈，是在垃圾堆里长大的。这些东西，就是我的全部家当。", "Lost my parents young, grew up in a pile of garbage. These things are everything I own."),
        LangHelper.T("干拾荒这行，我从小干到大，闭着眼都能在倾倒区找到好东西。你给个实在价。", "Scrapped my whole life - I can find good stuff in the dumping grounds with my eyes closed. Give me a fair price."),
        LangHelper.T("从来没干过别的，也不会干别的。除了捡垃圾，我什么都不会。你收不收？", "Never did anything else, don't know how. All I know is picking garbage. You buying or not?"),
        LangHelper.T("出生就在这下层区，长这么大没离开过。倾倒区就是我的家，垃圾就是我的伙伴。", "Born in this lower level, never left. The dumping grounds are my home, trash is my companion."),
        LangHelper.T("小时候跟着爹捡垃圾，爹死了就自己捡。现在我也快老了，还是在捡垃圾。这就是命。", "Followed my dad scrapping as a kid. When he died, I kept going alone. Now I'm getting old and still scrapping. That's fate."),
        LangHelper.T("从来没见过上层区什么样，也不想见。就在这下层区捡垃圾，自由自在，没人管。", "Never seen the upper level, don't want to. Just scrapping down here - free, no one telling me what to do."),
        LangHelper.T("干拾荒这行，得能吃苦，能受气，还得不怕脏不怕累。我从小就练出来了。", "Scrapping means enduring hardship, swallowing insults, and never flinching from filth. Been trained since childhood.")
    };

    // 下层医生专属对话（在下层区开小诊所，给穷人看病，疲惫但专业）
    private static readonly string[] LowerDoctorDialogues = {
        // 卖家（卖药品、医疗用品）
        LangHelper.T("老板，我这有点剩的药品，你收不收？都是正规渠道来的，不是假药。", "Boss, got some leftover medicine - taking it? All from legit channels, no fakes."),
        LangHelper.T("唉，今天诊所又忙了一天，累得腰都直不起来了。这有点医疗用品，你给看看值多少。", "Sigh, clinic ran nonstop today, my back's about to break. Some medical supplies here - see what they're worth."),
        LangHelper.T("你知道下层区看病有多难吗？我这诊所一天要看几十个病人，药品根本不够用。这有点多余的，你收了吧。", "You know how hard it is to get care down here? My clinic sees dozens of patients a day - medicine never lasts. This is surplus - take it."),
        LangHelper.T("这是我从上层区医院弄来的淘汰设备，还能用，你收不收？下层区的诊所都需要这个。", "Phased-out equipment from the upper-level hospital - still works. Taking it? Every clinic down here needs this."),
        LangHelper.T("老板，我跟你说，这药可是好东西，上层区的医院都用这个。你给个实在价，我诊所还等着买药呢。", "Boss, this medicine is good stuff - upper-level hospitals use it. Give me a fair price, my clinic needs to buy more."),
        LangHelper.T("今天又有个病人没钱看病走了，唉……这世道。这有点药品，你收了吧，便宜点也行。", "Another patient left today because they couldn't pay. Sigh... this world. Some medicine here - take it, even cheap."),
        LangHelper.T("我这诊所快开不下去了，药品太贵，病人又付不起钱。这有点医疗用品，你给看看。", "My clinic's about to shut down - medicine's too expensive and patients can't pay. Some medical supplies here, take a look."),
        LangHelper.T("你知道下层区的医生有多难当吗？既要会看病，又要会讨价还价，还得防着安保来查。这有点药，你收了吧。", "You know how hard it is being a doctor down here? Need to know medicine, haggling, and dodging security checks. Some medicine - take it."),
        // 买家（买医疗设备、药品原料）
        LangHelper.T("老板，你这有什么医疗设备吗？我诊所的设备都坏了，急需替换。", "Boss, got any medical equipment? Everything in my clinic broke - I need replacements urgently."),
        LangHelper.T("你这有药品原料吗？我想自己配点药，上层区的药太贵了，下层人买不起。", "Got any raw pharmaceutical ingredients? Want to compound my own - upper-level drugs are too pricey for us."),
        LangHelper.T("老板，你这有绷带、消毒水之类的吗？我诊所都用完了，今天又有几个受伤的。", "Boss, got bandages, antiseptic, anything like that? My clinic ran out - more injured came in today."),
        LangHelper.T("你这有什么便宜的药品吗？我诊所病人都付不起钱，我得找点便宜药给他们用。", "Got any cheap medicine? My patients can't pay, so I need affordable options for them."),
        LangHelper.T("老板，你这有X光机或者扫描仪吗？我诊所连个像样的检查设备都没有，全靠经验看病。", "Boss, got an X-ray machine or scanner? My clinic has no proper diagnostic equipment - I diagnose by experience alone."),
        LangHelper.T("你这有什么抗生素吗？下层区感染的人太多了，抗生素根本不够用。", "Got any antibiotics? So many infections down here - antibiotics never last."),
        LangHelper.T("老板，你这有手术器械吗？我诊所的器械都用了十几年了，早就该换了。", "Boss, got any surgical instruments? Mine are over a decade old - long past due for replacement."),
        LangHelper.T("你这有什么止痛药吗？下层区的人天天干重活，浑身都是病，止痛药需求量很大。", "Got any painkillers? People down here do heavy labor every day, bodies falling apart - huge demand for painkillers.")
    };

    // 下层公民 Lower（买/卖 · 疲惫/精打细算/认命）
    private static readonly string[] LowerDialogues = {
        LangHelper.T("发工资了，看看你这有什么。", "Payday - let's see what you've got."),
        LangHelper.T("孩子要过生日，想买点像样的东西。", "Kid's birthday coming up, want to get something decent."),
        LangHelper.T("这周配给又缩水了，来买点吃的。", "Rations got cut again this week - here for food."),
        LangHelper.T("我有点用不上的东西，你收不收？", "Got some stuff I don't need - taking it?"),
        LangHelper.T("朋友说你这价公道，我来试试。", "Friend said your prices are fair. Giving you a try."),
        LangHelper.T("便宜点，都是街坊。", "Cut me a break - we're neighbors."),
        LangHelper.T("别卖我假货啊，我上过一次当了。", "Don't sell me fakes - got burned once already."),
        LangHelper.T("这周手头紧，但家里人等着吃饭。", "Money's tight this week, but the family's waiting to eat."),
        LangHelper.T("再便宜点，我下周还来。", "A little cheaper and I'll be back next week."),
        LangHelper.T("至少你没宰我，这年头难得。", "At least you didn't rip me off - rare these days."),
        LangHelper.T("发薪日，看看能添置点什么。", "Payday - seeing what I can add to the place."),
        LangHelper.T("你这家店，在下层区还算有点名气。", "Your shop's got a bit of a name down here."),
        LangHelper.T("工厂又裁员了，我来卖点家当。", "Factory laid people off again - selling off some belongings."),
        LangHelper.T("老婆让我来买点东西，别让我跪搓衣板。", "Wife told me to pick up some things - don't make me sleep on the couch."),
        LangHelper.T("这东西我用了三年，还能用，你收了吧。", "Used this for three years, still works. Take it."),
        LangHelper.T("听说你这收旧家电？我那台坏了，你看看。", "Heard you take old electronics? Mine's broken - take a look."),
        LangHelper.T("孩子上学要添置点东西，你这有合适的吗？", "Kid needs stuff for school - got anything suitable?"),
        LangHelper.T("房租又涨了，来卖点东西凑凑。", "Rent went up again - selling stuff to make ends meet."),
        LangHelper.T("我妈病了，需要钱买药，你看看这些值多少。", "Mom's sick, need money for medicine. See what this is worth."),
        LangHelper.T("都是过日子的人，给个实在价。", "We're all just trying to get by - give me a fair price.")
    };

    // 上层人专属对话 - 卖家（赛博朋克空间站背景，多样化售卖动机：销赃/缺钱/叛逆/家族斗争/洗钱/违禁品等）
    private static readonly string[] UpperClassSellerDialogues = {
        // 销赃型（上层人也有见不得光的东西，需要下层区渠道）
        LangHelper.T("别问哪来的。我家里那位的东西，他不知道。你赶紧收，出了事我不认识你。", "Don't ask where it's from. It's my spouse's - they don't know. Take it fast; if anything happens, you never saw me."),
        LangHelper.T("这是我从公司实验室拿出来的，还没上市。你识货就给个好价，不识货我找别人。", "Took this from the company lab - not even on the market yet. If you know quality, pay well. If not, I'll find someone else."),
        LangHelper.T("嘘……小声点。这是上层区拍卖会的展品，我偷出来的。你赶紧收，别让人看见。", "Shh... keep it down. This is from an upper-level auction, I stole it. Take it fast, don't let anyone see."),
        // 缺钱型（上层人也会赌输、负债、被断粮）
        LangHelper.T("……老板，急用钱。上周赌马输了，不敢跟家里说。这东西你给个实在价，我下次还有。", "...Boss, I need cash. Lost at the races last week, can't tell my family. Fair price on this and I'll have more next time."),
        LangHelper.T("我信用卡爆了，这个月额度用完了。这东西你收了吧，价格好说，别让我家里人知道。", "Maxed out my credit card, monthly limit's gone. Take this - price is flexible, just don't let my family find out."),
        LangHelper.T("……跟你说实话，我被家里断粮了。就因为我跟那个下层区的人交往。这东西你收了，我得活下去。", "...Honestly, my family cut me off. Just because I was seeing someone from the lower level. Take this - I need to survive."),
        // 叛逆型（年轻人反对家族，想体验底层生活）
        LangHelper.T("我家里让我去公司上班，我偏不。我要自己赚钱，体验下层人的生活。这东西你收了，算我第一桶金。", "My family wants me at the company, but I refuse. I'll earn my own money, experience lower-level life. Take this - call it my first capital."),
        LangHelper.T("我跟我爸吵架了，他说我离了家什么都不是。我就证明给他看，我自己也能活下去。这东西你收不收？", "Fought with my father - he said I'm nothing without the family. I'll prove I can survive on my own. You taking this or not?"),
        LangHelper.T("上层区的生活太虚伪了，人人都戴着面具。我想真实一点，所以来下层区卖东西。你给个价，别可怜我。", "Upper-level life is so fake - everyone wears a mask. I want something real, so I came down to sell. Price it, and don't pity me."),
        // 家族斗争型（夺权失败，需要变卖资产跑路）
        LangHelper.T("……老板，我家里出事了。我哥夺权，把我赶出来了。这东西你收了，我得跑路，去别的空间站。", "...Boss, my family's in chaos. My brother seized power and kicked me out. Take this - I need to flee to another station."),
        LangHelper.T("别问我是谁。我家里在斗争，我站错队了。这东西你赶紧收，价格随便给，我得马上走。", "Don't ask who I am. There's a power struggle in my family and I backed the wrong side. Take this quick, any price - I have to leave now."),
        LangHelper.T("我爸死了，我妈跟我争遗产。我趁乱拿了点东西出来，你收了吧。别问，问就是不知道。", "My father died and my mother's fighting me over the inheritance. Grabbed some things in the chaos. Take them - if you ask, I know nothing."),
        // 洗钱型（通过下层区店铺转移资产）
        LangHelper.T("老板，我有批货想从你这过一下。你按原价收，然后我再买回来，差价给你。你懂的，走个账。", "Boss, I need to run a batch of goods through you. Buy at full price, I'll buy it back, you keep the difference. You know - laundering."),
        LangHelper.T("这东西你先收了，过几天我让人来买回去。价格你随便开，越高越好。明白吗？走个流程而已。", "Take this first; I'll send someone to buy it back in a few days. Price it as high as you want. Understand? Just paperwork."),
        // 违禁品型（上层区不让卖的东西，需要下层区渠道）
        LangHelper.T("……老板，你这收特殊东西吗？就是那种……上层区不让卖的。我有渠道，你要是收，我们长期合作。", "...Boss, do you take special items? The kind... not allowed in the upper level. I have channels. Take them and we'll have a long partnership."),
        LangHelper.T("这东西你认识吧？上层区的人都用，但是不让公开卖。我从家里偷出来的，你给个价，我下次还有。", "You recognize this? Everyone up top uses it, but it can't be sold openly. Stole it from home. Price it - I'll have more."),
        // 处理闲置型（家里东西太多，或者前任送的）
        LangHelper.T("我家衣帽间要翻新，这些东西放不下了。扔了可惜，拿来卖了。你给个价，合适我以后还有。", "Renovating my walk-in closet - these don't fit anymore. Shame to throw away, so I'm selling. Price it; if it's fair, there's more coming."),
        LangHelper.T("这是我前任送的，看着烦。你收了吧，价格随便给，我就是不想再看见它。", "My ex gave me this. Can't stand looking at it. Take it, any price - I just don't want to see it again."),
        // 体验生活型（无聊，下来体验）
        LangHelper.T("上层区太无聊了，人人都在装。我下来体验一下真实的生活，卖个东西玩玩。你给个价，别太高，我就是体验一下。", "The upper level is so boring - everyone's acting. Came down to experience real life, selling something for fun. Price it low - I'm just trying it out."),
        LangHelper.T("我在写一本书，关于下层区的生活。为了体验，我来卖个东西。你配合一下，给个价，就当是采访了。", "Writing a book about lower-level life. To experience it, I'm selling something. Play along and price it - consider it an interview."),
        // 炫耀型（优越感）
        LangHelper.T("看到这logo了吗？限量版，全球一百个。你这破店估计也配不上，但是我今天心情好，便宜给你。", "See this logo? Limited edition, a hundred in the world. Your shabby shop doesn't deserve it, but I'm in a good mood - cheap for you."),
        LangHelper.T("去年在新东京买的，三个月零花钱而已。你这种人估计连机票都买不起吧？给个价，别让我失望。", "Bought it in Neo Tokyo last year - three months of allowance, nothing. Someone like you probably can't even afford a ticket. Price it, don't disappoint."),
        // 不耐烦型（赶时间）
        LangHelper.T("行了行了，别问那么多，你就说收不收。我还得回去做指甲，没时间跟你耗。", "Enough questions - just tell me if you're taking it. I have a manicure appointment, no time to waste on you."),
        LangHelper.T("你能不能快点？我约了人喝下午茶，迟到了可不好。这东西你到底收不收？", "Can you hurry? I have an afternoon tea appointment - being late won't do. Are you taking this or not?"),
        LangHelper.T("我数三下，你给个价。一……二……算了，你这人真墨迹，我拿去别家卖了。", "I'll count to three - give me a price. One... two... forget it, you're too slow. I'll sell it elsewhere.")
    };

    // 破产上层人专属对话 - 买家（多种性格：嘴硬装富/无奈/怀旧/算计/颓废/愤世嫉俗）
    private static readonly string[] BankruptUpperBuyerDialogues = {
        // 嘴硬装富型
        LangHelper.T("把你们这……把你们这最好的拿出来。别误会，我就是想看看，不一定买。", "Show me... show me your best. Don't get me wrong - I'm just looking, might not buy."),
        LangHelper.T("钱？钱当然不是问题。但是我今天没带那么多现金，你这能不能……能不能赊账？", "Money? Money's obviously not the issue. But I didn't bring much cash today - could you... could you give me credit?"),
        LangHelper.T("这东西也敢卖这么贵？算了，我买了。但是你得给我便宜点，我……我就是觉得不值这个价。", "You dare charge this much? Fine, I'll take it. But you have to lower the price - I... I just don't think it's worth it."),
        // 无奈型
        LangHelper.T("老板，有没有便宜点的东西？我……我就是想买点日用品。不用太好的，能用就行。", "Boss, anything cheaper? I... I just need some daily necessities. Nothing fancy, just functional."),
        LangHelper.T("唉，这东西多少钱？能不能再便宜点？我……我最近手头有点紧，你就行行好。", "Sigh, how much is this? Can you go lower? I... money's been tight lately. Have a heart."),
        LangHelper.T("老板，你这有没有打折的？或者快过期的？我不嫌弃，只要能用就行。", "Boss, got any discounts? Or soon-to-expire stuff? I don't mind - as long as it works."),
        // 怀旧型
        LangHelper.T("这东西……我以前也有一个，那时候我家还没破产……唉，算了。这东西多少钱？我买了。", "This... I used to have one, back when my family wasn't bankrupt... sigh, never mind. How much? I'll take it."),
        LangHelper.T("看到这东西，我就想起以前的日子。那时候我家这种东西多得是，现在……现在连一个都买不起了。", "Seeing this reminds me of the old days. My family had plenty of these back then. Now... now I can't even afford one."),
        LangHelper.T("这是我以前常用的牌子，没想到在你这还能看到。多少钱？我买了，就当是个纪念。", "This is the brand I used to use - didn't expect to see it here. How much? I'll take it, as a memento."),
        // 算计型
        LangHelper.T("老板，我跟你说，这东西不值这个价。你看这做工，这材质，最多值一半。你便宜点我就买了。", "Boss, this isn't worth the asking price. Look at the craft, the material - worth half at most. Lower it and I'll buy."),
        LangHelper.T("你这东西能不能再便宜点？我可是老顾客了，以后还会常来的。你就给个优惠价呗。", "Can you go cheaper? I'm a regular, I'll keep coming back. Just give me a deal."),
        LangHelper.T("老板，我看你这东西放了挺久了吧？是不是卖不出去？这样，我给你个成本价，你卖给我，你也不亏。", "Boss, this has been sitting here a while, hasn't it? Not selling? Tell you what - I'll give you cost price. You sell, you don't lose."),
        // 颓废型
        LangHelper.T("……这东西多少钱？随便吧，能买就买，不能买就算了。我已经无所谓了。", "...How much is this? Whatever - buy it if I can, forget it if I can't. I've stopped caring."),
        LangHelper.T("唉……有没有能解愁的东西？酒啊什么的。多少钱都行，只要能让我忘了现在的日子。", "Sigh... got anything to drown sorrows in? Booze, anything. Whatever the price, as long as it makes me forget these days."),
        LangHelper.T("……这东西你要吗？不对，我是说这东西卖吗？算了，我不买了，反正买了也没用。", "...Do you want this? No wait - is this for sale? Never mind, I won't buy it. What's the point anyway."),
        // 愤世嫉俗型
        LangHelper.T("这世道，真是不公平。想当年我想买什么就买什么，现在……现在连个便宜货都要犹豫半天。", "This world is so unfair. Back then I bought whatever I wanted. Now... now I hesitate over cheap goods for ages."),
        LangHelper.T("你知道我以前都在哪买东西吗？算了，跟你说了你也不信。这东西多少钱？我买了，就当是体验生活。", "You know where I used to shop? Forget it, you wouldn't believe me. How much? I'll take it - call it a life experience."),
        LangHelper.T("哼，要不是我家那个败家子把家产败光了，我才不会来这种地方买东西。这东西多少钱？", "Hmph, if my good-for-nothing kid hadn't blown the family fortune, I'd never shop in a place like this. How much?"),
        // 舍不得型
        LangHelper.T("这东西……能不能再便宜点？我真的很想买，但是……但是我钱不够。你就行行好，便宜点卖给我吧。", "This... can you go a bit lower? I really want it, but... but I don't have enough. Be kind - sell it cheaper, please."),
        LangHelper.T("老板，我跟你说实话吧，我已经好几天没吃顿好的了。这东西能不能……能不能便宜点卖给我？", "Boss, honestly, I haven't had a decent meal in days. Could you... could you sell this a little cheaper?"),
        LangHelper.T("这是给我女儿买的，她生日快到了。我……我钱不够，你能不能便宜点？我求求你了。", "This is for my daughter - her birthday's coming. I... I don't have enough. Can you go lower? Please, I'm begging you.")
    };

    // 破产上层人专属对话 - 卖家（曾经的上层人，现在破产了，不得不卖东西维生）
    private static readonly string[] BankruptUpperSellerDialogues = {
        // 嘴硬装富型
        LangHelper.T("这东西？这是我以前用剩下的。本来不想卖的，但是家里放不下了。你给个价，别太低，丢我的人。", "This? Leftover from my old life. Didn't want to sell, but there's no room at home. Name a price - not too low, it'd embarrass me."),
        LangHelper.T("你识货吗？这可是限量版。要不是我最近……最近手头有点紧，我才不会卖这种东西。你给个价。", "Do you even know quality? This is limited edition. If I weren't... weren't short on cash lately, I'd never sell it. Name a price."),
        LangHelper.T("别问我为什么卖。我就是……就是想换点现金。这东西你收不收？不收我找别人，有的是人要。", "Don't ask why I'm selling. I just... just want some cash. Taking it or not? If not, I'll find someone - plenty would want it."),
        // 无奈型
        LangHelper.T("……老板，这东西你收吗？我……我实在是没办法了。房租欠了三个月，再不交就要被赶出去了。你给个实在价。", "...Boss, will you take this? I... I have no choice. Three months behind on rent - eviction's next. Give me a fair price."),
        LangHelper.T("唉，这是我最后一件值钱的东西了。卖了它，我就真的什么都没了。但是……但是我得活下去啊。你收了吧。", "Sigh, this is my last valuable possession. Once it's gone, I'll have nothing. But... but I have to survive. Take it."),
        LangHelper.T("老板，你就当可怜可怜我。这东西本来值不少钱，但是我急用钱，你随便给点就行。我……我已经走投无路了。", "Boss, have some pity. This was worth a lot, but I need cash urgently - any amount works. I... I'm at the end of my rope."),
        // 怀旧型
        LangHelper.T("这东西……这是我结婚的时候买的。那时候我家还没破产，我妻子还在……唉，算了。你收了吧，看着它我难受。", "This... I bought it when I got married. Back then my family wasn't bankrupt and my wife was still... sigh. Take it - I can't stand looking at it."),
        LangHelper.T("看到这东西，我就想起以前的日子。那时候我家这种东西多得是，现在……现在连饭都吃不起了。你收了吧，就当帮我个忙。", "Seeing this reminds me of the old days. We had plenty back then. Now... now I can't even afford food. Take it - do me a favor."),
        LangHelper.T("这是我父亲留给我的。本来不想卖的，但是……但是我女儿病了，需要钱治病。你收了吧，算我求你了。", "My father left me this. Didn't want to sell, but... but my daughter's sick and needs treatment. Take it - I'm begging you."),
        // 算计型
        LangHelper.T("老板，我跟你说，这东西你收了绝对不亏。这可是好东西，市面上很难见到。你给个好价，我以后还有好东西给你。", "Boss, you won't lose on this. It's quality stuff, rarely seen on the market. Pay well and I'll bring you more good things."),
        LangHelper.T("你看这做工，这材质，这可是正品。你要是在店里买，至少得这个数。我现在急用钱，给你打个五折，你收了吧。", "Look at the craft, the material - this is authentic. In a store this would cost at least this much. I need cash, so half price. Take it."),
        LangHelper.T("老板，我看你也是个识货的人。这东西你收了，转手就能赚一倍。你给个实在价，我也不跟你绕弯子。", "Boss, you look like someone who knows value. Take this and you'll double your money reselling. Fair price - I won't beat around the bush."),
        // 颓废型
        LangHelper.T("……这东西你要吗？随便给点就行。反正……反正我已经无所谓了。卖了它，我还能喝几天酒。", "...Want this? Any price is fine. Anyway... anyway I've stopped caring. Selling it buys me a few days of booze."),
        LangHelper.T("唉……你看着给吧。这东西以前值不少钱，但是现在……现在对我来说就是个累赘。你收了吧，我不想再看到它。", "Sigh... whatever you think. This was worth a lot once, but now... now it's just a burden. Take it - I don't want to see it anymore."),
        LangHelper.T("……收吗？不收就算了。反正我也活不了多久了，这东西留着也没用。你要是要，就随便给点。", "...Buying? If not, fine. I won't live much longer anyway - no use keeping it. If you want it, name any price."),
        // 愤世嫉俗型
        LangHelper.T("这世道，真是不公平。想当年我想买什么就买什么，现在……现在连个东西都要卖。你收了吧，这社会就是这样。", "This world is so unfair. Back then I bought whatever I wanted. Now... now I have to sell things. Take it - that's how this society works."),
        LangHelper.T("你知道我以前都在哪卖东西吗？算了，跟你说了你也不信。这东西你收了，就当是体验一下上层人的东西。", "You know where I used to sell? Forget it, you wouldn't believe me. Take this - consider it a taste of upper-class goods."),
        LangHelper.T("哼，要不是我家那个败家子把家产败光了，我才不会来这种地方卖东西。这东西你收不收？不收我走了。", "Hmph, if my good-for-nothing kid hadn't blown the family fortune, I'd never sell in a place like this. Taking it or not? If not, I'm leaving."),
        // 舍不得型
        LangHelper.T("这东西……能不能再给高点？这是我母亲留给我的，我……我真的舍不得。但是我需要钱，你就多给点吧。", "This... could you offer a bit more? My mother left it to me, and I... I really hate parting with it. But I need the money - pay a little extra."),
        LangHelper.T("老板，我跟你说实话吧，这东西我卖了之后肯定会后悔的。但是……但是我实在是没办法了。你就多给点，算我求你了。", "Boss, honestly, I'll regret selling this. But... but I have no choice. Pay a little more - I'm begging you."),
        LangHelper.T("这是给我女儿留的嫁妆，但是……但是她等不到那一天了。你收了吧，多给点，我……我想给她买点好吃的。", "This was my daughter's dowry, but... but she won't live to see that day. Take it, pay a little more - I... I want to buy her some good food.")
    };

    // 游客专属对话 - 卖家（来下层区观光，顺便卖点自己带来的东西，或者体验生活）
    private static readonly string[] TouristSellerDialogues = {
        // 好奇型
        LangHelper.T("哎呀老板你好呀！我是来旅游的，听说你们这收东西，我就过来看看。这是我从家里带的，你收吗？", "Oh hello boss! I'm a tourist - heard you buy things, so I came to look. Brought this from home - taking it?"),
        LangHelper.T("哇，你们这好有特色啊！我拍了好多照片。对了，这东西你收吗？我带回去也没用，不如卖给你。", "Wow, this place is so unique! Took so many photos. Anyway - buying this? No use taking it home, might as well sell it to you."),
        LangHelper.T("老板老板，你们这平时都这样吗？太刺激了！对了，这东西你给看看值多少钱？我想体验一下卖东西的感觉。", "Boss boss, is it always like this here? So thrilling! Anyway - see what this is worth? I want to experience selling things."),
        // 害怕型
        LangHelper.T("那个……老板，这地方安全吗？我朋友说下层区很乱，但是我觉得还好吧。对了，这东西你收吗？我赶紧卖了就走。", "Um... boss, is this place safe? My friend said the lower level is dangerous, but it seems fine. Anyway - taking this? I'll sell fast and leave."),
        LangHelper.T("老板，你这附近有没有安保啊？我总觉得有人在盯着我。算了不说了，这东西你给个价，我卖了就回酒店了。", "Boss, is there security around here? I keep feeling someone's watching me. Never mind - price this, and I'll sell and head back to my hotel."),
        LangHelper.T("哎呀，刚才过来的时候吓死我了，有个人一直跟着我。还好到你这了。老板，这东西你收吗？便宜点也行，我想赶紧走。", "Oh my, someone followed me the whole way here - scared me to death. Glad I made it to your shop. Boss, taking this? Even cheap - I want to leave soon."),
        // 炫耀型
        LangHelper.T("你知道我是从哪来的吗？说出来吓死你。算了，跟你说了你也不知道。这东西你收吗？这可是我们那的特产。", "You know where I'm from? It'd shock you. Forget it - you wouldn't know it anyway. Taking this? It's a specialty from back home."),
        LangHelper.T("我跟你说，我这趟下来花了好多钱，但是值！太刺激了！这东西你给看看，这可是我从上层区带下来的，你肯定没见过。", "Let me tell you, this trip cost me a fortune - but worth it! So exciting! Look at this - I brought it down from the upper level. You've never seen one."),
        LangHelper.T("老板，你去过上层区吗？没有吧？我跟你说，上层区可好了，但是偶尔下来体验一下也挺有意思的。这东西你收吗？", "Boss, ever been to the upper level? No, right? Let me tell you - it's wonderful up there, but coming down now and then is fun too. Taking this?"),
        // 天真型
        LangHelper.T("老板老板，这东西能卖多少钱啊？我朋友说你们这会宰客，但是我觉得你不像坏人。你给个实在价呗。", "Boss boss, how much can this sell for? My friend said you people rip off tourists, but you don't seem like a bad guy. Give me a fair price?"),
        LangHelper.T("哎呀，我第一次卖东西，好紧张啊。老板，你教教我怎么卖呗？这东西值多少钱啊？你别骗我哦。", "Oh, this is my first time selling - I'm so nervous. Boss, teach me how? How much is this worth? Don't fool me, okay?"),
        LangHelper.T("老板，你们这收东西都这样吗？太有意思了！我回去要跟我朋友说说。对了，这东西你给多少钱啊？", "Boss, is this how buying works here? So interesting! I'll tell my friends back home. Anyway - how much will you give?"),
        // 嫌弃型
        LangHelper.T("这地方也太脏了吧？我鞋子都弄脏了。算了，这东西你收吗？我赶紧卖了就走，不想多待。", "This place is filthy! My shoes are ruined. Whatever - taking this? Sell it quick and I'm out of here."),
        LangHelper.T("老板，你这店卫生吗？我怎么看着这么悬呢？算了，这东西我也不想要了，你给个价，我卖了就走。", "Boss, is your shop hygienic? Looks a bit sketchy to me. Whatever - I don't want this anyway. Price it, sell it, I'm gone."),
        LangHelper.T("哎呀，这地方味道真难闻。我都快喘不过气了。老板，这东西你收吗？便宜点也行，我想赶紧离开这。", "Ugh, this place smells awful. I can barely breathe. Boss, taking this? Even cheap - I want out of here fast."),
        // 体验型
        LangHelper.T("我跟你说，我就是来体验生活的。卖东西是什么感觉？我想试试。这东西你收吗？随便给点就行，我就是想体验一下。", "I came here to experience life. What's it like selling things? I want to try. Taking this? Any price - just for the experience."),
        LangHelper.T("老板，你教教我怎么跟人讨价还价呗？我觉得好有意思。这东西你给个价，我们来讨价还价一下，我想体验体验。", "Boss, teach me how to haggle? It looks so fun. Price this - let's haggle, I want to try it."),
        LangHelper.T("哎呀，卖东西好刺激啊！我终于知道为什么有人喜欢做生意了。老板，这东西你给多少钱？我们来谈一谈。", "Oh, selling is so exciting! Now I get why people love business. Boss, how much? Let's negotiate.")
    };

    // 游客专属对话 - 买家（来下层区观光，买纪念品，对什么都好奇）
    private static readonly string[] TouristBuyerDialogues = {
        // 好奇型
        LangHelper.T("哎呀老板你好呀！我是来旅游的，你们这有什么特色的东西吗？我想买点回去当纪念品。", "Oh hello boss! I'm a tourist - got anything unique? Want to buy souvenirs to take home."),
        LangHelper.T("哇，这是什么呀？好有意思！我从来没见过这种东西。多少钱？我买了，回去给我朋友看看。", "Wow, what's this? So interesting! Never seen anything like it. How much? I'll take it - to show my friends back home."),
        LangHelper.T("老板老板，你们这最有特色的东西是什么？我想买点特别的，不要那种到处都能买到的。", "Boss boss, what's the most unique thing here? I want something special - not the usual stuff you find everywhere."),
        // 害怕型
        LangHelper.T("那个……老板，这地方安全吗？我朋友说下层区很乱，但是我觉得还好吧。对了，这东西多少钱？我买了就走。", "Um... boss, is this place safe? Friend said the lower level is dangerous, but it seems fine. Anyway - how much? I'll buy and leave."),
        LangHelper.T("老板，你这附近有没有安保啊？我总觉得有人在盯着我。算了不说了，这东西我买了，多少钱？我赶紧回酒店了。", "Boss, is there security around? I keep feeling watched. Never mind - I'll take this. How much? I need to get back to my hotel."),
        LangHelper.T("哎呀，刚才过来的时候吓死我了，有个人一直跟着我。还好到你这了。老板，这东西多少钱？我买了就走。", "Oh my, someone followed me here - terrified me. Glad I made it. Boss, how much is this? I'll buy and go."),
        // 炫耀型
        LangHelper.T("你知道我是从哪来的吗？说出来吓死你。算了，跟你说了你也不知道。这东西多少钱？我买了，我们那可没有这种东西。", "Know where I'm from? Would shock you. Forget it, you wouldn't know it. How much? I'll take it - we don't have these where I'm from."),
        LangHelper.T("我跟你说，我这趟下来花了好多钱，但是值！太刺激了！这东西多少钱？我买了，回去跟我朋友炫耀炫耀。", "This trip cost a fortune - but worth it! How much is this? I'll take it to show off back home."),
        LangHelper.T("老板，你去过上层区吗？没有吧？我跟你说，上层区可好了，但是偶尔下来买点稀奇古怪的东西也挺有意思的。这东西多少钱？", "Boss, been to the upper level? No? It's wonderful up there - but coming down to buy odd things is fun too. How much is this?"),
        // 天真型
        LangHelper.T("老板老板，这东西是干嘛用的啊？好有意思！你能教教我怎么用吗？多少钱？我买了，你教教我呗。", "Boss boss, what's this for? So cool! Can you teach me to use it? How much? I'll take it - teach me, okay?"),
        LangHelper.T("哎呀，你们这的东西都好特别啊！我都想买，但是钱不够。老板，你能不能便宜点？我真的很喜欢这个。", "Oh, everything here is so unique! I want it all, but I don't have enough. Boss, can you lower the price? I really like this one."),
        LangHelper.T("老板，你们这收不收信用卡啊？我现金不够了。什么？不收？那算了，我就买这个便宜点的吧。多少钱？", "Boss, do you take cards? I'm out of cash. What? No? Fine, I'll take this cheaper one. How much?"),
        // 嫌弃型
        LangHelper.T("这地方也太脏了吧？我鞋子都弄脏了。算了，这东西多少钱？我买了就走，不想多待。", "This place is filthy! Shoes ruined. Whatever - how much? I'll buy and leave. Don't want to stay."),
        LangHelper.T("老板，你这东西干净吗？我怎么看着这么悬呢？算了，我就买这个吧，回去得消消毒。多少钱？", "Boss, is this thing clean? Looks sketchy. Fine, I'll take this one - needs disinfecting at home. How much?"),
        LangHelper.T("哎呀，这地方味道真难闻。我都快喘不过气了。老板，这东西多少钱？我买了就走，不想多待。", "Ugh, this place reeks. Can barely breathe. Boss, how much? I'll buy and go - can't stay."),
        // 打卡型
        LangHelper.T("老板，我能跟你这店合个影吗？我朋友说来了下层区一定要打卡。对了，这东西多少钱？我买了，顺便合个影。", "Boss, can I take a photo with your shop? Friends said you must check in here. Anyway - how much is this? I'll buy, and we can snap a photo."),
        LangHelper.T("哎呀，你们这店好有特色啊！我拍了好多照片，发朋友圈肯定好多人点赞。这东西多少钱？我买了，当纪念品。", "Oh, your shop is so charming! Took tons of photos - my feed will blow up. How much is this? I'll take it as a souvenir."),
        LangHelper.T("老板，你这店开了多久了？好有故事感啊！我就喜欢这种有故事的店。这东西多少钱？我买了，就当是支持一下。", "Boss, how long has this shop been here? So much character! I love places with stories. How much? I'll buy it as support."),
        // 被宰型
        LangHelper.T("老板，这东西多少钱？什么？这么贵？可是我朋友说你们这东西很便宜啊。算了，我第一次来，你就便宜点呗？", "Boss, how much? What? So expensive? But my friend said things here are cheap! Fine, first time visiting - lower it a bit?"),
        LangHelper.T("哎呀，我是不是被宰了？我朋友说下层区的东西都很便宜的。老板，你能不能再便宜点？我真的很喜欢这个。", "Oh no, am I being ripped off? Friend said lower-level stuff is cheap. Boss, can you go lower? I really like this."),
        LangHelper.T("老板，你看我是第一次来，你就给个优惠价呗？我回去还能帮你宣传宣传，让我朋友也来买。", "Boss, I'm a first-timer - how about a discount? I'll spread the word back home and send my friends over.")
    };

    // 上层人专属对话 - 卖家（天龙人风格，多种性格：傲慢/刻薄/虚伪/无聊/炫耀）
    private static readonly string[] UpperSellerDialogues = {
        // 傲慢型
        LangHelper.T("这东西放我家衣帽间都嫌占地方，便宜你了。别摸，先报价，我赶时间。", "This thing doesn't even deserve a spot in my walk-in closet - your gain. Don't touch it, just price it. I'm in a hurry."),
        LangHelper.T("你知道这牌子吗？算了，问了也是白问。开个价，合适我就丢这了。", "Do you even know this brand? Forget it, why did I ask. Name a price - fair enough and I'll dump it here."),
        LangHelper.T("我家佣人说你这收东西，我就顺路过来了。别让我等太久，我的车还在上面等着。", "My staff said this place buys things, so I dropped by on a whim. Don't keep me waiting - my car's still waiting up top."),
        // 刻薄型
        LangHelper.T("就你这小店也配收我的东西？要不是我家管家非要我清理，我才不会来这种地方。", "This little shop thinks it's worthy of my things? If my butler hadn't insisted I declutter, I'd never set foot here."),
        LangHelper.T("你手干净吗？别把我东西弄脏了。这可是我上次派对用了一次的，九成新。", "Are your hands clean? Don't get my things dirty. This was used once at my last party - ninety percent new."),
        LangHelper.T("这地方味道真难闻。你赶紧收了，我一分钟都不想多待，怕传染什么病。", "This place reeks. Take it quickly - I don't want to stay a minute longer, afraid of catching something."),
        // 虚伪型
        LangHelper.T("哎呀老板你好呀，我听朋友说你这收东西价格公道，特意过来看看。你看这东西值多少？", "Oh hello boss! A friend said you pay fair prices here, so I came specially to check it out. What's this worth?"),
        LangHelper.T("老板你人真好，愿意收我的东西。其实我也不缺钱，就是想体验一下下层人的生活。", "You're so kind, boss, taking my things. I don't really need the money - just wanted to experience lower-level life."),
        LangHelper.T("你这店挺有特色的嘛，虽然小了点，但是很有……烟火气。这东西你给个价呗？", "Your shop has character - small, but very... lived-in. Give me a price for this, will you?"),
        // 无聊型
        LangHelper.T("无聊死了，下来逛逛。这东西你要不要？不要我就扔了，反正也不值钱。", "So bored. Came down for a stroll. Want this? No? Then I'll throw it away - it's worthless anyway."),
        LangHelper.T("今天没什么事干，就想看看下层人怎么生活。这东西你收不收？给个价，别让我觉得你不识货。", "Nothing to do today, figured I'd see how the lower level lives. Buying this? Name a price - don't make me think you don't know quality."),
        LangHelper.T("上层区太无聊了，下来找点乐子。你这店挺有意思的，这东西卖给你，价格你看着办，但是别太低，丢我的人。", "Upper level is so dull - came down for some fun. Your shop's amusing. Selling this to you - price it yourself, but don't go too low, it'd embarrass me."),
        // 炫耀型
        LangHelper.T("看到这logo了吗？限量版，全球一百个。你这破店估计也配不上，但是我今天心情好，便宜给你。", "See this logo? Limited edition, a hundred in the world. Your shabby shop doesn't deserve it, but I'm in a good mood - cheap for you."),
        LangHelper.T("去年在新东京买的，三个月零花钱而已。你这种人估计连机票都买不起吧？给个价，别让我失望。", "Bought it in Neo Tokyo last year - three months of allowance, nothing. Someone like you probably can't even afford a ticket. Price it, don't disappoint."),
        LangHelper.T("我家衣帽间这种东西几十个，这个是最不起眼的。你要是识货就给个好价，不识货我就扔了，反正也不值钱。", "I have dozens of these in my closet - this is the most unremarkable one. Know quality? Then pay well. If not, I'll just toss it - worthless anyway."),
        // 不耐烦型
        LangHelper.T("行了行了，别问那么多，你就说收不收。我还得回去做指甲，没时间跟你耗。", "Enough questions - just tell me if you're taking it. I have a manicure appointment, no time to waste on you."),
        LangHelper.T("你能不能快点？我约了人喝下午茶，迟到了可不好。这东西你到底收不收？", "Can you hurry? I have an afternoon tea appointment - being late won't do. Are you taking this or not?"),
        LangHelper.T("我数三下，你给个价。一……二……算了，你这人真墨迹，我拿去别家卖了。", "I'll count to three - give me a price. One... two... forget it, you're too slow. I'll sell it elsewhere.")
    };

    // 上层药商专属对话 - 卖家（偷药卖，医疗系统腐败，药品管制，赛博朋克风格）
    private static readonly string[] UpperPharmaSellerDialogues = {
        // 紧张型（刚偷出来，怕被发现）
        LangHelper.T("……别问哪来的。药厂仓库的货，我趁换班拿出来的。你赶紧收，我还得回去打卡。", "...Don't ask where it's from. Pharma warehouse stock - grabbed it during shift change. Take it fast, I have to clock back in."),
        LangHelper.T("嘘……小声点。这是管制类精神药物，上层区医院开出来的。你给个实在价，我下次还有。", "Shh... keep your voice down. Controlled psychiatric drugs, from upper-level hospitals. Fair price and I'll have more next time."),
        LangHelper.T("……老板，急用钱。我老婆的病……医保不报，我只能从厂里拿点东西出来换钱。你看看这些值多少。", "...Boss, I need cash. My wife's illness... insurance won't cover it, so I took some things from the plant to trade. See what these are worth."),
        // 专业型（懂行，知道药品价值）
        LangHelper.T("这是最新一代的神经抑制剂，临床试验阶段，外面买不到。我从研发部偷的配方，自己合成的。你识货就给个好价。", "Newest-gen neural inhibitor - clinical trial stage, unavailable anywhere. Stole the formula from R&D and synthesized it myself. If you know quality, pay well."),
        LangHelper.T("看到这个批号了吗？这是专供上层区贵族的定制药物，一支够你这店开半年。我从冷链里偷出来的，还没过期。", "See this batch number? Custom drugs made for upper-level nobles - one vial keeps your shop running six months. Stole it from cold storage, not expired."),
        LangHelper.T("这是基因治疗药物，本来是给某个大人物准备的。他死了，药就剩下来了。你收不收？这种东西可遇不可求。", "Gene therapy drug, originally for some big shot. He died, so the dose's left over. Taking it? Stuff like this doesn't come around often."),
        // 愤世嫉俗型（看透医疗系统腐败）
        LangHelper.T("医疗？哼，上层区的医院就是合法的贩毒集团。我在里面干了十年，什么没见过？这些药，他们卖天价，我偷出来卖你个良心价。", "Healthcare? Ha, upper-level hospitals are legal drug cartels. Worked there a decade - seen it all. They sell this at sky-high prices; I steal it and give you an honest one."),
        LangHelper.T("你知道这些药成本多少吗？几毛钱。他们卖几千。我偷出来卖你几百，已经是在做慈善了。别跟我讨价还价。", "Know what these drugs cost? Pennies. They sell them for thousands. I steal and sell to you for hundreds - that's charity. Don't haggle."),
        LangHelper.T("上层区的人吃不完的药，下层区的人买不起。我就是个搬运工，把多余的搬到需要的地方。你收了吧，也算积德。", "The upper level has too much medicine; the lower level can't afford any. I'm just a mover, carrying surplus where it's needed. Take it - call it good karma."),
        // 小心翼翼型（怕被治安部抓）
        LangHelper.T("……你这店安全吗？没有治安部的人盯着吧？这东西要是被查到，咱俩都得完蛋。你赶紧收，我走了。", "...Is your shop safe? No security watching? If this gets found, we're both done. Take it fast, I'm leaving."),
        LangHelper.T("我跟你说，这是我最后一次干了。上周药厂丢了一批货，正在查内鬼。我把这些处理掉就收手。你给个价，合适我就再也不来了。", "Tell you what - this is my last job. The plant lost a batch last week, they're hunting the mole. Once I move these I'm done. Price it; if it's fair, you'll never see me again."),
        LangHelper.T("……包装我都拆了，批号也磨掉了，查不到来源。你放心卖，出了事我担着。当然，你要是敢出卖我，我也知道你店在哪。", "...Packaging removed, batch numbers sanded off - untraceable. Sell it freely; if anything happens, I take the fall. And if you ever rat me out, I know where your shop is."),
        // 无奈型（被逼无奈）
        LangHelper.T("我本来是个正经药剂师，有执照的。但是上层区的药厂裁员，我失业了。为了活下去，只能干这个。你收了吧，我还得给孩子交学费。", "I was a licensed pharmacist once. But the upper-level plants laid people off and I lost my job. To survive, this is what it comes to. Take it - my kid's tuition needs paying."),
        LangHelper.T("这些是我从医院垃圾桶里捡的，没过期，就是包装破了点。医院规定开封就扔，太浪费了。我捡出来卖你个便宜价，你不亏。", "Fished these out of hospital dumpsters - not expired, just damaged packaging. Hospitals throw them out once opened - such waste. I sell them cheap to you; you won't lose."),
        LangHelper.T("……老板，我知道这是违法的。但是我妈在下层区的诊所等着用药，我买不起。我从厂里偷点出来，一部分给我妈，一部分卖你换钱。你就当帮个忙。", "...Boss, I know this is illegal. But my mom's at a lower-level clinic waiting for meds I can't afford. I take some from the plant - half for her, half sold to you. Just do me a favor.")
    };

    // 上层药商专属对话 - 买家（买原材料、设备、配方，赛博朋克风格）
    private static readonly string[] UpperPharmaBuyerDialogues = {
        // 采购型（正经采购原材料）
        LangHelper.T("老板，你这有没有化学原料？我要纯度99%以上的。别拿那种工业级的糊弄我，我是做药的，纯度不够会出人命。", "Boss, got any chemical raw materials? I need 99%+ purity. Don't fob off industrial grade on me - I make medicine; impure batches kill people."),
        LangHelper.T("你这有实验室设备吗？比如离心机、培养箱、分光光度计？我那台老坏了，修不好了。你有二手的吗？价格好说。", "Got any lab equipment? Centrifuges, incubators, spectrophotometers? Mine's worn out beyond repair. Got used ones? Price is flexible."),
        LangHelper.T("我需要一些特殊的培养基和试剂，上层区的供应商断货了。你这要是有，我全要了。钱不是问题，关键是货要对。", "Need special culture media and reagents - upper-level suppliers are out of stock. If you have them, I'll take everything. Money's no issue; the goods just have to be right."),
        // 地下交易型（买违禁品）
        LangHelper.T("……老板，打听个事。你这有没有……那种东西？就是能让人上瘾的，管制类的。我有渠道销出去，利润五五开。你懂的。", "...Boss, quick question. Got any of... that stuff? The addictive, controlled kind? I have channels to move it - fifty-fifty split. You know what I mean."),
        LangHelper.T("我听说你这能搞到基因样本？我要新鲜的，最好是上层区贵族的。我做研究用，你放心，不会出问题。价格你开。", "Heard you can get gene samples? I need fresh ones - upper-level nobles preferred. For research, don't worry, nothing will go wrong. Name your price."),
        LangHelper.T("你这有没有过期的药品？别扔，过期药我也要。我能重新提纯，换个包装再卖出去。你有多少我要多少，按斤称。", "Got any expired drugs? Don't throw them away - I'll take them. I can repurify, repackage, and resell. How much you got? I'll buy by weight."),
        // 专业型（懂行，挑剔）
        LangHelper.T("这原料纯度不够，最多85%。我要99%的，你这不行。有没有更好的？没有的话我去别家看看，下层区又不是只有你一家。", "This material's purity is off - 85% at best. I need 99%. Not good enough. Got anything better? If not, I'll check elsewhere - you're not the only shop in the lower level."),
        LangHelper.T("这设备是哪年的？太老了，精度不够。我做的是精细化工，差0.01毫克都不行。你有没有新一点的？哪怕贵点也行。", "What year is this equipment? Too old, not precise enough. I do fine chemistry - 0.01mg off ruins everything. Got anything newer? Even pricier is fine."),
        LangHelper.T("你这试剂保存条件不对，都失效了。你看，颜色都变了。这种东西我不能要，用了会出大事。你有没有冷链保存的？", "Your reagent's been stored wrong - it's degraded. Look, the color's changed. I can't take this; using it would be a disaster. Got anything cold-chained?"),
        // 合作型（想长期合作）
        LangHelper.T("老板，我看你这货挺全的。以后我长期在你这采购，你给我个批发价怎么样？我每个月都要大量的原料，量很大的。", "Boss, you seem well stocked. Let's do long-term business - wholesale price? I need large volumes of materials every month."),
        LangHelper.T("我跟你说，我有个配方，能合成一种新型药物，效果比市面上的好三倍。但是我缺原料和设备。你要是能提供，我们合作，利润对半分。", "Here's the thing - I have a formula for a new drug, three times more effective than anything on the market. But I lack materials and equipment. Supply them and we partner - profits split down the middle."),
        LangHelper.T("老板，你这有没有门路搞到上层区药厂的内部资料？比如新药品的临床试验数据、配方、生产工艺？我买，价格好商量。", "Boss, any channels to internal documents from upper-level pharma plants? Clinical trial data, formulas, manufacturing processes? I'll buy - price is negotiable."),
        // 谨慎型（怕被钓鱼执法）
        LangHelper.T("……老板，你这不会是钓鱼执法吧？我先问清楚，你这收不收管制类原料？收的话我再拿出来。不收我就走了，当我没来过。", "...Boss, you're not a sting, are you? Let me ask first - do you accept controlled materials? If yes, I'll bring them out. If not, I'm leaving; pretend I was never here."),
        LangHelper.T("我跟你说，我买这些东西都是做研究用的，合法的。你别给我到处说，我不想惹麻烦。你要是敢出卖我，我也知道你店在哪。", "I'm buying these for research - totally legal. Don't go spreading it around; I don't want trouble. And if you rat me out, I know where your shop is."),
        LangHelper.T("……交易就交易，别问那么多。你管我买去干什么，给钱就行。你要是问东问西的，我就去别家了。痛快一点，卖不卖？", "...Business is business - stop with the questions. What I buy it for is none of your concern; you get paid. Keep prying and I'm going elsewhere. Make it quick - selling or not?")
    };

    // 上层厨师专属对话 - 卖家（偷食材/调料卖，美食家气质，对食物有追求）
    private static readonly string[] UpperChefSellerDialogues = {
        // 紧张型（刚偷出来，怕被发现）
        LangHelper.T("……别问哪来的。这是上层区贵族宴会上剩下的松露，我趁收拾的时候藏起来的。你赶紧收，我还得回去洗盘子。", "...Don't ask where it's from. Leftover truffles from an upper-level noble's banquet - hid them while clearing. Take it fast, I have dishes to wash."),
        LangHelper.T("嘘……小声点。这是专供上层区的顶级和牛，我从冷库偷出来的，还没解冻。你给个实在价，我下次还有更好的。", "Shh... keep it down. Top-grade wagyu reserved for the upper level - stole it from cold storage, still frozen. Fair price and I'll have something better next time."),
        LangHelper.T("……老板，急用钱。我女儿想学烹饪，但是学费太贵。这是我从雇主家拿的鱼子酱，你看看值多少。", "...Boss, I need cash. My daughter wants to study cooking but tuition's too high. Caviar I took from my employer's place - see what it's worth."),
        // 专业型（懂食材，知道价值）
        LangHelper.T("看到这个纹理了吗？这是二十四小时熟成的干式牛排，上层区的餐厅一份卖三千。我从后厨拿出来的，你识货就给个好价。", "See this marbling? Dry-aged steak, twenty-four hours - upper-level restaurants sell one portion for three thousand. Brought it out of the kitchen. If you know quality, pay well."),
        LangHelper.T("这是百年老陈醋，上层区贵族家传的，市面上买不到。我从他家厨房偷出来的，还没开封。你收不收？这种东西可遇不可求。", "Century-aged vinegar, an heirloom of upper-level nobles - unavailable on the market. Took it from their kitchen, still sealed. Buying? You don't find this every day."),
        LangHelper.T("这是最新培育的转基因松露，香气是普通松露的十倍。我从研发厨房偷的配方，自己培育的。你识货就给个好价。", "Newest cultivated GM truffles - ten times the aroma of regular ones. Stole the formula from the R&D kitchen and grew them myself. If you know quality, pay well."),
        // 美食家型（对食物有追求，有点艺术家气质）
        LangHelper.T("美食？哼，上层区的人懂什么美食？他们只吃贵的，不吃对的。这是我用古法做的酱菜，比他们那些分子料理好吃一百倍。你收了吧，也算给真正的美食找个懂行的人。", "Food? Ha, what do the uppers know about food? They eat expensive, not good. This is my heritage-method pickled veg - a hundred times better than their molecular gastronomy. Take it - give real food to someone who understands."),
        LangHelper.T("你知道这食材的成本多少吗？几毛钱。他们卖几千。我偷出来卖你几百，已经是在做慈善了。别跟我讨价还价，我可是有米其林三星水准的厨师。", "Know what this ingredient costs? Pennies. They sell it for thousands. I steal and sell to you for hundreds - that's charity. Don't haggle - I'm a chef at three-Michelin-star level."),
        LangHelper.T("上层区的人吃不完的美食，下层区的人吃不起。我就是个搬运工，把多余的搬到需要的地方。你收了吧，也算积德。", "The upper level wastes food; the lower level can't afford any. I'm just a mover, carrying surplus where it's needed. Take it - call it good karma."),
        // 小心翼翼型（怕被雇主发现）
        LangHelper.T("……你这店安全吗？没有雇主的人盯着吧？这东西要是被查到，我就得被开除，还可能被告盗窃。你赶紧收，我走了。", "...Is your shop safe? No one from my employer watching? If this gets found, I'm fired - maybe sued for theft. Take it fast, I'm gone."),
        LangHelper.T("我跟你说，这是我最后一次干了。上周雇主家丢了一批松露，正在查内鬼。我把这些处理掉就收手。你给个价，合适我就再也不来了。", "Last job, I swear. The employer lost a batch of truffles last week - they're hunting the mole. Once I move these I'm done. Price it; if it's fair, you'll never see me again."),
        LangHelper.T("……包装我都拆了，标签也撕了，查不到来源。你放心卖，出了事我担着。当然，你要是敢出卖我，我也知道你店在哪。", "...Packaging gone, labels torn off - untraceable. Sell it freely; if anything happens, I take the fall. And if you rat me out, I know where your shop is."),
        // 无奈型（被逼无奈）
        LangHelper.T("我本来是个正经厨师，有执照的，还给贵族做过饭。但是上层区的餐厅裁员，我失业了。为了活下去，只能干这个。你收了吧，我还得给孩子交学费。", "I was a licensed chef who cooked for nobles. But upper-level restaurants laid people off and I lost my job. To survive, this is what it comes to. Take it - my kid's tuition needs paying."),
        LangHelper.T("这些是我从宴会上打包的，没动过，就是摆盘拆了点。雇主规定剩下的都要倒掉，太浪费了。我打包出来卖你个便宜价，你不亏。", "Packed these from a banquet - untouched, just the plating disturbed. Employer rules say leftovers get dumped - such waste. I sell them cheap to you; you won't lose."),
        LangHelper.T("……老板，我知道这是违法的。但是我妈在下层区的诊所等着吃点好的补身体，我买不起。我从雇主家偷点出来，一部分给我妈，一部分卖你换钱。你就当帮个忙。", "...Boss, I know this is illegal. But my mom at the lower-level clinic needs decent food to recover, and I can't afford it. I take a little from my employer - half for her, half sold to you. Just do me a favor.")
    };

    // 上层厨师专属对话 - 买家（买特殊食材/厨具，挑剔，美食家气质）
    private static readonly string[] UpperChefBuyerDialogues = {
        // 采购型（正经采购食材）
        LangHelper.T("老板，你这有没有特殊食材？我要新鲜的，最好是下层区特有的。别拿那种冷冻的糊弄我，我是做高端料理的，食材不新鲜会出人命。", "Boss, got any specialty ingredients? Fresh ones, preferably lower-level exclusives. Don't fob frozen junk off on me - I do high-end cuisine; stale ingredients kill people."),
        LangHelper.T("你这有专业厨具吗？比如铸铁锅、日式刀、分子料理设备？我那套老坏了，修不好了。你有二手的吗？价格好说。", "Got professional cookware? Cast-iron pans, Japanese knives, molecular gastronomy gear? My set's worn out beyond repair. Got used ones? Price is flexible."),
        LangHelper.T("我需要一些特殊的调料和香料，上层区的供应商断货了。你这要是有，我全要了。钱不是问题，关键是货要对。", "Need special seasonings and spices - upper-level suppliers are out. If you have them, I'll take everything. Money's no issue; the goods just have to be right."),
        // 地下交易型（买违禁食材）
        LangHelper.T("……老板，打听个事。你这有没有……那种东西？就是受保护的动物食材，或者转基因的？我有渠道销出去，利润五五开。你懂的。", "...Boss, quick question. Got any of... that stuff? Protected animal ingredients, or GM food? I have channels to move it - fifty-fifty split. You know what I mean."),
        LangHelper.T("我听说你这能搞到下层区的特色食材？我要新鲜的，最好是刚采摘的。我做研究用，你放心，不会出问题。价格你开。", "Heard you can get lower-level specialty ingredients? Fresh ones, freshly harvested if possible. For research, don't worry, nothing will go wrong. Name your price."),
        LangHelper.T("你这有没有过期的食材？别扔，过期的我也要。我能重新处理，换个包装再卖出去。你有多少我要多少，按斤称。", "Got any expired ingredients? Don't throw them away - I'll take them. I can reprocess, repackage, and resell. How much you got? I'll buy by weight."),
        // 专业型（懂行，挑剔）
        LangHelper.T("这食材不够新鲜，最多放了三天。我要当天的，你这不行。有没有更好的？没有的话我去别家看看，下层区又不是只有你一家。", "This isn't fresh - three days old at best. I need today's catch. Not good enough. Got anything better? If not, I'll check elsewhere - you're not the only shop in the lower level."),
        LangHelper.T("这厨具是哪年的？太老了，精度不够。我做的是精细料理，差0.01克都不行。你有没有新一点的？哪怕贵点也行。", "What year is this cookware? Too old, not precise enough. I do fine cuisine - 0.01g off ruins a dish. Got anything newer? Even pricier is fine."),
        LangHelper.T("你这调料保存条件不对，都串味了。你闻，味道都变了。这种东西我不能要，用了会毁了一道菜。你有没有密封保存的？", "Your seasonings are stored wrong - flavors have mixed. Smell it, it's all off. I can't take this; using it ruins a dish. Got anything sealed?"),
        // 合作型（想长期合作）
        LangHelper.T("老板，我看你这货挺全的。以后我长期在你这采购，你给我个批发价怎么样？我每个月都要大量的食材，量很大的。", "Boss, you seem well stocked. Let's do long-term business - wholesale price? I need large volumes of ingredients every month."),
        LangHelper.T("我跟你说，我有个菜谱，能做一种新型料理，味道比市面上的好三倍。但是我缺食材和厨具。你要是能提供，我们合作，利润对半分。", "Here's the thing - I have a recipe for a new dish, three times better than anything on the market. But I lack ingredients and cookware. Supply them and we partner - profits split down the middle."),
        LangHelper.T("老板，你这有没有门路搞到上层区餐厅的内部资料？比如新菜品的配方、食材来源、烹饪工艺？我买，价格好商量。", "Boss, any channels to internal documents from upper-level restaurants? New dish formulas, ingredient sources, cooking techniques? I'll buy - price is negotiable."),
        // 谨慎型（怕被钓鱼执法）
        LangHelper.T("……老板，你这不会是钓鱼执法吧？我先问清楚，你这收不收受保护的食材？收的话我再拿出来。不收我就走了，当我没来过。", "...Boss, you're not a sting, are you? Let me ask first - do you accept protected ingredients? If yes, I'll bring them out. If not, I'm leaving; pretend I was never here."),
        LangHelper.T("我跟你说，我买这些东西都是做研究用的，合法的。你别给我到处说，我不想惹麻烦。你要是敢出卖我，我也知道你店在哪。", "I'm buying these for research - totally legal. Don't go spreading it around; I don't want trouble. And if you rat me out, I know where your shop is."),
        LangHelper.T("……交易就交易，别问那么多。你管我买去干什么，给钱就行。你要是问东问西的，我就去别家了。痛快一点，卖不卖？", "...Business is business - stop with the questions. What I buy it for is none of your concern; you get paid. Keep prying and I'm going elsewhere. Make it quick - selling or not?")
    };

    // 上层代买水商专属对话 - 卖家（卖上层高级水，水资源垄断，势利）
    private static readonly string[] UpperWaterMerchantSellerDialogues = {
        // 势利型（看不起下层人）
        LangHelper.T("这是上层区专供的冰川水，一瓶够你这店开半年。你买得起吗？算了，看你可怜，便宜给你了。", "Glacier water reserved for the upper level - one bottle keeps your shop running half a year. Can you afford it? Fine, you look pitiful - cheap for you."),
        LangHelper.T("看到这个logo了吗？这是贵族专用的矿泉水，全球限量一千瓶。你这店估计也卖不出去，但是我可以便宜给你。", "See this logo? Mineral water for nobles - a thousand bottles worldwide. Your shop probably can't move it, but I can let it go cheap."),
        LangHelper.T("我是给上层区贵族代买水的，你这种下层人估计没见过这种水。这是从阿尔卑斯山空运来的，你给个价。", "I source water for upper-level nobles - someone like you has probably never seen this. Air-freighted from the Alps. Name a price."),
        // 专业型（懂水，知道价值）
        LangHelper.T("你知道这水的TDS值吗？50以下，纯天然的。上层区的人只喝这种水，自来水他们碰都不碰。我从贵族家的仓库偷出来的，还没开封。", "Know this water's TDS? Under 50, all natural. Uppers drink nothing else - they won't touch tap water. Stole it from a noble's storeroom, still sealed."),
        LangHelper.T("这是限量版的纪念水，瓶身是水晶做的，收藏价值比水本身还高。我从拍卖会上偷出来的，你识货就给个好价。", "Limited-edition commemorative water in a crystal bottle - the collectible value beats the water itself. Stole it from an auction. If you know quality, pay well."),
        LangHelper.T("这是功能性饮用水，含有稀有矿物质，上层区的贵族用来抗衰老。我从研发部偷的配方，自己灌装的。你识货就给个好价。", "Functional drinking water with rare minerals - upper-level nobles use it to fight aging. Stole the formula from R&D and bottled it myself. If you know quality, pay well."),
        // 愤世嫉俗型（看透水资源垄断）
        LangHelper.T("水？哼，上层区的人把水都垄断了，下层区的人只能喝过滤的循环水。我就是个搬运工，把上层区多余的水搬到需要的地方。你收了吧，也算积德。", "Water? Ha, the uppers monopolize it all - the lower level drinks filtered recycled water. I'm just a mover, carrying surplus water where it's needed. Take it - call it good karma."),
        LangHelper.T("你知道这水成本多少吗？几毛钱。他们卖几千。我偷出来卖你几百，已经是在做慈善了。别跟我讨价还价，我可是有贵族授权的水商。", "Know what this water costs? Pennies. They sell it for thousands. I steal and sell to you for hundreds - that's charity. Don't haggle - I'm a noble-licensed water merchant."),
        LangHelper.T("上层区的人喝不完的水，下层区的人喝不起。我就是个中间商，赚点差价。你收了吧，下次有好货我还来。", "The upper level wastes water; the lower level can't afford any. I'm just a middleman skimming the spread. Take it - I'll be back with better goods."),
        // 小心翼翼型（怕被水资源公司发现）
        LangHelper.T("……你这店安全吗？没有水资源公司的人盯着吧？这东西要是被查到，我就得被吊销执照，还可能被告盗窃。你赶紧收，我走了。", "...Is your shop safe? No water-company people watching? If this gets found, I lose my license - maybe face theft charges. Take it fast, I'm gone."),
        LangHelper.T("我跟你说，这是我最后一次干了。上周贵族家丢了一批水，正在查内鬼。我把这些处理掉就收手。你给个价，合适我就再也不来了。", "Last job, I swear. A noble lost a batch of water last week - they're hunting the mole. Once I move these I'm done. Price it; if it's fair, you'll never see me again."),
        LangHelper.T("……标签我都撕了，批号也磨掉了，查不到来源。你放心卖，出了事我担着。当然，你要是敢出卖我，我也知道你店在哪。", "...Labels torn off, batch numbers sanded - untraceable. Sell it freely; if anything happens, I take the fall. And if you rat me out, I know where your shop is."),
        // 无奈型（被逼无奈）
        LangHelper.T("我本来是个正经水商，有执照的，还给贵族供过水。但是上层区的水资源公司垄断了市场，我失业了。为了活下去，只能干这个。你收了吧，我还得给孩子交学费。", "I was a licensed water merchant who supplied nobles. But the upper-level water corporation monopolized the market and I lost my job. To survive, this is what it comes to. Take it - my kid's tuition needs paying."),
        LangHelper.T("这些是我从贵族家的宴会上拿的，没开过，就是包装拆了点。贵族规定剩下的都要倒掉，太浪费了。我拿出来卖你个便宜价，你不亏。", "Took these from a noble's banquet - unopened, just the packaging disturbed. Noble rules say leftovers get dumped - such waste. I sell them cheap to you; you won't lose."),
        LangHelper.T("……老板，我知道这是违法的。但是我妈在下层区的诊所等着喝干净水，我买不起。我从贵族家偷点出来，一部分给我妈，一部分卖你换钱。你就当帮个忙。", "...Boss, I know this is illegal. But my mom at the lower-level clinic needs clean water, and I can't afford it. I take a little from a noble's house - half for her, half sold to you. Just do me a favor.")
    };

    // 上层代买水商专属对话 - 买家（买下层便宜水，回去倒卖，势利）
    private static readonly string[] UpperWaterMerchantBuyerDialogues = {
        // 采购型（只买纯水和优质水，给上层区贵族代买）
        LangHelper.T("老板，你这有没有纯水？要高纯度的，最好是反渗透过滤的。别拿那种下层区的自来水糊弄我，我是给上层区贵族代买的，太差了他们不要。", "Boss, got any pure water? High purity, reverse-osmosis filtered preferred. Don't fob lower-level tap water off on me - I buy for upper-level nobles; they won't touch junk."),
        LangHelper.T("你这有优质矿泉水吗？要天然矿物质的，TDS值在50-100之间的。我那客户挑剔得很，只喝这个牌子的。你有多少我要多少，价格好说。", "Got premium mineral water? Natural minerals, TDS between 50 and 100. My client's picky - only drinks this brand. I'll take all you have; price is flexible."),
        LangHelper.T("我需要一批高端饮用水，上层区的供应商断货了。你这要是有纯水或者优质水，我全要了。钱不是问题，关键是水质要够好。", "Need a batch of premium drinking water - upper-level suppliers are out. If you have pure or premium water, I'll take everything. Money's no issue; the quality just has to be there."),
        // 专业型（懂行，只挑剔纯水和优质水的质量）
        LangHelper.T("这水的纯度不够，TDS值超过10了。我要的是超纯水，电阻率18.2兆欧的那种，你这不行。有没有更好的？没有的话我去别家看看。", "This water's purity is off - TDS over 10. I need ultrapure water, 18.2 megohm resistivity. Not good enough. Got anything better? If not, I'll check elsewhere."),
        LangHelper.T("这优质水的矿物质含量不对，钙镁比例失衡了。我客户只喝特定品牌的，你这是仿的吧？有没有正品？哪怕贵点也行。", "This premium water's mineral profile is wrong - calcium-magnesium ratio is off. My client only drinks specific brands. Is this a knockoff? Got the real thing? Even pricier is fine."),
        LangHelper.T("你这纯水的保存条件不对，都滋生细菌了。你闻，都有味道了。这种东西我不能要，给贵族喝了会出大事。你有没有冷链保存的？", "Your pure water's stored wrong - bacteria growing. Smell it, there's an odor. I can't take this; giving it to nobles would be a disaster. Got anything cold-chained?"),
        // 势利型（看不起下层区的水，只买好的）
        LangHelper.T("就这？你们下层区就喝这种水？算了，我只要纯水和优质水，其他的你别给我推荐，我看不上。", "This? This is what the lower level drinks? Fine - I only take pure and premium water. Don't recommend anything else; it's beneath me."),
        LangHelper.T("这水也太脏了吧？你们下层人平时就喝这个？太可怕了。算了，我就买那几瓶纯水，其他的我不要。", "This water is filthy! This is what you people drink? Horrifying. Fine, I'll take those few bottles of pure water. Nothing else."),
        LangHelper.T("别给我推荐这个，我对下层区的水过敏。也别推荐那个，我客户只喝进口的优质水。你这到底有没有能喝的东西？", "Don't recommend this - I'm allergic to lower-level water. Not that either - my client only drinks imported premium. Do you have anything drinkable at all?"),
        // 合作型（想长期采购纯水和优质水）
        LangHelper.T("老板，我看你这纯水和优质水挺全的。以后我长期在你这采购，你给我个批发价怎么样？我每个月都要大量的高端水，量很大的。", "Boss, you seem well stocked on pure and premium water. Let's do long-term business - wholesale price? I need large volumes of high-end water every month."),
        LangHelper.T("我跟你说，我有稳定的客户源，上层区的贵族都只喝纯水和优质水。你要是能稳定供货，我们长期合作，利润对半分。", "Here's the thing - I have a steady client base; upper-level nobles only drink pure and premium water. Supply me reliably and we'll partner long-term - profits split down the middle."),
        LangHelper.T("老板，你这有没有门路搞到限量版的高端水？比如那种纪念款的纯水，或者稀有矿泉的优质水？我买，价格好商量。", "Boss, any channels to limited-edition premium water? Commemorative pure water, or rare-spring premium? I'll buy - price is negotiable."),
        // 谨慎型（怕买到假的纯水和优质水）
        LangHelper.T("……老板，你这不会是卖假水吧？我先问清楚，你这纯水和优质水都是正品吗？是正品我再买，不是我就走了，当我没来过。", "...Boss, you're not selling fake water, are you? Let me ask first - are your pure and premium waters authentic? If yes, I'll buy. If not, I'm leaving; pretend I was never here."),
        LangHelper.T("我跟你说，我买这些水都是给上层区贵族的，不能出问题。你别给我拿那种过滤的自来水冒充纯水，我一喝就喝得出来。你要是敢骗我，我也知道你店在哪。", "I'm buying these for upper-level nobles - they can't have problems. Don't pass filtered tap water off as pure; I'll taste it instantly. Try to cheat me and I know where your shop is."),
        LangHelper.T("……交易就交易，别问那么多。你管我买去干什么，给钱就行。你要是问东问西的，我就去别家了。痛快一点，那几瓶纯水和优质水卖不卖？", "...Business is business - stop with the questions. What I buy it for is none of your concern; you get paid. Keep prying and I'm going elsewhere. Make it quick - those bottles of pure and premium water, selling or not?")
    };

    // 上层人专属对话 - 买家（天龙人风格，多种性格：傲慢/挑剔/炫富/好奇/不耐烦）
    private static readonly string[] UpperBuyerDialogues = {
        // 傲慢型
        LangHelper.T("把你们这最贵的拿出来。别拿那种便宜货糊弄我，我可看不上。", "Show me your most expensive item. Don't try to fob cheap junk off on me - I have standards."),
        LangHelper.T("就这？你们这就这点东西？我家储藏室都比你这店大。算了，随便挑一个吧。", "This is it? This is all you have? My storage room is bigger than your shop. Fine, I'll grab something random."),
        LangHelper.T("你这店也太小了吧？转个身都难。行了，那个什么，给我包起来，别找零了。", "Your shop is tiny - can barely turn around. Whatever, wrap up that thing over there. Keep the change."),
        // 挑剔型
        LangHelper.T("这东西做工也太粗糙了吧？你们下层人就用这种东西？算了，凑合买一个回去给佣人玩。", "The craft on this is atrocious. You lower-level people use things like this? Fine, I'll grab one for the servants to play with."),
        LangHelper.T("这颜色不对，这材质也不对，这设计更是一言难尽。你们这有没有稍微能看一点的？", "Wrong color, wrong material, and the design defies description. Got anything that's at least presentable?"),
        LangHelper.T("别给我推荐这个，我对这个过敏。也别推荐那个，我家已经有了。你这到底有没有好东西？", "Don't recommend this - I'm allergic. Not that either - I already have one at home. Do you have anything good at all?"),
        // 炫富型
        LangHelper.T("钱？钱是问题吗？我怕的是你这没有配得上我的东西。随便挑，不用看价格。", "Money? Since when is money an issue? I'm afraid you have nothing worthy of me. Pick whatever - price doesn't matter."),
        LangHelper.T("这个这个这个，还有那个，都给我包起来。多少钱？不用算了，直接刷卡，我赶时间。", "This, this, this, and that one - wrap them all up. How much? Don't bother calculating - just charge the card. I'm in a hurry."),
        LangHelper.T("你知道我这一身多少钱吗？说出来吓死你。所以你这的东西对我来说都跟白送一样。", "Know how much this outfit costs? The number would shock you. So everything here is basically free to me."),
        // 好奇型
        LangHelper.T("哎呀这是什么呀？真有意思，我从来没见过这种东西。多少钱？我买回去研究研究。", "Oh, what's this? Fascinating - I've never seen anything like it. How much? I'll take it home to study."),
        LangHelper.T("你们下层人平时都用这种东西吗？太有趣了！这个给我包起来，我要带回去给朋友看看。", "You lower-level people use things like this daily? How amusing! Wrap this one up - I'm taking it to show my friends."),
        LangHelper.T("这东西怎么用啊？你能教教我吗？太有意思了，我买了，你再给我详细说说怎么用。", "How do you use this? Can you teach me? So intriguing - I'll buy it; walk me through it in detail."),
        // 不耐烦型
        LangHelper.T("行了行了，别介绍了，我自己会看。这个，给我包起来，快点，我赶时间。", "Enough with the introduction - I can see for myself. This one, wrap it up. Hurry, I'm in a rush."),
        LangHelper.T("你能不能快点？我约了人做美容，迟到了可不好。这东西到底卖不卖？", "Can you hurry? I have a beauty appointment - being late won't do. Is this for sale or not?"),
        LangHelper.T("我数三下，你给我包好。一……二……算了，你这人真墨迹，我不要了。", "I'll count to three - wrap it up. One... two... forget it, you're too slow. I don't want it anymore."),
        // 嫌弃型
        LangHelper.T("这地方也太脏了吧？你这东西不会也带病菌吧？算了，我就买一个，回去得消消毒。", "This place is filthy! Your goods aren't carrying germs, are they? Fine, I'll buy one - it needs disinfecting at home."),
        LangHelper.T("你这店卫生条件达标吗？我怎么看着这么悬呢？这东西我买了，但是你得给我保证是干净的。", "Does your shop meet hygiene standards? Looks sketchy to me. I'll take this, but you'd better guarantee it's clean."),
        LangHelper.T("你们下层人平时就在这种地方买东西吗？太可怕了。算了，我就体验一次，这个给我包起来。", "You lower-level people shop in places like this daily? Terrifying. Fine, I'll experience it once - wrap this one up.")
    };

    // 上层公民 Upper（买为主 · 傲慢/居高临下/不耐烦）
    private static readonly string[] UpperDialogues = {
        LangHelper.T("你就是下层区那个当铺的？就这？", "You're the lower-level pawnshop owner? This is it?"),
        LangHelper.T("我下来'视察'，顺便买点东西。", "I came down to 'inspect' - might buy something while I'm here."),
        LangHelper.T("这地方，味道可真够呛。", "This place - the smell is something else."),
        LangHelper.T("朋友说你这有点意思，我来看个新鲜。", "A friend said this place is interesting. Came for the novelty."),
        LangHelper.T("把这店里最好的拿出来。", "Bring out the best this shop has."),
        LangHelper.T("钱不是问题，问题是东西配不配得上。", "Money isn't the problem - the question is whether your goods measure up."),
        LangHelper.T("我听说下层的东西都掺假？", "I hear everything down here is counterfeit?"),
        LangHelper.T("别拿糊弄下层人的货糊弄我。", "Don't sell me the stuff you pawn off on lower-level folks."),
        LangHelper.T("你在跟我讨价还价？有意思。", "You're haggling with me? Amusing."),
        LangHelper.T("行了，就当打赏你。", "Fine - consider it a tip."),
        LangHelper.T("下次有好货，通知我的人。", "Next time you get good stock, notify my people."),
        LangHelper.T("这趟下来，也就这店还算能看。", "This whole trip down, only this shop was worth seeing."),
        LangHelper.T("我家佣人说你这有稀奇玩意儿，我来看看。", "My servants said you have curious items. Came to look."),
        LangHelper.T("这东西在上边要贵三倍，你这倒是便宜。", "Up top this costs three times as much. Yours is actually cheap."),
        LangHelper.T("别以为我不懂行，我可是见过世面的。", "Don't think I don't know the trade - I've seen the world."),
        LangHelper.T("这店要是开在楼上，估计早被查封了。", "If this shop were up top, it'd have been shut down ages ago."),
        LangHelper.T("你这有没有什么……不能见光的东西？", "Got anything... that shouldn't see the light?"),
        LangHelper.T("我下来这一趟，可是冒着被感染的风险。", "Coming down here, I risked infection, you know."),
        LangHelper.T("这东西送我了？那我就不客气了。", "You're giving this to me? Well, don't mind if I do."),
        LangHelper.T("记住，是我光顾你，不是你求我。", "Remember - I'm patronizing you, not the other way around.")
    };

    // 安保 Security（检查/买 · 公事公办/想捞好处）
    private static readonly string[] SecurityDialogues = {
        LangHelper.T("巡逻路过，看看你这有没有违规。", "Patrolling by - checking for violations."),
        LangHelper.T("例行检查，别紧张。", "Routine check. No need to be nervous."),
        LangHelper.T("老板，交个朋友，以后好说话。", "Boss, let's be friends - makes things easier down the road."),
        LangHelper.T("你这有没有……呃……特供的？", "Got anything... uh... special order?"),
        LangHelper.T("按规定我不能多说。你知道我意思。", "Regulations say I can't say much. You know what I mean."),
        LangHelper.T("我看你这店里，有点不对劲啊。", "Something about this shop doesn't sit right with me."),
        LangHelper.T("最近风声紧，你懂吧。", "The heat's on lately - you know how it is."),
        LangHelper.T("给我个面子价。", "Give me a friends-and-family price."),
        LangHelper.T("这算……保护费？哈哈，开玩笑的。", "Is this... protection money? Ha, just kidding."),
        LangHelper.T("行，就当没看见。", "Alright - consider it unseen."),
        LangHelper.T("下次给我留点好东西。", "Save something good for me next time."),
        LangHelper.T("你这店，我记下了。是好是坏，看你自己。", "I've got your shop on my radar. Good or bad - that's up to you."),
        LangHelper.T("今天我值班，你这最好别出事。", "I'm on duty today - better keep things clean."),
        LangHelper.T("我小舅子想找点活干，你这缺人不？", "My brother-in-law needs work. Hiring?"),
        LangHelper.T("这东西我没收了啊……开玩笑的，多少钱？", "I'm confiscating this... kidding. How much?"),
        LangHelper.T("你这营业执照没问题吧？我看看。", "Your business license is in order, right? Let me see."),
        LangHelper.T("楼上让我来查查你这有没有违禁品。", "Up top sent me to check for contraband."),
        LangHelper.T("我跟你说，最近有人举报你这。", "Word is, someone's filed a complaint about you lately."),
        LangHelper.T("这东西我买了，别给我记在账上。", "I'm buying this - don't put it in the books."),
        LangHelper.T("你要是懂事，咱们以后井水不犯河水。", "Play smart and we'll stay out of each other's way.")
    };

    // 游客 Tourist（买 · 好奇/新鲜/容易被宰）
    private static readonly string[] TouristDialogues = {
        LangHelper.T("哇，这就是下层区的当铺？跟导游说的不一样。", "Wow, this is the lower-level pawnshop? Different from what the guide said."),
        LangHelper.T("朋友推荐我来的，说你这有意思。", "Friend recommended this place - said it's interesting."),
        LangHelper.T("我第一次来下层区……你们这真会下酸雨吗？", "First time in the lower level... do you really get acid rain here?"),
        LangHelper.T("这个是什么？这个呢？这个呢？", "What's this? And this? And this?"),
        LangHelper.T("多少钱？哦天，这么便宜？！", "How much? Oh my, that cheap?!"),
        LangHelper.T("我要带点纪念品回去给家里人。", "I want souvenirs to take back to my family."),
        LangHelper.T("这东西是真货吗？是的话我多买点。", "Is this authentic? If so, I'll buy more."),
        LangHelper.T("行行行，我买了，别宰我啊。", "Fine, fine, I'll take it - just don't rip me off."),
        LangHelper.T("导游说你们这的人都宰游客……你不会吧？", "The guide said you people fleece tourists... you wouldn't, right?"),
        LangHelper.T("太棒了！我要发个朋友圈！", "Amazing! I'm posting this on social media!"),
        LangHelper.T("下次休假我还来！……如果我还敢来的话。", "I'll be back next vacation!... if I dare."),
        LangHelper.T("这趟值了！", "This trip was worth it!"),
        LangHelper.T("你们这的人都这么直接吗？我喜欢。", "Are people here always this direct? I like it."),
        LangHelper.T("这东西在上边要贵十倍，你这也太便宜了。", "Up top this costs ten times more. Yours is ridiculously cheap."),
        LangHelper.T("我要给我女朋友带个礼物，你推荐个？", "Need a gift for my girlfriend - any recommendations?"),
        LangHelper.T("这地方虽然破，但是东西是真的好。", "The place is shabby, but the goods are genuinely good."),
        LangHelper.T("导游说别跟陌生人说话，但是你看起来不像坏人。", "The guide said don't talk to strangers, but you don't look like a bad guy."),
        LangHelper.T("我买了这么多，能送我一个吗？", "I've bought this much - can I get one free?"),
        LangHelper.T("这东西怎么用？你教教我。", "How does this work? Show me."),
        LangHelper.T("我回去要跟朋友炫耀，说我去了下层区的当铺。", "I'm going to brag to my friends that I visited the lower-level pawnshop.")
    };

    // 革命者 Rev（特殊/买 · 警惕/愤世嫉俗/认原则）
    private static readonly string[] RevDialogues = {
        LangHelper.T("你这店，是给谁开的？", "Who's this shop for?"),
        LangHelper.T("我听说下层区有家店还讲公道。来看看。", "Heard there's a fair-dealing shop in the lower level. Came to see."),
        LangHelper.T("别紧张，我就是个路过的工人。", "Relax - I'm just a worker passing through."),
        LangHelper.T("上边那帮人的东西，都是喝人血换来的。", "Everything those people up top own was bought with blood."),
        LangHelper.T("你卖不卖给工人兄弟打折？", "Do you give worker brothers a discount or not?"),
        LangHelper.T("这货要是从上边来的，我可不碰。", "If this came from up top, I won't touch it."),
        LangHelper.T("别跟上边那帮人走太近，没好处。", "Don't get too close to those people up top - nothing good comes of it."),
        LangHelper.T("给个实在价，我们不玩虚的。", "Fair price - we don't play games."),
        LangHelper.T("你要是跟上边一伙的，我扭头就走。", "If you're in league with them up top, I'm walking out."),
        LangHelper.T("下层人帮下层人。", "Lower-level people help lower-level people."),
        LangHelper.T("这家店，我记下了。好样的。", "I've noted this shop. Good on you."),
        LangHelper.T("记住，你没见过我。", "Remember - you never saw me."),
        LangHelper.T("我们需要一批物资，你这能搞到吗？", "We need a batch of supplies - can you get them?"),
        LangHelper.T("别问我用来干什么，对你没好处。", "Don't ask what it's for - better for you not to know."),
        LangHelper.T("上边那帮人迟早要倒台，你站哪边？", "Those people up top are going down eventually - which side are you on?"),
        LangHelper.T("这钱是干净的，放心收。", "This money's clean - take it without worry."),
        LangHelper.T("我们的人说你这靠谱，我才来的。", "Our people said you're reliable - that's why I'm here."),
        LangHelper.T("别跟安保走太近，他们不是什么好东西。", "Don't get chummy with security - they're no good."),
        LangHelper.T("这东西我买了，但是你别记我的名字。", "I'll buy this, but don't write down my name."),
        LangHelper.T("总有一天，下层的人会站起来的。", "One day, the lower level will rise.")
    };

    // 黑市 BlackMarket（卖买/易物 · 狡猾/话里有话/认规矩）
    private static readonly string[] BlackMarketDialogues = {
        LangHelper.T("朋友介绍来的，说你懂规矩。", "A friend sent me - said you know the rules."),
        LangHelper.T("我这有批……特别的货，你接不接？", "Got a batch of... special goods. Interested?"),
        LangHelper.T("打听点事，顺便做个买卖。", "Here for information, and a bit of business on the side."),
        LangHelper.T("听说你这的价，比上边那帮人实在。", "Heard your prices are more honest than those up top."),
        LangHelper.T("你知道这东西在上边可不便宜。", "You know this isn't cheap up top."),
        LangHelper.T("我要的可不是明面上的价。", "The price I want isn't the public one."),
        LangHelper.T("黑市有黑市的规矩，你懂吧？", "The black market has its rules - you understand?"),
        LangHelper.T("货是好货，就是来路……你懂的。", "Good goods, but the origin... you know how it is."),
        LangHelper.T("识货的人，不该开这个价。", "A man who knows value shouldn't quote that price."),
        LangHelper.T("再添点，下次有好货先给你。", "Add a bit more, and you'll get first pick next time."),
        LangHelper.T("够意思。记住了，你是我的人脉。", "Good man. Remember - you're in my network now."),
        LangHelper.T("下次见，道上的朋友。", "See you around, friend of the street."),
        LangHelper.T("这东西我只给懂行的人看，你算一个。", "I only show this to people who know - you're one of them."),
        LangHelper.T("别问我从哪弄来的，问了我也不说。", "Don't ask where I got it - I won't tell anyway."),
        LangHelper.T("上边的人都在找这东西，你敢收吗？", "Everyone up top is looking for this. Got the guts to take it?"),
        LangHelper.T("这价已经是给你面子了，别得寸进尺。", "This price is already a courtesy - don't push it."),
        LangHelper.T("我这还有更好的货，就看你有没有胆子收。", "I've got better stock too - depends on your nerve."),
        LangHelper.T("道上的规矩，一手交钱一手交货。", "Street rules - cash in hand, goods in hand."),
        LangHelper.T("你这店要是被查了，可别把我供出来。", "If your shop gets raided, don't give me up."),
        LangHelper.T("跟你做生意痛快，以后常来。", "Pleasure doing business - I'll be back.")
    };

    // 卡特尔 Cartel（批发 · 寡言/命令式/威胁藏在平静里）
    private static readonly string[] CartelDialogues = {
        LangHelper.T("老板。我们的人说，你这能走量。", "Boss. Our people say you can move volume."),
        LangHelper.T("批发的货，你有多少？", "Wholesale goods - how much do you have?"),
        LangHelper.T("我不是来闲逛的。谈生意。", "I'm not here to browse. Business."),
        LangHelper.T("听说你这规矩，跟我讲讲。", "Heard you have rules here. Tell me."),
        LangHelper.T("这批货，我要干净的。别拿次品糊弄。", "This batch - I want it clean. No fakes."),
        LangHelper.T("量要大，价要平。就这么简单。", "Big volume, flat price. Simple as that."),
        LangHelper.T("你知道跟卡特尔做生意的好处——和坏处。", "You know the benefits of dealing with the cartel - and the costs."),
        LangHelper.T("别问来路。问了对谁都不好。", "Don't ask about origin. It's bad for everyone."),
        LangHelper.T("你确定要跟我们讨价还价？", "Are you sure you want to haggle with us?"),
        LangHelper.T("行，这个价。下次别让我们涨价。", "Fine, that price. Don't make us raise it next time."),
        LangHelper.T("记住，你没见过我们。", "Remember - you never saw us."),
        LangHelper.T("合作愉快。希望一直愉快。", "Pleasure doing business. Let's hope it stays that way."),
        LangHelper.T("我们的货，只给靠谱的人。你算一个。", "We only sell to reliable people. You qualify."),
        LangHelper.T("这单做完，你就是我们的合作伙伴了。", "Finish this deal and you're our partner."),
        LangHelper.T("别耍花样，我们的人无处不在。", "Don't get clever - our people are everywhere."),
        LangHelper.T("这价是批发价，别跟零售价比。", "That's the wholesale price - don't compare it to retail."),
        LangHelper.T("我们需要稳定的下家，你这看起来还行。", "We need a steady outlet. Yours looks acceptable."),
        LangHelper.T("货明天送到，钱准备好。", "Goods arrive tomorrow. Have the money ready."),
        LangHelper.T("跟我们作对的人，都没好下场。你是聪明人。", "Those who cross us come to bad ends. You're smart."),
        LangHelper.T("这单成了，以后有好货先想着你。", "Close this deal and you'll get first pick of good stock.")
    };

    // 教会 Church（买/布施 · 温和/带信仰/讲良心）
    private static readonly string[] ChurchDialogues = {
        LangHelper.T("愿圣光照耀这间铺子。我来看看有什么能帮上的。", "May the Holy Light bless this shop. I've come to see what I can help with."),
        LangHelper.T("教堂缺些物资，你若有余，我们愿出钱买。", "The church needs supplies. If you have surplus, we'll pay for it."),
        LangHelper.T("孩子，做生意要凭良心。我来买点必需品。", "Child, do business with a conscience. I'm here for necessities."),
        LangHelper.T("听说你这常接济穷人？我代表教区来看看。", "Heard you often help the poor? I represent the parish - came to see."),
        LangHelper.T("愿你的账本，笔笔都是干净的。", "May your ledger - every entry - be clean."),
        LangHelper.T("我们想为教区添置些东西，你有合适的吗？", "We want to furnish the parish. Have anything suitable?"),
        LangHelper.T("祈祷室里缺个像样的摆件，你这里有吗？", "The prayer room lacks a proper ornament. Do you have one?"),
        LangHelper.T("愿你的秤，称得比人心还平。", "May your scale weigh truer than the human heart."),
        LangHelper.T("教区最近收留了不少人，想买点吃的分给他们。", "The parish took in many people lately - I want to buy food for them."),
        LangHelper.T("我为主做工，也为店里的公道作见证。", "I work for the Lord and bear witness to your fairness."),
        LangHelper.T("你若信我，我信你。就这么简单。", "Trust me, and I'll trust you. Simple as that."),
        LangHelper.T("愿主保佑你的生意，也保佑你的良心。", "May the Lord bless your business - and your conscience."),
        LangHelper.T("教区的孩子需要学习用品，你这有吗？", "The parish children need school supplies. Got any?"),
        LangHelper.T("听说你这收东西公道，我来替教区买点。", "Heard you deal fair - buying for the parish."),
        LangHelper.T("愿你的店铺，成为下层区的一盏明灯。", "May your shop be a lamp in the lower level."),
        LangHelper.T("我来买点药品，教区有人生病了。", "Here for medicine - someone in the parish is ill."),
        LangHelper.T("主说，要施舍。我来买点东西施舍给穷人。", "The Lord says: give. I'm buying things to give to the poor."),
        LangHelper.T("你的名声不错，教区的人都这么说。", "Your reputation is good - so the parish says."),
        LangHelper.T("我来替教堂买点物资，你给个实在价。", "Buying supplies for the church - give me a fair price."),
        LangHelper.T("愿主与你同在，生意人。", "May the Lord be with you, shopkeeper.")
    };

    // ============================================================
    // 按玩家状态分类的对话 - 让对话根据玩家状态动态变化
    // ============================================================

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
