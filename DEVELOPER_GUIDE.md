# Wage's Perks 开发者入口指南（DEVELOPER GUIDE）

> 本文档是"新特性/新补丁怎么加"的唯一入口。新人（含 AI）开工前先读本节，按 1/2 章流程走，
> 3/4 章查挂点与持久化契约，第 6 章红线必须遵守。
> 版本锚点：2026-09-23（v1.2.8 开发中，阶段 2 生命周期驱动 + 阶段 1 统一持久化已落地）。

## 0. 一句话架构

```
特性层   CustomStartingPerk 子类（20 个）→ CustomStartingPerks.All 注册表 → 生命周期统一驱动
挂载层   PatchRegistry.ApplyAll()（197 挂载点）→ ManualPatcher.TryPatch* → Harmony
持久化层 WageSaveStore（per-slot 文件）→ 打烊 Flush 统一落盘 / 读档同步读 + 30 帧 OnGameLoaded
```

- **特性**：玩法功能 + 可选状态，走基类虚方法，**不需要**自己挂 patch（生命周期自动驱动）。
- **补丁**：对原生方法的前置/后置拦截，改游戏行为，必须显式注册。
- **持久化**：所有跨档状态一律走 WageSaveStore，**禁止**直接 PlayerPrefs / 自建文件。

---

## 1. 新增一个 Perk（10 分钟）

### 1.1 建类（新文件 `NewPerk.cs`；大了按域拆 partial）

```csharp
using System;
using Il2Cpp;

namespace JacksonPerks;

// 继承唯一特性基类。不要新建第二个基类（历史 WagePerkBase 已删）。
internal sealed class NewPerk : CustomStartingPerk
{
    internal const string PerkId = "新特性ID";          // 唯一标识，不可变（存档依赖）
    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("中文名", "English Name");
    internal override string Description => LangHelper.T("中文描述", "English description");
    internal override int Cost => 2;                    // 正=付费选；负=送点（负面特性）
    internal override int Type => 0;                    // 0=绿(POSITIVE) 1=红(NEGATIVE) 2=黄(NEUTRAL)
    internal override int MaxSlot => 0;                 // 可叠加槽位；0=单选
    internal override string[] IncompatibleIds => new string[] { };  // 互斥特性（可选）

    // 必实现：开新档（初始发放/状态初始化）。由 PlayerStore.StartNewGame Postfix 统一驱动。
    internal override void OnNewGame() { }

    // 可选：每日结算（StoreEventManager.OnDayStart 统一驱动，同日幂等去重已内置）。
    internal override void OnDayStart() { }

    // 可选：存档。契约：只写内存 WageSaveStore.SetInt(...)，禁止自己调 Flush()。
    internal override void OnSaveGame() { }

    // 可选：读档数据就绪后（LoadIfPending 30 帧后驱动）。契约：必须幂等，可被多次调用。
    internal override void OnGameLoaded() { }

    // 激活判定：各特性来源不同（多数 Core.PerkActive；RobinCrusoe 走 startType；蛙娘委托蛙哥），不统一。
    internal static bool IsActive() => Core.PerkActive(PerkId);
}
```

### 1.2 注册（CustomStartingPerks.cs 的 All 数组）

```csharp
internal static readonly CustomStartingPerk[] All = new CustomStartingPerk[]
{
    ...
    new NewPerk(),   // ⚠️ 数组顺序 = 特性选择界面显示顺序，不要排序
};
```

- 漏登记 = 特性不可选 + IsActive 恒 false（DEBUG 构建有断言报警，Release 不报——必须自己核对）。
- 有意不作为可选特性（门面/委托类）打 `[NonSelectablePerk]`，如 WageGirlPerk。

### 1.3 状态持久化（跨档状态必走）

```csharp
// 写（内存优先，打烊自动落盘）
WageSaveStore.SetInt(NewPerk.PerkId, "key", value);   // 命名空间建议用 PerkId

// 读（内存 miss 自动查文件/旧层）
int v = WageSaveStore.GetInt(NewPerk.PerkId, "key", defaultValue);
bool has = WageSaveStore.HasKey(NewPerk.PerkId, "key");

// 契约：OnSaveGame 里只 Set，不 Flush（统一落盘 priority 0 收尾，防截断他人写入）
```

### 1.4 校验清单（可勾选）

- [ ] 继承 CustomStartingPerk + 实现全部 abstract（Id/DisplayName/Description/OnNewGame）
- [ ] 已加入 CustomStartingPerks.All（顺序位置符合预期 UI 显示）
- [ ] PerkId 唯一且不变
- [ ] 状态读写只走 WageSaveStore，无直接 PlayerPrefs
- [ ] 编译 0 错误 + 游戏内特性选择界面出现该项且可点击
- [ ] 5 场景回归：新档/老档/读档/跨天/强退

---

## 2. 新增一个 Patch（10 分钟）

### 2.1 写方法

放哪个文件（按域）：

| 补丁归属 | 放哪 |
|---|---|
| 生命周期/每日/读档/UI 门面 | `Patches.Lifecycle.cs`（partial） |
| 交易/议价/客户 | `Patches.Trade.cs`（partial） |
| NPC/供应商 | `Patches.Npc.cs`（partial） |
| 物品/场景/世界 | `Patches.World.cs`（partial） |
| 检查/治安 | `Patches.Inspection.cs`（partial） |
| 本地化 | `Patches.Localization.cs`（partial） |
| 单个特性的补丁 | 特性类内部（用 `patchHost` 指定），如 RobinCrusoePerk/WageGirlSystem |

方法模板：

```csharp
// Postfix：宿主方法执行后
public static void PostfixXxx(宿主类型 __instance, 原方法参数)
{
    try { /* 逻辑 */ }
    catch (System.Exception ex) { Core.LogMsg("[特性] PostfixXxx: " + ex.Message); }
}
// Prefix：宿主方法执行前；返回 false 跳过原方法
public static bool PrefixXxx(宿主类型 __instance, ref 类型 __result, 原方法参数) { ... }
```

### 2.2 注册（PatchRegistry.ApplyAll() 加一行）

```csharp
// 标准挂载（宿主类 Patches 的方法）
ManualPatcher.TryPatch(typeof(宿主类), "方法名", "Prefix方法", "Postfix方法");
// 带参数类型（重载歧义时）
ManualPatcher.TryPatch(typeof(宿主类), "方法名", null, "Postfix方法",
    new System.Type[] { typeof(参数1), typeof(参数2) });
// 挂到特性类（patchHost 参数）
ManualPatcher.TryPatch(typeof(宿主类), "方法名", null, "Postfix方法", null, typeof(特性类));
// 按名精确挂（私有带参方法 Il2Cpp 参数匹配失败时兜底；方法名必须与当前游戏版本一致）
ManualPatcher.TryPatchByName(typeof(宿主类), "方法名", "Prefix方法", "Postfix方法");
// 挂所有同名重载
ManualPatcher.TryPatchAllOverloads(typeof(宿主类), "方法名", "Prefix方法", "Postfix方法");
// 低优先级（所有系统写完后再跑，如统一落盘）：priority 0
ManualPatcher.TryPatch(typeof(宿主类), "方法名", null, "Postfix方法", null, typeof(类), 0);
```

### 2.3 挂点规范（能挂哪 / 禁挂哪）

| 时机 | 挂点 | 说明 |
|---|---|---|
| 每帧 | `InputActionManager.Update` Postfix | **唯一**每帧入口；禁 MelonLoader OnUpdate（已实锤不可靠 + 禁访问 PlayerStore.Instance） |
| 每日 | `StoreEventManager.OnDayStart` Postfix | 唯一每日权威信号（同日幂等去重已内置） |
| 开新档 | `PlayerStore.StartNewGame` Postfix | 禁 GameMaster.NewGame（实测从不触发） |
| 存档 | `PlayerStore.SaveGame` Postfix | priority 400 先写内存，统一落盘 0 收尾 |
| 读档 | 键值=同步读；引用恢复=WageSaveStore 30 帧后 OnGameLoaded | 禁 LoadGame Postfix 内恢复引用类型（容器未就绪） |

### 2.4 校验清单（可勾选）

- [ ] 方法有 try/catch 防御（空 catch 必须带注释说明防御语义）
- [ ] 已注册进 PatchRegistry.ApplyAll()（Patch 自检计数 +1）
- [ ] 方法名/参数与当前游戏版本一致（游戏更新会改名）
- [ ] 编译 0 错误 + 游戏内 [Patch自检] 全过（197 基线，新增 +N）
- [ ] 每帧逻辑未用 MelonLoader OnUpdate

---

## 3. 权威挂点表（生命周期，特性勿自行挂 patch）

| 基类钩子 | 权威挂点 | 触发时机 | 幂等/隔离 |
|---|---|---|---|
| `OnNewGame()` | PlayerStore.StartNewGame Postfix | 开新档 | 无（每档一次） |
| `OnDayStart()` | StoreEventManager.OnDayStart Postfix → NotifyDayStart | 每天 | DayKey 同日去重 + 逐特性 try/catch |
| `OnSaveGame()` | PlayerStore.SaveGame Postfix → NotifySaveGame | 打烊 | 不门控（未激活特性也要清理残留） |
| `OnGameLoaded()` | WageSaveStore.LoadIfPending 30 帧后 → NotifyGameLoaded | 读档数据就绪 | 必须幂等 |

> 驱动模板见 `CustomStartingPerks.Drive()`：逐特性独立 try/catch + 异常日志带特性 Id + 门控策略集中。

## 4. 持久化时序铁律（WageSaveStore）

- **写**：`SetInt/SetString/...` 只碰内存字典（零 I/O），打烊 `Flush()` 原子写文件（.tmp→替换）。
- **读档**：LoadGame Postfix → `OnLoadGame()` **同步读文件**（键值不依赖容器）→ 30 帧后 `OnGameLoaded` 驱动引用恢复。
- **隔离**：文件名 = runID（存档身份），天然按档隔离；开局 runID 空窗写 pending 文件。
- **禁止**：业务代码直接 Flush（截断后续写入）；恢复引用类型放在 LoadGame Postfix（容器未就绪必失败）。

## 5. 文件组织规范

- 巨石类拆 partial：`类名.{域}.cs`（如 Patches.Trade.cs / WagePowerPerk.Meta.cs）。
- 切分用确定性脚本（cs_split.py）+ 成员→组映射清单，禁止多 agent 并行改同一 .cs。
- 切完复核：原文件应变几十行壳（不能只看编译）。

## 6. 红线清单（用户硬约束）

1. **拆包实锤**：机制结论必须有拆包证据（dump.cs / cheatsheet [L1]），禁猜测。
2. **Debug 门控**：诊断日志用完必删（发布前），不靠 DebugMode 门控留代码。
3. **特性选择界面必须可交互**（最高红线，冒烟第 1 项）；禁止任何导致 Perk 界面无法交互的做法。
4. **每帧逻辑**只走 `InputActionManager.Update` Postfix；禁 MelonLoader OnUpdate（且 OnUpdate 禁访问 PlayerStore.Instance）。
5. **蛙哥统一译为 Wage**。
6. **N网文案（简介/更新公告/话术）在对话内交付**，不写 txt/md 文件。
7. 修 bug 先 grep 所有调用者修根因，不补单路径。
8. 代码复用七级阶梯：原生/已有/标准库优先，不为单个效果新建多个类。
9. 打包发布必带：README 开头爱发电 https://afdian.com/a/wagesperks + QQ群 1109707341 + ConfigGuide.txt。
10. 开发机保持开发状态（DebugMode 开 + 诊断日志保留），仅用户明确说发布才关。
