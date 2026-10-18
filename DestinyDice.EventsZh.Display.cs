using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine;

namespace WagePerks
{

    public static partial class DestinyDice
{
// 10-19 P3 A组#19 待确认用途（派活单§四：DestinyDice 事件中文·用途待拍板；未注册=现状未生效，拍板后接线或作废）
        public static void PostfixGetDisplayName(StoreEvent __instance, ref string __result)

        {

            try

            {

                if (__instance == null || string.IsNullOrEmpty(__result)) return;

                string zh = GetEventZh(__instance.identifier);

                if (zh != null) __result = zh;

            }

            catch (System.Exception ex) { Core.LogMsg("[DestinyDice.EventsZh.Display] 异常: " + ex.Message); }

        }
// 10-19 P3 A组#20 待确认用途（派活单§四：DestinyDice 事件中文·用途待拍板；未注册=现状未生效，拍板后接线或作废）
        public static void PostfixQueueFuturEvent(StoreEvent storeEvent)

        {

            try

            {

                if (storeEvent != null && !string.IsNullOrEmpty(storeEvent.identifier))

                {

                    LocalizeEvent(storeEvent);

                }

            }

            catch (System.Exception ex) { Core.LogMsg("[DestinyDice.EventsZh.Display] 异常: " + ex.Message); }

        }
}
}
