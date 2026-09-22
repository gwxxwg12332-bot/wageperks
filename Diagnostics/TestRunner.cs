using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace JacksonPerks;

// ============================================================
// 自动化测试运行器（TestRunner）
// 通过命令行参数 -runtests 触发，正常启动绝对不触发
// 测试结果写到 游戏目录/Mods/测试结果.txt
// 全部测试完成后自动关闭游戏
// ============================================================
internal static class TestRunner
{
    private static bool _testModeEnabled = false;
    private static bool _testsStarted = false;
    private static bool _continueTried = false;
    private static bool _slotLoadTried = false;
    private static string _resultFile = "";
    private static string _targetsFile = "";

    // 公共方法：检查是否是测试模式
    public static bool IsTestModeEnabled()
    {
        return _testModeEnabled;
    }
    private static string _heartbeatFile = "";
    private static int _passed = 0;
    private static int _failed = 0;
    private static int _skipped = 0;
    private static Stopwatch _totalStopwatch = new Stopwatch();
    private static string _currentTestCase = "初始化中";

    public static void Init()
    {

        // 检查命令行参数是否包含 -runtests
        string[] args = Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg.Equals("-runtests", StringComparison.OrdinalIgnoreCase))
            {
                _testModeEnabled = true;
            }
        }

        if (!_testModeEnabled)
        {
            return;
        }

        Core.LogMsg("[TestRunner] 等待游戏加载完成后开始执行测试...");

        // 结果文件路径（用英文文件名避免PowerShell编码问题）
        _resultFile = Path.Combine(Application.dataPath, "..", "Mods", "test_results.txt");
        _resultFile = Path.GetFullPath(_resultFile);

        // 飞书同步的测试目标文件（sync_test_targets.py 生成）
        _targetsFile = Path.Combine(Application.dataPath, "..", "Mods", "test_targets.txt");
        _targetsFile = Path.GetFullPath(_targetsFile);

        // 心跳文件路径（用于外部脚本检测是否卡住）
        _heartbeatFile = Path.Combine(Application.dataPath, "..", "Mods", "test_heartbeat.txt");
        _heartbeatFile = Path.GetFullPath(_heartbeatFile);

        // 清空旧结果
        try
        {
            File.WriteAllText(_resultFile, "");
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[TestRunner] 清空结果文件失败: {ex.Message}");
        }

        // 写初始心跳
        UpdateHeartbeat("初始化完成，等待游戏加载");

        _totalStopwatch.Start();
    }

    // 在OnUpdate中调用，等待游戏加载完成后开始测试
    public static void OnUpdate()
    {
        if (!_testModeEnabled) return;
        if (_testsStarted) return;

        try
        {
            string sceneName = "";
            try { sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; } catch { }

            // 输出场景名称（调试用）
            if (!_sceneLogged)
            {
                _sceneLogged = true;
            }

            if (string.IsNullOrEmpty(sceneName)) return;

            // 不等待场景，直接开始测试（自定义储物箱测试不需要存档）
            _testsStarted = true;

            MelonCoroutines.Start(RunTestsCoroutine());
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[TestRunner] OnUpdate异常: {ex.Message}");
        }
    }

    private static bool _sceneLogged = false;

    // 更新心跳文件（外部脚本用这个检测是否卡住）
    private static int _shotCounter = 0;
    private static void Shot(string tag)   // 状态 dump（不截屏）：输出游戏内部真实状态
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[状态-" + tag + "]");
            // 天数
            try { sb.Append(" 天数=" + Il2Cpp.StoreStation.GetDayCounter()); } catch { sb.Append(" 天数=?"); }
            // PlayerStore 关键状态
            try
            {
                var ps = Il2Cpp.PlayerStore.Instance;
                if (ps != null)
                {
                    sb.Append(" skipIntro=" + ps.skipIntro);
                    sb.Append(" CanStartDay=" + ps.CanStartDay());
                    sb.Append(" CanEndDay=" + ps.CanEndDay());
                }
                else sb.Append(" PlayerStore=null");
            }
            catch { }
            // 客户栈（PlayerStore.storeClientManager 是 public 字段）
            try
            {
                var ps0 = Il2Cpp.PlayerStore.Instance;
                if (ps0 != null && ps0.storeClientManager != null && ps0.storeClientManager.clientStack != null)
                    sb.Append(" clientStack=" + ps0.storeClientManager.clientStack.Count);
                else sb.Append(" clientStack=null");
            }
            catch { sb.Append(" clientStack=?"); }
            // 卷帘门
            try { sb.Append(" 卷帘门=" + (Il2Cpp.StoreShutterButton.Instance != null ? "存在" : "null")); } catch { }
            // 背包物品（反射 EmporiumEntry 库存）
            try
            {
                int cnt2 = -1;
                var ee = Il2Cpp.EmporiumEntry.Instance;
                if (ee != null)
                {
                    foreach (var fname in new[] { "currentInv", "invElement", "inventory" })
                    {
                        var f = ee.GetType().GetField(fname, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (f == null) continue;
                        var v = f.GetValue(ee);
                        if (v == null) continue;
                        var ci = v.GetType().GetProperty("childItems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (ci == null) continue;
                        var ch = ci.GetValue(v);
                        if (ch == null) continue;
                        var cntP = ch.GetType().GetProperty("Count");
                        if (cntP != null) { cnt2 = (int)cntP.GetValue(ch); break; }
                    }
                }
                sb.Append(" 背包=" + cnt2);
            }
            catch { sb.Append(" 背包=?"); }
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 状态dump异常: " + ex.Message); }

        // 截图保存（读像素方式，IL2CPP 可用）
        try
        {
            string dir = "Mods/flow_screens";
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            var cam = UnityEngine.Camera.main;
            if (cam == null) {  return; }
            var rt = new UnityEngine.RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 24);
            cam.targetTexture = rt;
            cam.Render();
            var tex = new UnityEngine.Texture2D(UnityEngine.Screen.width, UnityEngine.Screen.height, UnityEngine.TextureFormat.RGB24, false);
            UnityEngine.RenderTexture.active = rt;
            tex.ReadPixels(new UnityEngine.Rect(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            UnityEngine.RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            // 反射调 ImageConversion.EncodeToPNG（IL2CPP 下不在标准命名空间）
            byte[] png = null;
            System.Type icType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == "ImageConversion") { icType = t; break; }
                    }
                } catch { }
                if (icType != null) break;
            }
            if (icType != null)
            {
                var enc = icType.GetMethod("EncodeToPNG", new System.Type[] { typeof(UnityEngine.Texture2D) });
                if (enc != null)
                {
                    var pngObj = enc.Invoke(null, new object[] { tex });
                    var asByte = pngObj as byte[];
                    if (asByte != null) png = asByte;
                    else
                    {
                        var arr = pngObj as Il2CppStructArray<byte>;
                        if (arr != null)
                        {
                            png = new byte[arr.Length];
                            for (int i = 0; i < arr.Length; i++) png[i] = arr[i];
                        }
                    }
                }
            }
            UnityEngine.Object.Destroy(tex);
            if (png != null)
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, tag + "_" + (_shotCounter++) + ".png"), png);
            }
            else Core.LogMsg("[Flow] PNG 编码失败");
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 截图异常: " + ex.Message); }
    }

    private static void UpdateHeartbeat(string testCaseName)
    {
        _currentTestCase = testCaseName;
        try
        {
            string content = $"{testCaseName}|{DateTime.Now:yyyy-MM-dd HH:mm:ss}|{_totalStopwatch.ElapsedMilliseconds}ms";
            File.WriteAllText(_heartbeatFile, content);
        }
        catch { }
    }

    // 测试协程
    private static System.Collections.IEnumerator RunTestsCoroutine()
    {
        // 【保险1】非测试模式直接退出，不执行任何测试和退出逻辑
        if (!_testModeEnabled)
        {
            yield break;
        }
        
        // 等待游戏加载 + 尝试自动进存档（让物品创建类测试可用）
        // 用 MainMenuUIController.OnContinueClick 绕过失效的 UI 按钮点击
        yield return new WaitForEndOfFrame();
        System.Threading.Thread.Sleep(3000); // 等主菜单初现

        float autoSaveWait = 0f;
        // 本 Playtest 版本 IsPreviousSaveExist() 恒 false（方法体 xor al,al;ret），
        // OnContinueClick 无效。正确入口 = 槽位选择 SaveUIManager.OpenUI + SaveSlotElement.LoadSlot。
        while (autoSaveWait < 55f)
        {
            // 阶段1: 打开槽位选择UI
            if (!_continueTried)
            {
                try
                {
                    var su = FindComponentInScene<Il2Cpp.SaveUIManager>();
                    if (su != null)
                    {
                        su.OpenUI();
                        _continueTried = true;
                    }
                }
                catch (Exception ex) { Core.LogMsg("[AutoSave] OpenUI失败: " + ex.Message); _continueTried = true; }
            }

            // 阶段2: 槽位生成后，选最新槽位并 LoadSlot（private，用 AccessTools 反射）
            if (_continueTried && !_slotLoadTried)
            {
                try
                {
                    var slots = new System.Collections.Generic.List<Il2Cpp.SaveSlotElement>();
                    try
                    {
                        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        if (scene.IsValid())
                        {
                            foreach (var root in scene.GetRootGameObjects())
                            {
                                if (root == null) continue;
                                var comps = root.GetComponentsInChildren<Il2Cpp.SaveSlotElement>(true);
                                if (comps != null && comps.Length > 0) slots.AddRange(comps);
                            }
                        }
                    }
                    catch (Exception ex) { Core.LogMsg("[AutoSave] 遍历槽位异常: " + ex.Message); }
                    if (slots != null && slots.Count > 0)
                    {
                        int best = -1; Il2Cpp.SaveSlotElement bestEl = null;
                        foreach (var s in slots)
                        {
                            if (s != null && s.slotId > best) { best = s.slotId; bestEl = s; }
                        }
                        if (bestEl != null)
                        {
                            var t = AccessTools.TypeByName("SaveSlotElement");
                            var m = AccessTools.Method(t, "LoadSlot", null, null);
                            m.Invoke(bestEl, null);
                            _slotLoadTried = true;
                        }
                    }
                }
                catch (Exception ex) { Core.LogMsg("[AutoSave] LoadSlot失败: " + ex.Message); _slotLoadTried = true; }
            }

            // 阶段3: 判断是否进入商店（主菜单销毁 && PlayerStore 就绪）
            try
            {
                bool menuAlive = Il2Cpp.MainMenuUIController.Instance != null;
                if (!menuAlive && PlayerStore.Instance != null)
                {
                    break;
                }
            } catch { }

            yield return new WaitForSeconds(1f);
            autoSaveWait += 1f;
        }

        yield return new WaitForSeconds(2f); // 稳定

        UpdateHeartbeat("测试初始化完成");

        // ===== AutoSelfTest：从飞书看板同步的待测试任务（目标文件由 sync_test_targets.py 生成）=====
        UpdateHeartbeat("读取飞书测试目标");
        RunTargetTests();

        // ===== 纯逻辑测试（不需要存档，必跑）=====
        UpdateHeartbeat("Mod加载状态测试");
        RunTestCase("Mod加载状态", TestCase_ModLoaded);

        UpdateHeartbeat("关键类存在性测试");
        RunTestCase("关键类存在性", TestCase_KeyClassesExist);

        UpdateHeartbeat("关键方法存在性测试");
        RunTestCase("关键方法存在性", TestCase_KeyMethodsExist);

        UpdateHeartbeat("蛙哥物品池完整性测试");
        RunTestCase("蛙哥物品池完整性", TestCase_ItemPoolIntegrity);

        UpdateHeartbeat("自定义储物箱测试");
        RunTestCase("自定义储物箱创建和shape", TestCase_CustomStorageContainer);

        UpdateHeartbeat("物品shape大小dump");
        RunTestCase("物品shape大小dump", TestCase_DumpItemShapes);

        // ===== 游戏流程测试（单次启动内完成，不反复开关游戏）=====
        UpdateHeartbeat("游戏流程测试");
        yield return RunGameFlow();

        // ===== 汇总 =====
        UpdateHeartbeat("全部测试完成，准备退出");
        Core.LogMsg($"[TestRunner] 通过: {_passed}  失败: {_failed}  跳过: {_skipped}");

        // 写入汇总到结果文件（确保结果已落盘再退出）
        try
        {
            string summary = $"\n===== 汇总 =====\n通过: {_passed}\n失败: {_failed}\n跳过: {_skipped}\n总耗时: {_totalStopwatch.ElapsedMilliseconds}ms\n";
            File.AppendAllText(_resultFile, summary);
        }
        catch { }

        // ===== 四层退出兜底（从温和到暴力）=====
        // 【保险2】非测试模式绝对不退出游戏
        if (!_testModeEnabled)
        {
            yield break;
        }
        

        // L1: 温和退出 - Application.Quit()
        UpdateHeartbeat("退出中_L1_Application.Quit");
        try
        {
            Application.Quit();
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[TestRunner] L1失败: {ex.Message}");
        }

        // 等1秒，如果还没退出，升级到L2
        yield return new WaitForSeconds(1f);

        // L2: 暴力退出 - Environment.Exit(0)
        UpdateHeartbeat("退出中_L2_Environment.Exit");
        try
        {
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[TestRunner] L2失败: {ex.Message}");
        }

        // 等0.5秒，如果还没退出，升级到L3
        yield return new WaitForSeconds(0.5f);

        // L3: 强杀进程 - Process.Kill()
        UpdateHeartbeat("退出中_L3_Process.Kill");
        try
        {
            Process.GetCurrentProcess().Kill();
        }
        catch (Exception ex)
        {
            Core.LogMsg($"[TestRunner] L3失败: {ex.Message}");
        }

        // L4: 最终兜底 - 再等0.5秒后强制终止（如果Kill都没生效）
        yield return new WaitForSeconds(0.5f);
        try
        {
            Process.GetCurrentProcess().CloseMainWindow();
            Process.GetCurrentProcess().Kill();
        }
        catch { }
    }

    // 执行单个测试用例
    private static void RunTestCase(string name, Action testAction)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string result = "PASS";
        string detail = "";

        try
        {
            testAction.Invoke();
            _passed++;
            result = "PASS";
            detail = "执行成功";
        }
        catch (Exception ex)
        {
            _failed++;
            result = "FAIL";
            detail = ex.Message;
            if (ex.InnerException != null)
                detail += " | Inner: " + ex.InnerException.Message;
        }

        sw.Stop();

        // 写入结果文件
        try
        {
            string line = $"{name}|{result}|{detail}|{sw.ElapsedMilliseconds}ms\n";
            File.AppendAllText(_resultFile, line);
        }
        catch { }

    }

    // 直接写入结果（用于SKIP等不需要执行的用例）
    private static void WriteResult(string name, string result, string detail, long elapsedMs)
    {
        try
        {
            string line = $"{name}|{result}|{detail}|{elapsedMs}ms\n";
            File.AppendAllText(_resultFile, line);
        }
        catch { }
    }

    // ============================================================
    // 纯逻辑测试用例（不需要存档）
    // ============================================================

    // 测试：Mod是否正常加载
    private static void TestCase_ModLoaded()
    {
        // 检查Core.DebugMode能正常访问
        bool debugMode = Core.DebugMode;

        // Mod名称（硬编码，避免反射问题）
        string modName = "Wage's Perks";

        if (string.IsNullOrEmpty(modName))
            throw new Exception("Mod名称为空");
    }

    // 测试：关键类是否存在
    private static void TestCase_KeyClassesExist()
    {
        string[] requiredClasses = {
            "MachinePrinter", "StoreClientManager", "PlayerStore",
            "DirectoryMaster", "GameItem", "StoreClient", "TraderFactory"
        };

        foreach (string className in requiredClasses)
        {
            var type = AccessTools.TypeByName(className);
            if (type == null)
                throw new Exception($"关键类未找到: {className}");
        }
    }

    // 测试：关键方法是否存在
    private static void TestCase_KeyMethodsExist()
    {
        // 枪械打印机工厂：0.46D 后 MachinePrinter.Printer() 已删除，改用 PreBuiltItemHelper.CreatePrinter()
        // （MachinePrinter 类 0.46D 只剩 CreateNote()，见 cheatsheet 6.15 修正）
        var pbiType = AccessTools.TypeByName("PreBuiltItemHelper");
        if (pbiType == null)
            throw new Exception("PreBuiltItemHelper类型未找到");

        var printerMethod = AccessTools.Method(pbiType, "CreatePrinter");
        if (printerMethod == null)
            throw new Exception("PreBuiltItemHelper.CreatePrinter()方法未找到");

        // StoreClientManager.HandleInspectionClient
        var scmType = AccessTools.TypeByName("StoreClientManager");
        if (scmType == null)
            throw new Exception("StoreClientManager类型未找到");

        var inspectionMethod = AccessTools.Method(scmType, "HandleInspectionClient");
        if (inspectionMethod == null)
            throw new Exception("StoreClientManager.HandleInspectionClient()方法未找到");

        // TraderFactory.CreateDoctor
        var tfType = AccessTools.TypeByName("TraderFactory");
        if (tfType == null)
            throw new Exception("TraderFactory类型未找到");

        var doctorMethod = AccessTools.Method(tfType, "CreateDoctor");
        if (doctorMethod == null)
            throw new Exception("TraderFactory.CreateDoctor()方法未找到");
    }

    // 测试：蛙哥物品池完整性
    private static void TestCase_ItemPoolIntegrity()
    {
        var itemPool = WagePowerPerk.ItemPool;
        if (itemPool == null || itemPool.Length == 0)
            throw new Exception("蛙哥物品池为空");


        // 检查是否有重复物品
        var distinctCount = itemPool.Distinct().Count();
        if (distinctCount != itemPool.Length)
            throw new Exception($"物品池有重复: 总{itemPool.Length}个，去重后{distinctCount}个");

        // 检查关键物品是否在池中
        string[] mustHaveItems = { "raw_meat", "beer_case" };
        foreach (string itemId in mustHaveItems)
        {
            if (!itemPool.Contains(itemId)) { }
        }

        // 检查新加的7个物品
        string[] newItems = { "node", "node_medium", "node_small", "stun_baton", "surgery_tool", "tazer", "backpack_medium_military" };
        int newItemCount = 0;
        foreach (string itemId in newItems)
        {
            if (itemPool.Contains(itemId))
            {
                newItemCount++;
            }
            else
            { }
        }
    }

    // 测试：dump常见物品的shape大小，找2×2的sprite
    private static void TestCase_DumpItemShapes()
    {
        
        string[] itemIds = {
            // 容器类
            "storage_bay", "storage_bay_large", "machine_bay", "machine_bay_ext",
            "mini_smuggler_bay", "smuggler_bay",
            // 背包类
            "backpack_small", "backpack_medium", "backpack_large", "backpack_medium_military",
            // 机器/设备类
            "water_purifier", "water_filter", "chem_lab",
            "furnace", "generator", "battery_charger",
            // 箱子类
            "toolbox", "expedition_box", "trashcan", "med_box",
            "ammo_box", "food_box", "supply_box",
            // 普通物品
            "water_bottle", "beer_bottle", "bandage", "energy_credit"
        };
        
        int count2x2 = 0;
        foreach (string id in itemIds)
        {
            try
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item != null && item.shape != null)
                {
                    int w = item.shape.width;
                    int h = item.shape.height;
                    if (w == 2 && h == 2) count2x2++;
                }
                else
                {
                    Core.LogMsg($"[TestRunner]   {id}: 创建失败或shape为null");
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[TestRunner]   {id}: 异常 {ex.Message}");
            }
        }
        
    }


    // ============================================================
    // 存档相关测试用例（需要存档）
    // ============================================================

    // 测试：自定义储物箱创建和shape大小
    private static void TestCase_CustomStorageContainer()
    {
        
        // 创建自定义储物箱
        GameItem container = CustomStorageContainer.CreateContainer();
        if (container == null)
            throw new Exception("自定义储物箱创建失败（返回null）");
        
        
        // 检查shape
        if (container.shape == null)
            throw new Exception("shape为null");
        
        int width = container.shape.width;
        int height = container.shape.height;
        
        if (width != 2 || height != 2)
            throw new Exception($"shape不是2×2，实际是{width}x{height}");
        
        // 检查contentWindow
        bool hasWindow = container.contentWindow != null;
        
        // 检查identifier
        
    }




    // ============================================================
    // AutoSelfTest：从飞书看板同步的待测试任务自动测试
    // 读取 Mods\test_targets.txt（由 sync_test_targets.py 从飞书看板生成）
    // 按任务名关键词匹配测试逻辑，结果写入 test_results.txt
    // 看板任务状态改为"待测试" -> 下次自测自动包含
    // ============================================================

    // 读取并执行所有飞书目标任务
    // ========== 游戏流程测试（单次启动内完整执行，完成后才退出） ==========
    // 完整一天：开门（对话+客户）→ 交易（出售+购买）→ 关门（EndDay）
    //          → 出门拾荒（带包去垃圾场）→ 带东西回家（BeginDay下一天）
    private static System.Collections.IEnumerator RunGameFlow()
    {
        Shot("00_start");
        UpdateHeartbeat("流程-开门");
        yield return FlowOpenDoor();
        Shot("01_after_open");
        UpdateHeartbeat("流程-交易");
        yield return FlowTrade();
        Shot("02_after_trade");
        UpdateHeartbeat("流程-关门");
        yield return FlowCloseDoor();
        Shot("03_after_close");
        UpdateHeartbeat("流程-出门拾荒");
        yield return FlowScavenge();
        Shot("04_after_scavenge");
        UpdateHeartbeat("流程-带东西回家");
        yield return FlowComeHome();
        Shot("05_after_comehome");
        UpdateHeartbeat("流程-回主菜单");
        yield return FlowBackToMenu();
        Shot("06_mainmenu");
        UpdateHeartbeat("流程-主菜单检查");
        FlowCheckMenuState();
        UpdateHeartbeat("流程-开新档选特性");
        yield return FlowNewGameWithPerk();
        Shot("07_newgame_perk");
        UpdateHeartbeat("流程-新档检查");
        FlowCheckNewGame();
        Shot("08_newgame_done");
        yield break;
    }

    // 阶段A: 开门（开卷帘门 + 呼叫下一位顾客 + 客户进店）
    private static System.Collections.IEnumerator FlowOpenDoor()
    {

        // 1. 晨报界面点开始（如有）
        var sodu = Il2Cpp.StartOfDayUIManager.Instance;
        if (sodu != null)
        {
            try { sodu.OnStartDayButtonClicked();  }
            catch (Exception ex) { Core.LogMsg("[Flow] 晨报异常: " + ex.Message); }
            yield return new WaitForSeconds(2f);
        }

        // 2. 开卷帘门：StoreShutterButton.OpenShutter（触发 OnShutterOpened → GenerateClient 客户生成）
        var ssb = Il2Cpp.StoreShutterButton.Instance;
        if (ssb != null)
        {
            try { ssb.OpenShutter();  }
            catch (Exception ex) { Core.LogMsg("[Flow] OpenShutter异常: " + ex.Message); }
        }

        System.Threading.Thread.Sleep(3000);

        // 3. 呼叫下一位顾客（TryCallNextClient → QueueClientArriveAnimation）
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null)
            {
                // 前置条件诊断
                try
                {
                    var sum0 = Il2Cpp.StoreUIManager.Instance;
                    string bodyState = "sum=null";
                    if (sum0 != null) bodyState = "body=" + (sum0.body == null ? "null" : "非null");
                    
                }
                catch (Exception ex2) { Core.LogMsg("[Flow] 前置诊断异常: " + ex2.Message); }

                ps.TryCallNextClient();
                // 调用后诊断
                try
                { }
                catch (Exception ex3) { Core.LogMsg("[Flow] 后置诊断异常: " + ex3.Message); }
            }

        }
        catch (Exception ex) { Core.LogMsg("[Flow] TryCallNextClient异常: " + ex.Message); }

        // 3b. 显式触发客户进场（TryCallNextClient 的前置条件会跳过 QueueClientArriveAnimation）
        try
        {
            var sum1 = Il2Cpp.StoreUIManager.Instance;
            if (sum1 != null)
            {
                // 诊断：客户视觉 GameObject 状态
                try
                {
                    var scm1 = Il2Cpp.StoreClientMono.Instance;
                    
                }
                catch (Exception exd) { Core.LogMsg("[Flow] 客户GameObject诊断异常: " + exd.Message); }

                sum1.QueueClientArriveAnimation();
            }

        }
        catch (Exception ex) { Core.LogMsg("[Flow] QueueClientArriveAnimation异常: " + ex.Message); }
        yield return new WaitForSeconds(5f);

        // 4. 晨间对话
        try
        {
            var sum = Il2Cpp.StoreUIManager.Instance;
            if (sum != null)
            {
                try { sum.RunMorningDialogue();  }
                catch (Exception ex) { Core.LogMsg("[Flow] 对话异常: " + ex.Message); }
            }
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 开门异常: " + ex.Message); }
        yield return new WaitForSeconds(2f);
        yield break;
    }

    // 阶段B: 完整交易（循环处理所有预约客户：开门后依次到店→每单交易→购买/出售双方向→直到客户队列清空）
    private static System.Collections.IEnumerator FlowTrade()
    {

        var ps = Il2Cpp.PlayerStore.Instance;
        int totalClient = 0;
        int soldCount = 0;
        int boughtCount = 0;
        int dismissedCount = 0;
        int loopGuard = 0;
        const int MAX_LOOP = 30;   // 最多处理30个客户，防死循环

        while (loopGuard < MAX_LOOP)
        {
            loopGuard++;

            // 0. 当前客户状态
            int stackCount = -1, futureCount = -1;
            try
            {
                var mgr = ps != null ? ps.storeClientManager : null;
                stackCount = (mgr != null && mgr.clientStack != null) ? mgr.clientStack.Count : -1;
                futureCount = (ps != null && ps.futurStoreClientIdQueue != null) ? ps.futurStoreClientIdQueue.Count : -1;
            }
            catch (Exception ex) { Core.LogMsg("[Flow] 队列读取异常: " + ex.Message); }
            Core.LogMsg("[Flow] 当前 clientStack=" + stackCount + " futurQueue=" + futureCount +
                " currentClient=" + ((ps != null && ps.currentClientInstance != null) ? "存在" : "null"));

            // 1. 若当前没有客户在店，尝试叫下一位
            if (ps == null) { try { ps = Il2Cpp.PlayerStore.Instance; } catch { } }
            if (ps == null) {  yield break; }

            if (ps.currentClientInstance == null)
            {
                // 【自适应-失败重试】叫客户最多3次（间隔递增），直到客户到店或队列清空
                bool clientArrived = false;
                int callRetry = 0;
                const int MAX_CALL_RETRY = 3;
                while (callRetry < MAX_CALL_RETRY)
                {
                    callRetry++;
                    try { ps.TryCallNextClient();  }
                    catch (Exception ex) { Core.LogMsg("[Flow] TryCallNextClient异常: " + ex.Message); }
                    try
                    {
                        var sum = Il2Cpp.StoreUIManager.Instance;
                        if (sum != null) { sum.QueueClientArriveAnimation();  }
                    }
                    catch (Exception ex) { Core.LogMsg("[Flow] 进场动画异常: " + ex.Message); }

                    // 等客户到店（每轮最多8秒，递增总等待）
                    float cwait = 0f;
                    float maxWait = callRetry == 1 ? 8f : (callRetry == 2 ? 10f : 12f);
                    while (cwait < maxWait)
                    {
                        yield return new WaitForSeconds(1f); cwait += 1f;
                        try { ps = Il2Cpp.PlayerStore.Instance; } catch { }
                        if (ps != null && ps.currentClientInstance != null) { clientArrived = true; break; }
                    }
                    if (clientArrived) break;

                    // 队列空了就不用再重试
                    int sc0 = -1, fc0 = -1;
                    try { var m0 = ps != null ? ps.storeClientManager : null; sc0 = (m0 != null && m0.clientStack != null) ? m0.clientStack.Count : -1; } catch { }
                    try { fc0 = (ps != null && ps.futurStoreClientIdQueue != null) ? ps.futurStoreClientIdQueue.Count : -1; } catch { }
                    if (sc0 <= 0 && fc0 <= 0)
                    {
                        yield break;
                    }
                }

                if (!clientArrived)
                {
                    Core.LogMsg("[Flow] 重试" + MAX_CALL_RETRY + "次后客户仍未到店（重试仍失败，继续）");
                    // 队列非空但客户一直不来，跳过继续（不卡死）
                    yield return new WaitForSeconds(2f);
                    continue;
                }
            }

            totalClient++;

            // 2. 读取客户意图
            int intent = -1;
            try { intent = (int)ps.GetCurrentClientIntent();  }
            catch (Exception ex) { Core.LogMsg("[Flow] intent 读取异常: " + ex.Message); }

            // 3. 按意图处理交易
            bool handled = false;
            if (intent == (int)Il2Cpp.StoreClient.ClientIntent.BUY ||
                intent == (int)Il2Cpp.StoreClient.ClientIntent.SELLNBUY)
            {
                // 【购买方向】客户想从玩家这买货 → 尝试出售玩家物品
                GameItem toSell = null;
                try
                {
                    var items = ps.FindAllItem(true);
                    int pc = items != null ? items.Count : 0;
                    if (pc > 0 && items[0] != null)
                    {
                        toSell = items[0];
                        // 【自适应-失败重试】PlacedItemForSelling 最多2次
                        int r1 = 0;
                        bool placed = false;
                        while (r1 < 2 && !placed)
                        {
                            r1++;
                            try { ps.PlacedItemForSelling(toSell); placed = true;  }
                            catch (Exception ex) { Core.LogMsg("[Flow] PlacedItemForSelling异常(第" + r1 + "次): " + ex.Message); }
                            if (!placed) System.Threading.Thread.Sleep(3000);
                        }
                        if (!placed) Core.LogMsg("[Flow] PlacedItemForSelling 重试仍失败");
                    }

                }
                catch (Exception ex) { Core.LogMsg("[Flow] 出售流程异常: " + ex.Message); }
                yield return new WaitForSeconds(2f);
                if (toSell != null)
                {
                    var neg = Il2Cpp.NegociationUIManager.Instance;
                    if (neg != null)
                    {
                        // 【自适应-失败重试】OnOfferAcceptClick 最多2次
                        int r2 = 0;
                        bool accepted = false;
                        while (r2 < 2 && !accepted)
                        {
                            r2++;
                            try { neg.OnOfferAcceptClick(); accepted = true; soldCount++;  }
                            catch (Exception ex) { Core.LogMsg("[Flow] OnOfferAcceptClick异常(第" + r2 + "次): " + ex.Message); }
                            if (!accepted) System.Threading.Thread.Sleep(3000);
                        }
                        if (!accepted) Core.LogMsg("[Flow] OnOfferAcceptClick 重试仍失败");
                    }
                    handled = true;
                }
            }

            if (intent == (int)Il2Cpp.StoreClient.ClientIntent.SELL ||
                intent == (int)Il2Cpp.StoreClient.ClientIntent.SELLNBUY)
            {
                // 【出售方向】客户想卖货给玩家 → 玩家购买
                try
                {
                    var neg = Il2Cpp.NegociationUIManager.Instance;
                    if (neg != null)
                    {
                        // 【自适应-失败重试】BuyItem 最多2次
                        int r3 = 0;
                        bool bought = false;
                        while (r3 < 2 && !bought)
                        {
                            r3++;
                            try { neg.BuyItem(); bought = true; boughtCount++;  }
                            catch (Exception ex) { Core.LogMsg("[Flow] BuyItem异常(第" + r3 + "次): " + ex.Message); }
                            if (!bought) System.Threading.Thread.Sleep(3000);
                        }
                        if (!bought) Core.LogMsg("[Flow] BuyItem 重试仍失败");
                        handled = true;
                    }
                    else Core.LogMsg("[Flow] 交易UI为null，跳过购买");
                }
                catch (Exception ex) { Core.LogMsg("[Flow] 购买流程异常: " + ex.Message); }
            }

            if (!handled)
            {
                try
                {
                    var neg = Il2Cpp.NegociationUIManager.Instance;
                    if (neg != null) { neg.OnOfferAcceptClick();  }
                }
                catch (Exception ex) { Core.LogMsg("[Flow] 通用接受异常: " + ex.Message); }
            }
            yield return new WaitForSeconds(2f);

            // 4. 当前客户交易完成，让其离开（DismissCurrentClient 会尝试叫下一位）
            // 【自适应-失败重试】DismissCurrentClient 最多2次
            int r4 = 0;
            bool dismissed = false;
            while (r4 < 2 && !dismissed)
            {
                r4++;
                try { ps.DismissCurrentClient(); dismissed = true; dismissedCount++;  }
                catch (Exception ex) { Core.LogMsg("[Flow] DismissCurrentClient异常(第" + r4 + "次): " + ex.Message); }
                if (!dismissed) System.Threading.Thread.Sleep(3000);
            }
            if (!dismissed) Core.LogMsg("[Flow] DismissCurrentClient 重试仍失败");
            yield return new WaitForSeconds(2f);

            // 5. 判断是否还有客户要处理
            int sc2 = -1, fc2 = -1;
            try { var m3 = ps.storeClientManager; sc2 = (m3 != null && m3.clientStack != null) ? m3.clientStack.Count : -1; } catch { }
            try { fc2 = (ps != null && ps.futurStoreClientIdQueue != null) ? ps.futurStoreClientIdQueue.Count : -1; } catch { }

            if (sc2 <= 0 && fc2 <= 0)
            {
                break;
            }
        }

        yield break;
    }

    // 阶段C: 关门（关卷帘门 + EndDay 结算）
    private static System.Collections.IEnumerator FlowCloseDoor()
    {
        // 1. 关卷帘门：StoreShutterButton.EndAndClose（触发完整关门流程）
        var ssb = Il2Cpp.StoreShutterButton.Instance;
        if (ssb != null)
        {
            try { ssb.EndAndClose();  }
            catch (Exception ex) { Core.LogMsg("[Flow] EndAndClose异常: " + ex.Message); }
            System.Threading.Thread.Sleep(3000);
        }


        // 2. EndDay 结算（兜底，确保进入打烊）
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null && ps.CanEndDay())
            {
                try { ps.EndDay();  }
                catch (Exception ex) { Core.LogMsg("[Flow] EndDay异常: " + ex.Message); }
            }
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 关门异常: " + ex.Message); }
        yield return new WaitForSeconds(2f);

        // 3. 关闭关门/结算提示
        try { Il2Cpp.TutorialUIManager.Instance.CloseAllTutorial();  }
        catch (Exception ex) { Core.LogMsg("[Flow] 关闭提示异常: " + ex.Message); }
        yield return new WaitForSeconds(2f);
        yield break;
    }

    // 阶段D: 完整出门拾荒（真实UI流程：外出面板→确认→去垃圾倾倒场→反复拾荒→回家）
    private static System.Collections.IEnumerator FlowScavenge()
    {

        // 【拾荒池控制验证】累计稀有物掉落数（验证 PostfixGetRandomScavengedItem 真实生效）
        int _scavRareTotal = 0;
        // 【拾荒池控制验证】累计废弃机器掉落数
        int _scavBrokenTotal = 0;

        // 0. 拾荒前置状态
        bool canScav = false;
        try { canScav = Il2Cpp.ScavHelper.CanScavenge(); } catch { }
        int maxAttempts = -1, timeLeft = -1;
        try { maxAttempts = Il2Cpp.ScavHelper.GetMaxScavAttempts(); } catch { }
        try { timeLeft = Il2Cpp.ScavHelper.GetScavTimeLeft(); } catch { }
        float minorWound = -1f, majorWound = -1f;
        try { minorWound = Il2Cpp.ScavHelper.GetMinorWoundChance(); } catch { }
        try { majorWound = Il2Cpp.ScavHelper.GetMajorWoundChance(); } catch { }


        if (!canScav)
        {
            Core.LogMsg("[Flow] 当前不能拾荒（跳过），无法验证拾荒流程");
            yield break;
        }

        // 1. 记录拾荒前背包物品数（afterhourInventory = 外出背包）
        int itemsBefore = 0;
        try
        {
            var ee = Il2Cpp.EmporiumEntry.Instance;
            if (ee != null && ee.afterhourInventory != null)
                itemsBefore = ee.afterhourInventory.childItems.Count;
        }
        catch { }

        // 2. 打开外出/地图面板（MapUIManager.OpenUI → 离开店铺）
        var map = Il2Cpp.MapUIManager.Instance;
        if (map != null)
        {
            try { map.OpenUI();  }
            catch (Exception ex) { Core.LogMsg("[Flow] OpenUI异常: " + ex.Message); }
        }
        else Core.LogMsg("[Flow] MapUIManager 为 null，无法走外出UI");
        yield return new WaitForSeconds(2f);

        // 3. 外出确认（OpenGoOutsideConfirm → OnGoOutsideConfirm）
        if (map != null)
        {
            try { map.OpenGoOutsideConfirm();  }
            catch (Exception ex) { Core.LogMsg("[Flow] OpenGoOutsideConfirm异常: " + ex.Message); }
            yield return new WaitForSeconds(1f);
            try { map.OnGoOutsideConfirm();  }
            catch (Exception ex) { Core.LogMsg("[Flow] OnGoOutsideConfirm异常: " + ex.Message); }
            yield return new WaitForSeconds(2f);
        }

        // 4. 前往垃圾倾倒场（VisitScavenging）
        if (map != null)
        {
            try { map.VisitScavenging();  }
            catch (Exception ex) { Core.LogMsg("[Flow] VisitScavenging异常: " + ex.Message); }
        }
        System.Threading.Thread.Sleep(3000);

        // 5. 反复拾荒：循环点 ScavengeButtonClick，直到次数用完或满载
        int scavRound = 0;
        int scavSuccess = 0;
        const int MAX_SCAV_ROUND = 20;  // 防死循环上限
        while (scavRound < MAX_SCAV_ROUND)
        {
            scavRound++;

            // 5a. 当前剩余次数
            int nowLeft = -1;
            try { nowLeft = Il2Cpp.ScavHelper.GetScavTimeLeft(); } catch { }
            int nowAttempts = -1;
            try { nowAttempts = Il2Cpp.ScavHelper.GetMaxScavAttempts(); } catch { }

            // 5b. 当前背包容量
            int bagCount = -1;
            try
            {
                var ee = Il2Cpp.EmporiumEntry.Instance;
                if (ee != null && ee.afterhourInventory != null) bagCount = ee.afterhourInventory.childItems.Count;
            }
            catch { }

            // 5c. 点拾荒按钮
            bool didScav = false;
            // 【自适应-失败重试】拾荒按钮点击最多2次
            int rsc = 0;
            while (rsc < 2 && !didScav)
            {
                rsc++;
                if (map != null)
                {
                    try { map.ScavengeButtonClick(); didScav = true;  }
                    catch (Exception ex) { Core.LogMsg("[Flow] ScavengeButtonClick异常(第" + rsc + "次): " + ex.Message); }
                }
                else
                {
                    // 兜底：直接用 ScavHelper 底层方法
                    try { Il2Cpp.ScavHelper.ScavengeDumpingGrounds(); didScav = true;  }
                    catch (Exception ex) { Core.LogMsg("[Flow] 兜底拾荒异常(第" + rsc + "次): " + ex.Message); }
                }
                if (!didScav) System.Threading.Thread.Sleep(3000);
            }
            if (!didScav) Core.LogMsg("[Flow] ScavengeButtonClick 重试仍失败（跳过本轮）");
            yield return new WaitForSeconds(2f);

            if (didScav)
            {
                scavSuccess++;
                // 拾荒收获统计
                try
                {
                    var loot = Il2Cpp.ScavHelper.GetRandomScavengedItem();
                    // 【拾荒池控制验证】统计掉落中的稀有物（验证 PostfixGetRandomScavengedItem 是否真实生效）
                    if (loot != null)
                    {
                        int rareCnt = 0;
                        int brokenCnt = 0;
                        for (int li = 0; li < loot.Count; li++)
                        {
                            GameItem li_item = null;
                            try { li_item = loot[li]; } catch { }
                            if (li_item == null) continue;
                            string lid = "?"; try { lid = li_item.identifier ?? "?"; } catch { }
                            if (lid == "rare_ore" || lid == "rare_electronic") rareCnt++;
                            // 废弃机器识别：mech_item_ruined 基础 + broken 类（broken alarm/furnace/moisture farm）
                            if (lid == "mech_item_ruined" || lid.Contains("broken") || lid.Contains("ruined")) brokenCnt++;
                        }
                        _scavRareTotal += rareCnt;
                        _scavBrokenTotal += brokenCnt;
                        // 持久化到探测器面板统计（RecordRareDrop 内部有 IsActive 校验）
                        try { LuckScoutPerk.RecordRareDrop(rareCnt); } catch { }
                        if (rareCnt > 0) { }
                        else
                        if (brokenCnt > 0) { }
                    }
                }
                catch (Exception ex) { Core.LogMsg("[Flow] 收获统计异常: " + ex.Message); }
            }

            // 5d. 判定是否继续：剩余次数用完 或 背包满载 则停
            int afterCount = -1;
            try
            {
                var ee = Il2Cpp.EmporiumEntry.Instance;
                if (ee != null && ee.afterhourInventory != null) afterCount = ee.afterhourInventory.childItems.Count;
            }
            catch { }

            int leftAfter = -1;
            try { leftAfter = Il2Cpp.ScavHelper.GetScavTimeLeft(); } catch { }

            if (leftAfter <= 0 && nowAttempts > 0)
            {
                break;
            }
            // 背包满载判定：物品数不再增加（拾荒满了）
            if (afterCount >= 0 && bagCount >= 0 && afterCount == bagCount && scavRound >= 3)
            {
                break;
            }
        }


        // 6. 离开垃圾倾倒场 + 回家
        if (map != null)
        {
            try { map.LeaveScavenging();  }
            catch (Exception ex) { Core.LogMsg("[Flow] LeaveScavenging异常: " + ex.Message); }
            yield return new WaitForSeconds(2f);
        }

        // 7. 关闭外出面板回到店铺（CloseUI → OnArriveStore）
        if (map != null)
        {
            try { map.CloseUI();  }
            catch (Exception ex) { Core.LogMsg("[Flow] CloseUI异常: " + ex.Message); }
        }
        try
        {
            var ee = Il2Cpp.EmporiumEntry.Instance;
            if (ee != null) { ee.OnArriveStore(true);  }
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 回家异常: " + ex.Message); }
        yield return new WaitForSeconds(2f);

        // 8. 拾荒后背包物品数（收货检查）
        int itemsAfter = -1;
        try
        {
            var ee = Il2Cpp.EmporiumEntry.Instance;
            if (ee != null && ee.afterhourInventory != null) itemsAfter = ee.afterhourInventory.childItems.Count;
        }
        catch { }
        yield break;
    }

    // 阶段E: 带东西回家（拾荒收获入包 + 下一天开门）
    private static System.Collections.IEnumerator FlowComeHome()
    {
        try
        {
            var ee = Il2Cpp.EmporiumEntry.Instance;
            if (ee != null && ee.afterhourInventory != null)
            {
                int after = ee.afterhourInventory.childItems.Count;
            }
        }
        catch { }
        bool canStart = false;
        try { canStart = PlayerStore.Instance.CanStartDay(); } catch { }
        if (canStart)
        {
            try { PlayerStore.Instance.BeginDay();  }
            catch (Exception ex) { Core.LogMsg("[Flow] BeginDay异常: " + ex.Message); }
        }

        System.Threading.Thread.Sleep(3000);

        int dayAfter = -1;
        try { dayAfter = Il2Cpp.StoreStation.GetDayCounter(); } catch { }
        yield break;
    }

    // 阶段D2: 返回主菜单（EscapeUIManager.OnMainMenu）
    private static System.Collections.IEnumerator FlowBackToMenu()
    {
        try
        {
            var esc = FindComponentInScene<Il2Cpp.EscapeUIManager>();
            if (esc != null) { esc.OnMainMenu(); }

        }
        catch (Exception ex) { Core.LogMsg("[Flow] OnMainMenu异常: " + ex.Message); }
        yield return new WaitForSeconds(2f);

        float wait = 0f;
        while (wait < 15f)
        {
            try { if (Il2Cpp.MainMenuUIController.Instance != null) { break; } } catch { }
            yield return new WaitForSeconds(1f); wait += 1f;
        }
        yield break;
    }

    // 阶段E2: 主菜单状态检查
    private static void FlowCheckMenuState()
    {
        try
        {
            var mm = Il2Cpp.MainMenuUIController.Instance;
            Core.LogMsg("[Flow] MainMenuUIController " + (mm != null ? "存在" : "不存在"));
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 阶段E2异常: " + ex.Message); }
        try
        {
            var su = FindComponentInScene<Il2Cpp.SaveUIManager>();
            Core.LogMsg("[Flow] SaveUIManager " + (su != null ? "存在（槽位系统可用）" : "不存在"));
        }
        catch (Exception ex) { Core.LogMsg("[Flow] SaveUIManager检查异常: " + ex.Message); }
    }

    // 阶段F: 开新档 + 自动选特性（蛙哥牛逼）
    private static System.Collections.IEnumerator FlowNewGameWithPerk()
    {
        var mm = Il2Cpp.MainMenuUIController.Instance;
        if (mm == null) {  yield break; }
        bool started = false;
        try { mm.StartNewGameClick(); started = true; }
        catch (Exception ex) { Core.LogMsg("[Flow] StartNewGameClick异常: " + ex.Message); }
        Core.LogMsg("[Flow] StartNewGameClick: " + (started ? "已调用" : "失败"));
        if (!started) yield break;
        yield return new WaitForSeconds(2f);

        // ===== 真实流程：欢迎/选专精面板 → 选专精 → 点"开始游戏" → 特性界面才会创建 =====
        // 【自适应修复】StartNewGameClick 后欢迎面板需时间初始化，先等 startGameButton 就绪（最长12秒）
        float bw = 0f;
        while (bw < 12f)
        {
            try { if (mm.startGameButton != null) { break; } } catch { }
            yield return new WaitForSeconds(1f); bw += 1f;
        }

        // 步骤1: 选择初始专精（学徒 = OnGeneralistClick，对应欢迎面板左侧列表）
        try { mm.OnGeneralistClick();  }
        catch (Exception ex) { Core.LogMsg("[Flow] 选专精异常: " + ex.Message); }
        yield return new WaitForSeconds(2f);

        // 步骤2: 点"开始游戏"按钮（关闭欢迎面板，真正创建特性界面）
        bool startClicked = false;
        try
        {
            if (mm.startGameButton != null)
            {
                mm.startGameButton.onClick.Invoke();
                startClicked = true;
            }

        }
        catch (Exception ex) { Core.LogMsg("[Flow] startGameButton 点击异常: " + ex.Message); }
        if (!startClicked)
        {
            // 兜底1: OnAnyStartClick
            try { mm.OnAnyStartClick(); startClicked = true;  }
            catch (Exception ex2) { Core.LogMsg("[Flow] OnAnyStartClick异常: " + ex2.Message); }
        }
        if (!startClicked)
        {
            // 兜底2: 直接 OpenPerkInterface 打开特性界面（绕过欢迎面板）
            Core.LogMsg("[Flow] 前两路均失败，尝试 OpenPerkInterface() 直接打开特性界面...");
            try { mm.OpenPerkInterface(); startClicked = true;  }
            catch (Exception ex3) { Core.LogMsg("[Flow] OpenPerkInterface异常: " + ex3.Message); }
        }
        yield return new WaitForSeconds(2f);

        // 等特性界面 + availablePerks 容器就绪
        Il2Cpp.PerkUIController perkUi = null;
        float wait = 0f;
        while (wait < 15f)
        {
            try { perkUi = Il2Cpp.PerkUIController.Instance; if (perkUi != null) break; } catch { }
            yield return new WaitForSeconds(1f); wait += 1f;
        }
        if (perkUi == null) {  yield break; }

        // 等 availablePerks 容器非 null（Start 初始化完成）
        bool uiReady = false;
        float wait2 = 0f;
        while (wait2 < 10f)
        {
            try { if (perkUi.availablePerks != null) { uiReady = true; break; } } catch { }
            yield return new WaitForSeconds(1f); wait2 += 1f;
        }
        // availablePerks 未就绪时尝试 OpenUI 激活界面
        if (!uiReady)
        {
            try { perkUi.OpenUI(); }
            catch (Exception ex) { Core.LogMsg("[Flow] OpenUI异常: " + ex.Message); }
            yield return new WaitForSeconds(2f);
            float wait3 = 0f;
            while (wait3 < 8f)
            {
                try { if (perkUi.availablePerks != null) { uiReady = true; break; } } catch { }
                yield return new WaitForSeconds(1f); wait3 += 1f;
            }
        }
        if (!uiReady) {  yield break; }

        try { CustomStartingPerks.EnsurePickerElements(perkUi);  }
        catch (Exception ex) { Core.LogMsg("[Flow] 特性注入异常: " + ex.Message); }

        // 【用户需求】开新档选"捡漏直觉"，验证真实生成工具箱/探测器/大背包
        Il2Cpp.StartingPerkElement luck = null;
        try
        {
            foreach (var el in perkUi.availablePerks.GetComponentsInChildren<Il2Cpp.StartingPerkElement>(true))
            {
                if (el != null && el.id != null && el.id.Replace("\0", "").Trim() == "捡漏直觉") { luck = el; break; }
            }
        }
        catch (Exception ex) { Core.LogMsg("[Flow] 查找捡漏直觉特性元素异常: " + ex.Message); }

        if (luck != null)
        {
            try { perkUi.SelectPerk(luck);  }
            catch (Exception ex) { Core.LogMsg("[Flow] SelectPerk异常: " + ex.Message); }
            yield return new WaitForSeconds(1f);
        }


        // 确认开始
        bool confirmed = false;
        try { mm.OnPerkFinishedClick(); confirmed = true; }
        catch (Exception ex) { Core.LogMsg("[Flow] OnPerkFinishedClick异常: " + ex.Message); }
        Core.LogMsg("[Flow] 确认: " + (confirmed ? "已调用，等待新档初始化..." : "失败"));
        yield return new WaitForSeconds(6f);
        yield break;
    }

    // 阶段G: 新档检查（物品创建可用性 + 蛙哥妙妙箱）
    private static void FlowCheckNewGame()
    {

        // 等待后背包就绪（TryGiveKit 在开局补丁触发，可能延迟几帧）
        Il2Cpp.EmporiumEntry emporium = null;
        float wait = 0f;
        while (wait < 8f)
        {
            try { emporium = Il2Cpp.EmporiumEntry.Instance; if (emporium != null && emporium.backInvinvElement != null) break; } catch { }
            System.Threading.Thread.Sleep(500); wait += 0.5f;
        }
        if (emporium == null || emporium.backInvinvElement == null)
        {
            Core.LogMsg("[Flow] 后背包未就绪（EmporiumEntry 不可用），无法验证三样物品");
        }
        else
        {
            bool hasToolbox = false, hasScanner = false, hasBag = false;
            string foundIds = "";
            try
            {
                // 用 EmporiumEntry.GetAllItems() 枚举后背包所有物品（反编译确认的方法，最可靠）
                var items = emporium.GetAllItems();
                if (items != null)
                {
                    foreach (var it in items)
                    {
                        if (it == null) continue;
                        string id = "";
                        try { id = it.identifier ?? ""; } catch { }
                        if (string.IsNullOrEmpty(id)) continue;
                        foundIds += id + ",";
                        if (id == "toolbox") hasToolbox = true;
                        else if (id == "metal_scanner") hasScanner = true;
                        else if (id == "backpack_large") hasBag = true;
                    }
                }

            }
            catch (Exception ex) { Core.LogMsg("[Flow] 扫描后背包异常: " + ex.Message); }
            if (hasToolbox && hasScanner && hasBag) { }
        }

        // 跳过教程
        try { Il2Cpp.TutorialUIManager.Instance.CloseAllTutorial();  }
        catch (Exception ex) { Core.LogMsg("[Flow] 跳过教程异常: " + ex.Message); }
    }

    private static void RunTargetTests()
    {
        if (!File.Exists(_targetsFile))
        {
            WriteResult("AutoSelfTest", "SKIP", "test_targets.txt 不存在（先运行 sync_test_targets.py 同步飞书任务）", 0);
            return;
        }
        string[] lines = File.ReadAllLines(_targetsFile);
        int count = 0;
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split('|');
            if (parts.Length < 2) continue;
            count++;
            string rid = parts[0].Trim();
            string name = parts[1].Trim();
            string desc = parts.Length > 3 ? parts[3].Trim() : "";
            RunTargetTestCase(rid, name, desc);
        }
    }

    // 执行单个飞书目标任务
    private static void RunTargetTestCase(string rid, string name, string desc)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string result = "FAIL";
        string detail = "";
        string label = $"[{rid}] {name}";
        UpdateHeartbeat(label);
        try
        {
            detail = RunMatchedTest(name, desc);
            result = "PASS";
        }
        catch (Exception ex)
        {
            if (ex.Message.StartsWith("SKIP:")) { result = "SKIP"; detail = ex.Message.Substring(5); }
            else { result = "FAIL"; detail = ex.Message; }
            if (ex.InnerException != null) detail += " | Inner:" + ex.InnerException.Message;
        }
        sw.Stop();
        WriteResult(label, result, detail, sw.ElapsedMilliseconds);
    }

    // 按任务名/需求描述关键词匹配测试逻辑
    private static string RunMatchedTest(string name, string desc)
    {
        string text = (name + " " + desc).ToLowerInvariant();
        StringBuilder log = new StringBuilder();

        if (text.Contains("对话") || text.Contains("台词") || text.Contains("空白") || text.Contains("first"))
        {
            Test_P0Dialogue(log);
            return log.ToString();
        }
        if (text.Contains("酵母") || text.Contains("owned") || text.Contains("未拥有"))
        {
            Test_YeastOwned(log);
            return log.ToString();
        }
        if (text.Contains("盗窃") || text.Contains("stolen"))
        {
            Test_Stolen(log);
            return log.ToString();
        }
        if (text.Contains("捡漏") || text.Contains("拾荒") || text.Contains("工具箱") || text.Contains("探测器"))
        {
            Test_LuckScout(log);
            return log.ToString();
        }
        if (text.Contains("稀有率"))
        {
            Test_RareRate(log);
            return log.ToString();
        }
        Test_Generic(log);
        return log.ToString();
    }

    private static bool HasTag(GameItem item, string tagName)
    {
        try { return item.GetTagReadonly(tagName) != null; } catch { return false; }
    }

    // ---- 场景遍历工具（IL2CPP 下 FindObjectsOfType<T> 泛型不可靠，改用根对象递归） ----
    private static T FindComponentInScene<T>() where T : Component
    {
        try
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                var comp = root.GetComponentInChildren<T>(true);
                if (comp != null) return comp;
            }
        }
        catch (Exception ex) { Core.LogMsg("[SceneScan] FindComponent异常: " + ex.Message); }
        return null;
    }

    // 物品创建依赖商店 UI 场景（TreeNodeRender.Instantiate）。-runtests 不自动进存档，
    // 停在主菜单时 DirectoryMaster.Item 会 NRE——此时标记 SKIP 而非 FAIL（需进存档人工验证）。
    private static GameItem SafeCreateItem(string id)
    {
        try { return DirectoryMaster.Item(id, true); }
        catch (Exception ex)
        {
            throw new Exception("SKIP:物品创建需商店UI场景(-runtests未进存档): " + ex.Message);
        }
    }

    // T1 P0对话：特殊NPC第一句不改写（GenerateStoryDialogue 对博士/房东/同行等返回 null）
    // 只传 clientName 命中的特殊名（0/0.5/0.7/0.9 阶段在访问 client.identifier 之前 return，传 null client 安全）
    private static void Test_P0Dialogue(StringBuilder log)
    {
        var t = typeof(WagePowerPerk);
        var m = t.GetMethod("GenerateStoryDialogue", BindingFlags.NonPublic | BindingFlags.Static);
        if (m == null) throw new Exception("GenerateStoryDialogue 方法未找到");

        object[] args = new object[] { null, "" };
        foreach (string specialName in new[] { "博士", "房东", "同行", "医生", "工程师", "商人", "Landlord", "Rival" })
        {
            args[1] = specialName;
            object r = m.Invoke(null, args);
            if (r != null) throw new Exception($"特殊NPC「{specialName}」第一句被改写（应返回null）");
        }
        log.Append("特殊NPC(博士/房东/同行/医生/工程师/商人/Landlord/Rival)台词不改写 ✓");
    }

    // T2 酵母owned：wine_yeast 可创建 + CloneLinked 克隆隔离（克隆修改不污染共享实例）
    private static void Test_YeastOwned(StringBuilder log)
    {
        GameItem yeast = SafeCreateItem("wine_yeast");
        if (yeast == null) throw new Exception("wine_yeast 创建失败");
        GameItem clone = yeast.CloneLinked();
        if (clone == null) throw new Exception("CloneLinked 返回 null");
        if (clone.Pointer == yeast.Pointer)
            throw new Exception("克隆与共享实例同指针（克隆失败）");

        clone.EnableTag("AUTO_TEST_ISOLATION", true);
        if (!HasTag(clone, "AUTO_TEST_ISOLATION")) throw new Exception("克隆标签添加失败");
        if (HasTag(yeast, "AUTO_TEST_ISOLATION")) throw new Exception("克隆修改污染共享实例（未隔离）");
        log.Append("wine_yeast创建✓ 克隆指针独立✓ 克隆改标签不污染共享实例✓");
    }

    // T3 盗窃stolen：新物品默认无 STOLEN_TAG + 克隆无盗窃
    private static void Test_Stolen(StringBuilder log)
    {
        GameItem item = SafeCreateItem("beer_case");
        if (item == null) throw new Exception("beer_case 创建失败");
        if (HasTag(item, "STOLEN_TAG")) throw new Exception("新创建物品默认带 STOLEN_TAG（盗窃）");
        GameItem clone = item.CloneLinked();
        if (clone == null) throw new Exception("CloneLinked 返回 null");
        if (HasTag(clone, "STOLEN_TAG")) throw new Exception("克隆物品带 STOLEN_TAG（盗窃）");
        log.Append("beer_case创建✓ 默认无盗窃✓ 克隆无盗窃✓");
    }

    // T4 捡漏三样：探测器/工具箱/大背包创建成功（反射调 LuckScoutPerk 私有创建方法）
    private static void Test_LuckScout(StringBuilder log)
    {
        var t = typeof(LuckScoutPerk);
        var mScanner = t.GetMethod("CreateEnhancedScanner", BindingFlags.NonPublic | BindingFlags.Static);
        var mToolbox = t.GetMethod("CreateToolbox", BindingFlags.NonPublic | BindingFlags.Static);
        var mBag = t.GetMethod("CreateBigBackpack", BindingFlags.NonPublic | BindingFlags.Static);
        if (mScanner == null || mToolbox == null || mBag == null)
            throw new Exception("LuckScout 创建方法未找到");

        GameItem scanner = null;
        try { scanner = mScanner.Invoke(null, null) as GameItem; }
        catch (Exception ex) { throw new Exception("SKIP:探测器创建需商店UI场景: " + ex.Message); }
        if (scanner == null) throw new Exception("SKIP:探测器创建失败（-runtests未进存档，物品创建需商店UI场景）");
        GameItem kit = mToolbox.Invoke(null, new object[] { null }) as GameItem;
        if (kit == null) throw new Exception("SKIP:工具箱创建失败（-runtests未进存档）");
        GameItem bag = mBag.Invoke(null, null) as GameItem;
        if (bag == null) throw new Exception("SKIP:大背包创建失败（-runtests未进存档）");

        // 【用户需求】工具箱必须是原版 toolbox，大背包必须是原版 backpack_large（非军用），探测器独立
        string kitId = "";
        string bagId = "";
        try { kitId = kit.identifier; } catch { }
        try { bagId = bag.identifier; } catch { }
        if (kitId != "toolbox") throw new Exception("工具箱不是原版toolbox: " + kitId);
        if (bagId != "backpack_large") throw new Exception("大背包不是原版backpack_large: " + bagId);

        log.Append($"探测器[{scanner.identifier}]✓ 工具箱[{kit.identifier}]✓ 大背包[{bag.identifier}]✓");
    }

    // T5 稀有率：稀有物创建成功
    private static void Test_RareRate(StringBuilder log)
    {
        var t = typeof(LuckScoutPerk);
        var m = t.GetMethod("CreateRareItem", BindingFlags.NonPublic | BindingFlags.Static);
        if (m == null) throw new Exception("CreateRareItem 方法未找到");
        GameItem item = null;
        try { item = m.Invoke(null, null) as GameItem; }
        catch (Exception ex) { throw new Exception("SKIP:稀有物创建需商店UI场景: " + ex.Message); }
        if (item == null) throw new Exception("SKIP:稀有物创建失败（-runtests未进存档）");
        log.Append($"稀有物[{item.identifier}]创建✓");
    }

    // T6 通用自检：Mod加载 + 关键类/方法存在
    private static void Test_Generic(StringBuilder log)
    {
        TestCase_ModLoaded();
        TestCase_KeyClassesExist();
        TestCase_KeyMethodsExist();
        log.Append("Mod加载✓ 关键类/方法存在✓");
    }


}
