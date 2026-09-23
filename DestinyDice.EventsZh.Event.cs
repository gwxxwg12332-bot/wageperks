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
        public static string GetEventZh(string id)

        {

            string name;

            if (id != null && EventZhName.TryGetValue(id, out name)) return name;

            return null;

        }
        public static void LocalizeEvent(StoreEvent evt)

        {

            try

            {

                if (evt == null || string.IsNullOrEmpty(evt.identifier)) return;

                // 英文模式：跳过汉化覆写，显示原生英文事件（游戏设英文即英文）

                if (LangHelper.IsEnglish()) return;

                string id = evt.identifier;

                string zhName, zhNews, zhDesc;

                if (EventZhName.TryGetValue(id, out zhName))

                {

                    evt.displayName = zhName;

                    if (EventZhNews.TryGetValue(id, out zhNews)) evt.newsName = zhNews;

                    if (EventZhDesc.TryGetValue(id, out zhDesc)) evt.newsDescription = zhDesc;


                }

                else if (!string.IsNullOrEmpty(evt.newsName) && TitleZhName.TryGetValue(evt.newsName, out zhName))

                {

                    // 英文标题兜底：无 identifier 匹配（如填充新闻 TRANSPORT SHIP MISSING）

                    evt.displayName = zhName;

                    evt.newsName = zhName;

                    string zhDesc2;

                    if (TitleZhDesc.TryGetValue(evt.newsName, out zhDesc2)) evt.newsDescription = zhDesc2;


                }

                else

                { }

            }

            catch (Exception exl) { Core.LogMsg("[命运骰子] 汉化事件失败: " + exl.Message); }

        }
}
}
