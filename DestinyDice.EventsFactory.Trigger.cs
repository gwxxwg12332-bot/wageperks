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
        public static string TriggerRandomEvent(GameItem dice)
        {

            try

            {

                // 从游戏原生事件蓝图池随机挑一个，明天触发（参照MoreEvents模式）

                var evtMgr = (StoreStation.instance != null) ? StoreStation.instance.storeEventManager : null;

                if (evtMgr == null)

                {


                    return null;

                }

                var blueprints = StoreEventManager.normalEventBlueprints;

                if (blueprints == null || blueprints.Count == 0)

                {


                    return null;

                }



                // 随机挑一个蓝图（排除当前正在进行的，避免重复）

                StoreEventBlueprint bp = null;

                for (int attempt = 0; attempt < 5; attempt++)

                {

                    int idx = UnityEngine.Random.Range(0, blueprints.Count);

                    var candidate = blueprints[idx];

                    if (candidate == null || candidate.storeEventFunc == null) continue;

                    try

                    {

                        if (!evtMgr.IsEventActive(candidate.identifier)) { bp = candidate; break; }

                    }

                    catch { bp = candidate; break; }

                }

                if (bp == null)

                {

                    for (int i = 0; i < blueprints.Count; i++)

                    {

                        var candidate = blueprints[i];

                        if (candidate != null && candidate.storeEventFunc != null) { bp = candidate; break; }

                    }

                }

                if (bp == null) { Core.LogMsg("[命运骰子] 事件蓝图全为空"); return null; }



                StoreEvent evt = bp.storeEventFunc.Invoke();

                if (evt == null) { Core.LogMsg("[命运骰子] 事件实例化失败"); return null; }



                // 汉化事件文本（显示名/新闻标题/新闻描述）

                LocalizeEvent(evt);



                // 明天触发（IsKnown=true，玩家能看到预告）

                evtMgr.QueueFuturEvent(evt, 1, true);




                // 触发计数（写标签 + 更新面板标题）

                if (dice != null)

                {

                    int trig = GetTagInt(dice, DICE_TRIGGER_TAG);

                    SetTagInt(dice, DICE_TRIGGER_TAG, trig + 1);
                    int cur = GetTagInt(dice, DICE_VALUE_TAG);

                    UpdateDicePanelTitle(dice, cur);

                }
                return evt.identifier;
            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 触发事件异常: " + ex.Message);

            
                return null;
            }
                    }
}
}
