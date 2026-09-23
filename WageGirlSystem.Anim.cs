using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    private static void EnsureSprites()
    {
        if (_spIdle != null && _spIdle.Length > 0 && _spIdle[0] != null) return; // 09-20 修：元素级防重入
        try
        {
            _spIdle = LoadSpriteGroup(WageGirlAnimFrames.Idle);
            _spHappy = LoadSpriteGroup(WageGirlAnimFrames.Happy);
            _spHungry = LoadSpriteGroup(WageGirlAnimFrames.Hungry);
            _spThirsty = LoadSpriteGroup(WageGirlAnimFrames.Thirsty);
            _spSick = LoadSpriteGroup(WageGirlAnimFrames.Sick);
            _spDirty = LoadSpriteGroup(WageGirlAnimFrames.Dirty);
            _spSleepy = LoadSpriteGroup(WageGirlAnimFrames.Sleepy);
            _spAngry = LoadSpriteGroup(WageGirlAnimFrames.Angry);
            _spShy = LoadSpriteGroup(WageGirlAnimFrames.Shy);
            _spFull = LoadSpriteGroup(WageGirlAnimFrames.Full);
            _spAway = LoadSpriteGroup(WageGirlAnimFrames.Away);
            _spReturn = LoadSpriteGroup(WageGirlAnimFrames.Return);
            _spWalk = LoadSpriteGroup(WageGirlAnimFrames.Walk);
            _curAnimSprites = _spIdle;
        }
        catch { }
    }
    private static Sprite[] LoadSpriteGroup(string[] b64s)
    {
        var arr = new Sprite[b64s.Length];
        EnsureLoadImageMethod();
        for (int i = 0; i < b64s.Length; i++)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(b64s[i]);
                var tex = new Texture2D(64, 96, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (_loadImageMethod != null)
                {
                    try
                    {
                        _loadImageMethod.Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)bytes });
                        tex.wrapMode = TextureWrapMode.Clamp;
                        arr[i] = Sprite.Create(tex, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0.5f), 200f); // 09-22 回缩1倍：64×96 超采样 → 显示 32×48
                        continue;
                    }
                    catch { }
                }
                try { UnityEngine.Object.Destroy(tex); } catch { }
            }
            catch { }
        }
        return arr;
    }
    private static void EnsureLoadImageMethod()
    {
        if (_loadImageMethod != null) return;
        try
        {
            Type icType = null;
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (Type t in types)
                {
                    if (t != null && t.Name == "ImageConversion") { icType = t; break; }
                }
                if (icType != null) break;
            }
            if (icType != null)
            {
                // 09-20 修：GetMethod 精确参数匹配失败（IL2CPP 签名是 byte[]）→ 宽松遍历
                foreach (var m in icType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (m.Name == "LoadImage") { var ps = m.GetParameters(); if (ps.Length == 2) { _loadImageMethod = m; break; } }
                }
            }
        }
        catch { }
    }
    private static void EnsureStaticIcon()
    {
        try
        {
            if (_staticIconSprite != null) return;
            EnsureLoadImageMethod();
            if (_loadImageMethod == null) return;
            byte[] bytes = Convert.FromBase64String(WageGirlAnimFrames.Idle[0]);
            var tex = new Texture2D(64, 96, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            _loadImageMethod.Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)bytes });
            Color[] src = tex.GetPixels(); // 64×96
            var dst = new Color[32 * 48];
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    int s0 = (y * 2) * 64 + (x * 2);
                    Color c = (src[s0] + src[s0 + 1] + src[s0 + 64] + src[s0 + 65]) * 0.25f;
                    dst[y * 32 + x] = c;
                }
            }
            try { UnityEngine.Object.Destroy(tex); } catch { }
            var tex2 = new Texture2D(32, 48, TextureFormat.RGBA32, false);
            tex2.filterMode = FilterMode.Point;
            tex2.wrapMode = TextureWrapMode.Clamp;
            tex2.SetPixels(dst);
            tex2.Apply();
            Sprite sp = Sprite.Create(tex2, new Rect(0, 0, 32, 48), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            _staticIconSprite = sp;
        }
        catch { }
    }
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (atlasPath == ICON_ATLAS && name == ICON)
            {
                EnsureStaticIcon();
                if (_staticIconSprite != null) { __result = _staticIconSprite; return false; }
            }
        }
        catch { }
        return true;
    }
    private static string EvalState()
    {
        try {
            if (_animMode == 2) return "angry";
            if (_animMode == 1) return "walk";
            try { if (_walking) return "walk"; } catch { } // 平时走动用walk帧
            // 09-19 修：K_LEAVE>0 但蛙娘实体在店里（读档恢复）→ 不挥手，走正常状态
            try { if (GetStat(K_LEAVE) > 0 && _cachedGirlItem == null) return "away"; } catch { }
            int mood = GetStat(K_MOOD), health = GetStat(K_HEALTH), sat = GetStat(K_SAT), th = GetStat(K_TH), clean = GetStat(K_CLEAN), sleep = GetStat(K_SLEEP);
            int aff = GetAffection();
            if (mood <= 20) return "angry";
            if (health <= 30) return "sick";
            if (sat <= 30) return "hungry";
            if (th <= 30) return "thirsty";
            if (clean <= 30) return "dirty";
            if (sleep <= 30) return "sleepy";
            if (mood >= 70) return "happy";
            if (aff >= 80) return "shy";
            if (sat >= 80 && mood >= 60) return "full";
            return "idle";
        } catch { return "idle"; }
    }
    private static void SetAnimMode(int mode, bool force = false)
    {
        try
        {
            if (_animMode == mode && !force) return;
            _animMode = mode;
            _frameIndex = 0;
            _frameTimer = 0f;
            _animModeTimer = 0f;
            _curAnimSprites = mode == 1 ? _spWalk : mode == 2 ? _spAngry : _spIdle;
        }
        catch { }
    }
    private static bool ExistsCached()
    {
        try
        {
            int f = Time.frameCount;
            if (f - _existsCacheFrame < EXISTS_CACHE_FRAMES) return _existsCache;
            _existsCacheFrame = f;
            _existsCache = Exists();
            return _existsCache;
        }
        catch { return Exists(); }
    }
    public static void OnUpdateTick()
    {
        try
        {
            // 同帧去重：InputActionManager.Update 每帧可被多次调用（多实例/多 Patch，09-12 实锤）
            // 本方法用 Time.deltaTime 累加计时器，重复调用会导致动画与移动加速——必须按帧号去重
            if (Time.frameCount == _lastTickFrame) return;
            _lastTickFrame = Time.frameCount;
        }
        catch { }
        try
        {
            if (!ExistsCached()) return;
            if (Patches.CurrentUITradeMode != 0) return; // 交易中不动画不移动
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // 游戏暂停
            EnsureSprites();
            bool hasSprites = _curAnimSprites != null && _curAnimSprites.Length > 0;

            // 外出动画延迟：walk帧播完再移除实体
            if (_leavingTimer > 0f) {
                _leavingTimer -= dt;
                if (_leavingTimer <= 0f) { try { RemoveGirlFromScene(); } catch { } }
            }
            // 回归动画：return帧播完再切idle
            if (_returnTimer > 0f) {
                _returnTimer -= dt;
                _curState = "return";
                _curAnimSprites = _spReturn;
                _stateFrameSec = 0.125f;
                _frameTimer += dt;
                if (_frameTimer >= _stateFrameSec) { _frameTimer = 0f; if (_spReturn != null && _spReturn.Length > 1) _frameIndex = (_frameIndex + 1) % _spReturn.Length; }
                TryApplyAnimFrame();
                if (_returnTimer <= 0f) { _curState = ""; _frameIndex = 0; _frameTimer = 0f; }
            }
            else
            // 状态自动判定
            try {
                string ns = EvalState();
                if (ns != _curState) { _curState = ns; _frameIndex = 0; _frameTimer = 0f;
                    _curAnimSprites = ns == "happy" ? _spHappy : ns == "hungry" ? _spHungry : ns == "thirsty" ? _spThirsty : ns == "sick" ? _spSick : ns == "dirty" ? _spDirty : ns == "sleepy" ? _spSleepy : ns == "angry" ? _spAngry : ns == "shy" ? _spShy : ns == "full" ? _spFull : ns == "away" ? _spAway : ns == "return" ? _spReturn : ns == "walk" ? _spWalk : _spIdle;
                    _stateFrameSec = ns == "happy" ? 0.25f : ns == "hungry" ? 0.3f : ns == "dirty" ? 0.3f : ns == "sick" ? 0.5f : ns == "sleepy" ? 0.5f : ns == "angry" ? 0.2f : (ns == "away" || ns == "return" || ns == "walk") ? 0.125f : 2f; // 09-20 优化：idle 频率 0.375→2 秒
                }
            } catch { }
            // 帧相关（sprite 加载失败时跳过帧应用，不影响移动）
            if (hasSprites)
            {
                // 偷动作：播完自动回待机
                if (_animMode == 2)
                {
                    _animModeTimer += dt;
                    if (_animModeTimer >= _frameMs[2] * _curAnimSprites.Length)
                        SetAnimMode(0);
                }
                // 帧索引推进（按当前动作帧率）
                _frameTimer += dt;
                if (_frameTimer >= _stateFrameSec)
                {
                    _frameTimer = 0f;
                    if (_curAnimSprites.Length > 1)
                        _frameIndex = (_frameIndex + 1) % _curAnimSprites.Length;
                }
                // 09-22 帧应用：ApplyAnimationFrame 原生零调用方（拆包实锤）——mod 必须自己调；Prefix 会替换成 mod 帧
                TryApplyAnimFrame();
            }

            // 移动状态机（09-22 走一会停一会）：走动 3-7 步（每 2.5s 一步）→ 停 3-6 秒 → 再走；不依赖 sprite
            if (_walking)
            {
                _moveTimer += dt;
                if (_moveTimer >= 2.5f)
                {
                    _moveTimer = 0f;
                    _stepsTaken++;
                    if (TryMoveStep()) { SetAnimMode(1, true); }
                    else { SetAnimMode(0); }
                    if (_stepsTaken >= _walkSteps)
                    {
                        _walking = false; _pauseTimer = 0f; SetAnimMode(0);
                    }
                }
            }
            else
            {
                _pauseTimer += dt;
                if (_pauseTimer >= _pauseDuration)
                {
                    // 09-20 优化：前半好感（<50）不走动、不播放 walk 动画
                    if (GetAffection() < 50) { _pauseDuration = 10f; return; } // 好感<50：待在角落不动
                    _walking = true;
                    _stepsTaken = 0;
                    _walkSteps = 3 + Core.Rng.Next(0, 5);            // 走 3-7 步
                    _pauseDuration = 3f + (float)Core.Rng.Next(0, 4); // 停 3-6 秒
                }
            }
        }
        catch { }
    }
    private static void TryApplyAnimFrame()
    {
        try
        {
            // 09-21 修：拖拽时跳过蛙娘动画（避免干扰正在拖拽的物品）
            try { var drg = Il2Cpp.ItemMouseDragHandler.current; if (drg != null && drg.IsDraggingItem) return; } catch { }
            // 读档后旧引用已销毁（parentInventory==null）→ 立即重置重找
            try { if (_cachedGirlItem != null && _cachedGirlItem.parentInventory == null) { _cachedGirlItem = null; _cachedEl = null; _cacheRefreshFrames = 0; } } catch { _cachedGirlItem = null; _cachedEl = null; }
            if (_cachedGirlItem == null || _cacheRefreshFrames <= 0)
            {
                _cacheRefreshFrames = 120;
                _cachedGirlItem = FindGirlItem();
                _cachedEl = null; // 实体可能是新对象 → 丢掉旧 Cast 结果
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f; // 读档后强制重新判定状态
                    if (_cachedGirlItem != null) { try { ApplyIcon(_cachedGirlItem); } catch { } } // 09-20 图标链修复：ApplyIcon 即写 modifiedShape=2×3
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f; // 读档后强制重新判定状态
            }
            else _cacheRefreshFrames--;
            if (_cachedGirlItem == null) return;
            // 09-23 性能：Cast 只在 _cachedEl 为空时做（正常帧直接命中缓存）
            GameItemElement el = _cachedEl;
            if (el == null)
            {
                try { el = _cachedGirlItem as GameItemElement; } catch { }
                if (el == null) { try { el = _cachedGirlItem.Cast<GameItemElement>(); } catch { } }
                if (el != null) _cachedEl = el;
            }
            if (el == null)
            {
                // 09-23 读档/过天后物品重建——旧缓存 Cast 失败立即重找（不等 120 帧）——根治掉动态
                _cachedGirlItem = FindGirlItem();
                _cachedEl = null;
                if (_cachedGirlItem != null) {
                    // 读档后：游戏重建的蛙娘 sprite 是存档旧版 → 强制清旧发新
                    try { RemoveGirlFromScene(); } catch { }
                    try { TryGiveToBackpack(); } catch { }
                    _cachedGirlItem = FindGirlItem(); // 重新找新实体
                    _spIdle = null; _spHappy = null; _spHungry = null; _spThirsty = null; _spSick = null; _spDirty = null; _spSleepy = null; _spAngry = null; _spShy = null; _spFull = null; _spAway = null; _spReturn = null; _spWalk = null;
                    try { EnsureSprites(); } catch { }
                    _curState = ""; _frameIndex = 0; _frameTimer = 0f;
                }
                {
                    try { el = _cachedGirlItem as GameItemElement; } catch { }
                    if (el == null) { try { el = _cachedGirlItem.Cast<GameItemElement>(); } catch { } }
                    if (el != null) _cachedEl = el;
                }
            }
            if (el == null) return;
            Sprite f = _curAnimSprites[_frameIndex % _curAnimSprites.Length];
            // 09-20 修：第0帧null fallback——循环找下一个非null帧（IL2CPP首次类型延迟导致arr[0]=null）
            if (f == null) {
                for (int k = 1; k < _curAnimSprites.Length; k++) {
                    int idx = (_frameIndex + k) % _curAnimSprites.Length;
                    if (_curAnimSprites[idx] != null) { f = _curAnimSprites[idx]; break; }
                }
            }
            if (f == null) return;
            el.ApplyAnimationFrame(f);
        }
        catch { }
    }
    private static bool TryMoveStep()
    {
        if (!BuildConfig.WageGirlAutoMove) return false; // 09-23 自动移动开关
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return false;
            GameGridInventory inv = null;
            GameItem g = null;
            GameGridInventory[] grids = new GameGridInventory[]
            {
                em.invElement as GameGridInventory,
                em.frontInvinvElement as GameGridInventory,
                em.showcaseElement as GameGridInventory,
                em.backInvinvElement as GameGridInventory
            };
            foreach (var gi in grids)
            {
                if (gi == null || gi.childItems == null) continue;
                for (int i = 0; i < gi.childItems.Count; i++)
                {
                    var c = gi.childItems[i];
                    if (c == null) continue;
                    if (c.identifier == ENTITY_ID) { inv = gi; g = c; break; }
                }
                if (g != null) break;
            }
            if (inv == null || g == null) return false;

            if (_girlShape == null)
            {
                var gsb = new GridShapeBuilder();
                gsb.SetDataFill(2, 3);
                _girlShape = gsb.Build();
            }

            // 09-22 随机落格（拆包正确姿势）：requestedNum=数量(1) 不是格子号；随机格中心像素点(每格16px,+8中心)
            // → TryInventorySlot(item, 1, Vector2像素点, shape, null) 自动换算格位 → TryAcceptOnce 落位
            // 09-20 图标链修复后：modifiedShape=2×3 由 SetSpriteAndShape 保证，落格直接用 _girlShape（2×3）
            GridShape shape = _girlShape;
            // 网格宽高（拆包权威：inv.inventoryShape.width/height——格子数；兜底物品 shape）
            int gw = 0, gh = 0;
            GridShape invShape = null;
            try { invShape = inv.inventoryShape; if (invShape != null) { gw = invShape.width; gh = invShape.height; } } catch { }
            if (gw <= 0 || gh <= 0)
            {
                if (shape != null) { try { gw = shape.width; gh = shape.height; } catch { } }
            }
            // 09-22 诊断（用完删）：任何情况都打——区分 shape 为空 / 宽高为 0 / 盲试结果（独立节流防被 tick 诊断挡）
            if (Time.time - _lastMoveDiagTime > 5f)
            {
                _lastMoveDiagTime = Time.time;
            }
            if (gw > 0 && gh > 0)
            {
                int tryCount = 0, hitCount = 0;
                for (int t = 0; t < 8; t++)
                {
                    try
                    {
                        tryCount++;
                        int cx = Core.Rng.Next(0, gw);
                        int cy = Core.Rng.Next(0, gh);
                        // 09-23 拆包正确姿势：GridShapeBuilder(item.shape) + SetPosition(cx,cy) + 3参 TryInventorySlot + TryAcceptOnce
                        // （5参 Vector2 像素点版是陷阱——GetGridPosition 换算后 clamp 0 → 总左上角）
                        var b = new GridShapeBuilder(shape);
                        b.SetPosition(cx, cy);
                        var m = inv.TryInventorySlot(g, b.shape, null);
                        if (m != null && m.IsValid())
                        {
                            hitCount++;
                            m.TryAcceptOnce();
                            return true;
                        }
                    }
                    catch { }
                }
                if (Time.time - _lastMoveDiagTime > 5f)
                {
                    _lastMoveDiagTime = Time.time;
                }
            }
            // 兜底：Expel + TryFindOneValidInventorySlot（至少能动，可能左上角）
            if (!inv.Expel(g)) return false;
            var slot = inv.TryFindOneValidInventorySlot(g, false);
            if (slot != null && slot.IsValid())
            {
                slot.TryAcceptOnce();
                return true;
            }
            inv.UncheckedAccept(g); // 兜底放回（绝不丢实体）
            return false;
        }
        catch { return false; }
    }
    public static void PostfixResolveSpriteByName(GameItemElement __instance, string name, ref Sprite __result)
    {
        try {
            if (__instance == null) return;
            if (__instance.identifier != ENTITY_ID && name != ICON) return;
            // 设成当前动画帧（和 ApplyAnimationFrame 一致）——不再设静态占位图标避免交替
            try { if (_curAnimSprites != null && _curAnimSprites.Length > 0) { var f = _curAnimSprites[_frameIndex % _curAnimSprites.Length]; if (f != null) { __result = f; return; } } } catch { }
            // 09-19 删除占位图标兜底
        } catch { }
    }
    public static void PrefixApplyAnimationFrame(GameItemElement __instance, ref Sprite frame)
    {
        try
        {
            if (__instance == null) return;
            // 拆包实锤：identifier 读档后丢失 → 加 IsTag(TAG) 兜底（TAG 随档）
            if (__instance.identifier != ENTITY_ID && !__instance.IsTag(TAG)) return;
            EnsureSprites();
            if (_curAnimSprites == null || _curAnimSprites.Length == 0)
            {
                if (Time.time - _lastDiagTime > 5f)
                {
                    _lastDiagTime = Time.time;
                }
                return;
            }
            Sprite s = _curAnimSprites[_frameIndex % _curAnimSprites.Length];
            if (s == null) {
                for (int k = 1; k < _curAnimSprites.Length; k++) {
                    int idx = (_frameIndex + k) % _curAnimSprites.Length;
                    if (_curAnimSprites[idx] != null) { s = _curAnimSprites[idx]; break; }
                }
            }
            if (s != null) frame = s;
        }
        catch { }
    }
}
