using System;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WageSurvival;
internal static partial class SurvivalFood
{
    internal static int GetSatiety() => SaveStore.GetInt("sat", 100);
    internal static int GetThirstPct() => SaveStore.GetInt("thirst", 100);
    internal static int GetHealth() => SaveStore.GetInt("health", 100);
    internal static int GetMood() => SaveStore.GetInt("mood", 50);
    internal static int GetClean() => SaveStore.GetInt("clean", 100);
    internal static int GetSleep() => SaveStore.GetInt("sleep", 100);
    internal static int GetSocial() => SaveStore.GetInt("social", 50);

    internal static void SetSatiety(int v) => SaveStore.SetInt("sat", v);
    internal static void SetThirstPct(int v) => SaveStore.SetInt("thirst", v);
    internal static void SetHealth(int v) => SaveStore.SetInt("health", v);
    internal static void SetMood(int v) => SaveStore.SetInt("mood", v);
    internal static void SetClean(int v) => SaveStore.SetInt("clean", v);
    internal static void SetSleep(int v) => SaveStore.SetInt("sleep", v);
    internal static void SetSocial(int v) => SaveStore.SetInt("social", v);

    internal static void PostfixOnDayStart()
    {
        try
        {
            // 节点爆发事件（nodeKey变化时触发）
            RollNodeFx();

            // 每天衰减：饱食-20 / 口渴-25（乘耐旱系数）
            int sat = Math.Max(0, GetSatiety() - 20 - FxNum("satD"));
            int thBase = (int)(25 * GetThirstEffMult());
            int th = Math.Max(0, GetThirstPct() - thBase - FxNum("thD"));
            SetSatiety(sat); SetThirstPct(th);

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

            // 社交：有交易+5 / 无交易-5 + 节点修正
            int deals = SaveStore.GetInt("deals", 0);
            int social = GetSocial() + (deals > 0 ? 5 : -5) + FxNum("socD") + FxNum("socR");
            SetSocial(Math.Max(0, Math.Min(100, social)));
            SaveStore.SetInt("deals", 0);

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

            // 救场系统：连续饱食<=0达5天 → 好心客户送食1-2份
            int starvingDays = SaveStore.GetInt("starvingDays", 0);
            if (sat <= 0) starvingDays++;
            else starvingDays = 0;
            SaveStore.SetInt("starvingDays", starvingDays);
            if (starvingDays >= 5)
            {
                Core.LogMsg("[WageSurvival] 救场：好心客户送食");
                TryGiveRandomFood(UnityEngine.Random.value < 0.5f ? 2 : 1);
                SetSatiety(30); // 饱食+30
                starvingDays = 0;
                SaveStore.SetInt("starvingDays", 0);
            }

            // 患病判定：节点池sick+20 → 每日患病概率
            int sickChance = GetSickChanceAdd();
            if (sickChance > 0)
            {
                TryInfect(sickChance / 100.0);
            }

            Core.LogMsg($"[WageSurvival] 每日结算: sat={sat} th={th} h={h} clean={clean} sleep={sleep} social={social} mood={mood} | 昂扬={ec} 粮仓={gd}/7 挨饿={starvingDays}天");

            // 刷新状态面板
            RefreshStatusPanel();
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] PostfixOnDayStart 异常: " + ex.Message);
        }
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

    // 售价影响：饱食/心情/Nodes/昂扬/粮仓/补偿buff
    internal static void PostfixGetCurrentValue(GameItem __instance, ref long __result)
    {
        try
        {
            int bonus = GetSellBonusPct();
            if (bonus != 0) __result = (long)(__result * (100 + bonus) / 100.0);
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
                var food = DirectoryMaster.Item("bread", true); // 最简版：给面包
                if (food != null)
                {
                    var l = new Il2CppSystem.Collections.Generic.List<GameItem>();
                    l.Add(food);
                    ((GameInventory)em.backInvinvElement).UncheckedAcceptAll(l);
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

    // 每帧检测：Z键切换状态面板
    private static int _zKeyFrame = -1;
    internal static void PostfixFrameUpdate()
    {
        try
        {
            // 昏迷跳天（帧钩子异步逐轮日切）
            SurvivalFood.TickSkipDays();

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
            Core.LogMsg("[WageSurvival] PostfixFrameUpdate 异常: " + ex.Message);
        }
    }
}
