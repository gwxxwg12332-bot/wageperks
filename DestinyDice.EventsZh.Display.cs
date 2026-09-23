using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks
{

    public static partial class DestinyDice
{
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
