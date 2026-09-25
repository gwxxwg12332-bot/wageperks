using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;
partial class WageSaveStore

{
    // ===== Lifecycle =====

    /// <summary>全局打烊落盘（PlayerStore.SaveGame Postfix，Core.cs 注册）。
    /// 所有系统的内存状态统一落盘——不依赖任何特性是否选中，存储层自治。</summary>
    public static void PostfixSaveGame()
    {
        Flush();
    }

    // ===================== ③ 读档 =====================

    /// <summary>读档（PlayerStore.LoadGame Postfix 调）。
    /// 键值文件同步读取（键值不依赖容器就绪）——消除"清缓存→30帧后读文件"空窗口：
    /// 空窗口内 GetStat/GetBlood 会把默认值物化进缓存 → 读档状态被刷丢（蛙娘六维刷0 / 鲁滨逊血量刷满）。
    /// 容器引用恢复仍走延迟 LoadIfPending → OnGameLoaded（childItems 就绪后）。</summary>
    internal static void OnLoadGame()
    {
        try
        {
            ClearMem();
            _loadingComplete = false; // 09-26 读档期间禁止写入，防止空窗期默认值污染存档
            _pendingLoad = true;
            _pendingLoadFrames = 0;
            _loadedOnce = false;
            TryDoLoad(); // runID 未就绪时返回 false，由轮询下帧补读
            Core.LogMsg("[SaveStore] 读档挂点触发，键值" + (_loadedOnce ? "已同步加载" : "待轮询补读"));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSaveStore] 异常: " + ex.Message); }
    }

    /// <summary>轮询驱动（每帧调，轻量）。文件未读则下帧补读；已读则只等延迟帧驱动 OnGameLoaded。</summary>
    internal static void LoadIfPending()
    {
        if (!_pendingLoad) return;
        try
        {
            if (!_loadedOnce)
            {
                TryDoLoad(); // runID 补就绪后读文件（空窗口 ≤1 帧）
                if (!_loadedOnce) return;
            }
            if (++_pendingLoadFrames < LOAD_DELAY_FRAMES) return;
            _pendingLoad = false;
            // 阶段2：文件数据就绪后驱动全部特性 OnGameLoaded。
            // 挂点必须在**这里**而不是 PlayerStore.LoadGame Postfix —— 后者执行时容器 childItems 尚未就绪，
            // 直接恢复引用类型必失败（阶段1 已用血泪验证，见 WageSaveStore.cs 顶部时序说明）。
            try { CustomStartingPerks.NotifyGameLoaded(); }
            catch (Exception ex2) { Core.LogMsg("[SaveStore] OnGameLoaded 驱动失败: " + ex2.Message); }
            // 09-26 防御重洗白：读档数据就绪后扫全店，已洗白物品若恢复违禁 tag 则重新洗白
            try
            {
                var em = Il2Cpp.EmporiumEntry.Instance;
                if (em != null)
                {
                    var all = em.GetAllItems();
                    foreach (var it in all)
                    {
                        try
                        {
                            if (it != null && it.IsTag("wage_washed") && Il2Cpp.ContrabandHelper.GetContrabandLevel(it) > 0)
                            {
                                Il2Cpp.ContrabandHelper.RemoveContrabandStatus(it);
                                Core.LogMsg("[蛙娘] 读档防御重洗白: " + it.identifier);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex3) { Core.LogMsg("[蛙娘] 读档重洗白扫描异常: " + ex3.Message); }
        }
        catch (Exception ex)
        {
            _pendingLoad = false;
            Core.LogMsg("[SaveStore] 加载失败: " + ex.Message);
        }
    }

    /// <summary>读文件（带 runID 就绪检测）。runID 未就绪（LoadGame 早期）返回 false，等轮询补读；
    /// 就绪则加载文件并标记 _loadedOnce。返回是否本次完成加载。</summary>
    private static bool TryDoLoad()
    {
        try
        {
            string key = ResolveKey();
            if (key == PENDING_KEY)
            {
                // runID 尚未就绪（LoadGame 存档头解析中）→ 放弃本次，等轮询下帧重试。
                // 不能读 pending 文件：pending 是"开局 runID 空窗期"数据，读档场景下可能属旧会话残留。
                return false;
            }
            string path = FilePathFor(key);
            if (path == null || !File.Exists(path))
            {
                // 正式文件未建立：可能是开局 runID 空窗期写入的 pending 数据 → 并入（同档早期数据不丢）
                string pending = FilePathFor(PENDING_KEY);
                if (pending != null && File.Exists(pending))
                {
                    ReadInto(pending);
                    File.Delete(pending);   // 并入后清理，避免下次重复并入
                    _curKey = key;
                    _loadedOnce = true;
                    _loadingComplete = true; // 09-26 加载完成，放开写入
                    Core.LogMsg("[SaveStore] 正式文件未建立，已从 pending 并入（" + _mem.Count + " 项）");
                    DumpForDiagnostics();
                    return true;
                }
                Core.LogMsg("[SaveStore] 无存档文件（新档）：" + key);
                _curKey = key;
                _loadedOnce = true;
                _loadingComplete = true; // 09-26 加载完成，放开写入
                return true;
            }
            ReadInto(path);
            _curKey = key;
            _loadedOnce = true;
            _loadingComplete = true; // 09-26 加载完成，放开写入
            Core.LogMsg("[SaveStore] 已加载 " + key + "（" + _mem.Count + " 项）");
            DumpForDiagnostics();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
