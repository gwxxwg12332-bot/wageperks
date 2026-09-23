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
}
