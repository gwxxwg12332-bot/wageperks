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
        public static void NewsOnGUI()

        {

            try

            {

                if (!_newsOpen || _newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                int pages = (_newsAllEvents.Count + 3) / 4;

                if (pages <= 1) return;

                float w = 110f, h = 34f;

                float y = UnityEngine.Screen.height - 60f;

                float cx = UnityEngine.Screen.width / 2f;

                if (UnityEngine.GUI.Button(new UnityEngine.Rect(cx - 130f, y, w, h), LangHelper.T("◀ 上一页", "◀ Prev")))

                {

                    _newsPage--;

                    if (_newsPage < 0) _newsPage = pages - 1;

                    ApplyNewsPage();

                }

                UnityEngine.GUI.Label(new UnityEngine.Rect(cx - 15f, y, 30f, h), (_newsPage + 1) + "/" + pages);

                if (UnityEngine.GUI.Button(new UnityEngine.Rect(cx + 20f, y, w, h), LangHelper.T("下一页 ▶", "Next ▶")))

                {

                    _newsPage++;

                    if (_newsPage >= pages) _newsPage = 0;

                    ApplyNewsPage();

                }

            }

            catch { }

        }
        public static void EnsureNewsButtons()

        {

            try

            {

                var mgr = Il2Cpp.NewsUIManager.Instance;

                if (mgr == null || mgr.uiGameObject == null) return;

                if (!_newsBtnCreated)

                {

                    var parent = mgr.uiGameObject.transform;

                    _newsPrevBtn = CreateNewsButton(parent, "WageNewsPrev", LangHelper.T("◀ 上一页", "◀ Prev"), new UnityEngine.Vector2(20f, 20f), new UnityEngine.Vector2(110f, 36f), PrevNewsPage);

                    _newsNextBtn = CreateNewsButton(parent, "WageNewsNext", LangHelper.T("下一页 ▶", "Next ▶"), new UnityEngine.Vector2(140f, 20f), new UnityEngine.Vector2(110f, 36f), NextNewsPage);

                    _newsBtnCreated = true;


                }

                SetNewsButtonsActive(true);

            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] 创建按钮异常: " + ex.Message); }

        }
        public static void SetNewsButtonsActive(bool active)

        {

            try

            {

                if (_newsPrevBtn != null) _newsPrevBtn.SetActive(active);

                if (_newsNextBtn != null) _newsNextBtn.SetActive(active);

            }

            catch { }

        }
        public static void PrevNewsPage()

        {

            try

            {

                if (_newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                int pages = (_newsAllEvents.Count + 3) / 4;

                _newsPage--;

                if (_newsPage < 0) _newsPage = pages - 1;

                ApplyNewsPage();

            }

            catch { }

        }
        public static void NextNewsPage()

        {

            try

            {

                if (_newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                int pages = (_newsAllEvents.Count + 3) / 4;

                _newsPage++;

                if (_newsPage >= pages) _newsPage = 0;

                ApplyNewsPage();

            }

            catch { }

        }
        public static void ApplyNewsPage()

        {

            _newsPaging = true;

            try

            {

                var mgr = Il2Cpp.NewsUIManager.Instance;

                if (mgr == null || _newsAllEvents == null) return;

                var slice = SlicePage(_newsPage);

                // ① 隐藏全部4个layout（对齐PopulateUI）

                if (mgr.layout1 != null) mgr.layout1.SetActive(false);

                if (mgr.layout2 != null) mgr.layout2.SetActive(false);

                if (mgr.layout3 != null) mgr.layout3.SetActive(false);

                if (mgr.layout4 != null) mgr.layout4.SetActive(false);

                // ② 按count选layout+elements（对齐PopulateUI分支：>3→layout4, ==3→layout3, ==2→layout2, else→layout1）

                UnityEngine.GameObject layout;

                Il2CppSystem.Collections.Generic.List<Il2Cpp.NewsUIElement> elements;

                int cnt = slice.Count;

                if (cnt > 3) { layout = mgr.layout4; elements = mgr.layout4Elements; }

                else if (cnt == 3) { layout = mgr.layout3; elements = mgr.layout3Elements; }

                else if (cnt == 2) { layout = mgr.layout2; elements = mgr.layout2Elements; }

                else { layout = mgr.layout1; elements = mgr.layout1Elements; }

                // ③ 激活选中的layout（对齐ActivateLayout）

                if (layout != null) layout.SetActive(true);

                // ④ elements全部 SetActive(false) + ClearElement（对齐ActivateLayout遍历）

                if (elements != null)

                {

                    for (int j = 0; j < elements.Count; j++)

                    {

                        try

                        {

                            if (elements[j] != null)

                            {

                                if (elements[j].gameObject != null) elements[j].gameObject.SetActive(false);

                                elements[j].ClearElement();

                            }

                        }

                        catch { }

                    }

                }

                // ⑤ 填充循环：SetActive(true) + PopulateUI（对齐PopulateUI填充循环）

                if (elements != null)

                {

                    int n = UnityEngine.Mathf.Min(cnt, elements.Count);

                    for (int i = 0; i < n; i++)

                    {

                        try

                        {

                            if (elements[i] == null || elements[i].gameObject == null) continue;

                            elements[i].gameObject.SetActive(true);

                            elements[i].PopulateUI(slice[i]);

                        }

                        catch { }

                    }

                }


            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] ApplyPage异常: " + ex.Message); }

            _newsPaging = false;

        }
        public static void PostfixNewsPopulateUI()

        {

            try

            {

                _newsOpen = true;

                var ss = Il2Cpp.StoreStation.Instance;

                if (ss == null || ss.storeEventManager == null)

                {

                    Core.LogMsg("[报纸翻页] StoreStation/EventManager为空");

                    return;

                }

                try

                {

                    _newsAllEvents = ss.storeEventManager.GetActiveEvents();

                    // 同原生 b__25_0 过滤：移除 isHiddenFromNewsPaper（报纸隐藏）事件

                    if (_newsAllEvents != null)

                    {

                        for (int i = _newsAllEvents.Count - 1; i >= 0; i--)

                        {

                            try { if (_newsAllEvents[i].isHiddenFromNewsPaper) _newsAllEvents.RemoveAt(i); } catch { }

                        }

                    }

                    // 统一汉化所有事件（覆盖所有来源路径，不依赖QueueFuturEvent——启动预置等事件也能汉化）

                    if (_newsAllEvents != null)

                    {

                        for (int i = 0; i < _newsAllEvents.Count; i++)

                        {

                            try { LocalizeEvent(_newsAllEvents[i]); } catch { }

                        }

                    }

                    // 报纸连载注入钩子：独立mod（SerialNewsMod）注册后在此注入连载内容（配置外挂，不硬编码）

                    try { if (NewsSerialInjector != null) NewsSerialInjector(_newsAllEvents); } catch { }

                    _newsPage = 0;


                    // 初始即渲染当前页（含连载注入）：原生PopulateUI在Postfix之前已渲染（无连载），必须重渲染

                    ApplyNewsPage();

                    // 创建/显示翻页按钮（UGUI，挂报纸面板下）

                    EnsureNewsButtons();

                    // 不扩layout（克隆element会破坏原生布局导致空白）：原生4条布局，翻页按钮切换数据源

                    // 翻页时 SelectLayout(4条切片) 由按钮/方向键 触发

                }

                catch (Exception ex2)

                {

                    Core.LogMsg("[报纸翻页] GetActiveEvents异常: " + ex2.Message);

                }

            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] PopulateUI异常: " + ex.Message); }

        }
        public static Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent> SlicePage(int page)

        {

            var slice = new Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent>();

            if (_newsAllEvents == null) return slice;

            int start = page * 4;

            for (int i = start; i < start + 4 && i < _newsAllEvents.Count; i++)

            {

                try { slice.Add(_newsAllEvents[i]); } catch { }

            }

            return slice;

        }
        public static void PostfixNewsToggleUI()

        {

            try { _newsOpen = !_newsOpen; } catch { }

        }
        public static void PostfixNewsCloseUI()

        {

            try { _newsOpen = false; } catch { }

            try { SetNewsButtonsActive(false); } catch { }

        }
        public static void PostfixNewsInputUpdate()

        {

            try

            {

                if (!_newsOpen || _newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                bool left = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow);

                bool right = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow);

                if (!left && !right) return;


                int pages = (_newsAllEvents.Count + 3) / 4;

                _newsPage += right ? 1 : -1;

                if (_newsPage < 0) _newsPage = pages - 1;

                if (_newsPage >= pages) _newsPage = 0;

                ApplyNewsPage();

            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] 异常: " + ex.Message); }

        }
}
}
