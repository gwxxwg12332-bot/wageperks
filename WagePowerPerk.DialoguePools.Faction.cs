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
}
