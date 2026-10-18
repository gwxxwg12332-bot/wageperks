using System;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WageSurvival;
internal static partial class SurvivalFood
{
    // 10-06 修复（日志实锤）：双装让路时 sat/thirst 按档型分流——单数据源。
    // 背景：让路模式 WS 只保留消费链（EatBite/喝水），OnDayStart 衰减归 WP（WS 不挂）→ WS 自己的 sat 无人扣减=永远 100
    //      =「饱食已满，不吃」误判（玩家反馈：饱食没满却吃不了，Latest.log 14:23 连续 7 次 EatBite 饱食已满）。
    // 修复：鲁滨逊档（startType=15，WP 衰减+面板在管）→ 转发 WP "RobinCrusoe" 命名空间（吃一口 WP 面板同步涨）；
    //      非鲁滨逊档（WP 衰减不跑）→ WS 自己的键（由 PatchRegistry 让路保留 OnDayStart 衰减挂点负责扣减）。
    // 10-07 阶段D D-3 联动：WpNs 随 WP PERK_ID 改 "RobinCrusoe"→"SurvivalGlobal"（WP 侧已改——转发读同一命名空间，否则双装鲁滨逊档 WS 转发读空）
    private const string WpNs = "SurvivalGlobal";
    private static bool WpOwnsSat => Core._yieldMode && SurvivalTrade.IsRobinsonRun();
    internal static int GetSatiety() => WpOwnsSat ? WageAPI.WageSaveStore.GetInt(WpNs, "sat", 100) : SaveStore.GetInt("sat", 100);
    internal static int GetThirstPct() => WpOwnsSat ? WageAPI.WageSaveStore.GetInt(WpNs, "thirst", 100) : SaveStore.GetInt("thirst", 100);
    internal static int GetHealth() => SaveStore.GetInt("health", 100);
    internal static int GetMood() => SaveStore.GetInt("mood", 50);
    internal static int GetClean() => SaveStore.GetInt("clean", 100);
    internal static int GetSleep() => SaveStore.GetInt("sleep", 100);
    internal static int GetSocial() => SaveStore.GetInt("social", 50);
    // 10-19 P1-1 三线死亡计数 getter（对齐 JP RobinCrusoePerk.Survival.State.cs:21-23；键与 JP 同域 "SurvivalGlobal"，老档计数可继承）
    internal static int GetStarveDays() => SaveStore.GetInt("starveDays", 0);       // 濒饿持续（饱食<20或口渴<20）
    internal static int GetCritDays() => SaveStore.GetInt("critDays", 0);           // 病危持续（健康<20）
    internal static int GetThirstDeathDays() => SaveStore.GetInt("thirstDeath", 0); // 渴死持续（口渴<20）

    // 10-13 Step2（P0）：六维写入统一失效交易缓存——加成缓存(售价/预算/议价)与六维联动，Set* 即刷新（SetCompBuffDays 同型先例；调用点全为业务事件非帧循环）
    internal static void SetSatiety(int v) { if (WpOwnsSat) WageAPI.WageSaveStore.SetInt(WpNs, "sat", v); else SaveStore.SetInt("sat", v); InvalidateTradeCaches(); }
    internal static void SetThirstPct(int v) { if (WpOwnsSat) WageAPI.WageSaveStore.SetInt(WpNs, "thirst", v); else SaveStore.SetInt("thirst", v); InvalidateTradeCaches(); }
    internal static void SetHealth(int v) { SaveStore.SetInt("health", v); InvalidateTradeCaches(); }
    internal static void SetMood(int v) { SaveStore.SetInt("mood", v); InvalidateTradeCaches(); }
    internal static void SetClean(int v) { SaveStore.SetInt("clean", v); InvalidateTradeCaches(); }
    internal static void SetSleep(int v) { SaveStore.SetInt("sleep", v); InvalidateTradeCaches(); }
    internal static void SetSocial(int v) { SaveStore.SetInt("social", v); InvalidateTradeCaches(); }

    internal static void PostfixOnDayStart()
    {
        try
        {
            // 10-07 #12 修复：删除 10-06 让路时代门控（`_yieldMode && IsRobinsonRun()` 跳过衰减）——阶段D"鲁滨逊完全归 WS"后
            //   WP 已删自身 Survival 衰减挂点（PatchRegistryTable 无条目+旧注册死代码），WS=唯一衰减宿主；
            //   残留门控导致鲁滨逊档日结衰减被跳过 → 六维恒 100（饱食/口渴满 → 双击食物/水无变化 = #4"不能吃饭喝水"根因）+ 无自然衰减。
            //   现直接走完整衰减链（无双扣风险——WP 衰减已删）。
            // 节点爆发事件（nodeKey变化时触发）
            RollNodeFx();

            // 每天衰减：饱食-20 / 口渴-25（乘耐旱系数）
            // 10-18 审阅方案A 落地（修正版）：健康 <30 时饱食/口渴衰减 ×2——这才是"健康=体力条"：
            //   抽血-15 健康 → 健康<30 → 饿/渴更快 → 吃喝消耗↑ → 卖血收益被生活成本吃掉（真实取舍）。
            //   （健康自身衰减不乘：健康归零无后果——审阅 §3.3 实锤；方案稿原代码把 decayMul 乘在 hD 上是照抄示意，
            //    效果与"体力条"意图不符，已修正为乘饱食/口渴。）
            int decayMul = GetHealth() < 30 ? 2 : 1;
            int sat = Math.Max(0, GetSatiety() - 20 * decayMul - FxNum("satD"));
            int thBase = (int)(25 * GetThirstEffMult());
            int th = Math.Max(0, GetThirstPct() - thBase * decayMul - FxNum("thD"));
            SetSatiety(sat); SetThirstPct(th);
            Core.LogMsg("[WageSurvival] 日结衰减: sat " + (sat + 20 * decayMul + (int)FxNum("satD")) + "→" + sat + " / th " + (th + thBase * decayMul + (int)FxNum("thD")) + "→" + th);

            // 健康：+10自然恢复 + 节点恢复修正 - 节点衰减（乘糙人抗造系数）
            int h = Math.Min(100, Math.Max(0, GetHealth() + 10 + FxNum("hR") - (int)(FxNum("hD") * GetWearEffMult())));
            if (IsBloodWeak()) h = Math.Max(0, h - 10); // 卖血虚弱：健康衰减加速
            SetHealth(h);

            // 清洁：-10衰减 + 节点修正
            int clean = Math.Max(0, Math.Min(100, GetClean() - 10 - FxNum("cleanD") + FxNum("cleanR")));
            SetClean(clean);

            // 睡眠：+30恢复 + 节点修正 + 补觉高效
            int sleep = Math.Min(100, Math.Max(0, GetSleep() + 30 + FxNum("sleepR") + GetCompBuffSleepRestore()));
            SetSleep(sleep);

            // 睡觉回血
            AddBlood(100);

            // 卖血虚弱递减
            TickBloodRest();

            // 社交：10-06 用户拍板成交即时 +5（RecordDeal 每单加）——日结只处理"无客日 -5"（有交易日不再额外加，防双加）
            int deals = SaveStore.GetInt("deals", 0);
            int social = GetSocial() + (deals > 0 ? 0 : -5) + FxNum("socD") + FxNum("socR");
            SetSocial(Math.Max(0, Math.Min(100, social)));
            SaveStore.SetInt("deals", 0);
            Core.LogMsg("[WageSurvival] 日结社交: deals=" + deals + " social " + (social - (deals > 0 ? 0 : -5) - (int)FxNum("socD") - (int)FxNum("socR")) + "→" + GetSocial());

            // 食物变质
            DecayFoods();

            // 昂扬累计：饱食>=80 且 健康>=80 每2天 +1%售价（封顶5）
            int es = SaveStore.GetInt("elevStreak", 0);
            int ec = SaveStore.GetInt("elevCount", 0);
            if (sat >= 80 && h >= 80)
            {
                es++;
                if (es >= 2) { ec = Math.Min(5, ec + 1); es = 0; }
            }
            else es = 0;
            SaveStore.SetInt("elevStreak", es);
            SaveStore.SetInt("elevCount", ec);

            // 粮仓：饱食>=80 连续7天 → 售价+5%
            int gd = sat >= 80 ? SaveStore.GetInt("granary", 0) + 1 : 0;
            SaveStore.SetInt("granary", gd);

            // 补偿buff每日递减
            TickCompBuffs();

            // 心情结算：三项>=80 → +5；任一项<60 → -10（乘摆烂反弹系数）+ 节点心情 + 补偿buff心情
            int mood = GetMood();
            bool satOK = sat >= 80, thOK = th >= 80, hOK = h >= 80;
            if (satOK && thOK && hOK) mood = Math.Min(100, mood + 5);
            else if (sat < 60 || th < 60 || h < 60) mood = Math.Max(0, mood - (int)(10 * GetMoodDampMult()));
            mood = Math.Max(0, Math.Min(100, mood + FxNum("mood")));
            mood = Math.Min(100, mood + GetCompBuffMoodBonus());
            SetMood(mood);

            // 觅食判定：躺板板（健康<20）30%翻1-2份食物，清洁<50 15%翻1份
            int hNow = GetHealth(), cNow = GetClean();
            if (hNow < 20 && UnityEngine.Random.value < 0.30f)
            {
                Core.LogMsg("[WageSurvival] 躺板板翻找食物");
                TryGiveRandomFood(UnityEngine.Random.value < 0.5f ? 2 : 1);
            }
            else if (cNow < 50 && UnityEngine.Random.value < 0.15f)
            {
                Core.LogMsg("[WageSurvival] 店内翻找食物");
                TryGiveRandomFood(1);
            }

            // ===== 三线生存判定（10-19 四仓断路 P1-1 移植 JP RobinCrusoePerk.Survival.Patches.cs:140-157 死亡四线）=====
            //   阈值直接复用 SurvivalNodeDefs.cs:116 NODE_CRIT=20（与 JP NodeDefs.cs:138 同值，禁止新定义/字面量）
            //   注意：原 :126-141 救场区（starvingDays 计数+5天送食+归零）已整体替换——原实现送食后归零=永远到不了死亡
            //   新语义：濒饿5天送食不归零、第6天起仍持续才饿死（JP :150/:155）；渴死4天/病死4天独立判定（饿优先）
            bool starving = sat < NODE_CRIT || th < NODE_CRIT;
            int sd5 = starving ? GetStarveDays() + 1 : 0;
            int td5 = th < NODE_CRIT ? GetThirstDeathDays() + 1 : 0;
            int cd5 = h < NODE_CRIT ? GetCritDays() + 1 : 0;
            SaveStore.SetInt("starveDays", sd5);
            SaveStore.SetInt("thirstDeath", td5);
            SaveStore.SetInt("critDays", cd5);
            // ===== 救场安全网（濒饿5天送食 / 濒渴第3天送水 / 病危第3天送药；困难模式无救助）=====
            if (sd5 == 5) RescueFeed();
            else if (td5 >= 3 && sd5 < 5) RescueWater();
            else if (cd5 == 3 && sd5 < 5 && td5 < 3) RescueMedicine();
            // ===== 三种生存死亡（救场当天豁免、送食后仍恶化才死）=====
            if (sd5 > 5) { ExecuteGameOverBy("starvation"); return; }
            if (td5 >= 4 && sd5 < 5) { ExecuteGameOverBy("thirst"); return; }
            if (cd5 > 3 && sd5 < 5 && td5 < 4) { ExecuteGameOverBy("disease"); return; }
            Core.LogMsg($"[WageSurvival] 三线计数: 饿{sd5} 渴{td5} 病{cd5}");

            // 患病判定：节点池sick+20 → 每日患病概率
            int sickChance = GetSickChanceAdd();
            if (sickChance > 0)
            {
                TryInfect(sickChance / 100.0);
            }

            Core.LogMsg($"[WageSurvival] 每日结算: sat={sat} th={th} h={h} clean={clean} sleep={sleep} social={social} mood={mood} | 昂扬={ec} 粮仓={gd}/7 濒饿={GetStarveDays()}天");

            // 10-07 阶段D D-1：账本夜报（承接 WP :156 尾部夜报——先补后删）
            AppendLedgerReport();

            // 10-10 #43：状态客户集合随档清理（权威日切=StoreEventManager.OnDayStart Postfix——SurvivalStatusClients.ClearStatusClients；readonly 集合 .Clear() 两处）
            ClearStatusClients();
            // 10-13 Step2（P0）：日切刷新交易缓存（放 #43 清理之后——时序约束：先清集合再失效缓存，面板刷新时全新状态）
            InvalidateTradeCaches();

            // 刷新状态面板
            RefreshStatusPanel();
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] PostfixOnDayStart 异常: " + ex.Message);
        }
    }

    // ===== 救场安全网（10-19 P1-1 移植 JP RobinCrusoePerk.Death.cs:87-135；保留两个原被静默丢失的语义）=====
    //   ① 困难模式无救助（robinson_hard==1 三处 return）；② 按更缺的送（giveWater 判定，避免濒饿送食物、濒渴送错）
    private static void RescueFeed()
    {
        try
        {
            if (SaveStore.GetInt("robinson_hard", 0) == 1) return; // 困难模式：无救助（濒饿直接 GameOver）
            int n = UnityEngine.Random.Range(1, 3); // 1-2 份
            bool giveWater = GetThirstPct() < GetSatiety(); // 按更缺的送（JP Death.cs:94 语义）
            for (int i = 0; i < n; i++) GiveRescueItem(giveWater ? "bottled_water" : "processed_meat", 1);
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了 " + n + " 份" + (giveWater ? "水" : "食物") + "，先撑住", "A kind customer sent " + n + " " + (giveWater ? "waters" : "food") + " — hang in there"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] RescueFeed 异常: " + ex.Message); }
    }
    // 濒渴第3天送水（渴死第4天判定前救场缓刑；带水瓶送普通水，对齐 JP 09-20 拍板语义）
    private static void RescueWater()
    {
        try
        {
            if (SaveStore.GetInt("robinson_hard", 0) == 1) return; // 困难模式：无救助
            EmporiumEntry em2 = EmporiumEntry.Instance;
            if (em2 == null || em2.backInvinvElement == null) return;
            for (int wi = 0; wi < 2; wi++)
            {
                GameItem witem = DirectoryMaster.Item("bottled_water", true); // 普通瓶
                if (witem == null) witem = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("bottled_water"); // 兜底：至少带水普通瓶
                else { try { Il2Cpp.WaterHelper.AddWater(witem, 2, -1, false, 0, 1, true); } catch { } } // 普通质量水 grade=2
                try { witem.DisableTag("stolen", true); } catch { }
                try { Il2Cpp.GeneralHelper.SetItemOwned(witem, true); } catch { }
                WageAPI.WageItemGrant.GrantToInventory(witem, em2.backInvinvElement);
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了 2 份水，先撑住", "A kind customer sent 2 waters — hang in there"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] RescueWater 异常: " + ex.Message); }
    }
    private static void RescueMedicine()
    {
        try
        {
            if (SaveStore.GetInt("robinson_hard", 0) == 1) return; // 困难模式：无救助
            GiveRescueItem("bandage_item", 1);
            try { StoreUIManager.Instance.Notify(LangHelper.T("好心客户送来了药品，快用上", "A kind customer sent medicine — use it now"), "green"); } catch { }
        }
        catch (Exception ex) { Core.LogMsg("[WageSurvival] RescueMedicine 异常: " + ex.Message); }
    }
    // 救场发物（显式目标防重叠，WageItemGrant 契约内置）
    private static void GiveRescueItem(string id, int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            for (int i = 0; i < count; i++)
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item == null) continue;
                try { Il2Cpp.GeneralHelper.SetItemOwned(item, true); } catch { }
                WageAPI.WageItemGrant.GrantToInventory(item, em.backInvinvElement);
            }
        }
        catch { }
    }

    // 食物变质：每2天质量+1（0=新鲜/1=变质/2=腐烂）
    private static void DecayFoods()
    {
        try
        {
            int today = Il2Cpp.StoreStation.GetDayCounter();
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return;
            int count = 0;
            foreach (GameItem item in em.GetAllItems())
            {
                if (item == null || !IsFood(item)) continue;
                int q = GetFoodQuality(item);
                int last = GetFoodDecayDay(item);
                if (today - last >= 2 && q < 2)
                {
                    SetFoodQuality(item, q + 1);
                    SetFoodDecayDay(item, today);
                    count++;
                }
            }
            if (count > 0) Core.LogMsg($"[WageSurvival] 食物变质 {count} 件");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] DecayFoods 异常: " + ex.Message);
        }
    }

    private static int GetFoodDecayDay(GameItem item)
    {
        try
        {
            if (item.IsTag(FOOD_DECAY_DAY_TAG))
            {
                var ts = item.GetTagReadonly(FOOD_DECAY_DAY_TAG);
                if (ts != null) return ts.GetInt();
            }
        }
        catch { }
        return Il2Cpp.StoreStation.GetDayCounter();
    }
    private static void SetFoodDecayDay(GameItem item, int day)
    {
        try
        {
            if (!item.IsTag(FOOD_DECAY_DAY_TAG)) item.EnableTag(FOOD_DECAY_DAY_TAG, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(day); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(FOOD_DECAY_DAY_TAG, il2cppAct, false);
        }
        catch { }
    }

    // 售价影响：饱食/心情/Nodes/昂扬/粮仓/补偿buff + 鲁滨逊档食物品质分档（10-06 复刻 WP ApplyRobinsonMarkup）
    internal static void PostfixGetCurrentValue(GameItem __instance, ref long __result)
    {
        try
        {
            // 10-06 复刻 WP Patches.Trade.Markup.cs:213-239：鲁滨逊档卖食物按品质分档
            // 品质3=售价归零 / 品质2=×0.1 / 已食用=×0.2 / 品质0=×1.3（与 WP 完全一致；普通档不参与）
            if (SurvivalTrade.IsRobinsonRun() && FoodIdentifyHelper.IsFood(__instance))
            {
                int q = GetFoodQuality(__instance);
                switch (q)
                {
                    case 3:
                        __result = 0L;
                        SurvivalTrade.AddPriceLabel(__instance, "wage_survival_food_q3",
                            LangHelper.T("食物品质3档 售价归零", "Quality-3 food: zero sell value"), -100);
                        break;
                    case 2:
                        __result = (long)(__result * 0.1);
                        SurvivalTrade.AddPriceLabel(__instance, "wage_survival_food_q2",
                            LangHelper.T("食物品质2档 售价×10%", "Quality-2 food: sell ×10%"), -90);
                        break;
                    default:
                        if (SurvivalFood.IsEaten(__instance))
                        {
                            __result = (long)(__result * 0.2);
                            SurvivalTrade.AddPriceLabel(__instance, "wage_survival_food_eaten",
                                LangHelper.T("已食用 售价×20%", "Eaten food: sell ×20%"), -80);
                        }
                        // 10-06 用户拍板：品质0（新鲜）卖×1 原价——不溢价（WP 的 ×1.3 不采用），无词条
                        break;
                }
            }
            int bonus = GetSellBonusPct();
            if (bonus != 0)
            {
                __result = (long)(__result * (100 + bonus) / 100.0);
                // 10-06 复刻 WP AddTradeLabel：挂"售价+X%"词条 → tooltip/详情显示加价原因（判重+更新，高频调用安全）
                SurvivalTrade.AddPriceLabel(__instance, "wage_survival_sell_bonus",
                    LangHelper.T("生存售价加成 " + (bonus > 0 ? "+" : "") + bonus + "%", "Survival sell bonus " + (bonus > 0 ? "+" : "") + bonus + "%"), bonus);
            }
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] PostfixGetCurrentValue 异常: " + ex.Message);
        }
    }

    // 客户预算加成：心情/Nodes/昂扬影响客户预算（10-03 阶段C：非鲁滨逊专用——鲁滨逊由 SurvivalTrade 门控，防双跑）
    internal static void PostfixPickClient(StoreClient __result)
    {
        try
        {
            if (__result == null) return;
            if (SurvivalTrade.IsRobinsonRun()) return;
            int bonus = GetBudgetBonusPct();
            if (bonus > 0 && !__result.useClientBudget)
            {
                __result.clientCash = (int)(__result.clientCash * (1.0 + bonus / 100.0));
            }
            // 客流削减：心情差→客户预算减少
            int mood = GetMood();
            if (mood < 20) __result.clientCash = (int)(__result.clientCash * 0.5); // 崩溃-50%
            else if (mood < 40) __result.clientCash = (int)(__result.clientCash * 0.7); // 低落-30%
            else if (mood < 60) __result.clientCash = (int)(__result.clientCash * 0.9); // 低迷-10%
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] PostfixPickClient 异常: " + ex.Message);
        }
    }

    // 交易声望倍率：心情/Nodes影响声望
    internal static void PostfixGetTradeRepMultiplier(ref double __result)
    {
        try
        {
            int bonus = GetBargainBonusPct();
            if (bonus != 0) __result *= (1.0 + bonus / 100.0);
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] PostfixGetTradeRepMultiplier 异常: " + ex.Message);
        }
    }

    // 随机给食物（觅食判定用）
    internal static void TryGiveRandomFood(int count)
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            for (int i = 0; i < count; i++)
            {
                var food = WageAPI.WageItemFactory.Create("bread"); // 10-06 收敛：WageItemFactory 权威创建（A 类纯创建）；最简版给面包
                if (food != null)
                {
                    // 10-06 二期修正：统一入口 GrantToInventory 显式目标（无所有权转移对齐原行为）；原无确认无日志，入口失败 Destroy+日志
                    WageAPI.WageItemGrant.GrantToInventory(food, em.backInvinvElement);
                }
            }
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] TryGiveRandomFood 异常: " + ex.Message);
        }
    }

    // 禁外出（低落/崩溃时禁外出）
    internal static bool PrefixOpenGoOutsideConfirm()
    {
        try
        {
            if (IsForbiddenOutside())
            {
                StoreUIManager.Instance.Notify(LangHelper.T("状态太差，无法外出", "Too weak to go outside"), "red");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] PrefixOpenGoOutsideConfirm 异常: " + ex.Message);
        }
        return true;
    }

    // 患病判定：随机概率→健康-40
    internal static void TryInfect(double chance)
    {
        try
        {
            if (UnityEngine.Random.value < (float)chance)
            {
                SetHealth(Math.Max(0, GetHealth() - 40));
                Core.LogMsg("[WageSurvival] 患病！健康-40");
            }
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] TryInfect 异常: " + ex.Message);
        }
    }

    // 10-06 WS 单装修复：特性选择界面点不动（cheatsheet 5.6 [L1]——StartingPerkElement.OnPointerClick 内
    // NewGameData[+0x30](isInMainMenu)==0 → return 无操作；进一次游戏后 isInMainMenu=1 → 能选）。
    // WP 侧 Patches.Lifecycle.cs:587 有此 Prefix，拆分时未复制到 WS → 单装点不动。此处复制核心恢复逻辑（WS 无精神错乱，不需 MadnessRoller 分支）。
    public static void PrefixOnPointerClick(Il2Cpp.StartingPerkElement __instance)
    {
        try
        {
            Il2Cpp.NewGameData instance = Il2Cpp.NewGameData.Instance;
            if (instance != null && !instance.isInMainMenu)
            {
                instance.isInMainMenu = true;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[选点] Prefix异常: " + ex.Message); }
    }

    // 10-07 P2-7：检测 WagePerks 程序集是否在场（单装 WS 判定用——WP 在场才让路 Z 键）
    // 10-07 性能优化项7（三审合并 Memos EKPd6iXxqJhFFGisNuExK3）：原每帧 AppDomain.GetAssemblies 扫描（鲁滨逊档每帧跑）——
    //   程序集集合运行期不变（MelonLoader 启动即全加载），静态缓存一次扫描结果，零风险一行。
    private static bool? _wagePerksPresent = null;
    internal static bool IsWagePerksPresent()
    {
        if (_wagePerksPresent.HasValue) return _wagePerksPresent.Value;
        bool found = false;
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (asm.GetType("WagePerks.RobinCrusoePerk") != null) { found = true; break; } } catch { }
            }
        }
        catch { }
        _wagePerksPresent = found;
        return found;
    }

    // 每帧检测：Z键切换状态面板（10-06 让路修复：非鲁滨逊档由本挂点处理面板，鲁滨逊档让 WagePerks 的 Z 键接管——普通档双装面板无主修复）
    private static int _zKeyFrame = -1;
    // 10-07 性能优化项3（三审 Memos EKPd6iXxqJhFFGisNuExK3）：每帧路径 catch 日志风暴节流——面板未就绪早期帧会每帧刷日志
    private static float _frameLogLastTime = -1f;
    internal static void PostfixFrameUpdate()
    {
        try
        {
            // 10-07 性能优化项2（三审 Memos EKPd6iXxqJhFFGisNuExK3）：红线4 合规——读档轮询从 Core.OnUpdate 迁入本链
            // （InputActionManager.Update Postfix=统一每帧入口）。_yieldMode 门控保持：双装停轮询防双读
            // （语义与原 Core.OnUpdate 完全一致，仅换入口）。
            if (!Core._yieldMode) { try { WageAPI.Core.Update(); } catch { } }

            // 昏迷跳天（帧钩子异步逐轮日切）
            SurvivalFood.TickSkipDays();

            // 10-07 面板缩放：hover 鲁滨逊面板（ws_status）滚轮缩放——放在鲁滨逊档让路 return 之前（双装时 ws_status 不创建，IsOpen 检查自然跳过）
            try { PanelZoomHelper.FrameUpdate(); } catch { }

            // 10-06 让路门控：鲁滨逊档面板归 WagePerks（其 HandleHotkeys IsActive=startType==15），本链让开避免双 Z 处理打架
            // 10-07 P2-7（玩家"单装 WS 生存面板呼不出"）：让路假设 WP 在场接管鲁滨逊档 Z 键——单装 WS（无 WP）时无人处理。
            //   仅 WP 程序集在场才让路；WP 不在场由本链处理（RefreshStatusPanel 按鲁滨逊档走，WS 自包含）。
            if (SurvivalTrade.IsRobinsonRun() && IsWagePerksPresent()) return;

            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Z)) return;
            int frame = UnityEngine.Time.frameCount;
            if (frame == _zKeyFrame) return;
            _zKeyFrame = frame;
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr != null && mgr.IsOpen("rc_status"))
            {
                mgr.CloseWindow("rc_status");
                Core.LogMsg("[WageSurvival] Z键：关闭面板");
            }
            else
            {
                RefreshStatusPanel(force: true);
                Core.LogMsg("[WageSurvival] Z键：打开面板");
            }
        }
        catch (System.Exception ex)
        {
            // 10-07 性能优化项3：每帧路径日志风暴节流（同异常 5 秒内只打一次，防刷屏；入口防御保留）
            if (_frameLogLastTime < 0f || UnityEngine.Time.time - _frameLogLastTime > 5f)
            {
                _frameLogLastTime = UnityEngine.Time.time;
                Core.LogMsg("[WageSurvival] PostfixFrameUpdate 异常: " + ex.Message);
            }
        }
    }
}
