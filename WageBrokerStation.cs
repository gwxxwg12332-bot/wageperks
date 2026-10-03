using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace WagePerks;

// 10-03 蛙哥交易站（方案B：完全自定义，不进原生客户体系）
// 设计稿：02_豆包产出\蛙哥交易站设计稿_20261003.md
internal static class WageBrokerStation
{
    // 三段状态门控（学Brewing）
    private static bool _wageVisit = false;         // 玩家点了蛙哥入口
    private static bool _placingWageStock = false;  // 正在铺蛙哥货
    private static bool _addingOurStock = false;    // 正在放自家货（放行柜台）

    private static SpriteRenderer _sceneRenderer;
    private static Sprite _originalSceneSprite;

    // ===== 1. 地图按钮（学Brewing EnsureMapButton）=====
    internal static void PostfixMapOpenUI(MapUIManager __instance)
    {
        try
        {
            EnsureWageMapButton(__instance);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] MapOpenUI异常: " + ex.Message); }
    }

    private static void EnsureWageMapButton(MapUIManager map)
    {
        try
        {
            // 找升级商人按钮的parent
            var upgradeBtn = map.upgradeMerchantButton;
            if (upgradeBtn == null) { Core.LogMsg("[蛙哥交易站] upgradeMerchantButton=null"); return; }
            Transform parent = upgradeBtn.transform.parent;

            // 防重复注入
            var existing = parent.Find("Wage_Broker_Station");
            if (existing != null) { existing.GetComponent<Button>().onClick.RemoveAllListeners(); existing.GetComponent<Button>().onClick.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(OnClickWageStation)); return; }

            // 克隆升级商人按钮
            var newBtn = UnityEngine.Object.Instantiate(upgradeBtn, parent);
            newBtn.gameObject.name = "Wage_Broker_Station";
            Core.LogMsg("[蛙哥交易站] 地图按钮创建");

            // 学Brewing: LayoutElement.ignoreLayout=true（脱离自动布局，防挤乱）
            var le = newBtn.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null) le.ignoreLayout = true;

            // 固定位置（学Brewing: anchoredPosition=(270,-15)）
            var newRect = newBtn.GetComponent<RectTransform>();
            if (newRect != null)
            {
                newRect.anchoredPosition = new Vector2(310f, -15f);
                Core.LogMsg("[蛙哥交易站] 按钮位置: " + newRect.anchoredPosition);
            }

            // 防汉化：删TMPLocalizer/LocalizeStringEvent
            foreach (var mb in newBtn.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var t = mb.GetType();
                if (t.Name == "TMPLocalizer" || t.Name == "LocalizeStringEvent")
                {
                    UnityEngine.Object.DestroyImmediate(mb);
                }
            }

            // 改文字（学Brewing:2853 GetComponentInChildren<TextMeshProUGUI>(true) + 显式cast TMP_Text）
            var tmp = newBtn.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (tmp != null)
            {
                string oldText = ((TMP_Text)tmp).text;
                Core.LogMsg("[蛙哥交易站] 改前text: '" + oldText + "'");

                // 换字体（学Brewing:2863）
                var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f != null && f.name == "NotoSansSC-VariableFont_wght");
                if (font != null) ((TMP_Text)tmp).font = font;

                ((TMP_Text)tmp).text = LangHelper.T("蛙哥交易站", "Wage's Trading Post");
                tmp.ForceMeshUpdate(); // 强制刷新
                Core.LogMsg("[蛙哥交易站] 改后text: '" + ((TMP_Text)tmp).text + "'");
                Core.LogMsg("[蛙哥交易站] 改文字成功: " + tmp.gameObject.name);
            }
            else
            {
                Core.LogMsg("[蛙哥交易站] ⚠️ GetComponentInChildren<TextMeshProUGUI>(true)=null！");
            }

            // 点击事件
            var btn = newBtn.GetComponent<Button>();
            btn.onClick.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(OnClickWageStation));
            Core.LogMsg("[蛙哥交易站] 地图按钮就绪");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] EnsureMapButton异常: " + ex.Message); }
    }

    private static void OnClickWageStation()
    {
        try
        {
            _wageVisit = true;
            Core.LogMsg("[蛙哥交易站] 点击进入交易站");
            // 复用原生升级商人入口（VisitUpgradeMerchant）
            var map = UnityEngine.Object.FindObjectOfType<MapUIManager>();
            if (map != null) map.VisitUpgradeMerchant();
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] OnClick异常: " + ex.Message); }
    }

    // ===== 2. 场景铺货门控（学Brewing PlaceInventorInventory）=====
    // 进场景前还原（学Brewing:3190 BeforeVisit）
    internal static void PrefixVisitUpgradeMerchant()
    {
        try
        {
            // 非蛙哥场景 → 还原原场景图
            if (!_wageVisit)
            {
                Core.LogMsg("[蛙哥交易站] BeforeVisit → 还原场景图");
                RestoreSceneSprite();
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] PrefixVisit异常: " + ex.Message); }
    }

    // 离开复位（学Brewing:3235 Leave）
    internal static void PostfixHandleLeaveUpgradeMerchant()
    {
        try
        {
            Core.LogMsg("[蛙哥交易站] Leave → 复位flag");
            _wageVisit = false;
            _placingWageStock = false;
            _addingOurStock = false;
            RestoreSceneSprite();
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] PostfixLeave异常: " + ex.Message); }
    }

    internal static void PrefixPlaceInventorInventory(StoreClientList __instance)
    {
        try
        {
            _placingWageStock = _wageVisit;
            if (_placingWageStock)
            {
                Core.LogMsg("[蛙哥交易站] PrefixPlaceInventorInventory → 换场景图");
                ApplySceneSprite();
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] PrefixPlace异常: " + ex.Message); }
    }

    internal static void PostfixPlaceInventorInventory(StoreClientList __instance)
    {
        try
        {
            if (_placingWageStock)
            {
                Core.LogMsg("[蛙哥交易站] PostfixPlaceInventorInventory → EndPlacement");
                EndPlacement();
                _wageVisit = false;
                _placingWageStock = false;
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] PostfixPlace异常: " + ex.Message); }
    }

    // 拦原生货（蛙哥场景里不出现原生升级商人的货）
    internal static bool PrefixAddDirectSellingItemToTable()
    {
        // _placingWageStock=true且_addingOurStock=false → 拦原生货
        if (_placingWageStock && !_addingOurStock)
        {
            Core.LogMsg("[蛙哥交易站] 拦原生货 ✓");
            return false;
        }
        return true;
    }

    // ===== 3. 场景图替换 =====
    private static void ApplySceneSprite()
    {
        try
        {
            var map = UnityEngine.Object.FindObjectOfType<MapUIManager>();
            if (map == null || map.upgradeStoreScene == null) return;

            _sceneRenderer = map.upgradeStoreScene.transform.GetChild(0).GetComponent<SpriteRenderer>();
            if (_sceneRenderer == null) return;

            // 首次记录原图
            if (_originalSceneSprite == null) _originalSceneSprite = _sceneRenderer.sprite;

            // 换蛙哥场景图（先空着——后面嵌入PNG资源）
            // _sceneRenderer.sprite = 蛙哥场景图;
            Core.LogMsg("[蛙哥交易站] 场景图替换（待嵌入PNG资源）");
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] ApplySceneSprite异常: " + ex.Message); }
    }

    // 还原原场景图
    private static void RestoreSceneSprite()
    {
        try
        {
            if (_sceneRenderer != null && _originalSceneSprite != null)
            {
                _sceneRenderer.sprite = _originalSceneSprite;
                Core.LogMsg("[蛙哥交易站] 还原原场景图");
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] RestoreSceneSprite异常: " + ex.Message); }
    }

    // ===== 4. EndPlacement（铺蛙哥货）=====
    private static void EndPlacement()
    {
        try
        {
            _addingOurStock = true;
            Core.LogMsg("[蛙哥交易站] EndPlacement → 铺蛙哥货");

            // 1. 检测已安装的mod NPC
            var availableNPCs = new List<(string modId, string npcId, string itemId)>();
            // BrewingExpansion：泥泥名片
            if (ModItemExists("be_nini_card")) availableNPCs.Add(("BrewingExpansion", "be_nini_card", "be_nini_card"));
            // Ratlin：鼠布林肋明治
            if (ModItemExists("ratlin_ribwich_meat_expander")) availableNPCs.Add(("Ratlin", "ratlin_ribwich_meat_expander", "ratlin_ribwich_meat_expander"));
            // Wine：假酒大师
            if (ModItemExists("retired_winemaker")) availableNPCs.Add(("Wine", "retired_winemaker", "retired_winemaker"));

            Core.LogMsg("[蛙哥交易站] 检测到mod NPC: " + availableNPCs.Count + "个");
            foreach (var npc in availableNPCs) Core.LogMsg("[蛙哥交易站]   - " + npc.modId);

            // 2. 随机召唤一个mod NPC
            if (availableNPCs.Count > 0)
            {
                var rand = new System.Random();
                var chosen = availableNPCs[rand.Next(availableNPCs.Count)];
                Core.LogMsg("[蛙哥交易站] 随机召唤: " + chosen.modId + " NPC=" + chosen.npcId);
                // TODO: 召唤NPC + 上mod物品
            }
            else
            {
                Core.LogMsg("[蛙哥交易站] 无第三方mod NPC可召唤");
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] EndPlacement异常: " + ex.Message); }
        finally { _addingOurStock = false; }
    }

    // ===== 5. 离开还原 =====
    internal static void OnLeaveStation()
    {
        try
        {
            _wageVisit = false;
            _placingWageStock = false;
            _addingOurStock = false;

            // 还原原场景图
            if (_sceneRenderer != null && _originalSceneSprite != null)
            {
                _sceneRenderer.sprite = _originalSceneSprite;
                Core.LogMsg("[蛙哥交易站] 离开→还原场景图");
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥交易站] OnLeave异常: " + ex.Message); }
    }

    // ===== 6. 软联动检测 =====
    internal static bool ModItemExists(string id)
    {
        try
        {
            var item = DirectoryMaster.Item(id, false);
            return item != null;
        }
        catch { return false; }
    }
}
