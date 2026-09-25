# Wage's Perks 统一入口文档（UNIFIED ENTRY）

> 版本：v1.2.10 · 2026-09-26
> 本文件是**新 Perk 注册 / 新 Patch 挂载的唯一入口指南**。新增功能前先读本文件，
> 不遵守 = 功能可能不生效 / 破坏特性选择界面 / 与其他系统互拦。

---

## 0. 总览：两条注册链

| 要做什么 | 唯一入口 | 禁止的替代做法 |
|---|---|---|
| 加一个新特性（Perk） | `CustomStartingPerks.All` 数组 + `CustomStartingPerk` 子类 | 直接 new StartingPerk / 建第二个特性基类 |
| 挂一个新 Patch | `PatchRegistry.RegisterXxx()` + `ManualPatcher.TryPatch(...)` | `[HarmonyPatch]` attribute / PatchAll / 在 Core.cs 散挂 |
| 每日逻辑 / 存档逻辑 | 特性 override `OnDayStart()` / `OnSaveGame()` | 另挂散 Postfix 抢生命周期 |
| 每帧逻辑 | `InputActionManager.Update` Postfix（仅一处） | MelonLoader `OnUpdate`（实锤不可靠） |

---

## 1. 新 Perk 注册（5 步）

### 1.1 建子类
继承 `CustomStartingPerk`（`JacksonPerks\CustomStartingPerk.cs`，**唯一特性基类**）：

```csharp
internal sealed class MyNewPerk : CustomStartingPerk
{
    internal override string Id => "my_new_perk";            // 唯一 id（小写下划线）
    internal override string DisplayName => "...";            // 中文名
    internal override string Description => "...";            // 描述（可含\n）
    internal override int Cost => 1;                          // 特性点消耗
    internal override int MaxSlot => 0;                       // 可叠加次数（0=1次）
    internal override int Type => 0;                          // 0绿 1红 2黄
    internal override string[] IncompatibleIds => new[] { "other_perk" }; // 互斥

    internal override void OnNewGame() { /* 新档初始化 */ }
    internal override void OnDayStart() { /* 每日 */ }
    internal override void OnSaveGame() { /* 只写内存 */ }
    internal override void OnGameLoaded() { /* 幂等恢复 */ }
}
```

### 1.2 登记进白名单
`CustomStartingPerks.All` 数组（`CustomStartingPerks.cs` L15）：
- **顺序 == 特性选择界面显示顺序**（`EnsurePickerElements` 按序遍历）——不要"顺手排序"
- 不在数组中的子类 = 不可选 + `IsActive` 恒 false
- 有意排除的加 `[NonSelectablePerk]`（例：WageGirlPerk 门面特性）
- 已取消的特性**不**加标注（DEBUG 断言会报出来提醒清理）

### 1.3 IsActive 判定
**不**放基类（各特性来源不同）：
- 多数：`Core.PerkActive("my_new_perk")`
- RobinCrusoePerk：`ps.startType` 特判
- WageGirlPerk：委托给 WagePowerPerk

### 1.4 每日/存档逻辑走基类钩子（不要散挂 Postfix）

| 钩子 | 权威挂点 | 备注 |
|---|---|---|
| `OnNewGame()` | `PlayerStore.StartNewGame` Postfix | **禁止**挂 `GameMaster.NewGame`（实测从不触发） |
| `OnDayStart()` | `StoreEventManager.OnDayStart` Postfix | 唯一每日权威信号；**不要**挂 `StoreClientManager.OnNewDay`（8字节自增+静默跳过） |
| `OnSaveGame()` | `PlayerStore.SaveGame` Postfix | **契约：只写内存，禁止调 Flush()**（统一落盘由 WageSaveStore priority=0 收尾） |
| `OnGameLoaded()` | `WageSaveStore.LoadIfPending()` 数据就绪后 | **必须幂等**；不能挂 LoadGame Postfix（容器未就绪） |

### 1.5 编译自检
DEBUG 构建时 `AssertAllRegistered()` 防漏登记（Release 自动剔除）。

---

## 2. 新 Patch 挂载（3 步）

### 2.1 写 Patch 方法
放 `Patches` 类（或专用宿主类，如 `RobinCrusoePerk` / `WageGirlSystem` / `ContainerUpgradeV2`）。
每个系统一个宿主类，**不新建** `XxxPatches` 散类。

### 2.2 在 PatchRegistry 注册
`PatchRegistry.ApplyAll()` → 5 个 Register 方法（`PatchRegistry.cs`）：
- `RegisterCoreAndWanted`（核心 + 通缉犯 + 全局）
- `RegisterUnifiedDayStart`（每日/存档生命周期）
- `RegisterDiceNewsDrag`（命运骰子 + 新闻/日历）
- `RegisterInitDirectoryAndStartUI`（目录初始化 + 特性界面）
- `RegisterPerksTail`（各 Perk 业务尾巴——**已知技术债：混合多系统，重构时按系统拆分**）

```csharp
ManualPatcher.TryPatch(
    typeof(原生类),          // 目标方法所属类
    "原生方法名",            // 目标方法名
    prefix: "PrefixXxx",     // 可选，宿主类里的 Prefix 方法名
    postfix: "PostfixXxx",   // 可选，宿主类里的 Postfix 方法名
    parameterTypes: null,    // 重载歧义时才传（原生参数类型数组）
    patchHost: typeof(MyHost), // 宿主类（默认 Patches）
    priority: null,          // 只有"所有系统跑完后必须最后执行"才传低值（如 0）
    finalizer: null          // 可选
);
```

### 2.3 三条铁律（否则功能不生效 / 互拦 / 阻塞界面）
1. **挂点前查 ISIL/dump.cs**：方法存在吗？签名匹配吗？不猜
2. **`__result` 类型必须和原生完全一致**：Il2Cpp 值类型用 `Il2CppSystem.*` 镜像名（ValueTuple 事故教训）
3. **重逻辑不挂 Update/UI 事件**：挂 OnDayStart/SaveGame/LoadGame 固定入口

---

## 3. 权威挂点速查（拆包实锤）

| 原生方法 | 用途 | 挂法 |
|---|---|---|
| `StoreEventManager.OnDayStart` | 每日唯一权威信号 | Postfix（多系统共存，各带幂等键） |
| `PlayerStore.SaveGame` | 打烊落盘 | Postfix；**WageSaveStore 用 priority=0 最后跑** |
| `PlayerStore.LoadGame` | 读档 | Postfix（数据恢复） |
| `PlayerStore.StartNewGame` | 新档 | Postfix（初始化/发放） |
| `PlayerStore.HandleSkipIntro` | 唯一发放点（流浪者） | Postfix |
| `InputActionManager.Update` | **唯一每帧挂点** | Postfix（禁 MelonLoader OnUpdate） |
| `GameItem.MayTarget/CanTarget/Target` | 拖拽目标判定 | Prefix（**多个系统共存：必须各自严格判定"是不是我管的"，否则互拦**） |
| `GameItem.GetCurrentValue/GetNegociatedValue` | 价格 | Prefix+Postfix |
| `RenderHandler.LoadFromAtlas` | 自定义 sprite | Prefix（多系统共存，各自判定） |

---

## 4. 持久化契约（WageSaveStore）

- 运行时状态只碰**内存字典**（`WageSaveStore.SetXxx/GetXxx`）
- `SaveGame` Postfix（priority=0）一次性原子落盘——**其他系统禁止自己落盘**
- `LoadGame` 后 `LoadIfPending()` 数据就绪才触发 `OnGameLoaded`
- **#10 门控**：`_loadingComplete` 未就绪时写入被丢弃（读档空窗期默认值不污染存档）
- 实体类"是否已存在/已发放"判定：**查实体（ExistsInScene），禁用 PlayerPrefs 标记**（残留污染事故）

---

## 5. 硬禁用清单（红线）

| 禁用 | 后果 |
|---|---|
| `[HarmonyPatch]` attribute / PatchAll | 方法名变化静默失效；与 PatchRegistry 双轨混乱 |
| `GameMaster.NewGame` 挂新档逻辑 | 实测从不触发 → 功能无声失效 |
| `StoreClientManager.OnNewDay` 挂每日逻辑 | 8字节自增 + 宿主 null 静默 return |
| MelonLoader `OnUpdate` | 实锤不可靠（NRE 洪水）；每帧逻辑全走 InputActionManager.Update Postfix |
| UI 按钮点击里同步调 `EndDay/BeginDay/SaveGame` | UI 叠叠 + 协程爆炸卡死（卖血卡死事故） |
| 特性选择界面打开时执行重逻辑 | 界面无法交互（硬约束#3） |
| 拦截/让路其他 mod 的 patch | 拦截别人=废掉自己（历史事故） |
| 诊断日志发布 | 打包前必删（DebugMode 门控） |

---

## 6. 已知技术债（重构路线）

- `PatchRegistry.RegisterPerksTail` 混合多系统（RobinCrusoe/蛙娘/妙妙箱/命运骰子/交易链），注册序列顺序敏感，按系统拆分需先建回归基线
- `WageSaveStore`（587 行）待拆（等开发确认 #10 稳定）
- `GameItem.MayTarget/CanTarget/Target` 5 系统共存——合并为单补丁+内部分发是优化方向，**但顺序可能变化，必须先建回归基线**
- `Core.cs` L60/L67 保留 2 个历史直连注册（AddDirectSellingItemToTable Postfix / PlaceInventorInventory Postfix）——**勿重复注册**，后续可迁入 PatchRegistry

---

## 7. 兼容性约定

- 检测到第三方 mod 冲突：`ModCompat.LogLoadedConflicts()` 只**记录不干预**
- 第三方同方法共存：我们各自做严格目标判定（如 ContainerUpgrade 升级键模式 vs 我们的拖拽）
- 版本适配：游戏更新导致方法名变化 → 看启动日志 `[Patch自检] 补丁挂载: 成功 X / 失败 Y`
