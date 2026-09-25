using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;
partial class WageSaveStore

{
private const string DIR_NAME = "WagesPerks";
    private const string FILE_PREFIX = "wages_data_";
    private const string PENDING_KEY = "pending";
    private const string HEAD_RUNID = "#runID=";
    private const string HEAD_SAVED = "#saved=";
    private const string HEAD_VERSION = "#storeVersion=1";
    private const int LOAD_DELAY_FRAMES = 30;   // 读档后延迟帧数（时序：容器/管理器未就绪）

    // ① 内存缓存 —— 唯一真相源，运行时只碰它
    private static readonly Dictionary<string, string> _mem = new Dictionary<string, string>();

    // 旧层(PlayerPrefs)迁移专用：runID 缓存（读旧 key 前缀用，切档必须刷新）
    private static string _legacyRunId = null;

    private static string _curKey = null;        // 当前文件键（runID 或 pending）
    private static bool _pendingLoad;            // 读档待加载标志（Postfix 只设它）
    private static int _pendingLoadFrames;       // 帧延迟计数
    private static bool _loadedOnce;             // 本次读档：键值文件是否已加载（键值不依赖容器就绪，LoadGame Postfix 即可读）
    private static bool _loadingComplete = true;  // 09-26 写入门控：mod启动默认true，读档期间false，加载完true
    internal static bool LoadComplete => _loadingComplete; // 09-26 暴露给外部读门控状态
    private static bool _dirty;                  // 有未落盘改动

    // ===================== 路径 =====================

    private static string DirPath
    {
        get
        {
            try { return Path.Combine(Application.dataPath, "..", "Mods", DIR_NAME); }
            catch { return null; }
        }
    }






































    // 【开发诊断】当前内存项数 —— 供 Debug 面板 / 日志使用
    internal static int Count { get { try { return _mem.Count; } catch { return 0; } } }
    internal static bool HasPendingChange { get { return _dirty; } }
}
