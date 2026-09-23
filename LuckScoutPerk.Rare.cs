using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class LuckScoutPerk : CustomStartingPerk
{
    private static int GetScannerChance(GameItem scanner)

    {

        return GetCurrentChance();

    }
    internal static void RecordRareDrop(int count)

    {

        try

        {

            if (!IsActive() || count <= 0) return;

            int total = GetRareCount() + count;

            SetRareCount(total);


        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Rare] 异常: " + ex.Message); }

    }
    public static void PostfixCreateTooltip(RichTextBuilder builder, GameItem item)

    {

        try

        {

            if (!IsActive()) return;

            if (builder == null || item == null) return;

            string id = "?"; try { id = item.identifier ?? "?"; } catch { }

            if (id != "metal_scanner") return;

            if (!item.IsTag(SCANNER_TAG)) return; // 非加强探测器不加



            int chance = GetScannerChance(item);

            int total = GetRareCount();

            int scavs = GetScavCount();

            // RichTextBuilder 无 Append：用 AddLine 追加（cheatsheet 1186）

            builder.AddLine(LangHelper.T("◆ 捡漏直觉（稀有物强化）", "◆ Luck Scout (Rare Loot Boost)"), bold: true);

            builder.AddLine(LangHelper.T("稀有掉落率：", "Rare drop rate: ") + chance + "%" + (chance >= MAX_CHANCE ? LangHelper.T("（已满级）", " (MAX)") : ""));

            builder.AddLine(LangHelper.T("累计拾荒：", "Total scavenges: ") + scavs + LangHelper.T(" 次 | 累计稀有物：", " | Rare finds: ") + total + LangHelper.T(" 件", ""));

        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Rare] 异常: " + ex.Message); }

    }
    private static string[] BuildRarePool()
    {
        var list = new System.Collections.Generic.List<string> {
            // 500
            "skincare_cream",
            // 350
            "black_injector", "desequencer", "crypto_module_cmd", "module_extractor_advanced",
            // 325
            "bottled_water_premium",
            // 310
            "shotgun",
            // 300
            "turbo_booster_adv", "advanced_flux_agent", "crypto_module_sec",
            // 280
            "smg",
            // 250
            "crypto_module_med", "crypto_module_eng", "chem_module",
            "c4", "stun_gun", "blue_blood_bag", "wine_yeast_infinite", "metal_scanner", "c4_set",
            // 200
            "surgery_tool", "pheromone_perfume", "crypto_module_sup", "crypto_module_ser", "glock_receiver"
        };
        if (BuildConfig.HardMode)
        {
            list.Add("system_capped_neural_core");     // 受限神经模组（硬爽版加回均匀池；09-13 用户拍板：未受限全删）
        }
        return list.ToArray();
    }
    private static GameItem CreateRareItem()
    {
        try
        {
            // 神经模组独立 roll（仅标准版；未命中/创建失败回落原池；09-13 用户拍板：未受限已全删，仅剩受限 2%）
            if (!BuildConfig.HardMode)
            {
                double nr = Core.Rng.NextDouble();
                string neuralId = null;
                if (nr < BuildConfig.NeuralRollChance / 100f) neuralId = "system_capped_neural_core";
                if (neuralId != null)
                {
                    try
                    {
                        GameItem neural = DirectoryMaster.Item(neuralId, true);
                        if (neural != null)
                        {
                            try { neural.DisableTag("not_purchased", true); neural.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                            return neural;
                        }
                    }
                    catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Rare] 异常: " + ex.Message); }
                }
            }

            // 从随机起点尝试最多 5 个候选，避免个别物品创建失败导致掉落为空
            int start = Core.Rng.Next(RARE_VALUE_POOL.Length);

            for (int attempt = 0; attempt < Math.Min(5, RARE_VALUE_POOL.Length); attempt++)

            {

                string id = RARE_VALUE_POOL[(start + attempt) % RARE_VALUE_POOL.Length];

                GameItem item = null;

                try { item = DirectoryMaster.Item(id, true); } catch { }

                if (item == null) continue;

                try { item.DisableTag("not_purchased", true); item.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

                return item;

            }

            // 全部失败兜底：原稀有物

            try

            {

                var fallback = DirectoryMaster.Item("skincare_cream", true);

                if (fallback != null) { try { fallback.DisableTag("not_purchased", true); } catch { } }

                return fallback;

            }

            catch { return null; }

        }

        catch { return null; }

    }
    private static int GetCurrentChance()

    {

        int level = GetScavLevel();

        return Math.Min(MAX_CHANCE, BASE_CHANCE + level * UPGRADE_CHANCE);

    }
}
