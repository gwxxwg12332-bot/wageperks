using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class LuckScoutPerk : CustomStartingPerk
{
    private static void SetScannerChance(GameItem scanner, int chance)

    {

    }
    public static void PostfixGetMaxScavAttempts(ref int __result)

    {

        try

        {

            // 09-10 用户拍板：捡漏直觉不再加拾荒次数（任何职业；此前 +10 有缩减 bug）
            // 09-12 硬爽版：加回 +10（仅非鲁滨逊职业；鲁滨逊走 GetScavCap 已含，防双 Postfix 叠加）
            if (BuildConfig.HardMode && IsActive() && !RobinCrusoePerk.IsActive())
                __result += 10;

        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Scavenge] 异常: " + ex.Message); }

    }
    public static void PostfixGetScavTimeLeft(ref int __result)

    {

        try

        {

            // 09-10 用户拍板：捡漏直觉不再加拾荒次数（任何职业）
            // 09-12 硬爽版：加回 +10（GetScavTimeLeft=实际可拾荒次数读口，须同步；非鲁滨逊防双加）
            if (BuildConfig.HardMode && IsActive() && !RobinCrusoePerk.IsActive())
                __result += 10;
            if (false) { }

        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Scavenge] 异常: " + ex.Message); }

    }
    public static void PrefixCanScavenge()

    {

        if (!IsActive()) return;

        // 不再额外判定受伤——原版 CanScavenge 已基于受伤免疫次数处理，

        // 额外加 woundState/isWoundedFresh 判定会导致治疗后仍无法拾荒。

        try

        {

            var ps = Il2Cpp.PlayerStore.Instance;

            if (ps != null && ps.healthData != null)

            {

                try { } catch { }

            }

        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Scavenge] 异常: " + ex.Message); }

    }
    public static void PostfixCanScavenge(ref bool __result)

    {

        if (!IsActive() || RobinCrusoePerk.IsActive()) return;

        try { } catch { }

        // 修正：无伤时原版可能因受伤免疫次数耗尽返回 false，改成 true

        // 受伤时完全交给原版判定（治疗后恢复免疫次数即可拾荒）

        if (!__result && ScavHelper.GetScavTimeLeft() > 0)

        {

            try

            {

                var ps = PlayerStore.Instance;

                if (ps != null)

                {

                    var health = ps.healthData;

                    bool isWounded = (health != null && health.woundState > 0);

                    bool isStable = (health != null && health.isWoundStable);

                    // 无伤 或 伤口已稳定（治疗过）都允许拾荒

                    if (!isWounded || isStable)

                    {

                        __result = true;


                    }

                    else

                    { }

                }

            } catch { }

        }

    }
    public static void PrefixScavengeDumpingGrounds()

    {

        _scavengeAllowedThisCall = ScavHelper.CanScavenge();

    }
    public static void PostfixScavengeDumpingGrounds()

    {

        try

        {

            if (!IsActive()) return;

            if (!_scavengeAllowedThisCall) return;  // 非真正拾荒（CanScavenge false 提前 return）不计数

            int count = GetScavCount() + 1;

            SetScavCount(count);

            int level = GetScavLevel();

            int newLevel = count / LEVELUP_EVERY;

            if (newLevel > level)

            {

                SetScavLevel(newLevel);


            }

        }

        catch (Exception ex) { Core.LogMsg("[捡漏直觉] 拾荒计数失败: " + ex.Message); }

    }
    public static void PostfixQuitToMenu() { try { ResetState();  } catch { } }
    public static void PostfixOnMainMenu() { try { ResetState();  } catch { } }
    public static void PostfixNewGame() { try { FullReset(); } catch { } }
    public static void PostfixGetRandomScavengedItem(Il2CppSystem.Collections.Generic.List<GameItem> __result)

    {

        try

        {

            if (!IsActive()) return;

            if (__result == null) return;

            GameItem scanner = FindScannerInOwned();

            if (scanner == null) return;

            int chance = Math.Min(MAX_CHANCE, GetScannerChance(scanner)); // 旧档标签可能>10，读取处clamp

            if (chance <= 0) return;



            // 首次拾荒必出标记（KEY_FIRST_RARE=0 且未触发过 → 必出；触发后置 1）

            bool rolled = (Core.Rng.NextDouble() * 100.0 < chance);
            if (!rolled) return;
            // 稀有物（价值池，已解锁物品）——不设首趟必出
            GameItem rare = CreateRareItem();
            if (rare != null)
            {
                __result.Add(rare);
                try { RecordRareDrop(1); } catch { }
                string rid = "?"; try { rid = rare.identifier ?? "?"; } catch { }
            }


        }

        catch (System.Exception ex) { Core.LogMsg("[LuckScoutPerk.Scavenge] 异常: " + ex.Message); }

    }
}
