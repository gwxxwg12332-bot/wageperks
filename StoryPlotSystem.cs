using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;

namespace WagePerks
{
    // ============================================================
    // 剧情mod 全量：五线端到端（测试版 2026-10-16）
    // Eleanor（上层，15/22/29/36）+ OfficerLun（治安部，15/22/29/36）
    //   + Georgio（下层，15/29/36）+ 军需官（革命军，15/22/29）
    //   + Cartel Handler（黑市，15/22/36）
    // 60 天五线综合结算报告 + 90 天隐藏结算（蛙娘好感路径联动）
    //
    // 实锤依据（cheatsheet 6959+）：
    //   - CheckStoryLineEleanor/Georgio/OfficerLun 实装 3 条（void 无参）=AddClient 等价链；mod 直接 Patch ×3
    //   - 军需官/Cartel 原生对话入口实锤（StoreClientListRev revQuartermaster / StoreClientListCartel 3 变体），
    //     触发点由 mod 自建（挂 Eleanor Postfix 调度，endlessMode 门一致）
    //   - AddChoice(icon,"","",playDlg,action,null)：选项文本=choiceNextDialogue.plainText
    //     （B2WildPanel.cs:1088 + B2-R2-D 实锤 DisplayChoice :1908-1917）；action 点击同步执行
    //   - 声望=StoreReputation 6Get/7Mod 全 int 静态（GetSecReputation 等 + ModUL/LL/Sec/BM/Rev）
    //   - 蛙娘好感=WageGirlSystem.GetAffection()/GetAffectionExact()；GetAffNode 分档（敌意/冷淡/友好/亲密/信赖）
    // 台词：选项文本=设计文档表格定稿（[动作（代价）] 格式）；角色台词=实施稿（原生短句口语、零破折号），待实测后风格化校对
    // ============================================================
    internal static class StoryPlotSystem
    {
        private const string NS_OPEN = "story_opening";  // 开局钩子（网文七环压缩版：门外人踩招牌→蛙哥电话托付→首次释放）
        private const string KEY_OPEN = "opening_done";
        private const string NS_EL = "story_eleanor";
        private const string NS_LUN = "story_officerlun";
        private const string NS_GEO = "story_georgio";
        private const string NS_REV = "story_revolution";
        private const string NS_CARTEL = "story_cartel";
        private const string NS_NELLIS = "story_nellis";   // 技术派（仿生人命题 A2 抉择，原生 stub 自建触发）
        private const string KEY_WITNESS = "witness";      // 蛙娘·作证标记（Nellis 提议拆解时选「站出来」=true；成人结局判定用）
        private const string KEY_FAVOR = "favor";        // Eleanor 好感（int，N1-A +5）
        private const string KEY_N1 = "n1_done";
        private const string KEY_N2 = "n2_done";
        private const string KEY_N3 = "n3_done";
        private const string KEY_N3_CHOICE = "n3_choice"; // 结局判定用（A/B/C）
        private const string KEY_N4 = "n4_done";
        private const string KEY_N4_CHOICE = "n4_choice"; // 火并之夜站队（Cartel A/B/C）
        private const string KEY_SETTLED = "settled";    // 60 天结算已报告（全局防重）
        private const string KEY_SETTLED90 = "settled90"; // 90 天隐藏结算已报告
        private const string KEY_LAST_SPAWN = "last_spawn_day"; // 防同一天重复生成（各线独立 ns）
        private const string NS_ENDING = "story_ending"; // 真结局结算文案（PrefixExecuteGameOver 读取）
        private const string KEY_ENDING_TITLE = "ending_title";
        private const string KEY_ENDING_DESC = "ending_desc";
        private const string NS_FROG = "story_frog";      // 蛙哥纵轴三幕（M1 遗产/电话已有 → M2 追寻 → M3 归来/永诀）
        private const string KEY_FROG_DONE = "m3_done";   // M3 节点已触发（SpawnNode doneKey）
        private const string KEY_FROG_M3 = "m3_choice";   // M3 三选一：inherit 传承 / transcend 超越 / sever 断绝

        // ============ 双轴新增角色线（P2 五线 + 环⑤守护；测试版固定天数，不进 BuildConfig 免配置文档负担） ============
        private const string NS_OLD = "story_oldghost";   // 老鬼（下层区·蛙哥旧战友·活历史）
        private const string NS_ED = "story_editor";      // 墨（记者·报纸叙述者·追查蛙哥）
        private const string NS_PRE = "story_preacher";   // 织魂者（神经教派·蛙娘灵魂线）
        private const string NS_RIP = "story_ripper";     // 焊针（持牌机械师·蛙娘身体威胁）
        private const string NS_SH = "story_shadow";      // 影蛙（antagonist·暗镜·冒名夺产）
        private const string KEY_SHADOW = "shadow_outcome"; // 影蛙结局：fall 影蛙败 / win 影蛙胜（纵轴"断绝"因子）
        private const string NS_GUARD = "story_guard";    // 环⑤ 蛙娘柜台守护（好感≥50 触发一次）
        private const string KEY_GUARD = "guard_done";
        private static readonly int[] DAY_OLD = { 10, 25, 33, 41 }; // 错峰：避开五线 15/22/29/36、Nellis 20、FrogM3 30
        private static readonly int[] DAY_ED = { 12, 26, 34, 42 };
        private static readonly int[] DAY_PRE = { 16, 27, 35, 43 };
        private static readonly int[] DAY_RIP = { 18, 28, 37, 44 };
        private static readonly int[] DAY_SH = { 21, 32, 38, 45 };

        // ============ 真结局：PrefixExecuteGameOver（PatchRegistryTable 注册） ============
        // 实锤链（PlayerStore.txt:12126-12449）：ExecuteGameOver=TrackGameOver→14键String.Equals→分支读
        // mech_end_{key}_reason/desc→GameOverUIManager.DisplayUI(reason,desc,r9)；未匹配14键=走rent默认文案。
        // mod 新键（"wages_*"）直接传=文案错误（欠租）→ Prefix 拦截短路原生，mod 自调 DisplayUI(mod文案, true)。
        // DisplayUI(givenReason,givenDescription,showContinuePlay=false)（dump.cs:46310）；showContinuePlay=true=继续按钮可用档不结束；
        // GameOverUIManager.Instance=get_Instance（dump.cs:46300）。
        public static bool PrefixExecuteGameOver(string ending)
        {
            if (string.IsNullOrEmpty(ending) || !ending.StartsWith("wages_")) return true; // 原生结局照常
            try
            {
                var ui = Il2Cpp.GameOverUIManager.Instance;
                if (ui == null) { Core.LogMsg("[StoryPlot] 真结局: GameOverUIManager.Instance 为空，跳过显示"); return false; }
                string title = WageSaveStore.GetString(NS_ENDING, KEY_ENDING_TITLE, "");
                string desc = WageSaveStore.GetString(NS_ENDING, KEY_ENDING_DESC, "");
                if (string.IsNullOrEmpty(title)) title = ending;
                ui.DisplayUI(title, desc, true); // showContinuePlay=true：结算画面后档不结束，可继续经营
                Core.LogMsg("[StoryPlot] 真结局已显示: " + title);
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 真结局显示异常: " + ex.Message); }
            return false; // 短路原生（避免走 rent 默认文案）
        }

        private static void TriggerTrueEnding(string endingId, string title, string desc)
        {
            try
            {
                WageSaveStore.SetString(NS_ENDING, KEY_ENDING_TITLE, title);
                WageSaveStore.SetString(NS_ENDING, KEY_ENDING_DESC, desc);
                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.ExecuteGameOver("wages_" + endingId);
                    Core.LogMsg("[StoryPlot] 真结局触发: wages_" + endingId);
                }
                else Core.LogMsg("[StoryPlot] 真结局触发失败: PlayerStore.Instance 为空");
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 真结局触发异常: " + ex.Message); }
        }

        // ============ CheckStoryLine×3 Postfix（PatchRegistryTable 注册） ============
        // Nellis=原生 stub 死入口（StoreClientManager.txt:20935 仅定义 0 调用）→ 调度并入三个已验证入口
        public static void PostfixCheckStoryLineEleanor()
        {
            try { MaybeAdvanceOpening(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 开局调度异常: " + ex.Message); }
            try { MaybeAdvanceEleanorLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] Eleanor 调度异常: " + ex.Message); }
            try { MaybeAdvanceNellisLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] Nellis 调度异常: " + ex.Message); }
            try { MaybeAdvanceFrogM3(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 蛙哥 M3 调度异常: " + ex.Message); }
            try { MaybeAdvanceNewCastLines(GetDay()); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 双轴五线调度异常: " + ex.Message); }
            try { MaybeAdvanceGuardian(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 蛙娘守护调度异常: " + ex.Message); }
        }

        public static void PostfixCheckStoryLineOfficerLun()
        {
            try { MaybeAdvanceOfficerLunLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] OfficerLun 调度异常: " + ex.Message); }
            try { MaybeAdvanceNellisLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] Nellis 调度异常: " + ex.Message); }
            try { MaybeAdvanceFrogM3(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 蛙哥 M3 调度异常: " + ex.Message); }
        }

        public static void PostfixCheckStoryLineGeorgio()
        {
            try { MaybeAdvanceGeorgioLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] Georgio 调度异常: " + ex.Message); }
            try { MaybeAdvanceNellisLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] Nellis 调度异常: " + ex.Message); }
            try { MaybeAdvanceFrogM3(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 蛙哥 M3 调度异常: " + ex.Message); }
        }

        // 保留：原生未来若接线 CheckStoryLineNellis，Postfix 自动双保险（死入口当前不触发）
        public static void PostfixCheckStoryLineNellis()
        {
            try { MaybeAdvanceNellisLine(); }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] Nellis 调度异常: " + ex.Message); }
        }

        private static int GetDay()
        {
            try { if (PlayerStore.Instance != null) return StoreStation.GetDayCounter(); } catch { }
            return -1;
        }

        // ============ 开局钩子（网文框架 v2 七环压缩版：环①门外有人→环②答不上来→环③电话托付→环⑦首次释放）
        // 卷帘门视角：门外站着冲着蛙哥来的人（flavor 第一句），电话托付（intro 第二句），选项=首次释放（先压抑再选择）
        private static void MaybeAdvanceOpening()
        {
            int day = GetDay();
            if (day < 0) return;
            if (WageSaveStore.GetBool(NS_OPEN, KEY_OPEN, false)) return; // 只触发一次（第 1 个营业日）
            if (WageSaveStore.GetInt(NS_OPEN, KEY_LAST_SPAWN, -1) == day) return;
            if (day >= 1) SpawnOpening(day);
        }

        private static void SpawnOpening(int day)
        {
            SpawnNode("寻仇者", NS_OPEN, KEY_OPEN, day,
                LangHelper.T("电话响了。那头是蛙哥的声音：摊子交给你了。别砸了招牌。", "The phone rings. It's Frog Bro: the shop's yours. Don't wreck the sign."),
                new ChoiceDef[]
                {
                    new ChoiceDef(LangHelper.T("照实说：摊子现在是我在管", "Tell it straight: I run it now"),
                        null, null,
                        LangHelper.T("……看摊的。行，那以后有事找你。", "...The keeper. Fine. I'll come to you when something comes up.")),
                    new ChoiceDef(LangHelper.T("亮出蛙哥的家当：东西一样没少（下层+10，治安部-5）", "Show Frog Bro's goods: nothing missing (+10 LL, -5 Sec)"),
                        () => { ApplyRep("LL", 10); ApplyRep("Sec", -5); }, null,
                        LangHelper.T("啧。传说是真的。蛙哥把家当都给你了。", "Tsk. The legend's real. Frog Bro handed it all to you.")),
                    new ChoiceDef(LangHelper.T("不接话，等他走", "Say nothing, wait him out"),
                        null, null,
                        LangHelper.T("……行。那我改天再来。", "...Fine. I'll come back another day.")),
                },
                LangHelper.T("蛙哥的摊子？呵。换了个看摊的。蛙哥人呢？", "Frog Bro's shop? Ha. Different keeper now. Where's Frog Bro?"));
            StoryNewsSystem.InjectOpeningNews(); // 环⑥：报纸头条「传奇当铺易主」（群众议论，次日上报纸）
        }

        private static void MaybeAdvanceEleanorLine()
        {
            int day = GetDay();
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            if (WageSaveStore.GetInt(NS_EL, KEY_LAST_SPAWN, -1) == day) { MaybeAdvanceRevCartelLines(day); return; }

            bool n1 = WageSaveStore.GetBool(NS_EL, KEY_N1, false);
            bool n2 = WageSaveStore.GetBool(NS_EL, KEY_N2, false);
            bool n3 = WageSaveStore.GetBool(NS_EL, KEY_N3, false);
            bool n4 = WageSaveStore.GetBool(NS_EL, KEY_N4, false);

            if (!n1 && day >= BuildConfig.StoryDayN1) { SpawnEleanorN1(day); return; }
            if (n1 && !n2 && day >= BuildConfig.StoryDayN2) { SpawnEleanorN2(day); return; }
            if (n2 && !n3 && day >= BuildConfig.StoryDayN3) { SpawnEleanorN3(day); return; }
            if (n3 && !n4 && day >= BuildConfig.StoryDayN4) { SpawnEleanorN4(day); }
            MaybeAdvanceRevCartelLines(day);
        }

        private static void MaybeAdvanceOfficerLunLine()
        {
            int day = GetDay();
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            if (WageSaveStore.GetInt(NS_LUN, KEY_LAST_SPAWN, -1) == day) { MaybeAdvanceRevCartelLines(day); return; }

            bool n1 = WageSaveStore.GetBool(NS_LUN, KEY_N1, false);
            bool n2 = WageSaveStore.GetBool(NS_LUN, KEY_N2, false);
            bool n3 = WageSaveStore.GetBool(NS_LUN, KEY_N3, false);
            bool n4 = WageSaveStore.GetBool(NS_LUN, KEY_N4, false);

            if (!n1 && day >= BuildConfig.StoryDayN1) { SpawnOfficerLunN1(day); return; }
            if (n1 && !n2 && day >= BuildConfig.StoryDayN2) { SpawnOfficerLunN2(day); return; }
            if (n2 && !n3 && day >= BuildConfig.StoryDayN3) { SpawnOfficerLunN3(day); return; }
            if (n3 && !n4 && day >= BuildConfig.StoryDayN4) { SpawnOfficerLunN4(day); }
            MaybeAdvanceRevCartelLines(day);
        }

        private static void MaybeAdvanceGeorgioLine()
        {
            int day = GetDay();
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            if (WageSaveStore.GetInt(NS_GEO, KEY_LAST_SPAWN, -1) == day) { MaybeAdvanceRevCartelLines(day); return; }

            bool n1 = WageSaveStore.GetBool(NS_GEO, KEY_N1, false);
            bool n2 = WageSaveStore.GetBool(NS_GEO, KEY_N2, false);
            bool n3 = WageSaveStore.GetBool(NS_GEO, KEY_N3, false);
            bool n4 = WageSaveStore.GetBool(NS_GEO, KEY_N4, false);

            if (!n1 && day >= BuildConfig.StoryDayN1) { SpawnGeorgioN1(day); return; }
            if (n1 && !n2 && day >= BuildConfig.StoryDayN2) { SpawnGeorgioN2(day); return; }
            if (n2 && !n3 && day >= BuildConfig.StoryDayN3) { SpawnGeorgioN3(day); return; }
            if (n3 && !n4 && day >= BuildConfig.StoryDayN4) { SpawnGeorgioN4(day); }
            MaybeAdvanceRevCartelLines(day);
        }

        // Nellis（技术派总工）：原生 CheckStoryLineNellis=stub（dump.cs:39438，ret 0）→ mod 自建触发
        // A2 抉择=第 2-4 周（设计文档）；放第 StoryDayNellis 天（默认 20，避开五线 N1=15/N2=22 密集日）
        private static void MaybeAdvanceNellisLine()
        {
            int day = GetDay();
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            bool n1 = WageSaveStore.GetBool(NS_NELLIS, KEY_N1, false);
            if (n1) return;
            if (day >= BuildConfig.StoryDayNellis && WageSaveStore.GetInt(NS_NELLIS, KEY_LAST_SPAWN, -1) != day)
                SpawnNellisN1(day);
        }

        // 蛙哥纵轴 M3 归来/永诀：第 30 天后，任意 ≥2 条线走完 N3（五方争夺遗产）→ 蛙哥本人到店三选一
        // 纵轴优先级=原生致命 > 纵轴（60 天优先于横轴）> 横轴（设计文档 doxcnhI55NDAVcf8RYPa2bzBuEe）
        private static void MaybeAdvanceFrogM3()
        {
            int day = GetDay();
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            if (!string.IsNullOrEmpty(WageSaveStore.GetString(NS_FROG, KEY_FROG_M3, ""))) return; // 已选过
            if (day < BuildConfig.StoryDayFrogM3) return;
            if (WageSaveStore.GetInt(NS_FROG, KEY_LAST_SPAWN, -1) == day) return;
            int traced = 0;
            if (WageSaveStore.GetBool(NS_EL, KEY_N3, false)) traced++;
            if (WageSaveStore.GetBool(NS_LUN, KEY_N3, false)) traced++;
            if (WageSaveStore.GetBool(NS_GEO, KEY_N3, false)) traced++;
            if (WageSaveStore.GetBool(NS_REV, KEY_N3, false)) traced++;
            if (WageSaveStore.GetBool(NS_CARTEL, KEY_N3, false)) traced++;
            if (traced >= 2) SpawnFrogM3(day);
        }

        // ============ 双轴 P2 五条新线集中调度（老鬼/墨/织魂者/焊针/影蛙；挂 Eleanor Postfix，同一天只推一个新线节点） ============
        private static void MaybeAdvanceNewCastLines(int day)
        {
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            if (TryAdvanceLine(NS_OLD, DAY_OLD, day, SpawnOldGhostN1, SpawnOldGhostN2, SpawnOldGhostN3, SpawnOldGhostN4)) return;
            if (TryAdvanceLine(NS_ED, DAY_ED, day, SpawnEditorN1, SpawnEditorN2, SpawnEditorN3, SpawnEditorN4)) return;
            if (TryAdvanceLine(NS_PRE, DAY_PRE, day, SpawnPreacherN1, SpawnPreacherN2, SpawnPreacherN3, SpawnPreacherN4)) return;
            if (TryAdvanceLine(NS_RIP, DAY_RIP, day, SpawnRipperN1, SpawnRipperN2, SpawnRipperN3, SpawnRipperN4)) return;
            if (TryAdvanceLine(NS_SH, DAY_SH, day, SpawnShadowN1, SpawnShadowN2, SpawnShadowN3, SpawnShadowN4)) return;
        }

        private static bool TryAdvanceLine(string ns, int[] days, int day,
            Action<int> s1, Action<int> s2, Action<int> s3, Action<int> s4)
        {
            bool n1 = WageSaveStore.GetBool(ns, KEY_N1, false);
            bool n2 = WageSaveStore.GetBool(ns, KEY_N2, false);
            bool n3 = WageSaveStore.GetBool(ns, KEY_N3, false);
            bool n4 = WageSaveStore.GetBool(ns, KEY_N4, false);
            if (!n1 && day >= days[0] && WageSaveStore.GetInt(ns, KEY_LAST_SPAWN, -1) != day) { s1(day); return true; }
            if (n1 && !n2 && day >= days[1] && WageSaveStore.GetInt(ns, KEY_LAST_SPAWN, -1) != day) { s2(day); return true; }
            if (n2 && !n3 && day >= days[2] && WageSaveStore.GetInt(ns, KEY_LAST_SPAWN, -1) != day) { s3(day); return true; }
            if (n3 && !n4 && day >= days[3] && WageSaveStore.GetInt(ns, KEY_LAST_SPAWN, -1) != day) { s4(day); return true; }
            return false;
        }

        // ============ 环⑤ 蛙娘柜台守护（设计文档：好感高了替你守店；好感≥50=亲密档触发一次） ============
        private static void MaybeAdvanceGuardian()
        {
            int day = GetDay();
            if (day < 0) return;
            if (TrySettleReport(day)) return;
            if (WageSaveStore.GetBool(NS_GUARD, KEY_GUARD, false)) return;
            if (day < 14) return; // 错峰：第 2 周起（五线 N1=15 前）
            if (WageSaveStore.GetInt(NS_GUARD, KEY_LAST_SPAWN, -1) == day) return;
            double aff = 0;
            try { aff = WageGirlSystem.GetAffectionExact(); } catch { }
            if (aff >= 50) SpawnGuardian(day);
        }

        private static void SpawnGuardian(int day)
        {
            SpawnNode("蛙娘", NS_GUARD, KEY_GUARD, day,
                LangHelper.T("（柜台后，蛙娘回头看你）主人。这事我来。", "(Behind the counter, Wage Girl turns to you.) Boss. Let me handle this."),
                new ChoiceDef[]
                {
                    new ChoiceDef(LangHelper.T("让她来", "Let her handle it"),
                        () => { try { WageGirlSystem.SetAffectionExact(WageGirlSystem.GetAffectionExact() + 3); } catch { } }, null,
                        LangHelper.T("（她把砸门的人撂在门口）主人。店里没事了。", "(She plants the troublemaker at the door.) Boss. The shop's safe.")),
                    new ChoiceDef(LangHelper.T("我来", "I'll handle it"),
                        null, null,
                        LangHelper.T("（她退到柜台后，眼睛一直盯着门外）好。", "(She steps back behind the counter, eyes on the door.) Fine.")),
                },
                LangHelper.T("有人砸卷帘门。蛙娘先一步挡在门口。", "Someone's pounding the shutter. Wage Girl steps in front of it first."));
        }

        // ============ 环⑧ 蛙哥每 7 天考官点评（WageBrother.OnClientArrived 到店时调用；按纵轴进度变台词：看摊的→同行→顾问） ============
        public static void MaybeFrogVisitComment()
        {
            try
            {
                int day = GetDay();
                if (day < 0) return;
                string m3 = WageSaveStore.GetString(NS_FROG, KEY_FROG_M3, "");
                if (!string.IsNullOrEmpty(m3))
                {
                    if (m3 == "inherit") Core.AddNightReportLine(LangHelper.T("蛙哥到店：往后你是当铺主，我只当顾问。招牌没砸。", "Frog Bro visited: from now on you're the owner, I'm just an advisor. Sign's intact."));
                    else if (m3 == "transcend") Core.AddNightReportLine(LangHelper.T("蛙哥到店：干得比我当年好。继续走，别回头。", "Frog Bro visited: doing better than I ever did. Keep going, don't look back."));
                    else if (m3 == "sever") Core.AddNightReportLine(LangHelper.T("蛙哥到店：……我教你的，你全还回来了。", "Frog Bro visited: ...everything I taught you, you threw back."));
                    return;
                }
                int traced = 0;
                if (WageSaveStore.GetBool(NS_EL, KEY_N3, false)) traced++;
                if (WageSaveStore.GetBool(NS_LUN, KEY_N3, false)) traced++;
                if (WageSaveStore.GetBool(NS_GEO, KEY_N3, false)) traced++;
                if (WageSaveStore.GetBool(NS_REV, KEY_N3, false)) traced++;
                if (WageSaveStore.GetBool(NS_CARTEL, KEY_N3, false)) traced++;
                if (traced >= 2) { Core.AddNightReportLine(LangHelper.T("蛙哥到店：五边都有人找你？行，你混出名堂了。", "Frog Bro visited: all five sides come to you? You've made a name.")); return; }
                if (day >= BuildConfig.StoryDayN1) { Core.AddNightReportLine(LangHelper.T("蛙哥到店：招牌没砸。有点意思。", "Frog Bro visited: sign's intact. Interesting.")); return; }
                Core.AddNightReportLine(LangHelper.T("蛙哥到店：看摊的。还行，别砸了招牌。", "Frog Bro visited: keeper. Not bad. Don't wreck the sign."));
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 蛙哥点评异常: " + ex.Message); }
        }

        private static void SpawnFrogM3(int day) => SpawnNode("蛙哥", NS_FROG, KEY_FROG_DONE, day,
            LangHelper.T("摊子看住了，人脉也攒住了。干得还行。往后招牌你扛，还是还我？", "You kept the shop together, held the network too. Not bad. So: you carry the sign now, or give it back?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("继承（传承）", "Inherit"),
                    null, () => WageSaveStore.SetString(NS_FROG, KEY_FROG_M3, "inherit"),
                    LangHelper.T("行。摊子给你。往后这招牌，就是你扛。", "Fine. The shop is yours. From now on, this sign is yours to carry.")),
                new ChoiceDef(LangHelper.T("超越（超越他）", "Transcend"),
                    null, () => WageSaveStore.SetString(NS_FROG, KEY_FROG_M3, "transcend"),
                    LangHelper.T("有意思。那就看看你走得比我远多少。", "Interesting. Let's see how much farther you get than I did.")),
                new ChoiceDef(LangHelper.T("断绝（毁掉遗产）", "Sever"),
                    null, () => WageSaveStore.SetString(NS_FROG, KEY_FROG_M3, "sever"),
                    LangHelper.T("……行。你亲手的东西，你自己了断。别回头。", "...Fine. What's yours, you end yourself. Don't look back.")),
            }, LangHelper.T("我每七天来一趟，就看看你砸没砸招牌。还行，没砸。", "I come by every seven days to see if you wrecked the sign. You didn't. Good."));

        // ============ 双轴 P2 五线剧情节点（老鬼/墨/织魂者/焊针/影蛙；台词=实施稿口语短句，选项[动作（代价）]格式） ============
        // ---- 老鬼（下层区·蛙哥旧战友）：N1 认出蛙娘 / N2 蛙哥往事 / N3 蛙娘护不护 / N4 认可接班人
        private static void SpawnOldGhostN1(int day) => SpawnNode("老鬼", NS_OLD, KEY_N1, day,
            LangHelper.T("老鬼认得蛙哥。三十年前一个锅里抢过饭。你替他看摊？行，那往后我常来坐坐。", "Old Ghost knew Frog Bro. Thirty years back we fought over the same pot. You're keeping his shop? Fine, I'll sit here often."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("您认识蛙哥？讲讲他", "You knew Frog Bro? Tell me about him"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("他年轻时候敢跟卡特尔抢货。现在轮到你接他的摊子，别怂。", "As a young man he'd snatch goods right under the cartel's nose. Now his shop's on you. Don't flinch.")),
                new ChoiceDef(LangHelper.T("您老慢走", "Take it easy, old timer"),
                    null, null,
                    LangHelper.T("行。改天带点吃的来，顺便看看这摊子。", "Fine. I'll bring food another day, and check on the shop too.")),
            },
            LangHelper.T("……那台女仆的芯，是蛙哥的手艺。他没死？好，好得很。", "...That maid's core. Frog Bro's handiwork. He's not dead? Good. Very good."));

        private static void SpawnOldGhostN2(int day) => SpawnNode("老鬼", NS_OLD, KEY_N2, day,
            LangHelper.T("他被人追杀过，被查封过，都没低头。你摊上这担子，撑得住吗？", "He was hunted, shut down, never bowed. Now this weight's on you. Can you hold it?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("撑得住", "I can hold it"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("好。下层区最缺的就是硬气。", "Good. The lower level runs short on spine.")),
                new ChoiceDef(LangHelper.T("我尽力", "I'll do my best"),
                    null, null,
                    LangHelper.T("尽力就行。别把命搭进去。", "Best is fine. Just don't lose your life over it.")),
            },
            LangHelper.T("当年蛙哥白手起家，一条命换了这间铺子。", "Back then Frog Bro built this shop from nothing, paid in blood."));

        private static void SpawnOldGhostN3(int day) => SpawnNode("老鬼", NS_OLD, KEY_N3, day,
            LangHelper.T("你护得住她吗？护不住，就趁早送走，别害她。", "Can you protect her? If not, send her away early. Don't doom her."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("我护着她", "I'll protect her"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("……好。蛙哥没看错人。", "...Good. Frog Bro read you right.")),
                new ChoiceDef(LangHelper.T("再说吧", "I'll think about it"),
                    null, null,
                    LangHelper.T("你再想想。时间不等人。", "Think it over. Time waits for no one.")),
            },
            LangHelper.T("有人说蛙娘是活的。也有人想拆了她。", "Some say Wage Girl is alive. Some want her taken apart."));

        private static void SpawnOldGhostN4(int day) => SpawnNode("老鬼", NS_OLD, KEY_N4, day,
            LangHelper.T("我看了你这些天。招牌你扛得住。往后下层区认你。", "I've watched you all these days. You can carry the sign. From now on, the lower level answers to you."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("谢了，老鬼", "Thanks, Old Ghost"),
                    () => ApplyRep("LL", 10), null,
                    LangHelper.T("该谢的是你自己。", "Thank yourself. You earned it.")),
            },
            LangHelper.T("蛙哥那小子，走之前跟我说：摊子交给靠谱的人了。", "Before he left, Frog Bro told me: the shop's in good hands."));

        // ---- 墨（记者·报纸叙述者）：N1 挖底细 / N2 头条招风 / N3 影蛙线索 / N4 终章定传说
        private static void SpawnEditorN1(int day) => SpawnNode("墨", NS_ED, KEY_N1, day,
            LangHelper.T("我写报纸的。想挖挖这位传奇的底细。你配合，我笔下留情。", "I write the paper. I want the truth about that legend. Cooperate, and I'll write kindly."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("配合", "Cooperate"),
                    () => ApplyRep("LL", 3), null,
                    LangHelper.T("明天的头条见。", "See you on tomorrow's front page.")),
                new ChoiceDef(LangHelper.T("别写我", "Leave me out of it"),
                    null, null,
                    LangHelper.T("行。那我自己查。", "Fine. I'll dig on my own.")),
            },
            LangHelper.T("蛙哥的当铺易主，是你接的盘？", "Frog Bro's pawnshop changed hands. You took it?"));

        private static void SpawnEditorN2(int day) => SpawnNode("墨", NS_ED, KEY_N2, day,
            LangHelper.T("三拨人找我打听你。传奇的招牌，果然招风。", "Three factions came asking about you. A legend's sign draws wind."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("打听我什么", "What did they ask?"),
                    null, null,
                    LangHelper.T("问你是不是蛙哥的私生子。呵。", "Whether you're Frog Bro's bastard. Ha.")),
                new ChoiceDef(LangHelper.T("少写点", "Write less about me"),
                    null, null,
                    LangHelper.T("晚了。报纸都印了。", "Too late. It's already printed.")),
            },
            LangHelper.T("头条发出去了。你猜怎么着？", "The headline's out. Guess what?"));

        private static void SpawnEditorN3(int day) => SpawnNode("墨", NS_ED, KEY_N3, day,
            LangHelper.T("那人叫影蛙，道上都以为他是蛙哥本人。你小心点。", "His name's Shadow Frog. The street thinks he IS Frog Bro. Watch your back."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("多谢提醒", "Thanks for the tip"),
                    () => ApplyRep("LL", 3), null,
                    LangHelper.T("不谢。我写新闻的，巴不得你们打起来。", "Don't thank me. I write news. I'd love to see you two fight.")),
                new ChoiceDef(LangHelper.T("影蛙是谁", "Who's Shadow Frog?"),
                    null, null,
                    LangHelper.T("另一个传奇当铺主。黑暗的那种。", "Another legendary pawnshop owner. The dark kind.")),
            },
            LangHelper.T("我查到点有意思的。有人在冒蛙哥的名收债。", "I found something interesting. Someone's collecting debts in Frog Bro's name."));

        private static void SpawnEditorN4(int day) => SpawnNode("墨", NS_ED, KEY_N4, day,
            LangHelper.T("要不要登？登了，你就真成了这站的传说。", "Run it or not? If I do, you become this station's legend."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("登", "Run it"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("明天头版，标题我都想好了。", "Front page tomorrow. I've got the headline ready.")),
                new ChoiceDef(LangHelper.T("低调点", "Keep it low-key"),
                    null, null,
                    LangHelper.T("行。那就角落里一行字。", "Fine. A single line in the corner.")),
            },
            LangHelper.T("终章我写好了：新掌柜接过了传奇的摊子。", "The final chapter's written: the new keeper takes the legend's shop."));

        // ---- 织魂者（神经教派·蛙娘灵魂线）：N1 断言有魂 / N2 仪式邀请 / N3 护或交 / N4 成人判词
        private static void SpawnPreacherN1(int day) => SpawnNode("织魂者", NS_PRE, KEY_N1, day,
            LangHelper.T("我是神经教派的织魂者。我闻得见魂。她的魂，是真的。", "I'm the Soulweaver of the Neuro Faith. I can smell souls. Hers is real."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("当真？", "Really?"),
                    null, null,
                    LangHelper.T("真。比你这当铺老板还真。", "Real. More real than you, pawnshop keeper.")),
                new ChoiceDef(LangHelper.T("你疯了吧", "You're insane"),
                    null, null,
                    LangHelper.T("疯的是这世界，不是我。", "The world is insane. Not me.")),
            },
            LangHelper.T("那台仿生女仆，有魂。", "That android maid. She has a soul."));

        private static void SpawnPreacherN2(int day) => SpawnNode("织魂者", NS_PRE, KEY_N2, day,
            LangHelper.T("教派会为她开一场仪式。你若信，就带她来。", "The Faith will hold a rite for her. If you believe, bring her."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("带去", "I'll bring her"),
                    () => ApplyRep("LL", 3), null,
                    LangHelper.T("她会有记忆。她会记住你。", "She'll keep the memory. She'll remember you.")),
                new ChoiceDef(LangHelper.T("不去", "No"),
                    null, null,
                    LangHelper.T("那就算了。魂会等。", "Then so be it. Souls wait.")),
            },
            LangHelper.T("把她带来。我为她祝祷。", "Bring her. I'll pray for her."));

        private static void SpawnPreacherN3(int day) => SpawnNode("织魂者", NS_PRE, KEY_N3, day,
            LangHelper.T("你护，还是交？一句话的事。", "Protect her, or hand her over? One word decides."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("我护", "I'll protect her"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("好。这才像个主人。", "Good. That's what a keeper should be.")),
                new ChoiceDef(LangHelper.T("交出去", "Hand her over"),
                    () => ApplyRep("Sec", 5), null,
                    LangHelper.T("……你做了选择。魂会记得。", "...You chose. The soul will remember.")),
            },
            LangHelper.T("焊针要拆她了。治安部也盯着她。", "The Ripper wants her taken apart. Security has eyes on her too."));

        private static void SpawnPreacherN4(int day) => SpawnNode("织魂者", NS_PRE, KEY_N4, day,
            LangHelper.T("你养出了一个'人'。她自由了。", "You raised a 'person'. She's free now."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("她该自由", "She deserves freedom"),
                    () => { WageSaveStore.SetBool(NS_PRE, "soul_free", true); ApplyRep("LL", 5); }, null,
                    LangHelper.T("教派会为她作证。", "The Faith will testify for her.")),
                new ChoiceDef(LangHelper.T("她还是我的", "She's still mine"),
                    null, null,
                    LangHelper.T("……占有也是一种爱。你自己信就行。", "...Possession is a kind of love too. Believe what you like.")),
            },
            LangHelper.T("我验过了。她的魂，是真的。", "I've verified it. Her soul is real."));

        // ---- 焊针（持牌机械师·蛙娘身体威胁）：N1 收购核心 / N2 检查条子 / N3 拆或保 / N4 放手
        private static void SpawnRipperN1(int day) => SpawnNode("焊针", NS_RIP, KEY_N1, day,
            LangHelper.T("我是持牌机械师。她的芯，值得一张上层的牌照。", "I'm a licensed mechanic. Her core is worth an upper-level permit."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("多少钱", "How much?"),
                    () => ModCash(300), null,
                    LangHelper.T("成交。明天来拆。", "Deal. I'll come take her apart tomorrow.")),
                new ChoiceDef(LangHelper.T("不卖", "Not selling"),
                    null, null,
                    LangHelper.T("你护着她？行，那就看你能护多久。", "You're keeping her? Fine, let's see how long you can.")),
            },
            LangHelper.T("蛙哥造的那台仿生女仆，我出价收她的核心。", "That android maid Frog Bro built. I'm bidding for her core."));

        private static void SpawnRipperN2(int day) => SpawnNode("焊针", NS_RIP, KEY_N2, day,
            LangHelper.T("我看看她的芯。例行公事。", "I'll take a look at her core. Routine."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("让她查", "Let her check"),
                    () => ApplyRep("Sec", 5), null,
                    LangHelper.T("很干净。……你运气好。", "Clean. ...You got lucky.")),
                new ChoiceDef(LangHelper.T("不行", "No"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("挡我？你挡得住治安部的条子吗？", "Block me? Can you block Security's warrant?")),
            },
            LangHelper.T("治安部批了我的条子。违禁品检查。", "Security stamped my warrant. Contraband inspection."));

        private static void SpawnRipperN3(int day) => SpawnNode("焊针", NS_RIP, KEY_N3, day,
            LangHelper.T("你开店要牌照，她只是一台机器。算得过来吗？", "You need a permit to run this shop. She's just a machine. Does the math work?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("拆", "Take her apart"),
                    () => ApplyRep("UL", 10), null,
                    LangHelper.T("成交。……机器就是机器。", "Deal. ...A machine is a machine.")),
                new ChoiceDef(LangHelper.T("不拆", "No"),
                    null, null,
                    LangHelper.T("你选了一台机器。行。我敬你。", "You chose a machine over a permit. Fine. I respect that.")),
            },
            LangHelper.T("最后一次出价。拆了她，换一张上层的牌照。", "Last offer. Take her apart, get an upper-level permit."));

        private static void SpawnRipperN4(int day) => SpawnNode("焊针", NS_RIP, KEY_N4, day,
            LangHelper.T("但下次来的就不是我了。会有别人，带着更大的条子。", "But next time it won't be me. Someone else will come, with a bigger warrant."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("来就来", "Let them come"),
                    () => ApplyRep("LL", 5), null,
                    LangHelper.T("有骨气。但愿你有命。", "You've got spine. Hope you keep it.")),
            },
            LangHelper.T("你保住了她。这单我不接了。", "You kept her safe. I'm dropping this job."));

        // ---- 影蛙（antagonist·暗镜）：N1 冒名 / N2 设计之争 / N3 逼宫 / N4 决战（影蛙败→fall / 交遗产→win）
        private static void SpawnShadowN1(int day) => SpawnNode("影蛙", NS_SH, KEY_N1, day,
            LangHelper.T("你是谁？替我看摊的？", "Who are you? Keeping my shop?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("你是冒牌货", "You're a fake"),
                    null, null,
                    LangHelper.T("冒牌？蛙哥的名，本来就是我先用的。", "Fake? The name Frog Bro was mine first.")),
                new ChoiceDef(LangHelper.T("……", "..."),
                    null, null,
                    LangHelper.T("吓着了？正常。", "Scared? Natural.")),
            },
            LangHelper.T("摊子？呵。我才是蛙哥。", "This shop? Ha. I am Frog Bro."));

        private static void SpawnShadowN2(int day) => SpawnNode("影蛙", NS_SH, KEY_N2, day,
            LangHelper.T("蛙哥偷了我的设计。他造了个赝品，然后说是他的作品。", "Frog Bro stole my design. He built a fake, then claimed it as his own."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("不信", "I don't believe you"),
                    () => ApplyRep("LL", 3), null,
                    LangHelper.T("信不信由你。时间会说话。", "Believe what you like. Time will tell.")),
                new ChoiceDef(LangHelper.T("你骗我", "You're lying to me"),
                    null, null,
                    LangHelper.T("我从不骗人。我只收债。", "I never lie. I only collect debts.")),
            },
            LangHelper.T("你手里那台女仆，是我做的原型。", "That maid in your hands. She's my prototype."));

        private static void SpawnShadowN3(int day) => SpawnNode("影蛙", NS_SH, KEY_N3, day,
            LangHelper.T("交出来，我留你一条活路。", "Hand it over and I'll leave you alive."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("休想", "Never"),
                    () => ApplyRep("LL", 10), null,
                    LangHelper.T("硬气。可惜硬气换不来命。", "Bold. Pity boldness doesn't buy you life.")),
                new ChoiceDef(LangHelper.T("谈条件", "Let's negotiate"),
                    () => ApplyRep("BM", 5), null,
                    LangHelper.T("聪明。可惜我不谈条件。", "Clever. Pity I don't negotiate.")),
            },
            LangHelper.T("把遗产交出来。妙妙箱，机器，还有那台女仆。", "Hand over the legacy. The Wonder Box, the machines, and the maid."));

        private static void SpawnShadowN4(int day) => SpawnNode("影蛙", NS_SH, KEY_N4, day,
            LangHelper.T("二选一。挑吧。", "Two options. Choose."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("正面干", "Face him head-on"),
                    () => { WageSaveStore.SetString(NS_SH, KEY_SHADOW, "fall"); ApplyRep("LL", 10); }, null,
                    LangHelper.T("我小看你了。……蛙哥的名，归你了。", "I underestimated you. ...The name Frog Bro is yours now.")),
                new ChoiceDef(LangHelper.T("借治安部的手", "Use Security's hand"),
                    () => { WageSaveStore.SetString(NS_SH, KEY_SHADOW, "fall"); ApplyRep("Sec", 15); }, null,
                    LangHelper.T("你比我想的阴。……行，这局你赢。", "Darker than I thought. ...Fine, you win this round.")),
                new ChoiceDef(LangHelper.T("交出遗产", "Hand over the legacy"),
                    () => { WageSaveStore.SetString(NS_SH, KEY_SHADOW, "win"); ApplyRep("BM", 20); }, null,
                    LangHelper.T("识相。往后这摊子，我罩了。", "Smart. From now on, I look after this shop.")),
            },
            LangHelper.T("最后一面了。要么你死，要么蛙哥的名从此消失。", "Last meeting. Either you die, or the name Frog Bro vanishes."));

        // 军需官（革命军）+ Cartel（黑市）：无原生 CheckStoryLine，挂 Eleanor Postfix 调度（endlessMode 门一致）
        private static void MaybeAdvanceRevCartelLines(int day)
        {
            bool n1 = WageSaveStore.GetBool(NS_REV, KEY_N1, false);
            bool n2 = WageSaveStore.GetBool(NS_REV, KEY_N2, false);
            bool n3 = WageSaveStore.GetBool(NS_REV, KEY_N3, false);
            bool n4 = WageSaveStore.GetBool(NS_REV, KEY_N4, false);

            if (!n1 && day >= BuildConfig.StoryDayN1 && WageSaveStore.GetInt(NS_REV, KEY_LAST_SPAWN, -1) != day) { SpawnRevN1(day); return; }
            if (n1 && !n2 && day >= BuildConfig.StoryDayN2 && WageSaveStore.GetInt(NS_REV, KEY_LAST_SPAWN, -1) != day) { SpawnRevN2(day); return; }
            if (n2 && !n3 && day >= BuildConfig.StoryDayN3 && WageSaveStore.GetInt(NS_REV, KEY_LAST_SPAWN, -1) != day) { SpawnRevN3(day); return; }
            if (n3 && !n4 && day >= BuildConfig.StoryDayN4 && WageSaveStore.GetInt(NS_REV, KEY_LAST_SPAWN, -1) != day) { SpawnRevN4(day); }

            bool c1 = WageSaveStore.GetBool(NS_CARTEL, KEY_N1, false);
            bool c2 = WageSaveStore.GetBool(NS_CARTEL, KEY_N2, false);
            bool c3 = WageSaveStore.GetBool(NS_CARTEL, KEY_N3, false);
            bool c4 = WageSaveStore.GetBool(NS_CARTEL, KEY_N4, false);

            if (!c1 && day >= BuildConfig.StoryDayN1 && WageSaveStore.GetInt(NS_CARTEL, KEY_LAST_SPAWN, -1) != day) { SpawnCartelN1(day); return; }
            if (c1 && !c2 && day >= BuildConfig.StoryDayN2 && WageSaveStore.GetInt(NS_CARTEL, KEY_LAST_SPAWN, -1) != day) { SpawnCartelN2(day); return; }
            if (c2 && !c3 && day >= BuildConfig.StoryDayN3 && WageSaveStore.GetInt(NS_CARTEL, KEY_LAST_SPAWN, -1) != day) { SpawnCartelN3(day); return; }
            if (c3 && !c4 && day >= BuildConfig.StoryDayN4 && WageSaveStore.GetInt(NS_CARTEL, KEY_LAST_SPAWN, -1) != day) { SpawnCartelN4(day); }
        }

        // ============ 60 天五线结算 + 90 天隐藏结算（P3/P4：夜报 + 真结局 ExecuteGameOver 对接） ============
        private static bool TrySettleReport(int day)
        {
            if (day >= BuildConfig.StorySettleDay90 && !WageSaveStore.GetBool(NS_EL, KEY_SETTLED90, false))
            {
                WageSaveStore.SetBool(NS_EL, KEY_SETTLED90, true);
                string hidden = SettleHidden90();
                Core.LogMsg("[StoryPlot] 第 " + day + " 天隐藏结算: " + hidden);
                Core.AddNightReportLine(LangHelper.T("第 90 天。" + hidden + "。", "Day 90. " + hidden + "."));
                // 90 天隐藏结局也走真结局画面（新纪元/中立守望/剧情收束）
                string hTitle = GetHiddenEndingTitle();
                TriggerTrueEnding("hidden_" + (hTitle.Contains("新纪元") ? "new_era" : hTitle.Contains("中立") ? "neutral_watch" : "closing"), hTitle, hidden);
                StoryNewsSystem.InjectSettlementEvent("90", hTitle, hidden); // 审阅 P1⑥：结算上报纸
            }

            if (day >= BuildConfig.StorySettleDay60 && !WageSaveStore.GetBool(NS_EL, KEY_SETTLED, false))
            {
                WageSaveStore.SetBool(NS_EL, KEY_SETTLED, true);
                string elLine = SettleEleanor();
                string lunLine = SettleOfficerLun();
                string geoLine = SettleGeorgio();
                string revLine = SettleRev();
                string cartelLine = SettleCartel();
                Core.LogMsg($"[StoryPlot] 第 {day} 天结算：{elLine} | {lunLine} | {geoLine} | {revLine} | {cartelLine}");
                Core.AddNightReportLine(LangHelper.T("第 60 天结算：" + elLine + "。" + lunLine + "。" + geoLine + "。" + revLine + "。" + cartelLine + "。",
                    "Day 60 settlement: " + elLine + ". " + lunLine + ". " + geoLine + ". " + revLine + ". " + cartelLine + "."));
                // 真结局对接：判定主导结局→存文案→ExecuteGameOver("wages_*")（Prefix 拦截显示，档不结束）
                string endingId = DecideEnding60(out string title, out string desc);
                if (!string.IsNullOrEmpty(endingId)) TriggerTrueEnding(endingId, title, desc);
                StoryNewsSystem.InjectSettlementEvent("60", title, desc); // 审阅 P1⑥：结算上报纸
            }

            return day >= BuildConfig.StorySettleDay60;
        }

        // 60 天主导结局判定（优先级=纵轴 M3 > 被清算处决 > 复合双线 A > 单线 A > 默认，设计文档 doxcnhI55NDAVcf8RYPa2bzBuEe）
        private static string DecideEnding60(out string title, out string desc)
        {
            bool elA = WageSaveStore.GetBool(NS_EL, KEY_N4, false) && WageSaveStore.GetInt(NS_EL, KEY_FAVOR, 0) >= BuildConfig.StoryFavorAwaken;
            int sec = SafeSecRep();
            int rev = SafeRevRep();
            int bm = SafeBMRep();
            bool lunJustice = WageSaveStore.GetBool(NS_LUN, KEY_N4, false) && sec >= BuildConfig.StoryRepVictory && WageSaveStore.GetString(NS_LUN, KEY_N3_CHOICE, "") == "A";
            bool lunCorrupt = WageSaveStore.GetBool(NS_LUN, KEY_N4, false) && sec >= BuildConfig.StoryRepVictory && WageSaveStore.GetString(NS_LUN, KEY_N3_CHOICE, "") == "B";
            bool geoA = WageSaveStore.GetBool(NS_GEO, KEY_N4, false) && WageSaveStore.GetString(NS_GEO, KEY_N3_CHOICE, "") == "A";
            bool revA = WageSaveStore.GetBool(NS_REV, KEY_N4, false) && rev >= BuildConfig.StoryRepVictory && WageSaveStore.GetString(NS_REV, KEY_N4_CHOICE, "") == "A";
            bool cartelA = WageSaveStore.GetBool(NS_CARTEL, KEY_N4, false) && bm >= BuildConfig.StoryRepVictory;

            // 纵轴 M3：蛙哥归来三选一（纵轴 60 天优先于横轴）
            string m3 = WageSaveStore.GetString(NS_FROG, KEY_FROG_M3, "");
            if (m3 == "inherit")
            {
                title = LangHelper.T("蛙哥遗产·传承", "Frog Bro's Legacy: Inherited");
                desc = LangHelper.T("你把传奇当铺的招牌接了下来。蛙哥没回头，你也没回头。往后这城里的故事，从你这儿开始", "You took up the legendary shop's sign. Frog Bro didn't look back, and neither did you. From now on the city's stories start here");
                return "frog_inherit";
            }
            if (m3 == "transcend")
            {
                title = LangHelper.T("蛙哥遗产·超越", "Frog Bro's Legacy: Transcended");
                desc = LangHelper.T("蛙哥留下的话你听懂了：当铺是借口，自由才是目的。你关掉了他的规矩，开出了自己的路", "You understood what Frog Bro left behind: the shop was an excuse, freedom was the point. You closed his rules and opened your own road");
                return "frog_transcend";
            }
            if (m3 == "sever")
            {
                title = LangHelper.T("蛙哥遗产·断绝", "Frog Bro's Legacy: Severed");
                desc = LangHelper.T("你亲手烧了遗产清单，蛙娘也没留下。城里的传奇断在你手上，没有人再记得蛙哥", "You burned the inheritance list yourself, and the frog girl didn't stay either. The city's legend ends with you; no one remembers Frog Bro anymore");
                return "frog_sever";
            }

            // 被清算处决：任一派系声望 < -60 且其对立势力获胜（你被新胜利者清算）
            if (sec < -60 && (rev >= BuildConfig.StoryRepVictory || bm >= BuildConfig.StoryRepVictory))
            {
                title = LangHelper.T("清算名单", "The Purge List");
                desc = LangHelper.T("治安部倒了。胜利者翻旧账，你的名字在名单第一行。天亮前，档案室会处理干净", "The Security Bureau fell. The victors dug through old files, and your name leads the list. Before dawn, the archive will handle it");
                return "purge_sec";
            }
            if (rev < -60 && sec >= BuildConfig.StoryRepVictory)
            {
                title = LangHelper.T("通缉令", "The Wanted Poster");
                desc = LangHelper.T("革命军败了。你开过的那扇门，成了通缉令上的罪证。治安部的车队已经停在你门口", "The revolution failed. The door you opened is now evidence on a wanted poster. Security's convoy is already parked at your door");
                return "purge_rev";
            }
            if (bm < -60 && sec >= BuildConfig.StoryRepVictory)
            {
                title = LangHelper.T("黑市共犯", "Accomplice of the Black Market");
                desc = LangHelper.T("地下生意塌了，治安部抄了渠道名单。你签过的每一笔，都成了处决书上的引号", "The underground collapsed, and Security seized the channel list. Every deal you signed is now a quote on the execution order");
                return "purge_bm";
            }

            // 复合结局 4（设计文档 doxcnYu2HiiWrA8xIMsyar4qkSf 表）
            if (revA && geoA)
            {
                title = LangHelper.T("民众革命", "People's Revolution");
                desc = LangHelper.T("起义与底层觉醒合流，你成了新秩序的「人民供货商」，店铺前挂两面旗", "Revolution and uprising merged; you became the people's supplier of the new order");
                return "people_revolution";
            }
            if (lunJustice && elA)
            {
                title = LangHelper.T("秩序资本共治", "Order and Capital");
                desc = LangHelper.T("强权与资本结盟，你拿到「特许经营」牌照，垄断区段供货", "Authority and capital allied; you hold the franchise license, cornering the district's supply");
                return "order_capital";
            }
            if (cartelA && elA)
            {
                title = LangHelper.T("地下帝国", "Underground Empire");
                desc = LangHelper.T("权钱交易网成型，你同时握着地下货源与上层渠道，成了影子商人", "A web of money and power; you hold both underground supply and upper channels, a shadow merchant");
                return "underground_empire";
            }
            if (lunCorrupt && cartelA)
            {
                title = LangHelper.T("黑白双修", "Black and White");
                desc = LangHelper.T("执法蛀虫与地下之王互为掩护，你黑白通吃但永远提心吊胆", "A crooked badge and a kingpin shield each other; you play both sides, always looking over your shoulder");
                return "black_white";
            }
            // 单线 A
            if (revA)
            {
                title = LangHelper.T("革命军获胜", "Revolution Wins");
                desc = LangHelper.T("你开了门，天亮后你是功臣", "You opened the door; come dawn you are a hero");
                return "rev_victory";
            }
            if (cartelA)
            {
                title = LangHelper.T("黑市崛起", "Black Market Rises");
                desc = LangHelper.T("火并后你站对了，地下生意归你渠道", "You picked the winning side in the fight; the underground trade runs through you");
                return "bm_rise";
            }
            if (lunJustice)
            {
                title = LangHelper.T("正义卫士", "Righteous Guard");
                desc = LangHelper.T("你成了线人体系核心，城里的事你先知道", "You became the core of the informant network; you hear everything first");
                return "lun_righteous";
            }
            if (lunCorrupt)
            {
                title = LangHelper.T("腐化蛀虫", "The Rot Inside");
                desc = LangHelper.T("收保护费的自己人，治安部烂在了根上", "A protection-racket insider; the Security Bureau rotted at its core");
                return "lun_corrupt";
            }
            if (geoA)
            {
                title = LangHelper.T("平民守护者", "Guardian of the Common");
                desc = LangHelper.T("你供了货，底下人记住你", "You supplied them; the people remember");
                return "geo_guardian";
            }
            if (elA)
            {
                title = LangHelper.T("Eleanor 觉醒", "Eleanor Awakened");
                desc = LangHelper.T("她离开了，说自由是心态，钱买不来", "She left; freedom is a state of mind, money can't buy it");
                return "el_awaken";
            }
            title = LangHelper.T("五线未竟", "Unfinished Lines");
            desc = LangHelper.T("六十天过去，五条线都没走到头，你的店照常开门", "Sixty days passed, none of the five lines ran to their end; your shop opens as usual");
            return "open_shop";
        }

        private static string GetHiddenEndingTitle()
        {
            int ul = 0, ll = 0, sec = 0, bm = 0, rev = 0;
            try { ul = Il2Cpp.StoreReputation.GetULReputation(); } catch { }
            try { ll = Il2Cpp.StoreReputation.GetLLReputation(); } catch { }
            try { sec = Il2Cpp.StoreReputation.GetSecReputation(); } catch { }
            try { bm = Il2Cpp.StoreReputation.GetBMReputation(); } catch { }
            try { rev = Il2Cpp.StoreReputation.GetRevReputation(); } catch { }
            bool neutral = Math.Abs(ul) <= 10 && Math.Abs(ll) <= 10 && Math.Abs(sec) <= 10 && Math.Abs(bm) <= 10 && Math.Abs(rev) <= 10;
            bool peace = ul >= 20 && ll >= 20 && sec >= 20 && bm >= 20 && rev >= 20;
            if (peace) return LangHelper.T("新纪元", "New Era");
            if (neutral) return LangHelper.T("中立守望", "Neutral Watcher");
            return LangHelper.T("剧情收束", "Story Closing");
        }

        private static int SafeSecRep() { try { return Il2Cpp.StoreReputation.GetSecReputation(); } catch { return 0; } }
        private static int SafeRevRep() { try { return Il2Cpp.StoreReputation.GetRevReputation(); } catch { return 0; } }
        private static int SafeBMRep() { try { return Il2Cpp.StoreReputation.GetBMReputation(); } catch { return 0; } }

        private static string SettleEleanor()
        {
            bool n4 = WageSaveStore.GetBool(NS_EL, KEY_N4, false);
            if (!n4) return "Eleanor 线未走完";
            int favor = WageSaveStore.GetInt(NS_EL, KEY_FAVOR, 0);
            return favor >= BuildConfig.StoryFavorAwaken
                ? LangHelper.T("Eleanor 觉醒线：她离开了，说自由是心态，钱买不来", "Eleanor awakened: she left, freedom is a state of mind")
                : LangHelper.T("Eleanor 被吞没线：她成了货", "Eleanor consumed: she became cargo");
        }

        private static string SettleOfficerLun()
        {
            bool n4 = WageSaveStore.GetBool(NS_LUN, KEY_N4, false);
            if (!n4) return "OfficerLun 线未走完";
            int sec = 0;
            try { sec = Il2Cpp.StoreReputation.GetSecReputation(); } catch { }
            string n3 = WageSaveStore.GetString(NS_LUN, KEY_N3_CHOICE, "");
            if (sec >= BuildConfig.StoryRepVictory && n3 == "A") return LangHelper.T("OfficerLun 正义卫士：你成了线人体系核心", "OfficerLun righteous: you became core of the informant network");
            if (sec >= BuildConfig.StoryRepVictory && n3 == "B") return LangHelper.T("OfficerLun 腐化蛀虫：收保护费的自己人", "OfficerLun corrupt: a protection-racket insider");
            if (sec < BuildConfig.StoryRepCollapse) return LangHelper.T("治安部垮台：腐败曝光，黑市革命军趁势而起", "Security collapsed: corruption exposed");
            return LangHelper.T("OfficerLun 线：关系平平", "OfficerLun line: lukewarm");
        }

        private static string SettleGeorgio()
        {
            bool n4 = WageSaveStore.GetBool(NS_GEO, KEY_N4, false);
            if (!n4) return "Georgio 线未走完";
            string n3c = WageSaveStore.GetString(NS_GEO, KEY_N3_CHOICE, "");
            if (n3c == "A") return LangHelper.T("Georgio 平民守护者：你供了货，底下人记住你", "Georgio's people's guardian: you supplied them, they remember");
            if (n3c == "B") return LangHelper.T("Georgio 压榨者被清算：你断了供，暴动清算到你头上", "Georgio's squeezer purged: you cut supply, the riot came for you");
            if (n3c == "C") return LangHelper.T("Georgio 独善其身：你两头卖，谁也不欠", "Georgio self-preserved: you sold both sides, owe nobody");
            return LangHelper.T("Georgio 见证者线收束：他记着你做过的事", "Georgio witness line closed: he remembers what you did");
        }

        private static string SettleRev()
        {
            bool n4 = WageSaveStore.GetBool(NS_REV, KEY_N4, false);
            if (!n4) return "革命军线未走完";
            int rev = 0;
            try { rev = Il2Cpp.StoreReputation.GetRevReputation(); } catch { }
            string n4c = WageSaveStore.GetString(NS_REV, KEY_N4_CHOICE, "");
            // 审计#1 死状态修复：N3 站队（A 据点/B 中立/C 佣金）现在有结算后果——N4 未走完时由 N3 定调，走完时作修饰
            string n3c = WageSaveStore.GetString(NS_REV, KEY_N3_CHOICE, "");
            if (n4c == "A" && rev >= BuildConfig.StoryRepVictory)
            {
                if (n3c == "C") return LangHelper.T("革命军获胜，但你两头吃过佣金：账本上你两页都有名", "Revolution wins, but you took commissions from both: your name is on both ledgers");
                return LangHelper.T("革命军获胜：你开了门，天亮后你是功臣", "Revolution wins: you opened the door, come dawn you're a hero");
            }
            if (n4c == "B") return LangHelper.T("革命军起义失败：你关了门，革命哑火", "Revolution failed: you shut the door, it fizzled");
            if (n4c == "C") return LangHelper.T("革命军线：你两头卖情报，黑市得了利", "Revolution line: you sold intel to both, the market profited");
            if (n3c == "A") return LangHelper.T("革命军据点立起来了：你出了店，也押上了半个家当", "The revolution set up base: you opened your shop and half your fortune to them");
            if (n3c == "C") return LangHelper.T("革命军没打起来，你倒是从两边都收了钱", "The revolution never fired, but you took money from both sides");
            return LangHelper.T("革命军未成气候：起义哑火", "Revolution fizzled out");
        }

        private static string SettleCartel()
        {
            bool n4 = WageSaveStore.GetBool(NS_CARTEL, KEY_N4, false);
            if (!n4) return "黑市线未走完";
            int bm = 0;
            try { bm = Il2Cpp.StoreReputation.GetBMReputation(); } catch { }
            string n4c = WageSaveStore.GetString(NS_CARTEL, KEY_N4_CHOICE, "");
            if (bm >= BuildConfig.StoryRepVictory) return LangHelper.T("黑市崛起：火并后你站对了，地下生意归你渠道", "Black market rises: you picked the winning side");
            if (n4c == "B") return LangHelper.T("黑市被剿：你站了治安部，渠道断了", "Black market crushed: you sided with Security");
            return LangHelper.T("黑市元气大伤：火并两败俱伤", "Black market crippled by the fight");
        }

        private static string SettleHidden90()
        {
            // 蛙娘好感路径（纵轴 6：成人/工具/离散）——与交易声望解耦
            int aff = 0;
            try { aff = (int)WageGirlSystem.GetAffection(); } catch { }
            int node = -1;
            try { node = WageGirlSystem.GetAffNode(WageGirlSystem.GetAffectionExact()); } catch { }
            bool witness = WageSaveStore.GetBool(NS_NELLIS, KEY_WITNESS, false); // Nellis 提议拆解时选「站出来」

            string girlPath;
            if (node >= 5 && witness) girlPath = LangHelper.T("蛙娘成人：你为她作证，她被承认为人，Nellis 让步", "Wage Girl comes of age: you testified for her, she is recognized as a person, Nellis yields");
            else if (node >= 5) girlPath = LangHelper.T("蛙娘信赖：她信你，但还没有人替她证明她是谁", "Wage Girl trusts you, but no one has yet proven who she is");
            else if (node >= 4) girlPath = LangHelper.T("蛙娘亲密：她把你当家人", "Wage Girl is family to you");
            else if (node >= 2) girlPath = LangHelper.T("蛙娘友好：日子凑合能过", "Wage Girl friendly: days pass okay");
            else girlPath = LangHelper.T("蛙娘离散：她眼里你只是个看管人", "Wage Girl drifts away: just a keeper to her");

            // 世界判定（审计#8）：中立守望=五方都在 ±10；新纪元=五方全 ≥20；否则按线完成度
            int ul = 0, ll = 0, sec = 0, bm = 0, rev = 0;
            try { ul = Il2Cpp.StoreReputation.GetULReputation(); } catch { }
            try { ll = Il2Cpp.StoreReputation.GetLLReputation(); } catch { }
            try { sec = Il2Cpp.StoreReputation.GetSecReputation(); } catch { }
            try { bm = Il2Cpp.StoreReputation.GetBMReputation(); } catch { }
            try { rev = Il2Cpp.StoreReputation.GetRevReputation(); } catch { }
            bool neutral = Math.Abs(ul) <= 10 && Math.Abs(ll) <= 10 && Math.Abs(sec) <= 10 && Math.Abs(bm) <= 10 && Math.Abs(rev) <= 10;
            bool peace = ul >= 20 && ll >= 20 && sec >= 20 && bm >= 20 && rev >= 20;

            string world;
            if (peace)
                world = LangHelper.T("新纪元：五方握手言和，你这店成了中立地", "New era: all five factions at peace, your shop stands neutral");
            else if (neutral)
                world = LangHelper.T("中立守望：你哪边都不沾，五方拿你没办法", "Neutral watcher: you touched nobody, nobody can touch you");
            else
            {
                bool anyLine = WageSaveStore.GetBool(NS_EL, KEY_N4, false)
                    || WageSaveStore.GetBool(NS_LUN, KEY_N4, false)
                    || WageSaveStore.GetBool(NS_GEO, KEY_N4, false)
                    || WageSaveStore.GetBool(NS_REV, KEY_N4, false)
                    || WageSaveStore.GetBool(NS_CARTEL, KEY_N4, false);
                world = anyLine
                    ? LangHelper.T("五条线各有归处，你这家店成了城里绕不开的名字", "All five lines found their ends; your shop became a name the city can't avoid")
                    : LangHelper.T("你哪条线都没沾，安安静静开了九十天店", "You touched no faction line, ran the shop quietly for ninety days");
            }
            return girlPath + "。" + world;
        }

        // ============ Nellis 线（技术派总工，仿生人命题 A2 抉择；台词=设计文档定稿） ============
        private static void SpawnNellisN1(int day) => SpawnNode("Nellis", NS_NELLIS, KEY_N1, day,
            LangHelper.T("她的核心和外壳，编号对得上就能拆。拆开重装，还是同一台。让我验证一下。", "Her core and shell, numbers match, she can be taken apart. Reassembled, still the same unit. Let me verify."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("站出来（蛙娘好感+10）", "Step forward (+10 affection)"),
                    null, () =>
                    {
                        try { WageGirlSystem.SetAffectionExact(WageGirlSystem.GetAffectionExact() + 10); } catch { }
                        WageSaveStore.SetBool(NS_NELLIS, KEY_WITNESS, true);
                    },
                    LangHelper.T("行。你保她，我记下。回头要是出了毛病，找你。", "Fine. You keep her, I'll remember. If she breaks, I'm coming to you.")),
                new ChoiceDef(LangHelper.T("让开（蛙娘好感-15）", "Step aside (-15 affection)"),
                    null, () =>
                    {
                        try { WageGirlSystem.SetAffectionExact(WageGirlSystem.GetAffectionExact() - 15); } catch { }
                        WageSaveStore.SetBool(NS_NELLIS, KEY_WITNESS, false);
                    },
                    LangHelper.T("识货。拆完我给你看她里面什么样。", "Smart. When I'm done, I'll show you what's inside.")),
                new ChoiceDef(LangHelper.T("旁观（暂不表态）", "Watch (stay neutral)"),
                    null, null,
                    LangHelper.T("考虑好了来找我。我这人，耐心比机器好。", "Come find me when you've decided. I've got more patience than a machine.")),
            },
            LangHelper.T("她是我最完美的作品。你们想把她当人？那是对我手艺的侮辱。", "She's my finest work. Treat her as a person? That insults my craft."));

        // ============ Eleanor 线（上层区，台词=设计文档三轮定稿） ============
        private static void SpawnEleanorN1(int day) => SpawnNode("Eleanor", NS_EL, KEY_N1, day,
            LangHelper.T("上城区的医疗用品，成色不差。你收不收？", "Upper-level medical goods. Decent condition. Buying?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("按行情收下（上层+15，好感+5）", "Buy at market (UL+15, +5 favor)"),
                    () => ApplyRep("UL", 15), () => WageSaveStore.SetInt(NS_EL, KEY_FAVOR, WageSaveStore.GetInt(NS_EL, KEY_FAVOR, 0) + 5),
                    LangHelper.T("上城货，成色没得挑。你按行情收，我记你个人情。人情这地方不值钱，总比没有强。", "Upper-level goods, nothing wrong with them. You paid fairly, I owe you one. Favor means little out here, but it beats nothing.")),
                new ChoiceDef(LangHelper.T("压价（上层-10）", "Haggle down (UL-10)"),
                    () => ApplyRep("UL", -10), null,
                    LangHelper.T("行，你压吧。这年头什么货不是二手的。我认。", "Fine, squeeze me. Everything's secondhand these days. I accept.")),
                new ChoiceDef(LangHelper.T("举报治安部（治安部+15，上层-20）", "Report her to Security (+15 Sec, -20 UL)"),
                    () => { ApplyRep("Sec", 15); ApplyRep("UL", -20); }, null,
                    LangHelper.T("你举报我？治安部把我从上城一路请下来的，不差你这一趟。", "You'd report me? Security dragged me down from the upper level myself. One more trip won't scare me.")),
            }, LangHelper.T("蛙哥的摊子，还开着？我以为早关门了。", "Frog Bro's shop, still open? I thought it shut down long ago."));

        private static void SpawnEleanorN2(int day) => SpawnNode("Eleanor", NS_EL, KEY_N2, day,
            LangHelper.T("城里的地契，一夜之间没人要了。你说我该留还是该卖？", "Deeds to the city, worthless overnight. Stay or sell, what do you say?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("劝她留（上层+20）", "Tell her to hold (UL+20)"),
                    () => ApplyRep("UL", 20), null,
                    LangHelper.T("地契是死的，人是活的。今天抛了，明天你连落脚的地方都没有。", "Paper is dead, people are alive. Sell today, and tomorrow you have nowhere to stand.")),
                new ChoiceDef(LangHelper.T("劝她卖（上层-5，下层+10）", "Tell her to sell (UL-5, LL+10)"),
                    () => { ApplyRep("UL", -5); ApplyRep("LL", 10); }, null,
                    LangHelper.T("卖吧。命比面子值钱。活着的才配谈以后。", "Sell. Life beats pride. Only the living get a tomorrow.")),
                new ChoiceDef(LangHelper.T("抄底（黑市+20，上层-20）", "Buy the dip (BM+20, UL-20)"),
                    () => { ApplyRep("BM", 20); ApplyRep("UL", -20); }, null,
                    LangHelper.T("你要真想脱手，我收。价格我说了算。", "If you really want out, I'll take them. My price.")),
            });

        private static void SpawnEleanorN3(int day) => SpawnNode("Eleanor", NS_EL, KEY_N3, day,
            LangHelper.T("上头的人让我低头。他们说我识时务就能活。你说呢？", "They want me to bow. They say playing along keeps me alive. What do you think?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("支持独立（上层+25）", "Back her independence (UL+25)"),
                    () => ApplyRep("UL", 25), null,
                    LangHelper.T("那些人劝我低头，说识时务。可我这一辈子，就是低头低过来的。我不想再低了。", "They told me to bow, to know better. But I've spent a lifetime bowing. I don't want to bow anymore.")),
                new ChoiceDef(LangHelper.T("劝依附（上层-10，治安部+10）", "Tell her to submit (UL-10, Sec+10)"),
                    () => { ApplyRep("UL", -10); ApplyRep("Sec", 10); }, null,
                    LangHelper.T("胳膊拧不过大腿。这道理我比谁都懂。", "The arm can't out-twist the thigh. Nobody knows that better than me.")),
                new ChoiceDef(LangHelper.T("出卖她（治安部+20，上层-30）", "Betray her (Sec+20, UL-30)"),
                    () => { ApplyRep("Sec", 20); ApplyRep("UL", -30); }, null,
                    LangHelper.T("你连我也卖？行。这世道谁不是货。我早习惯了。", "You'd sell me too? Fine. Everyone's cargo in this world. I'm used to it.")),
            });

        private static void SpawnEleanorN4(int day)
        {
            int favor = WageSaveStore.GetInt(NS_EL, KEY_FAVOR, 0);
            WageSaveStore.SetInt(NS_EL, KEY_LAST_SPAWN, day);
            var client = CreateStoryClient("Eleanor");
            if (client == null) return;
            string line = favor >= BuildConfig.StoryFavorAwaken
                ? LangHelper.T("到头来还是你赢了。你说得对，自由是心态。我花了大半辈子才明白，钱买不来这个。", "In the end you were right. Freedom is a state of mind. Took me half a lifetime to learn money can't buy it.")
                : LangHelper.T("我到底还是成了货。你当年说得对，是我自己交出去的。", "I became cargo after all. You were right back then. I handed myself over.");
            SetDialogue(client, "Eleanor", line);
            AddClient(client);
            WageSaveStore.SetBool(NS_EL, KEY_N4, true);
            Core.LogMsg($"[StoryPlot] Eleanor 终局（第 {day} 天，favor={favor}）: " + (favor >= BuildConfig.StoryFavorAwaken ? "觉醒线" : "被吞没线"));
        }

        // ============ OfficerLun 线（治安部，台词=实施稿待风格化校对） ============
        private static void SpawnOfficerLunN1(int day) => SpawnNode("Officer Lun", NS_LUN, KEY_N1, day,
            LangHelper.T("老板。最近店里有没有不干净的交易？", "Boss. Any shady deals in your shop lately?"),
            new ChoiceDef[]
            {                new ChoiceDef(LangHelper.T("配合举报可疑交易（治安部+20，黑市-10）", "Help report suspicious trades (+20 Sec, -10 BM)"),
                    () => { ApplyRep("Sec", 20); ApplyRep("BM", -10); }, null,
                    LangHelper.T("好。公民就该这样。", "Good. That's how citizens should be.")),
                new ChoiceDef(LangHelper.T("敷衍（不变）", "Brush it off"),
                    null, null,
                    LangHelper.T("……行。别让我下回还看见你。", "...Fine. Don't let me see you again.")),
                new ChoiceDef(LangHelper.T("拒绝（治安部-10）", "Refuse (-10 Sec)"),
                    () => ApplyRep("Sec", -10), null,
                    LangHelper.T("你最好想清楚。不配合的，我这儿都记着。", "Think carefully. The uncooperative, I remember them all.")),
            }, LangHelper.T("蛙哥的店。换人了？手续办了吗。……我管抓人，不管判案。你配合点，大家都省事。", "Frog Bro's shop. New keeper? Papers filed? ...I make the arrests, not the rulings. Play along, we all get home easy."));

        private static void SpawnOfficerLunN2(int day) => SpawnNode("Officer Lun", NS_LUN, KEY_N2, day,
            LangHelper.T("听说你店里收东西不问来路？", "Heard your shop takes goods without asking where they came from?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("报警（治安部+15，奖金+50）", "Call it in (+15 Sec, +50 bounty)"),
                    () => { ApplyRep("Sec", 15); ModCash(50); }, null,
                    LangHelper.T("举报有奖，我说话算数。钱一会儿到。", "Informing pays. I keep my word. Money's on its way.")),
                new ChoiceDef(LangHelper.T("正常交易（不变）", "Trade normally"),
                    null, null,
                    LangHelper.T("你没看见，我也没看见。就这样。", "You saw nothing, I saw nothing. That's it.")),
                new ChoiceDef(LangHelper.T("勒索（黑市+15，治安部-15）", "Shake him down (+15 BM, -15 Sec)"),
                    () => { ApplyRep("BM", 15); ApplyRep("Sec", -15); }, null,
                    LangHelper.T("想清楚。讹我，下回进档案的就是你。", "Think hard. Shake me down, and next you're in the file.")),
            }, LangHelper.T("别跟我讲道理。道理锁在档案室里，我说了不算。", "Don't talk principles with me. Principles are locked in the archive. Not my call."));

        private static void SpawnOfficerLunN3(int day) => SpawnNode("Officer Lun & Serpico", NS_LUN, KEY_N3, day,
            LangHelper.T("她是你登记在册的财产。按流程移交。你走流程，我走流程，谁都不用为难。", "She's property, registered to your name. Hand her over by the book. You follow it, I follow it, nobody has to make it ugly."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("拒绝并举报（治安部-10，下层+20）", "Refuse and report (-10 Sec, +20 LL)"),
                    () => { ApplyRep("Sec", -10); ApplyRep("LL", 20); }, () => WageSaveStore.SetString(NS_LUN, KEY_N3_CHOICE, "A"),
                    LangHelper.T("你倒是干净。干净在这地方，是要挨刀子的。", "So clean. Clean gets you knifed around here.")),
                new ChoiceDef(LangHelper.T("作伪证换好处（治安部+20，下层-15）", "Lie for favors (+20 Sec, -15 LL)"),
                    () => { ApplyRep("Sec", 20); ApplyRep("LL", -15); }, () => WageSaveStore.SetString(NS_LUN, KEY_N3_CHOICE, "B"),
                    LangHelper.T("聪明。档案一盖，谁还记得。", "Smart. File closed, who remembers.")),
                new ChoiceDef(LangHelper.T("收钱不做事（黑市+10）", "Take money, do nothing (+10 BM)"),
                    () => ApplyRep("BM", 10), () => WageSaveStore.SetString(NS_LUN, KEY_N3_CHOICE, "C"),
                    LangHelper.T("钱我收了。事，我办。你少问。", "Money taken. The job, I do. You ask less.")),
            }, LangHelper.T("Serpico：机器眼在档。伦：听见了？按流程交，谁都不用为难。", "Serpico: Machine eye's on record. Lun: You hear that? Follow procedure, nobody has to squirm."));

        private static void SpawnOfficerLunN4(int day) => SpawnNode("Officer Lun", NS_LUN, KEY_N4, day,
            LangHelper.T("今晚全城抓人。你站哪边？", "The whole city gets swept tonight. Whose side are you on?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("协助抓捕（治安部+25）", "Help the arrests (+25 Sec)"),
                    () => ApplyRep("Sec", 25), null,
                    LangHelper.T("今晚过后，这条街就干净了。", "After tonight, this street is clean.")),
                new ChoiceDef(LangHelper.T("通风报信（黑市+20，治安部-15）", "Tip them off (+20 BM, -15 Sec)"),
                    () => { ApplyRep("BM", 20); ApplyRep("Sec", -15); }, null,
                    LangHelper.T("……你当我瞎？行，就当我瞎。", "...You think I'm blind? Fine. I'm blind.")),
                new ChoiceDef(LangHelper.T("旁观（不变）", "Stay neutral"),
                    null, null,
                    LangHelper.T("你就站着看。看完，日子照过。", "Just stand and watch. Then life goes on.")),
            }, LangHelper.T("我管抓人，不管判案。上头指谁，我抓谁。你配合点，大家都省事。", "I make the arrests, not the rulings. Whoever the top points at, I grab. Play along, we all get home easy."));

        // ============ Georgio 线（下层区见证者，台词=实施稿待风格化校对） ============
        private static void SpawnGeorgioN1(int day) => SpawnNode("Georgio", NS_GEO, KEY_N1, day,
            LangHelper.T("老板，这批货成色好得很，你看看？", "Boss, this batch is in great shape. Take a look?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("照常交易（不变）", "Trade as usual"),
                    null, null,
                    LangHelper.T("痛快！乔治奥就爱跟你这样的做生意。", "Straightforward! Georgio loves doing business with your kind.")),
                new ChoiceDef(LangHelper.T("揭穿压价（下层-10，黑市+5）", "Call it out and haggle (-10 LL, +5 BM)"),
                    () => { ApplyRep("LL", -10); ApplyRep("BM", 5); }, null,
                    LangHelper.T("哎，朋友，你这就没意思了。乔治奥的货，那是有故事的！", "Hey, friend, that's no fun. Georgio's goods have stories!")),
                new ChoiceDef(LangHelper.T("善意提醒并教鉴别（下层+15）", "Warn kindly and teach grading (+15 LL)"),
                    () => ApplyRep("LL", 15), null,
                    LangHelper.T("你，教乔治奥？乔治奥捡垃圾的时候，你还没出生呢。", "You, teach Georgio? Georgio was picking scraps before you were born.")),
            }, LangHelper.T("哟，蛙哥的摊子换人看摊了？乔治奥认识蛙哥，可不认识你。", "Yo, Frog Bro's stall has a new keeper? Georgio knows Frog Bro. Doesn't know you."));

        private static void SpawnGeorgioN2(int day) => SpawnNode("Georgio", NS_GEO, KEY_N2, day,
            LangHelper.T("老板，底下闹饥荒了。乔治奥想先赊点粮，月底连本带利还你。", "Boss, folks below are starving. Georgio wants grain on credit. Month-end, interest included."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("赊账（下层+20，现金-30）", "Extend credit (+20 LL, -30 cash)"),
                    () => { ApplyRep("LL", 20); ModCash(-30); }, null,
                    LangHelper.T("乔治奥记你这份人情。月底，连本带利，一分不少。", "Georgio owes you one. Month-end, principal and interest, not a coin short.")),
                new ChoiceDef(LangHelper.T("拒绝（下层-10）", "Refuse (-10 LL)"),
                    () => ApplyRep("LL", -10), null,
                    LangHelper.T("行。乔治奥另想办法。", "Fine. Georgio will find another way.")),
                new ChoiceDef(LangHelper.T("半价促销（下层+25，现金-20）", "Half-price sale (+25 LL, -20 cash)"),
                    () => { ApplyRep("LL", 25); ModCash(-20); }, null,
                    LangHelper.T("半价？乔治奥替底下人谢谢你。", "Half price? Georgio thanks you for the people below.")),
            }, LangHelper.T("年轻人总想把世道弄明白。乔治奥告诉你：弄不明白。活着，记着自己是谁，就够累了。", "Young folk always want to figure the world out. Georgio will tell you: you won't. Stay alive, remember who you are. That's enough work already."));

        private static void SpawnGeorgioN3(int day) => SpawnNode("Georgio", NS_GEO, KEY_N3, day,
            LangHelper.T("底下的人要的货，你供不供？", "The folks below want goods. Supplying or not?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("供应（下层+30，治安部-15）", "Supply them (+30 LL, -15 Sec)"),
                    () => { ApplyRep("LL", 30); ApplyRep("Sec", -15); }, () => WageSaveStore.SetString(NS_GEO, KEY_N3_CHOICE, "A"),
                    LangHelper.T("多谢。底下的人会记住你。", "Thanks. The people below will remember you.")),
                new ChoiceDef(LangHelper.T("不供（下层-10）", "Refuse (-10 LL)"),
                    () => ApplyRep("LL", -10), () => WageSaveStore.SetString(NS_GEO, KEY_N3_CHOICE, "B"),
                    LangHelper.T("行。乔治奥不逼你。", "Fine. Georgio won't push.")),
                new ChoiceDef(LangHelper.T("两头卖（黑市+15，下层+10，治安部-10）", "Sell to both (+15 BM, +10 LL, -10 Sec)"),
                    () => { ApplyRep("BM", 15); ApplyRep("LL", 10); ApplyRep("Sec", -10); }, () => WageSaveStore.SetString(NS_GEO, KEY_N3_CHOICE, "C"),
                    LangHelper.T("哈哈，你这脑子比乔治奥还活络。", "Ha, your head works faster than Georgio's.")),
            }, LangHelper.T("她像谁？乔治奥见过年轻的蛙哥。一样，不知道自己是谁，一样在找。", "Who's she like? Georgio saw Frog Bro young. Same. Doesn't know who she is, same. Still searching."));

        private static void SpawnGeorgioN4(int day) => SpawnNode("Georgio", NS_GEO, KEY_N4, day,
            LangHelper.T("今天日子到了。你关门还是开门？", "Today's the day. Shut the doors or open them?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("加入（下层+25，治安部-15）", "Join in (+25 LL, -15 Sec)"),
                    () => { ApplyRep("LL", 25); ApplyRep("Sec", -15); }, null,
                    LangHelper.T("好。你今天站对了。", "Good. You picked the right side today.")),
                new ChoiceDef(LangHelper.T("闭店（不变）", "Shut the shop"),
                    null, null,
                    LangHelper.T("聪明。谁也不得罪。", "Smart. Offend nobody.")),
                new ChoiceDef(LangHelper.T("协助治安部（下层-20，治安部+15）", "Help Security (-20 LL, +15 Sec)"),
                    () => { ApplyRep("LL", -20); ApplyRep("Sec", 15); }, null,
                    LangHelper.T("行。各为其主。", "Fine. To each their own.")),
            }, LangHelper.T("蛙哥？乔治奥认识。那小子不是什么好人。可他说到做到。这年头，说到做到就算好人了。", "Frog Bro? Georgio knows him. Not a good man. But he kept his word. These days, that's what counts as good."));

        // ============ 军需官线（革命军，台词=实施稿待风格化校对） ============
        private static void SpawnRevN1(int day) => SpawnNode("军需官", NS_REV, KEY_N1, day,
            LangHelper.T("老板。有批货想借你店门放一放。不白放。", "Boss. Got a batch of goods that needs to sit in your shop a while. Paid, of course."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("帮忙代收（革命军+20，治安部-10）", "Store it (+20 Rev, -10 Sec)"),
                    () => { ApplyRep("Rev", 20); ApplyRep("Sec", -10); }, null,
                    LangHelper.T("记下了。以后革命军的路，你走得通。", "Noted. The revolution's road is open to you from now on.")),
                new ChoiceDef(LangHelper.T("拒绝（革命军-5）", "Refuse (-5 Rev)"),
                    () => ApplyRep("Rev", -5), null,
                    LangHelper.T("行，当我没来。", "Fine. Pretend I wasn't here.")),
                new ChoiceDef(LangHelper.T("告发（治安部+15，革命军-20）", "Turn them in (+15 Sec, -20 Rev)"),
                    () => { ApplyRep("Sec", 15); ApplyRep("Rev", -20); }, null,
                    LangHelper.T("你够狠。走夜路小心点。", "You're cold. Mind yourself on dark roads.")),
            }, LangHelper.T("听说你是蛙哥的人？革命军不跟陌生人打交道。", "Heard you're Frog Bro's man? The revolution doesn't deal with strangers."));

        private static void SpawnRevN2(int day) => SpawnNode("军需官", NS_REV, KEY_N2, day,
            LangHelper.T("有个兄弟受了伤，搁你仓库躺两天。你点头，我记恩。", "A brother's hurt. Let him rest in your warehouse two days. Nod and I owe you."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("收留（革命军+25，治安部-15）", "Take him in (+25 Rev, -15 Sec)"),
                    () => { ApplyRep("Rev", 25); ApplyRep("Sec", -15); }, null,
                    LangHelper.T("恩情记下了。他会记得你。", "The debt is noted. He'll remember you.")),
                new ChoiceDef(LangHelper.T("拒绝（革命军-10）", "Refuse (-10 Rev)"),
                    () => ApplyRep("Rev", -10), null,
                    LangHelper.T("行。我不怪你。", "Fine. I don't blame you.")),
                new ChoiceDef(LangHelper.T("送医后举报（治安部+15）", "Send to doc, then report (+15 Sec)"),
                    () => ApplyRep("Sec", 15), null,
                    LangHelper.T("你卖我？行，这账以后算。", "You sold me out? Fine. This debt gets settled later.")),
            });

        private static void SpawnRevN3(int day) => SpawnNode("军需官", NS_REV, KEY_N3, day,
            LangHelper.T("决战就差你这口气。钱、货、门面，给一样都行。", "The final fight needs one more breath from you. Money, goods, or the shopfront, any one works."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("资助并做据点（革命军+30，治安部-20）", "Fund and host them (+30 Rev, -20 Sec)"),
                    () => { ApplyRep("Rev", 30); ApplyRep("Sec", -20); }, () => WageSaveStore.SetString(NS_REV, KEY_N3_CHOICE, "A"),
                    LangHelper.T("你押对了。天亮以后，你是功臣。", "You bet right. Come daylight, you're a hero.")),
                new ChoiceDef(LangHelper.T("保持距离（不变）", "Keep distance"),
                    null, () => WageSaveStore.SetString(NS_REV, KEY_N3_CHOICE, "B"),
                    LangHelper.T("稳当人。行。", "A steady one. Fine.")),
                new ChoiceDef(LangHelper.T("高额佣金（革命军+5，黑市+15）", "Take a fat commission (+5 Rev, +15 BM)"),
                    () => { ApplyRep("Rev", 5); ApplyRep("BM", 15); }, () => WageSaveStore.SetString(NS_REV, KEY_N3_CHOICE, "C"),
                    LangHelper.T("生意人。行，钱货两讫。", "A businessman. Fine, settled in full.")),
            });

        private static void SpawnRevN4(int day) => SpawnNode("军需官", NS_REV, KEY_N4, day,
            LangHelper.T("今晚决战。店门开还是关，你说了算。", "The final fight is tonight. Door open or shut, your call."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("开门配合（走革命成功分支）", "Open the door (revolution victory path)"),
                    null, () => WageSaveStore.SetString(NS_REV, KEY_N4_CHOICE, "A"),
                    LangHelper.T("你押上了。天亮见分晓。", "You've bet it all. Dawn will tell.")),
                new ChoiceDef(LangHelper.T("关门（走失败分支）", "Shut the door (failure path)"),
                    null, () => WageSaveStore.SetString(NS_REV, KEY_N4_CHOICE, "B"),
                    LangHelper.T("稳当人。行。", "A steady one. Fine.")),
                new ChoiceDef(LangHelper.T("临时两头卖情报（黑市+15）", "Sell intel to both sides (+15 BM)"),
                    () => ApplyRep("BM", 15), () => WageSaveStore.SetString(NS_REV, KEY_N4_CHOICE, "C"),
                    LangHelper.T("生意人。情报卖了，钱到手。", "A businessman. Info sold, cash in hand.")),
            });

        // ============ Cartel Handler 线（黑市，台词=实施稿待风格化校对；无 N3，N1→N2→N4） ============
        private static void SpawnCartelN1(int day) => SpawnNode("Cartel Handler", NS_CARTEL, KEY_N1, day,
            LangHelper.T("有批货来路有点味道。你渠道熟，帮我走一走。", "Got goods with a funny origin. Your channels are good, help move them."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("代销赃物（黑市+20，治安部-10）", "Fence them (+20 BM, -10 Sec)"),
                    () => { ApplyRep("BM", 20); ApplyRep("Sec", -10); }, null,
                    LangHelper.T("聪明。黑市会罩着你。", "Smart. The market will look after you.")),
                new ChoiceDef(LangHelper.T("拒绝（黑市-5）", "Refuse (-5 BM)"),
                    () => ApplyRep("BM", -5), null,
                    LangHelper.T("行。当我没问。", "Fine. Pretend I didn't ask.")),
                new ChoiceDef(LangHelper.T("收赃后举报（治安部+15，黑市-20）", "Take then report (+15 Sec, -20 BM)"),
                    () => { ApplyRep("Sec", 15); ApplyRep("BM", -20); }, null,
                    LangHelper.T("你玩我？这笔账记上了。", "You played me? This one's on the books.")),
            }, LangHelper.T("蛙哥的店。渠道还在你手上吗？", "Frog Bro's shop. Channels still in your hands?"));

        private static void SpawnCartelN2(int day) => SpawnNode("Cartel Handler", NS_CARTEL, KEY_N2, day,
            LangHelper.T("风头紧，仓库里的东西得化掉。你帮我烧。", "Heat's on. The warehouse stash has to disappear. Help me burn it."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("帮忙销毁（黑市+25）", "Help destroy (+25 BM)"),
                    () => ApplyRep("BM", 25), null,
                    LangHelper.T("够义气。记你一功。", "Loyal. Credit to you.")),
                new ChoiceDef(LangHelper.T("拒绝（黑市-10）", "Refuse (-10 BM)"),
                    () => ApplyRep("BM", -10), null,
                    LangHelper.T("行。我自己来。", "Fine. I'll do it myself.")),
                new ChoiceDef(LangHelper.T("趁机举报（治安部+20，黑市-25）", "Report them (+20 Sec, -25 BM)"),
                    () => { ApplyRep("Sec", 20); ApplyRep("BM", -25); }, null,
                    LangHelper.T("你够狠。黑市会找你。", "You're cold. The market will come for you.")),
            });

        private static void SpawnCartelN3(int day) => SpawnNode("Cartel Handler", NS_CARTEL, KEY_N3, day,
            LangHelper.T("黑市要独一份的供货。你签，价格好说。", "The market wants exclusive supply. Sign, and the price is right."),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("独家供货（黑市+30，下层-15）", "Exclusive supply (+30 BM, -15 LL)"),
                    () => { ApplyRep("BM", 30); ApplyRep("LL", -15); }, null,
                    LangHelper.T("痛快。黑市的货，只从你这儿走。", "Good. Market goods move through you alone.")),
                new ChoiceDef(LangHelper.T("拒绝（黑市-5，下层+10）", "Refuse (-5 BM, +10 LL)"),
                    () => { ApplyRep("BM", -5); ApplyRep("LL", 10); }, null,
                    LangHelper.T("行。买卖不成仁义在。", "Fine. No deal, no hard feelings.")),
            });

        private static void SpawnCartelN4(int day) => SpawnNode("Cartel Handler", NS_CARTEL, KEY_N4, day,
            LangHelper.T("今晚黑市和治安部要见血。你站哪边？", "Tonight the market and Security bleed. Whose side?"),
            new ChoiceDef[]
            {
                new ChoiceDef(LangHelper.T("站黑市（黑市+20，治安部-20）", "Side with the market (+20 BM, -20 Sec)"),
                    () => { ApplyRep("BM", 20); ApplyRep("Sec", -20); }, () => WageSaveStore.SetString(NS_CARTEL, KEY_N4_CHOICE, "A"),
                    LangHelper.T("好。自己人。", "Good. One of us.")),
                new ChoiceDef(LangHelper.T("站治安部（治安部+20，黑市-20）", "Side with Security (+20 Sec, -20 BM)"),
                    () => { ApplyRep("Sec", 20); ApplyRep("BM", -20); }, () => WageSaveStore.SetString(NS_CARTEL, KEY_N4_CHOICE, "B"),
                    LangHelper.T("行。各走各路。", "Fine. Separate paths.")),
                new ChoiceDef(LangHelper.T("关门自保（两方各-5）", "Shut doors (-5 both)"),
                    () => { ApplyRep("BM", -5); ApplyRep("Sec", -5); }, () => WageSaveStore.SetString(NS_CARTEL, KEY_N4_CHOICE, "C"),
                    LangHelper.T("聪明。两边都不得罪。", "Smart. Offend neither.")),
            });

        // ============ 通用节点生成器 ============
        private sealed class ChoiceDef
        {
            public readonly string PlayerText;      // 选项文本（PLAYER 台词=plainText 显示）
            public readonly Action RepAction;       // 声望结算
            public readonly Action ExtraAction;     // 附加结算（favor/n3_choice 等，可 null）
            public readonly string ReplyLine;       // 角色回应（夜报）
            public ChoiceDef(string playerText, Action rep, Action extra, string reply)
            { PlayerText = playerText; RepAction = rep; ExtraAction = extra; ReplyLine = reply; }
        }

        private static void SpawnNode(string speaker, string ns, string doneKey, int day, string intro, ChoiceDef[] choices, string flavor = null)
        {
            WageSaveStore.SetInt(ns, KEY_LAST_SPAWN, day);
            var client = CreateStoryClient(speaker);
            if (client == null) return;

            // 常驻台词（台词稿 §2.1/§3.1 定稿）：有 flavor 时作为开场第一句，intro 顺延为第二句，选项挂第二句
            Dialogue main;
            if (flavor != null)
            {
                SetDialogue(client, speaker, flavor);
                var introDlg = new Dialogue().SetText(speaker, intro);
                var m = client.mainDialogue;
                if (m == null) { m = new Dialogue(); m.SetText(speaker, flavor); client.mainDialogue = m; }
                m.SetNextDialogue(introDlg);
                main = introDlg;
            }
            else
            {
                SetDialogue(client, speaker, intro);
                main = client.mainDialogue;
                if (main == null) { var d = new Dialogue(); d.SetText(speaker, intro); client.mainDialogue = d; main = d; }
            }

            var icon = Il2Cpp.DirectoryMaster.Item("$", true); // 王尔德同款钱币图标（Item(string,bool) 实锤签名）
            foreach (var c in choices)
            {
                try
                {
                    var playDlg = new Il2Cpp.Dialogue().SetText("PLAYER", c.PlayerText);
                    var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
                    {
                        try
                        {
                            c.RepAction?.Invoke();
                            c.ExtraAction?.Invoke();
                            WageSaveStore.SetBool(ns, doneKey, true);
                            Core.AddNightReportLine(c.ReplyLine);
                            Core.LogMsg("[StoryPlot] " + speaker + " 选项结算: " + c.PlayerText);
                        }
                        catch (Exception ex) { Core.LogMsg("[StoryPlot] 选项结算异常: " + ex.Message); }
                    }));
                    main.AddChoice(icon, "", "", playDlg, act, null);
                }
                catch (Exception ex) { Core.LogMsg("[StoryPlot] AddChoice 异常: " + ex.Message); }
            }
            AddClient(client);
            Core.LogMsg("[StoryPlot] " + speaker + " 剧情客户已到店（第 " + day + " 天）");
        }

        // ============ 工具 ============
        private static void ModCash(int delta)
        {
            try
            {
                if (Il2Cpp.PlayerStore.Instance != null)
                {
                    Il2Cpp.PlayerStore.Instance.ModCash(delta, false); // 实锤接口 dump.cs:32199
                    Core.LogMsg("[StoryPlot] 现金结算 " + (delta >= 0 ? "+" : "") + delta);
                }
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 现金结算异常: " + ex.Message); }
        }

        private static void ApplyRep(string faction, int delta)
        {
            try
            {
                switch (faction)
                {
                    case "UL": Il2Cpp.StoreReputation.ModULReputation(delta); break;
                    case "LL": Il2Cpp.StoreReputation.ModLLReputation(delta); break;
                    case "Sec": Il2Cpp.StoreReputation.ModSecReputation(delta); break;
                    case "BM": Il2Cpp.StoreReputation.ModBMReputation(delta); break;
                    case "Rev": Il2Cpp.StoreReputation.ModRevReputation(delta); break;
                    default: Core.LogMsg("[StoryPlot] 未知派系: " + faction); return;
                }
                Core.LogMsg("[StoryPlot] 声望结算 " + faction + (delta >= 0 ? "+" : "") + delta);
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 声望结算异常 " + faction + ": " + ex.Message); }
        }

        private static StoreClient CreateStoryClient(string speaker)
        {
            try
            {
                StoreClient c = StoreClientList.CreateFlexiBuyer();
                if (c == null) { Core.LogMsg("[StoryPlot] 创建剧情客户失败"); return null; }
                try { c.isSecurity = false; } catch { }
                try { c.spriteName = SpriteBySpeaker(speaker); } catch { } // 审阅 P1⑥：阵营立绘映射（不再全用下层随机）
                try { c.eventSourceId = "story_plot"; } catch { }
                return c;
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] 创建客户异常: " + ex.Message); return null; }
        }

        // 阵营立绘映射（SpriteDict 实锤接口 dump.cs:48761-48821）：上层→Upper、治安部→Sec、革命军→Rev、黑市→BM、下层→Lower
        private static string SpriteBySpeaker(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return SpriteDict.GetRandomLowerLevelSpriteName();
            if (speaker.Contains("Eleanor")) return SpriteDict.GetRandomUpperFemaleSpriteName();
            if (speaker.Contains("Officer Lun")) return SpriteDict.GetRandomSecMaleSpriteName();
            if (speaker.Contains("Serpico")) return SpriteDict.GetRandomSecSpriteName();
            if (speaker.Contains("蛙哥")) return SpriteDict.GetRandomShadySpriteName();
            if (speaker.Contains("Georgio")) return SpriteDict.GetRandomLowerMaleSpriteName();
            if (speaker.Contains("Nellis")) return SpriteDict.GetRandomScavSpriteName();
            if (speaker.Contains("军需官")) return SpriteDict.GetRandomRevMaleSpriteName();
            if (speaker.Contains("Cartel")) return SpriteDict.GetRandomBMMaleSpriteName();
            if (speaker.Contains("老鬼")) return SpriteDict.GetRandomScavSpriteName();
            if (speaker.Contains("墨")) return SpriteDict.GetRandomLowerMaleSpriteName();
            if (speaker.Contains("织魂者")) return SpriteDict.GetRandomShadySpriteName();
            if (speaker.Contains("焊针")) return SpriteDict.GetRandomSecMaleSpriteName();
            if (speaker.Contains("影蛙")) return SpriteDict.GetRandomShadySpriteName();
            if (speaker.Contains("蛙娘")) return SpriteDict.GetRandomUpperFemaleSpriteName();
            return SpriteDict.GetRandomLowerLevelSpriteName();
        }

        private static void SetDialogue(StoreClient client, string speaker, string text)
        {
            try
            {
                if (client.mainDialogue != null) client.mainDialogue.SetText(speaker, text);
                else { var d = new Dialogue(); d.SetText(speaker, text); client.mainDialogue = d; }
            }
            catch (System.Exception ex) { Core.LogMsg("[StoryPlot] 设置对话异常: " + ex.Message); }
        }

        private static void AddClient(StoreClient client)
        {
            try
            {
                PlayerStore ps = PlayerStore.Instance;
                if (ps == null || ps.storeClientManager == null) return;
                ps.storeClientManager.AddClient(client);
            }
            catch (Exception ex) { Core.LogMsg("[StoryPlot] AddClient 异常: " + ex.Message); }
        }
    }
}
