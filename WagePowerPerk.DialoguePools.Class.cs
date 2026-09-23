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
}
