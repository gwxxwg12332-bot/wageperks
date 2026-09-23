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

    // 里程碑对话（购买达到一定数量）

    // 获取客户阵营key（用于选择阵营对话池）

    // 根据阵营key获取对话池

    // 根据玩家状态获取对话（经济/经验/声誉综合判断）

    // 按客户意图选择阵营对话池（BUY/SELL 必须用对应意图池，避免买卖台词混淆）

    // 英文模式对话：已由各中文对话数组 LangHelper.T 化后按语言切换，旧 _EN 三池删除

    // 根据客户信息生成故事性对话

    // 矿工专属对话 - 卖家（刚下完矿来卖东西，疲惫麻木，赛博朋克底层风格）
    private static readonly string[] MinerSellerDialogues = {
        LangHelper.T("……老板，收东西不。刚下井，身上还都是灰。这是我从矿里带出来的，你看看。", "...Boss, buying? Just came up from the mine, still covered in dust. Brought this out of the shaft. Take a look."),
        LangHelper.T("咳……咳咳。老板，别嫌我脏，刚从井下上来。这东西是我在矿道深处捡的，你给个价。", "Cough... boss, don't mind the dirt, just came up from the pit. Found this deep in the tunnels. Name a price."),
        LangHelper.T("……累死了。老板，这是我这个月攒的，你收了吧。我得赶紧回去睡，明天还要下井。", "...Exhausted. Boss, this is what I saved up this month. Take it. I need to sleep - back down the shaft tomorrow."),
        LangHelper.T("咳……老板，你看这个。矿上淘汰的，还能用。我偷拿出来的，你别声张。", "Cough... boss, look at this. Retired from the mine, still works. I smuggled it out - keep quiet."),
        LangHelper.T("……老板，急用钱。孩子病了，我得凑医药费。这是我能拿出来的最好的东西了，你看着给。", "...Boss, I need cash fast. My kid's sick, gotta scrape together medicine money. This is the best I can offer. Whatever you think."),
        LangHelper.T("咳……咳咳。老板，这是我从废矿里淘的，差点没出来。你给个实在价，我下次有好货还来。", "Cough... boss, pulled this out of an abandoned mine, almost didn't make it out. Fair price and I'll bring better next time."),
        LangHelper.T("……老板，别问哪来的。矿上的事，你懂的。给个价，合适我就走。", "...Boss, don't ask where it's from. Mine business - you know how it is. Price it, fair and I'm gone."),
        LangHelper.T("累死了……老板，这是我这个月的份。你收了吧，我还得去接孩子放学。", "So tired... boss, this is my monthly quota. Take it - I still need to pick up my kid from school."),
        LangHelper.T("咳……老板，你看这个成色。我在井下待了十二个小时才弄到的。你给个好价，不然我真白干了。", "Cough... boss, look at the quality. Twelve hours underground for this. Pay well, or it was all for nothing."),
        LangHelper.T("……老板，我跟你说，这活不是人干的。但是没办法，得吃饭。这东西你收了吧。", "...Boss, this work isn't for humans. But there's no choice - a man's gotta eat. Take it."),
        LangHelper.T("咳……咳咳。老板，刚上井，脸都没洗。你别嫌弃，这货是好货。你看看值多少。", "Cough... boss, just surfaced, face unwashed. Don't judge - this is good stuff. See what it's worth."),
        LangHelper.T("……老板，急用钱。房租到期了，再不交就要被赶出去了。这是我能拿出来的全部了。", "...Boss, need cash. Rent's due - miss it and I'm out on the street. This is everything I've got."),
        LangHelper.T("累死了……老板，这是我从矿里带出来的。你收了吧，我得赶紧回去，老婆还等着我吃饭呢。", "Exhausted... boss, brought this from the mine. Take it - my wife's waiting for me to eat."),
        LangHelper.T("咳……老板，你看这个。矿上出事了，死了三个人，这是从死人身上拿的。你别忌讳，给个价。", "Cough... boss, look. Accident at the mine - three dead. Took this off a corpse. Don't be squeamish, name a price."),
        LangHelper.T("……老板，我跟你说，这活再干几年我就得尘肺了。但是没办法，得挣钱。这东西你收了吧。", "...Boss, a few more years of this and I'll have black lung. But there's no choice - gotta earn. Take it.")
    };

    // 矿工专属对话 - 买家（刚下完矿来买东西，疲惫麻木，赛博朋克底层风格）
    // 矿工专属对话 - 买家（刚下完矿来买烟酒，疲惫麻木，赛博朋克底层风格，空间站环境）
    private static readonly string[] MinerBuyerDialogues = {
        LangHelper.T("……老板，有烟吗？刚从井下爬上来，肺里全是灰，得抽一根压压。", "...Boss, got smokes? Just crawled up from the pit, lungs full of dust. Need one to settle."),
        LangHelper.T("咳……咳咳。老板，有酒吗？今天矿道又塌了，埋了两个，我得喝点压压惊。", "Cough... boss, got booze? Tunnel collapsed again today, buried two men. Need a drink to steady my nerves."),
        LangHelper.T("……老板，有吃的吗？干了十四个小时，胃里空得能听见回声。", "...Boss, got food? Worked fourteen hours - my stomach's echoing."),
        LangHelper.T("累死了……老板，有止痛药吗？腰疼得直不起来，明天还得下井，不去就扣钱。", "Exhausted... boss, got painkillers? My back won't straighten, but I mine tomorrow anyway - skip a day, lose pay."),
        LangHelper.T("咳……老板，有绷带吗？手上被掘进机划了个口子，血止不住，得包一下。", "Cough... boss, got bandages? The cutter ripped my hand open - can't stop the bleeding, need it wrapped."),
        LangHelper.T("……老板，有便宜的酒吗？发工资了，想喝点好的，但是别太贵，还得留钱交房租。", "...Boss, cheap booze? Payday, want something decent - but nothing pricey, rent's still due."),
        LangHelper.T("咳……咳咳。老板，有烟吗？最好是劲大的那种，淡的抽着没劲，压不住井下的味。", "Cough... boss, smokes? Strong ones if you've got them - light ones can't mask the mine smell."),
        LangHelper.T("……老板，有吃的吗？能放得住的那种，我带回去给孩子吃，他长身体，不能总吃营养膏。", "...Boss, food? Something that keeps - taking it home for my kid. He's growing, can't live on nutrient paste."),
        LangHelper.T("累死了……老板，有什么能解乏的吗？这活真不是人干的，但是没办法，空间站里除了下井没别的活。", "Exhausted... boss, anything to pick me up? This work isn't for humans, but on this station there's nothing but the mines."),
        LangHelper.T("咳……老板，有酒吗？今天差点被埋在井下，我得喝点庆祝一下还活着。明天的事明天再说。", "Cough... boss, got booze? Almost got buried down there today - need a drink to celebrate being alive. Tomorrow's problem for tomorrow."),
        LangHelper.T("……老板，有烟吗？给我来两包，一包自己抽，一包给工头。不给他塞点，他给我派最危险的矿道。", "...Boss, two packs of smokes - one for me, one for the foreman. No bribe, and he assigns me the deadliest tunnels."),
        LangHelper.T("咳……咳咳。老板，有吃的吗？便宜点的，能填饱肚子就行。营养果也行，虽然难吃，但是顶饿。", "Cough... boss, food? Something cheap that fills the belly. Even nutrient fruit - tastes awful but it lasts."),
        LangHelper.T("……老板，有止痛药吗？老毛病了，不吃疼得睡不着。医保不报这个，只能自己买。", "...Boss, painkillers? Old injury - can't sleep without them. Insurance won't cover it, so I buy my own."),
        LangHelper.T("累死了……老板，有酒吗？今天发工资，想喝点，但是别太贵，还得留钱给孩子交学费。", "Exhausted... boss, got booze? Payday, want a drink - nothing fancy, still gotta save for the kid's school fees."),
        LangHelper.T("咳……老板，有烟吗？刚上井，累得要死，得抽一根。这破地方，除了烟和酒，没什么能让人觉得还活着。", "Cough... boss, smokes? Just surfaced, dead tired, need one. In this dump, only smokes and booze remind you you're alive."),
        LangHelper.T("……老板，有酒吗？跟你说个事，今天矿上又死人了，才十九岁，刚来三个月。我得喝点，不然睡不着。", "...Boss, got booze? Something to tell you - another miner died today. Only nineteen, three months on the job. Need a drink or I can't sleep."),
    };

    // 水商/水贩专属对话（卖水的商人，更符合身份）
    internal static readonly string[] WaterMerchantDialogues = {
        LangHelper.T("老板，最近水价又涨了，你这还要货不？", "Boss, water prices went up again. Still buying?"),
        LangHelper.T("哎，老板，跟你说个事。最近供水紧张，我这水可是好不容易弄来的。", "Hey boss, listen. Water supply's tight lately - this was hard to get."),
        LangHelper.T("老板你好啊，我又来了。这次的水质量不错，你尝尝？", "Hello boss, back again. This batch is good quality - have a taste?"),
        LangHelper.T("嗨，老板，最近生意怎么样？我这水卖得可好了。", "Hi boss, how's business? My water sells like crazy."),
        LangHelper.T("老板，跟你商量个事。下次多给你留点好水，你给个实在价？", "Boss, let's make a deal - I'll save you better water next time, you give me a fair price?"),
        LangHelper.T("哎，老板，你这店缺水不？我这有批好水，便宜给你。", "Hey boss, running low on water? I've got a good batch, cheap for you."),
        LangHelper.T("老板，我跟你说，最近安保查得严，我这水可是冒风险弄来的。", "Boss, security's been cracking down - I risked a lot for this water."),
        LangHelper.T("嗨，老板，好久不见啊。最近水价波动大，你这还要不要？", "Hi boss, long time no see. Prices are swinging - still interested?"),
        LangHelper.T("老板，先喝口水，别急。我跟你说，这批水可是上等货。", "Boss, have some water first, no rush. This batch is top tier."),
        LangHelper.T("哎，老板，我这水可是从上层区弄来的，你给个好价？", "Hey boss, this water came from the upper level - pay well?"),
        LangHelper.T("老板，最近缺水缺得厉害，我这货可是抢手货，你要不要？", "Boss, water's scarcer than ever - this is hot merchandise. Want it?"),
        LangHelper.T("嗨，老板，又是我。这次多带了点，你能收多少？", "Hi boss, me again. Brought extra this time - how much can you take?"),
        LangHelper.T("老板，跟你说句实在话，这水我本来想留着自己用的，但是最近手头紧……", "Boss, honest truth - was going to keep this water for myself, but money's tight..."),
        LangHelper.T("哎，老板，你这店要是没水可不行啊，我这刚好有一批。", "Hey boss, a shop without water won't survive - lucky I've got a batch."),
        LangHelper.T("老板，我这水可是经过过滤的，比那些脏水强多了，你看看？", "Boss, this water's filtered - way better than that dirty stuff. Take a look?")
    };

    // 酒商/酒贩专属对话（卖酒的商人，更符合身份）
    internal static readonly string[] AlcoholMerchantDialogues = {
        LangHelper.T("老板，听说你这当铺酿的酒有两下子？我跑了大半个下层区，专门来收你的酒。", "Boss, heard your shop brews something special? Crossed half the lower level just to buy your wine."),
        LangHelper.T("哎，老板，跟你说个事。我收酒多年，你酿的酒在下层区可是小有名气啊。", "Hey boss, let me tell you - I've bought wine for years, and yours has quite a name down here."),
        LangHelper.T("老板你好啊，我又来了。这次带了点好原料，顺便看看你又酿了什么好酒。", "Hello boss, back again. Brought some quality ingredients - curious what you've brewed since."),
        LangHelper.T("嗨，老板，最近生意怎么样？我收的酒都快供不上酒鬼们了，你这有货没？", "Hi boss, business good? I can't keep the drunks supplied - you got stock?"),
        LangHelper.T("老板，跟你商量个事。你酿的酒我看看，价格好说，绝对不让你亏。", "Boss, let's talk. Show me your brew - price is flexible, you won't lose."),
        LangHelper.T("哎，老板，你这店缺酿酒原料不？我这有精选葡萄和特级酵母，便宜给你。", "Hey boss, need brewing supplies? I've got prime grapes and top-grade yeast, cheap for you."),
        LangHelper.T("老板，我跟你说，最近下层区的私酿越来越少了，你这酒留着也是留着，不如卖给我。", "Boss, homebrew's getting scarce down here. Your wine's just sitting there - might as well sell it to me."),
        LangHelper.T("嗨，老板，好久不见啊。最近酒不好收，你酿的酒要是有富余，就匀给我点。", "Hi boss, long time no see. Wine's hard to source lately - if you've got surplus, share some with me."),
        LangHelper.T("老板，先别急，我跟你说。你这酒的品质我懂，绝对给你个公道价。", "Boss, hold on - I know wine quality, and I'll give you a fair price for sure."),
        LangHelper.T("哎，老板，我这收的酒都转手卖给酒鬼们了，你酿的酒要是够好，不愁卖。", "Hey boss, I resell everything to the drunks - brew well and selling won't be a problem."),
        LangHelper.T("老板，最近酒鬼们都馋酒，你酿的有富余就卖给我，省得放着占地方。", "Boss, the drunks are thirsty these days. Sell me your surplus instead of letting it gather dust."),
        LangHelper.T("嗨，老板，又是我。这次带了纯净水和好酵母，你酿的酒呢？拿出来看看。", "Hi boss, me again. Brought pure water and good yeast - where's your brew? Let's see it."),
        LangHelper.T("老板，跟你说句实在话，我收过这么多酒，你酿的算是最对我胃口的。", "Boss, straight talk - of all the wine I've bought, yours suits my taste best."),
        LangHelper.T("哎，老板，你这店要是只卖东西不酿酒可太可惜了，你酿的酒有富余就卖给我。", "Hey boss, a shop like yours that doesn't brew is a waste. Sell me your surplus."),
        LangHelper.T("老板，我这收酒讲究的就是个品质，你酿的酒经过陈酿了吧？拿出来我看看。", "Boss, quality's everything when I buy wine. Yours is aged, right? Let me see it.")
    };

    // 下层农场主卖家专属对话（在下层区种植作物，卖农产品/种子/肥料，朴实接地气，赛博朋克底层麻木风格）
    // 下层农场主卖家专属对话（空间站水培种植，只卖营养果，朴实麻木，赛博朋克底层风格）
    private static readonly string[] FarmerSellerDialogues = {
        LangHelper.T("老板，刚从水培架上摘的营养果，还带着水珠呢。你尝尝，虽然没什么味道，但是顶饿。", "Boss, nutrient fruit fresh off the hydroponic rack, still dewy. Taste it - bland, but it fills you up."),
        LangHelper.T("哎，老板，今年水培的营养果产量低，营养液太贵了。这些是我能拿出来的最好的了。", "Hey boss, low yield this year - nutrient solution's too pricey. This is the best I can offer."),
        LangHelper.T("老板你好，我是下边种植区的。这些营养果是我自己水培的，没打药。难吃是难吃了点，但是维生没问题。", "Hello boss, I'm from the farming district below. These are my own hydroponic fruit, no chemicals. Tastes bad, sure, but it keeps you alive."),
        LangHelper.T("嗨，老板，最近营养液又涨价了，培一架子果成本越来越高。你给个实在价呗？", "Hi boss, nutrient solution keeps rising - rack costs more every season. Fair price, eh?"),
        LangHelper.T("老板，跟你说个事。我这营养果是用过滤水培的，比那些用脏水培的强多了。虽然吃着没什么味道，但是有营养啊。", "Boss, listen - I grow these in filtered water, way better than the dirty-water stuff. Tasteless, but nutritious."),
        LangHelper.T("哎，老板，你这收不收转基因营养果？上面不让种，我们下边偷偷培的，个头大，产量高。难吃，但是顶饿。", "Hey boss, taking GM nutrient fruit? Forbidden up top, so we grow it in secret down here - big, high-yield. Tastes bad, fills you up."),
        LangHelper.T("老板，我跟你说，最近治安部查得严，说我们水培的营养果不合规。我这可是冒风险拿来的。", "Boss, security's cracking down - says our hydroponic fruit doesn't meet regs. I risked a lot bringing this."),
        LangHelper.T("嗨，老板，好久不见啊。最近水培架闹菌，我这是好不容易保住的一点营养果。", "Hi boss, long time no see. Fungus hit the racks - barely saved this little bit."),
        LangHelper.T("老板，先尝尝，别急。我这营养果是自然熟的，比那些催熟的强。虽然吃着没什么味道，但是咱们下层人就靠这个维生。", "Boss, taste it first, no rush. Naturally ripened - better than the force-grown stuff. Tasteless, but this is what keeps us lower-level folks alive."),
        LangHelper.T("哎，老板，你这店缺不缺新鲜营养果？我这每天都能供货，长期合作给你优惠。", "Hey boss, need fresh nutrient fruit? I can supply daily - long-term deal, I'll cut you a discount."),
        LangHelper.T("老板，最近下边的水都被污染了，能培出营养果就不错了。你别嫌它难吃，这可是咱们维生的东西。", "Boss, the water below's contaminated - growing anything is a win. Don't knock the taste; this is our lifeline."),
        LangHelper.T("嗨，老板，又是我。这次带了点稀罕货，上面淘汰下来的品种，我偷偷弄来的苗培的。比普通的强点，但是也就那样。", "Hi boss, me again. Rare stuff this time - a breed the uppers phased out. Snuck the seedlings out and grew them. A bit better than standard, nothing special."),
        LangHelper.T("老板，跟你说句实在话，水培营养果赚不了几个钱，但是我除了培果啥也不会。这玩意上层人看不上，但是咱们下层人离了它活不了。", "Boss, honest truth - nutrient fruit barely pays. But it's all I know. The uppers sneer at it, but we can't live without it."),
        LangHelper.T("哎，老板，你这要是收的话，我下次给你带点蘑菇，种植区角落里长的，纯天然。比这营养果好吃多了，但是没这玩意顶饿。", "Hey boss, if you're buying, next time I'll bring mushrooms - wild from the farming district corners, all natural. Tastier than this, but not as filling."),
        LangHelper.T("老板，我这营养果可是早上刚摘的，皮上还带着霜呢。你摸摸，还凉着呢。别看它不起眼，这可是好东西，难吃但是能维生啊。", "Boss, picked these this morning - still frosted. Feel them, still cold. Don't judge by looks - good stuff, ugly but life-sustaining."),
        LangHelper.T("……老板，跟你说个事。我家那口子病了，诊所的药太贵。我把这茬营养果都拿来了，你给个实在价，我得凑药钱。", "...Boss, something to tell you. My other half's sick, clinic meds are robbery. Brought the whole harvest - fair price, I need medicine money."),
        LangHelper.T("老板，你知道吗？上层区的人连营养果是什么都不知道。他们吃的都是合成食品，比这好吃一百倍。但是咱们下层人，能吃上新鲜营养果就不错了。", "Boss, you know the uppers don't even know what nutrient fruit is? They eat synth food, a hundred times better. Down here, fresh fruit is already a blessing."),
    };

    // 下层农场主买家专属对话（买工具/种子/肥料/水，朴实接地气）
    // 下层农场主买家专属对话（只收种子，空间站水培种植，朴实麻木）
    private static readonly string[] FarmerBuyerDialogues = {
        LangHelper.T("老板，你这有没有好点的种子？我那批种子出芽率太低了，三粒才出一粒。", "Boss, got better seeds? My batch barely germinates - one in three."),
        LangHelper.T("哎，老板，你这有没有上层区下来的种子？贵点也行，产量高，出芽率也高。", "Hey boss, got seeds from the upper level? Pricier is fine - higher yield, better germination."),
        LangHelper.T("老板你好，我是下边种植区的。你这营养果种子怎么卖？我想多整点，扩一架子。", "Hello boss, farming district here. How much for nutrient fruit seeds? Want to expand a rack."),
        LangHelper.T("嗨，老板，最近种子价格涨得厉害，你这有没有便宜点的？我要得多，给个批发价。", "Hi boss, seed prices are climbing. Got anything cheaper? Buying bulk - wholesale me."),
        LangHelper.T("老板，跟你说个事。我那老品种产量太低了，你这有没有新品种的种子？", "Boss, listen - my old strain's yield is terrible. Got any new varieties?"),
        LangHelper.T("哎，老板，你这有没有转基因种子？上层区不让卖，你这要是有我全要了。产量高，个头大。", "Hey boss, got GM seeds? Forbidden up top - if you have them, I'll take everything. High yield, big fruit."),
        LangHelper.T("老板，我跟你说，去年的种子出芽率只有三成，你这能不能保证出芽率？不能保证我不敢买。", "Boss, last year's seeds only hit thirty percent. Can you guarantee germination? If not, I'm not buying."),
        LangHelper.T("嗨，老板，好久不见啊。我那水培架想扩种，你这有没有多点的营养果种子？", "Hi boss, long time no see. Expanding my racks - got plenty of nutrient fruit seeds?"),
        LangHelper.T("老板，先看看，别急。我想买点耐旱的种子，下层区浇水太费劲了，能省点是点。", "Boss, take your time. Want drought-resistant seeds - watering down here's a chore, every drop counts."),
        LangHelper.T("哎，老板，你这有没有抗虫的种子？种植区闹虫灾，普通种子根本活不了，全被啃了。", "Hey boss, pest-resistant seeds? Bug plague in the district - normal seeds get eaten alive."),
        LangHelper.T("老板，最近想整点新品种，你这有没有上层区淘汰下来的种子？便宜点就行，我试试。", "Boss, looking for new varieties - got any phased-out seeds from the upper level? Cheap's fine, I'll experiment."),
        LangHelper.T("嗨，老板，又是我。这次想买点蔬菜种子，光培营养果不行，得换着种，不然土壤都退化了。", "Hi boss, me again. Want vegetable seeds - can't grow only nutrient fruit, the soil's degrading."),
        LangHelper.T("老板，跟你说句实在话，种子是种地的根本，我宁愿多花点钱买好种子，也不买便宜货浪费架子。", "Boss, straight talk - seeds are everything to a farmer. I'd rather pay more for good ones than waste rack space on junk."),
        LangHelper.T("哎，老板，你这有没有育苗盘？种子直接撒基质里出芽率太低了，用育苗盘能高点。", "Hey boss, got seedling trays? Direct sowing germinates poorly - trays help a lot."),
        LangHelper.T("老板，我想买点水培架的配件，我那架子坏了几个，想修修。你这有没有？", "Boss, need hydroponic rack parts - a few of mine broke and I want to fix them. Got any?"),
        LangHelper.T("……老板，跟你说个事。我家那口子病了，但是种植架不能停，停了就没收入了。我想买点好种子，这茬要是能丰收，药钱就有着落了。", "...Boss, listen. My other half's sick, but the racks can't stop - no harvest, no income. Want good seeds; a good crop means medicine money."),
        LangHelper.T("老板，你知道吗？上层区的人吃的都是合成食品，连种子是什么都不知道。但是咱们下层人，种子就是命根子，有种子就有饭吃。", "Boss, the uppers eat synth food - they don't even know what a seed is. Down here, seeds are life itself: seeds mean food."),
    };

    // 全局对话修改（所有玩家都有，不需要选蛙哥牛逼特性）
    // 只修改对话，不处理物品添加

    // 全局对话生成（概率调整：60%阵营对话，20%卖家/买家对话，20%通用对话）
    // 全局对话生成（根据NPC交易物品类别和访问次数生成对话）

    // ============================================================
    // 动态买卖对话（说要买什么就说什么，按客户实际交易物品类别生成）
    // 避免写死商品名（如"书包"）造成出戏
    // ============================================================



    // 卖家：添加随机售卖物品到柜台（完整版：3-5个随机物品，智能推荐）
}
