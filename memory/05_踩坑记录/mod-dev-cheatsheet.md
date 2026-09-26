# Wage's Perks · Mod 开发经验手册（权威版）

> **唯一权威源**（2026-09-26 合并收编）。旧位置 4 处已改为 stub，指向本文件。
> **合并来源与去重说明**：
> - ① 02_豆包产出\DoubaoWork_chats\2026-09-01\new-chat\mod-dev-cheatsheet.md（UTF-16 损坏，清洗后 42 行 = **最新拆包实锤 2.6.15~2.6.25**，2026-09-26 基准）
> - ② 02_豆包产出\Doubao_chats\2026-09-02\new-chat\WagesPerks_MOD开发包_v1.0.9\01_经验手册\mod-dev-cheatsheet.md（5073 行完整手册，第 25~32 章为 [L1/L2/L3] 拆包实锤）
> - ③ 01_原始素材\第三方mod\...\经验文档\mod-dev-cheatsheet.md（362 行社区 FAQ/技术讨论 + 自研踩坑）
> - ④ 02_豆包产出\DoubaoWork_chats\2026-09-01\new-chat\mod-dev-cheatsheet.html（同源导出快照，已 stub）
> **去重规则**：重复主题以最新版（第一部分 2.6.XX）为准；手册/社区原文保留全量（宁全勿缺）。
> **备份**：mod-dev-cheatsheet.md.bak_before_pathmigrate 保留原样（路径迁移前备份，可回溯）。

---

## 第一部分 · 最新拆包实锤结论（2.6.15 ~ 2.6.25，2026-09-26 基准）

## 2.6.15 机制实锤：Unity 日切（EndDay/BeginDay）是协程+UI 驱动，按钮事件内同步循环调用=卡死
- EndDay/BeginDay 是完整一天流程（夜报/早报 UI + 存档 + 协程），不是计数器++；卖血按钮点击栈内同步循环 EndDay→EndNight→OnDayEnd→BeginDay ×3 → UI 叠层 + 协程嵌套爆炸 → 游戏卡死（实测，commit 6405185）。
- 正确姿势：只设状态（如 blood_rest=3）让正常日切自然递减；"跳 N 天"类需求必须走异步/帧钩子（InputActionManager.Update Postfix），禁止 UI 事件栈内同步推进原生日切状态机。
- 同源红线：OnDayStart 链上 8 个 Postfix + 双重结算叠加会放大每轮日切开销；日切重操作只能挂在权威日切挂点，不能手工连调。
## 2.6.16 机制实锤（第三方共存）：全局静态渲染标志污染其他容器 get_childItems=闪烁
- 第三方 ContainerUpgrade 充能器渲染用全局静态 `_inRechargerRender`（Prefix 置 true/Postfix 置 false），期间**所有容器** get_childItems Postfix 被错误裁剪成 4 个 → 一帧裁一帧不裁 = 拖出物品时高频重绘撞上标志 → 容器闪烁（拆包实锤 L8292-8319/L8270-8278）。
- 教训：渲染期"裁剪/替换返回值"的标志必须按容器实例（__instance）区分，全局布尔必然污染同帧其他容器；多 mod 共存排查闪烁先查"谁 patch 了渲染 getter + 是否全局标志"。
- 我们未 patch get_childItems/宽高 getter，每帧 FrameUpdate 全轻量状态检查无渲染调用——无参与，反馈第三方作者修。
## 2.6.17 机制实锤（Notify 链）：StoreUIManager.Notify 是转发壳，rep 链任一 null → 提示静默消失（09-26）
- StoreUIManager.Notify(message, color) 仅转发 ReputationUIManager.Notify（StoreUIManager ISIL L13134-13146）；ReputationUIManager.instance / 气泡UI / dialogBubbleList / text 任一 null → 0x180215630 抛 NRE → mod 空 catch 吞 → 不弹不崩（喂食绿色提示消失根因候选）。
- 气泡贴图 key = color+"p" 拼串（"green"→"greenp"）；GetSprite 字典查不到 → LogWarning "Sprite Value not found" + 回退 "default"（DialogBubbleList ISIL L406-428）；字典 key 来自 Inspector 序列化列表（L214-250），非代码字面量。
- 修法方向：改 Notify(msg) 默认色（"defaultp" 字符串表实锤存在）或自绘提示；先跑运行时日志确认 greenp 是否在字典（LogWarning 是现成信号）。
## 2.6.18 机制实锤（双击命中）：双击/拖放命中 = RenderHandler 泛型 Raycast（UGUI EventSystem.RaycastAll），只认 UGUI 元素（09-26）
- ItemMouseDoubleClickHandler.OnEventPress 命中函数 0x180C11000 = RenderHandler 泛型 Raycast（RGCTX + klass=RenderHandler_c 实锤）→ UGUI RaycastAll → GetComponent<T>（RenderHandler ISIL L3060/L3103-3116）；OnEventUpdate 在 isDoubleClick && lastClickedItem!=null 时调 DoubleClickAction（L341-366）。
- 蛙娘由 TryMoveStep 在 4 货架网格内随机落格、兜底 Expel+放回（WageGirlSystem.Anim.cs L341-433）→ 始终在网格（UGUI 元素）→ 双击理论上可命中；打不开断点靠运行时日志（WageGirl Postfix 的 [双击] 日志有无）定位。
## 2.6.19 机制实锤（0.46D 复核）：PerkUIController.OnChange 存在，勿删 L138 Finalizer（09-26）
- dump.cs:29793 `public void OnChange() { }`（0.46D dump 实锤存在）；PatchRegistry L138 挂 FinalizerPerkUiOnChange（宿主 Patches.Lifecycle.cs:391）注册有效，与交接文档"OnChange 不存在已删除"矛盾，以 dump 为准保留。
- DoubleClickAction 私有、签名 (GameItem, Vector2)（dump.cs:63721）；WageGirl/RobinCrusoe 双 Postfix 均带 try/catch（RobinCrusoePerk.Food.Consume.cs:40）互不阻断。
## 2.6.20 机制实锤（违禁tag系统）：GetContrabandLevel=IsTag("CONTRABAND_ITEM_TAG")，IsTag 读 modifiedState，DisableTag 改 state（09-26）
- 洗白 RemoveContrabandStatus 一次删 4 tag（CONTRABAND_LEVEL / CONTRABAND_ITEM_TAG / CONTRABAND + RemoveGameItemType("CONTRABAND")，ContrabandHelper ISIL L118-174）；GetContrabandLevel 权威判定 = IsTag("CONTRABAND_ITEM_TAG")（ISIL L6568-6573），删掉即=0。
- GameItem 双 TagSystem：state(0x1B0)=模板态 / modifiedState(0x1B8)=实例态；IsTag 读 modifiedState（GameItem ISIL L6132），DisableTag 改 state（L6527）→ ValidateShapeState → SyncModifiedState 把 state 重算写回 modifiedState（L14007-14009 / L13925-13937）。
- 恢复=物品重建/读档后 state 从目录模板重来：DirectoryMaster.Item(id) → ItemDirectory.CreateEmptyItem → InitContrabandItem（DirectoryMaster ISIL L2437；GunsItemDirectory 等模板批量调用）；InitContrabandItem 挂 8 个 TagState lambda 设初始值（b__10_0..7）。
## 2.6.21 洗白两条路径差异（09-26）
- 拖喂洗白（WageGirlSystem.Interact.cs:77）= RemoveContrabandStatus 全删 4 tag；面板「洗白所有」（StealAI_Fence.FenceReturn.cs:304-305）只 DisableTag("CONTRABAND_ITEM_TAG")+"contraband"（小写，非原生 tag）——未删 CONTRABAND_LEVEL / 大写 CONTRABAND / 物品类型，属不完整洗白，但 GetContrabandLevel 仍会=0（ITEM_TAG 已删）。
- 「蛙娘出去后违禁恢复」候选根因：读档/重建时 state 从模板恢复（InitContrabandItem），modifiedState 随 SyncModifiedState 恢复；验证法：洗白前记录 uniqueId，恢复后查同 id 实例 tag（同实例=state 被回写；换实例=物品被重建）。
## 2.6.22 价格链（09-26 11问实锤）
- GetCurrentValue(Bool useRetailMarkup=False, Bool withChild=True, Bool includeEvents=True, Bool forceMarkup=False, Int64 extraMarketMod=0):Int64（GameItem.txt L18913）；GetNegociatedValue():Int64（L17955）无参数。
- 原生价格读 itemFeatures：GetCurrentValue 内 FindItemFeatureByID("retailMarkUp")（L19868-19870）；GetInheritableClientMarketMod()（L21517）遍历 itemFeatures（L21540 读 [rbx+2A8]）→ GetValueStage（L21664）→ GetCurrentValueMod（L21684）累加。
- ItemFeature.GetCurrentValueMod（ItemFeature.txt L2580）：usePreExposeValue(0x68) 为真 → 读 preExposeValueModifier(0x58)/valueModifier(0x60)；为假 → 按 valueStage 读 valueModifier(0x6C/0x48)。
## 2.6.23 声望链（09-26 11问实锤）
- StoreReputation 实例 ModReputation(Double modValue)（StoreReputation.txt L98）：写 [rbx+16]=cur+modValue；黑市(0x1B0 分支) modValue<0 时 ×2（L243-245）；治安部 UL_PETITION 解锁 modValue<0 再 ×2（L251-261）；末尾 CheckReputation（L271/L393）。
- CheckReputation（L275）：clamp 到 [181EA9238](上限)/[181EA9248](下限) 常量（值待运行时）→ UpdateReputation；GetReputation=cvttsd2si([rcx+16])（L78），GetReputationExact=[rcx+16] double（L88）。
- 静态版 ModReputation(String factionId, Int32 value, Boolean notify=True)（L6126）+ ModReputationRaw(String,Int32,Boolean)（L6418）/ModReputationRaw(Double)（L6664）；调用方=ModSecReputation/ModRevReputation/ModNCReputation/ModCartelReputation/ModProgressReputation（L7671-7805）+ OnItemTraded（L2167）。
- 声名狼藉实现（mod）：PlayerStore.StartNewGame Postfix（PatchRegistry.cs:343）差值补法 diff=-99-cur → rep.ModReputation(diff)（InfamousPerk.cs:40-48）；目标全 -99。**"新档只到-49"=原生 ModReputation 倍率+CheckReputation clamp 常量（待运行时确认），PrefixModReputation(World.cs:262 负值减半)未注册=未生效**。
- 警报器：MachineAlarm.Operate(GameItem alarm)（dump.cs:28091；MachineAlarm.txt L2442）→ 按 alarm 上 faction tag（GetTagReadonly L2740）对 革命/治安/黑市/上层 GetXxxFaction→ModReputation(同一常量[181EA90F0]，值待运行时)（L2802-2841）。
## 2.6.24 NPC 链（09-26 11问实锤）
- SpecialNpcManager：0.46D dump.cs/ISIL 均未找到（dump.cs grep 0 命中；Assembly-CSharp 与 firstpass 无该类）——原生侧博士排期不可从当前 dump 确认。
- 博士排期（mod）：ScheduleJacksonToday（Patches.Npc.Jackson.cs:93-134）= DrJacksonFriendPerk.IsActive + 间隔(DoctorVisitInterval 默认7) → QueueFuturClient("inventorStorage",1)；**无声望判断，声名狼藉不拦博士**。
- 蛙娘偷物品：StealItems(mode,count,valueMode,out stolenNames)（Snatch.cs:55）候选=前柜/展示/货架 白名单；**偷的物品 id 不落档**（stolenNames 仅当次 ReportLine，L31/38-39），只存累计 SetStat("stolenValue")（L126）。
## 2.6.25 水瓶打印机 + 顾客预算（09-26 11问实锤）
- MachineBottlePrinter 产水=闭包类 <BottlePrinter>g__TryPrint|1(String bottleId, Int32 cost)（MachineBottlePrinter_DisplayClass6_0.txt L14）：PowerHelper.CanDrawPowerSource（L110）→ DirectoryMaster.Item(bottleId,true)（L116-117）→ GraphUtils.CanAccept/TryAcceptAll（L122/128）→ DrawPowerSource（L135）；瓶型 small/bottled/large_bottled_water（b__3/b__4/b__5，L438/514/592）。**无质量 tag**；质量=WaterHelper.GetWaterPurity（L5167）=水成分占比×10000（LIQUID_CONTAINER_TAG 判定 L5321 + Liquid.GetCurrentPartFromId("water") L5331-5336）。
- StoreClient 预算：clientCash(0x30)/clientBudget(0x34 私有)/useClientBudget(0x38)（dump.cs:38864-38866）；SetBudget(int)/SetBudget(int,int)/GetBudget（dump.cs:39047-39059）；SetClientBudget(Int32 amount, Int32 additionalRange)（StoreClient.txt L3916）=RNG.GetRandomInt(0,range)+amount 写 [rbx+52]；调用方=CreateJunker（StoreClientList L2693，L3095）；判定 HasBudgetLeftToBuy（StoreClient.txt L2942）=GetNegociatedValue(item)<=budget（L2980-2983）——**与 mod 价格 Postfix 耦合（0元购排查点）**。

---

## 第二部分 · 完整经验手册（2026-09-02 快照）

> 说明：原编号内部混乱（两个"十九"、两个"28"、第29~32章混排）系历史追加所致，内容无缺失，原样保留。

# Probably Stolen · Mod 开发完全经验手册

> 整理日期：2026-09-02（v4 迭代：第4层 AssetBundle 完整拆解 + 全工具链安装确认）| 作者：gwxxwg12332
> 来源：Wage's Perks 自研实战（100+ 次编译部署测试）+ 反编译 ExtraPerks / MoreEvents / TradePerks / XIAOWO 系列 / Contactless_Calendar / ModuleUpgradeKit + 官方 Discord + Il2CppDumper / Cpp2IL / ilspycmd / UnityPy / Ghidra / dnSpy / AssetRipper 全工具链拆包
> 证据等级：标记 `[L1]` 的内容经实际代码验证，可直接复制使用；标记 `[L0]` 的内容为推测，需进一步验证。
> 拆包工具链：Il2CppDumper（签名层）→ Cpp2IL pre-release.21（汇编层）→ ilspycmd 8.2（Mod DLL 反编译）→ global-metadata.dat 字符串提取
>
> **⚠️ 待确认与依据不足项汇总**（详见正文对应位置）：
> 1. 第 1.1 节"正式版将转为 Mono"为预期性表述，未经官方公告确认。
> 2. 第 4.4 节博士 `cooldown 10 天`、第 4.6 节"每周二"与代码 `day % 7 == 2` 的对应关系，依赖游戏内天数计数起点，需结合实际运行验证。
> 3. 第 6.10 节 `playerCash` 字段偏移 `0x10` 为特定版本下的观察值，版本更新后可能变化。
> 4. 第 15.3 节标题称"原版 22 个 StartingPerk"，但表格实际列出 23 项，二者不一致，需以 `StartingPerkList` 运行时枚举为准。
> 5. 第 16.1.2 节特性 ID 末尾追加 `\0` 的数量（7–8 个）为经验性建议，不同特性所需数量可能不同，未形成统一规则。
> 6. 第 17.1 节"唯一能解析 metadata 31 的 Cpp2IL 版本"为当前测试范围内的结论，不排除后续版本支持。
> 7. 文中所有"约 X+""X 余个"等概数（如约 150+ 物品 ID、200+ 未实装物品等）均为近似统计，精确数量以实际扫描为准。dump.cs 精确为 433,970 行 / 18.6MB。

---

## 一、开发环境（必看）

### 1.1 基础信息

| 项目 | 说明 |
| --- | --- |
| 游戏构建 | **IL2CPP**（正式版预计转为 Mono，届时开发难度将显著降低）⚠️待确认 |
| 加载器 | **MelonLoader 0.7.3**（IL2CPP / net6），使用官方 installer 安装 |
| 语言 | **仅支持 C#** |
| 反编译 | Il2CppDumper 导出 dump.cs（43.4万行/18.6MB）+ Cpp2IL ISIL 汇编 + global-metadata.dat 字符串提取 |
| 安装 | 将编译产物 DLL 放入 `Probably Stolen Playtest\Mods\` |
| 存档 | `C:\Users\用户名\AppData\LocalLow\Questing Goose Studio\Probably Stolen\save_NUMBER.es3`（Easy Save 3） |
| 日志 | `MelonLoader\Latest.log` |

**游戏依赖库**：Easy Save 3（存档）、Harmony（Hook）、DOTween（动画）、Json.NET、RNGNeeds（随机数）、Febucci.TextAnimator（文本动画）

### 1.2 .csproj 模板（直接复制）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>disable</Nullable>
    <AssemblyName>YourModName</AssemblyName>
    <RootNamespace>YourModName</RootNamespace>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <PropertyGroup>
    <GameDir>D:\Steam\steamapps\common\Probably Stolen Playtest</GameDir>
    <MelonDir>$(GameDir)\MelonLoader\net6</MelonDir>
    <Il2CppDir>$(GameDir)\MelonLoader\Il2CppAssemblies</Il2CppDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="MelonLoader"><HintPath>$(MelonDir)\MelonLoader.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2CppInterop.Runtime"><HintPath>$(MelonDir)\Il2CppInterop.Runtime.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="0Harmony"><HintPath>$(MelonDir)\0Harmony.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Assembly-CSharp"><HintPath>$(Il2CppDir)\Assembly-CSharp.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(Il2CppDir)\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Unity.TextMeshPro"><HintPath>$(Il2CppDir)\Unity.TextMeshPro.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.UI"><HintPath>$(Il2CppDir)\UnityEngine.UI.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
  <!-- 内嵌图标资源（可选） -->
  <ItemGroup>
    <EmbeddedResource Include="Icons\*.png" />
  </ItemGroup>
</Project>
```

### 1.3 入口类模板

```csharp
using MelonLoader;
[assembly: MelonInfo(typeof(YourModName.Core), "YourModName", "1.0.0", "Author")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace YourModName;
public sealed class Core : MelonMod
{
    internal static MelonLoader.ILogger Log;
    public override void OnInitializeMelon()
    {
        Log = ((MelonBase)this).LoggerInstance;
        Log.Msg("YourModName loaded.");
    }
}
```

### 1.4 编译环境必守规则（经 50 余次实践验证）

1. **一个 Mod 对应一个独立目录**——csproj 默认编译当前目录下所有 `.cs` 文件，残留旧版本 `.cs` 将导致 Hook 冲突与日志混乱。
2. **关闭游戏后再复制 DLL**——游戏运行时 DLL 被文件锁占用，`Copy-Item` 可能静默失败。
3. **放置新 DLL 前彻底清理旧 DLL**——Mods 目录中存在多个同名或关联 DLL 时，会导致同一方法被多次 Hook。
4. **复制后核验时间戳**——通过 `Get-ChildItem` 确认 DLL 更新时间符合预期。

---

## 二、Hook 与事件系统

### 2.1 ModHook 事件清单（订阅用）

```
客户生成：OnGenerateCustomerVeryEarly/Early/Normal/Late/VeryLate
游戏加载：OnGameLoadedInit/Early/Normal/Late
营业状态：OnShutterOpened/Closed(Early/Late)、OnLeaving/ReturningStore(Early/Late)
日常作息：OnGoingSleep/WakingUp(Early/Late)、OnHandlingNightlyServices(Early/Late)
物品/UI：OnModItemDirectoryInit、OnPlaceInventorInventoryItem(Early/Late)、OnCreateTooltipEarly
```

### 2.2 两种 Patch 模式

**模式 A：Attribute 模式（简单，目标方法必须存在）**
```csharp
[HarmonyPatch(typeof(TargetClass), "MethodName")]
static class MyPatch {
    static void Prefix(/* 参数名必须和原方法一致 */) { }
    static void Postfix(ref long __result) { }
}
```

**模式 B：手动 Patch（推荐，容错性更好）**——目标方法不存在时仅输出 Warning，不会导致崩溃
```csharp
internal static class ManualPatcher
{
    internal static void TryPatch(Harmony harmony, Type type, string name,
        string prefix = null, string postfix = null)
    {
        try {
            MethodBase method = AccessTools.Method(type, name);
            if (method == null) { Core.Log.Warning($"{type.Name}.{name} not found"); return; }
            harmony.Patch(method,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(ManualPatcher), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(ManualPatcher), postfix));
        } catch (Exception ex) {
            Core.Log.Warning($"Patch {type.Name}.{name}: {ex.Message}");
        }
    }
}
```

### 2.3 Harmony Patch 核心注意事项

- **大小写敏感**：`PostFix` ≠ `Postfix`——拼写错误将导致 Hook 不触发，且不产生任何报错。
- **参数名必须与原方法一致**——Hook `AddClient(StoreClient client)` 时若原参数名为 `storeClient`，将引发 IL Compile Error。不确定时使用 `__0`、`__1` 占位符。
- **MelonLoader 自动执行 PatchAll**——Attribute 模式无需手动调用。
- **Prefix 与 Postfix 的区别**：Prefix 可通过 `return false` 跳过原函数执行；Postfix 可修改 `__result` 返回值。
- **Hook 失败具有静默性**——加载成功不等于 Hook 成功，必须检查日志中是否存在 `Failed to HarmonyInit` 或 `IL Compile Error`。
- **`__instance` 是获取 Hook 目标对象实例的可靠方式**——Hook 实例方法时，`__instance` 即被调用对象本身。

### 2.4 每日效果触发时机（关键注意事项）

- **`OnNewDay`（跨天事件）在首次进入存档当天不触发**——当天仅触发 `BeginDay`。
- **每日效果或负面效果必须同时挂载两个节点**：`BeginDay`（确保进档当天生效）+ `OnNewDay`（确保后续跨天生效）。

### 2.5 正确 Hook 点速查表（按用途）

| 想做什么 | 该 Hook 谁 | 时机 |
|---|---|---|
| 每天调度客户/事件 | `PlayerStore.BeginDay` Postfix | 每天开始 |
| 每周固定日来客户 | `StoreClientManager.HandleContentUnlockClient` Postfix | 每天处理内容解锁客户 |
| 读档后修复旧存档 | `PlayerStore.LoadGame` Postfix | 读档 |
| 夜晚结束处理 | `PlayerStore.EndNight` Prefix | 夜晚结束 |
| 拦截物品创建 | `DirectoryMaster.Item` Prefix | 每次创建物品 |
| 客户进店开始对话 | `StoreClient.StartMainDialogue` Postfix | 交易区就绪 |
| 客户离开 | `PlayerStore.DismissCurrentClient` Postfix | 客户离开 |
| 玩家出售物品 | `PlayerStore.SellItem` Prefix+Postfix+Finalizer | 区分买/卖 |
| 物品议价价格 | `GameItem.GetNegociatedValue` Postfix | 改最终价 |
| 安保检查冷却 | `StoreClientManager.ComputeInspectionCooldownRange` Postfix | 改检查频率 |
| 赃物初始化 | `StolenHelper.InitStolenItem` Postfix | 物品成为赃物时 |

---

## 三、游戏事件系统（StoreEvent）—— 参考 MoreEvents

### 3.1 添加自定义事件

```csharp
// 在 OnInitializeMelon 中注册
foreach (var blueprint in MyEvents.NewNormalEventBlueprints)
    StoreEventManager.normalEventBlueprints.Add(blueprint);

// StoreEventBlueprint 构造：事件创建函数、数值、事件ID
// 第2参数在 MoreEvents 中值域 1~10（terrorist=2/food=7/party=5/crackdown=7/
//   goldenTicket=1/factory=4/hospital=4/commandLeak=3/robbedTrain=5/securityBreach=7）
//   语义疑似"出现权重"或"最小间隔天数"，[L0]未从游戏侧 ISIL 确认
new StoreEventBlueprint(new Func<StoreEvent>(CreateMyEvent), 7, "myEventId");
```

### 3.2 事件字段

| 字段 | 说明 |
|------|------|
| `identifier` | 事件唯一ID |
| `newsName`/`newsDescription` | 新闻标题/描述 |
| `displayName` | 显示名称 |
| `duration` | 持续天数 |
| `importance` | 重要性（99=最高）⚠️待确认 |
| `eventType` | `NORMALE`/`COSMETIC`/`THREAT`/`INNATE` |
| `eventArea` | `UPPER`/`LOWER`/`ALL` |
| `negociationDatas` | 价格影响列表 |
| `addClientFromEventActionId` | 触发客户的动作ID |

### 3.3 动态价格影响（NegociationData）

```csharp
// 正值=涨价，负值=降价
storeEvent.negociationDatas.Add(new NegociationData("WEAPON", 100, newsName, displayName));
```

**物品标签**：`POISON`、`MATERIAL`、`WEAPON`、`FOOD`、`MEDICAL`、`HOUSEHOLD_GOOD`、`SUBSTANCE`、`LUXURY_ITEM`、`ALCOHOL`、`CONTRABAND`、`TREAT`、`ACCESS_CARD`、`SUPPLY_CRATE`

### 3.4 事件动作系统

```csharp
StoreEventActionDict.actions["myEventModdedAction"] = (Action)delegate {
    PlayerStore.instance.AddDirectSellingItemToTable(DirectoryMaster.Item("c4"), true, true, false, 200);
};
storeEvent.addClientFromEventActionId = "myEventModdedAction";
```

### 3.5 事件链（排队后续事件）

```csharp
StoreStation.instance.storeEventManager.QueueFuturEvent(CreateResupplyEvent(), 3, true);
```

### 3.6 检查事件是否激活

```csharp
StoreStation.instance.storeEventManager.IsEventActive("eventId");
StoreStation.instance.storeEventManager.IsBlackoutEventActive();  // 停电事件
```

---
### 3.7 MoreEvents 完整拆解补充（源码直读，2026-09-03）

**MoreEvents（v1.0.0，作者 amyalt125）以源码工程形式发布，Core.cs 仅 637 行，是事件系统最完整的现成模板。**

#### 3.7.1 10 个事件模板（直接抄字段配置）

| 事件 identifier | 第2参数 | duration | importance | 价格标签（amount） | eventArea |
|---|---|---|---|---|---|
| terroristAttack | 2 | 7 | 9 | POISON+100, MATERIAL+200, WEAPON+100 | ALL |
| foodRecall | 7 | 1 | 3 | FOOD+25, MEDICAL+10, HOUSEHOLD_GOOD+10 | ALL |
| upperLevelParty | 5 | 2 | 3 | SUBSTANCE+25, LUXURY_ITEM+25, ALCOHOL+25 | UPPER |
| contrabandCrackdown | 7 | 3 | 7 | CONTRABAND+300 | ALL |
| goldenTicketHunt | 1 | 5 | 7 | TREAT+100 | ALL |
| factoryFarmRenovation | 4 | 3~4 | 99 | MATERIAL+40, FOOD+30 | ALL |
| hospitalRenovation | 4 | 3~4 | 91 | MEDICAL+30 | ALL |
| commandLeak | 3 | 5 | 2 | ACCESS_CARD+30 | ALL |
| robbedTrain | 5 | 3 | 9 | SUPPLY_CRATE-20, ACCESS_CARD+20 | ALL |
| securityBreach | 7 | 1 | 9 | （无价格影响，纯客户） | ALL |
| crazyManYellingAtTheSky | 10 | — | — | （装饰事件 COSMETIC，无价格） | — |

**事件链（排队后续事件）**：factoryFarmRenovation 激活时 `QueueFuturEvent(CreateFactoryFarmRenovationResupply(), 3, true)` 排队补货事件（MATERIAL-20, FOOD-15）；hospitalRenovation 同理排队 resupply（MEDICAL-15）。这是"事件→后续事件"的标准写法。

#### 3.7.2 事件 → 动作 → 客户 完整链路

```
StoreEventBlueprint(工厂, 权重, "terroristAttack")
  → StoreEvent.identifier = "terroristAttack"
  → storeEvent.addClientFromEventActionId = "terroristAttackModdedAction"
  → StoreEventActionDict.actions["terroristAttackModdedAction"] = delegate { ... }
```

事件激活 → 游戏按 addClientFromEventActionId 查 actions 字典 → 执行 delegate。delegate 里可以：加客户、加商品、任意逻辑。

动作 delegate 的客户生成模式（注意概率门控）：
```csharp
"contrabandCrackdownModdedAction": delegate {
    if (RNG.Roll(75)) {  // 75% 概率
        StoreClient c = StoreClientList.CreateInspectionClient();
        c.eventSourceId = "contrabandCrackdown";  // ★ 事件来源标记字段
        PlayerStore.Instance.storeClientManager.AddClient(c);
    }
}
```

#### 3.7.3 源码 Bug 记录（MoreEvents 自己的问题，别学）

1. **InvestigationProgressPatch.Prefix 参数没 ref**：`Prefix(string crimeID, int amount)` 里 `amount = RNG.MultiplyAndRound(amount, 1.25)` 修改无效——Prefix 改非 ref 参数不会传回。违禁品打击的犯罪进度加成实际没生效。
2. **displayName 复制粘贴错误**：crazyManYellingAtTheSky 和 hospitalRenovationResupply 的 displayName 都写成 "Factory Farm Renovation Resupply"。
3. **死代码**：expiredImmunivaxDumping 蓝图被注释，但 ActionDict 里还留着对应 delegate。
4. **DebugPatch 残留**：PhoneUIManager.CloseUI 的 Postfix 里 11 行 QueueFuturEvent 全注释。

#### 3.7.4 引用的游戏原生工厂（做事件客户直接复用）

| 类 | 方法 | 作用 |
|----|------|------|
| StoreClientList | CreateInspectionClient() | 检查客户 |
| StoreClientListTierSubstance | CreateUpperLevelHedonist() | 上层享乐者 |
| StoreClientListEvent | CreateFoodBuyerClient() / CreatePharmacistClient() | 事件客户 |
| StoreClientListInformation | CreateShadyCustomer() | 可疑客户 |
| PreBuiltItemHelper | LootCrateEngineering/Evidence/Medical/Security/Service() | 预制板条箱 |
| InsInjectorHelper | CreateExpiredInjector() | 过期注射器 |
| DirectoryMaster | Item("c4") / Item("sec_keycard") | 取物品 |

**⚠️ 对话文本全部硬编码英文**，不走本地化表——要汉化必须 Patch 或改源码。

#### 3.7.5 可复用结论
- 事件扩展 mod 不需要 Patch 任何方法体，纯注入蓝图即可（最安全）
- addClientFromEventActionId + StoreEventActionDict.actions 是"事件→客户"的标准桥梁
- eventSourceId 是客户的事件来源标记字段 [L1 源码确认]
- 需要"某事件激活时改某数值"→ Patch 目标方法 Prefix，**参数必须加 ref**

#### 3.7.6 MoreEvents 源码级开发思路提炼（真实源码，非反编译）

**来源**：moreevents.zip 内 Core.cs（637 行，单文件）+ csproj。以下均为源码确认，比 3.7（反编译推断）可信度高一级。

**3.7.6.1 蓝图注入模式——零 Patch 加事件的正确姿势（最值得借鉴）**
```csharp
// 所有事件 = 一个蓝图列表（数据）+ 一批工厂函数（逻辑）
public static List<StoreEventBlueprint> NewNormalEventBlueprints = new() {
    new StoreEventBlueprint(new Func<StoreEvent>(CreateTerroristAttack), 2, "terroristAttack"),
    // ... 第2参数值域 1~10，语义仍 [L0] 未确认
};
// 注册只做一件事：遍历 Add 进游戏管理器
foreach (var b in NewNormalEventBlueprints)
    StoreEventManager.normalEventBlueprints.Add(b);
```
**经验**：加事件/内容类扩展，首选"数据列表注入 + 工厂函数"，不碰游戏原逻辑，零 Harmony Patch。可扩展性最好（加一个事件=加一个工厂函数+一行蓝图）。

**3.7.6.2 事件剧本 / 链式排队——制造价格周期**
```csharp
// 涨价事件创建时，顺手排队一个"几天后的跌价事件"
StoreStation.instance.storeEventManager.QueueFuturEvent(CreateXxxResupply(), 3, true);
// resupply 事件用负价格标签（MATERIAL -20）实现"涨完再跌"
```
**经验**：`QueueFuturEvent(事件, 间隔天数, true)` = 事件链。涨→跌、禁→松 的经济周期玩法，做价格波动类特性直接抄这个模式。

**3.7.6.3 事件作为全局状态开关**
```csharp
[HarmonyPatch(typeof(SecData),"CommitCrime")]
public static bool Prefix(string crimeID, int amount) {
    if (StoreStation.instance.storeEventManager.IsEventActive("contrabandCrackdown"))
        amount = RNG.MultiplyAndRound(amount, 1.25);   // ⚠️ 没 ref，实际无效（源码级 bug）
    return true;
}
```
**经验**：`IsEventActive("id")` 可在任意 Patch 里检查 → 事件激活期间临时增强/削弱任意游戏逻辑。事件=全局状态机，比自建状态更贴近原生。

**3.7.6.4 手建客户的完整模板（可能解决我们"货物不初始化"的老问题）**
```csharp
StoreClient sc = new StoreClient();
sc.identifier = "spacerShit"; sc.displayName = "Lower Level Spacer";
sc.spriteName = SpriteDict.GetRandomUpperSpriteName();      // 随机外貌
sc.clientFaction = "FACTION_LOWER_LEVEL";
sc.useClientBudget = true; sc.SetBudget(RNG.GetRandomInt(35,50));
sc.clientIntent = StoreClient.ClientIntent.BUY;
sc.canClientExposeFeature = new Func<ItemFeature, StoreClient, bool>(ClientCanExposeFunc.BasicClientExposeCapacity);
sc.mainDialogue.SetText(name, "..." ).NextDialogue().SetText(name, "...").SetEndAction((Action)delegate{});
sc.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();  // 作者注释"This is stupid that i need to do this"
sc.clientBuyingTagList.Add("DOCUMENT");
sc.clientBuyingIdList.Add("toilet_paper"); ...
sc.AddBasicDialog();
sc.CompleteClientCreation();   // ← 关键收尾
```
**经验**：`CompleteClientCreation()` + `AddBasicDialog()` 是手建客户的标准收尾。我们之前测试客户"货物不初始化"，很可能缺的就是 `CompleteClientCreation()`。SELL 客户则靠 `SetEndAction` 里 `AddDirectSellingItemToTable` 在对话结束塞货。

**3.7.6.5 对话链式 API（官方干净写法）**
`mainDialogue.SetText(...).NextDialogue().SetText(...).SetEndAction(...)` —— 别手写 Dialogue 对象树。

**3.7.6.6 SELL 客户放货机制**
```csharp
sc.clientIntent = StoreClient.ClientIntent.SELL;
sc.isOfferWholesale = true; sc.wholesaleDiscount = -10;   // 批发客户
// 放货不在"货物初始化"，而在对话结束 SetEndAction：
SetEndAction((Action)delegate { PlayerStore.Instance.AddDirectSellingItemToTable(PreBuiltItemHelper.LootCrateXxx(), false, true, false, 100); });
```
**经验**：卖货客户的货=对话结束动作塞柜台。博士/酒商"有货"的机制核心。

**3.7.6.7 eventSourceId 来源标记**
`storeClient.eventSourceId = "foodRecall";` —— 给客户打来源标签。追踪客户来源，后续可做事件联动/统计。我们随机客户也建议加来源标记。

**3.7.6.8 概率门控生成客户**
`if (RNG.Roll(50)) { ... AddClient(...) }` —— 客户生成走概率，避免事件每次必刷。

**3.7.6.9 反面教材（源码级确认的坏味道，我们 mod 要避免）**
1. `Prefix(string crimeID, int amount)` 改局部参数**没加 ref** → 修改无效，还开着 `return true`。**Prefix 改参数必须 `ref`**（源码证实，不是猜）
2. `DebugPatch`（PhoneUIManager.CloseUI Postfix）调试残留，每次关手机打日志——我们发布版已用 Core.LogMsg 门控根治
3. `displayName` 复制粘贴错误多处（hospitalResupply 写成了 Factory Farm、cosmetic 事件 displayName 也是错的）
4. `using static` 引入无关类（HebrewNumber/Allocator2D）——IDE 自动补全残留
5. 一个事件用非法 id 注册但注释掉了（`"securityBreach"` 重复，未清理）
6. csproj 是标准 MelonLoader IL2CPP 模板（`$(GamePath)` 引用 Il2CppAssemblies），无可借鉴差异

---



## 四、客户系统（最核心）

### 4.1 添加客户的两种正确方式

**方式 A：未来客户队列（推荐，由游戏自动管理生命周期）**
```csharp
// 防重复：检查队列里是否已有
List<string> queue = PlayerStore.Instance.futurStoreClientIdQueue;
bool exists = queue.Any(id => id == "retired_gunsmith");
if (!exists) PlayerStore.Instance.QueueFuturClient("retired_gunsmith", 1);
```
- 游戏在合适时机自动生成客户，**外观、货物、对话均正确初始化**。

**方式 B：直接加入客户栈（立即排队）**
```csharp
var client = StoreClientListSpec.CreateRetiredGunsmith();
manager.TryAddClient(client, true);  // true = 去重
```

**禁止做法**：手动 `new StoreClient()` + `AddClient()` 后再修改字段——此方式下货物不会被初始化。

### 4.2 正确的 Hook 点（每周固定日来客户）

```csharp
[HarmonyPatch(typeof(StoreClientManager), "HandleContentUnlockClient")]
static void Postfix(StoreClientManager __instance) {
    int day = StoreStation.GetDayCounter() % 7;
    if (day == 2 && NetworkUpgrade.IsUnlocked("RETIRED_GUNSMITH"))
        __instance.TryAddClient(StoreClientListSpec.CreateRetiredGunsmith(), true);
}
```

### 4.3 关键 API

| API | 说明 |
| --- | --- |
| `TryAddClient(client, true)` | 第二个参数为 true 时启用去重 |
| `QueueFuturClient(clientId, day)` | 加入未来客户队列 |
| `StoreClientListSpec.CreateXxx()` | 客户创建工厂 |
| `StoreStation.GetDayCounter()` | 获取当前天数 |
| `NetworkUpgrade.IsUnlocked("ID")` | 检查网络升级是否解锁 |
| `StoreClientManager.GetCustomerTotalVisitedCount(id)` | 查询客户累计到访次数 |

### 4.4 客户 identifier 速查

| identifier | 说明 |
| --- | --- |
| `inventorStorage` | 博士（杰克逊），cooldown 10 天 ⚠️待确认，clientIntent=SELL |
| `retired_gunsmith` | 退休枪匠 |
| `retired_farmer` | 退休农夫 |
| `retired_junker_intro` | 退休废品商 |
| `retired_chemist` | 退休化学家 |

### 4.5 自定义客户完整创建流程（参考 MoreEvents / TradePerks）

**关键发现：客户的货物在 `SetEndAction` 的 delegate 中通过 `AddDirectSellingItemToTable` 添加，而非在客户创建时添加。**

```csharp
public static StoreClient CreateMyClient()
{
    StoreClient c = new StoreClient();
    c.identifier = "myClientId";
    c.displayName = "Client Name";
    c.spriteName = SpriteDict.GetRandomScavSpriteName();
    c.clientFaction = "FACTION_LOWER_LEVEL";
    c.useClientBudget = true;
    c.SetBudget(RNG.GetRandomInt(35, 50));
    c.clientIntent = StoreClient.ClientIntent.SELL;  // BUY/SELL
    c.isOfferWholesale = true;       // 批发模式
    c.wholesaleDiscount = -10;       // 负值=折扣
    c.canClientExposeFeature = new Func<ItemFeature, StoreClient, bool>(
        ClientCanExposeFunc.BasicClientExposeCapacity);

    // 链式对话
    c.mainDialogue.SetText(c.displayName, "第一句")
        .NextDialogue().SetText(c.displayName, "第二句")
        .SetEndAction((Action)delegate {
            // ★ 货物在这里添加！
            PlayerStore.Instance.AddDirectSellingItemToTable(
                DirectoryMaster.Item("raw_meat"), false, true, false, 100);
            // 最后一个参数是 heat（赃物热度），0=干净，100=全赃
        });

    // 收购列表（BUY 意图时）
    c.clientBuyingIdList = new Il2CppSystem.Collections.Generic.List<string>();
    c.clientBuyingTagList.Add("DOCUMENT");

    c.AddBasicDialog();
    c.CompleteClientCreation();  // ★ 必须调用
    return c;
}
```

**SpriteDict 随机头像**：`GetRandomUpperSpriteName()`、`GetRandomBMMaleSpriteName()`、`GetRandomBMFemaleSpriteName()`、`GetRandomScavSpriteName()`

**RNG 工具**：`RNG.Roll(percent)`、`RNG.GetRandomInt(min,max)`、`RNG.GetRandomIntExculsive(min,max)`、`RNG.MultiplyAndRound(value,mult)`

### 4.6 博士调度实战

- **每周二**（间隔 7 天）⚠️待确认：`QueueFuturClient("inventorStorage", 1)`
- 出售大机器 ×3 + 大储存 ×3 + 神经模组，含友情价 -20%
- **夜晚博士商店**：`EmporiumEntry.Instance.docInvElement`（GameSlotInventory），使用 `UncheckedAccept(item)` 添加物品
- 夜晚博士为 `GameCharacterItem`（常驻角色），上门博士为 `StoreClient`（临时客户）——**两套系统完全独立，不可混用**

### 4.7 上门客户检查清单

- [ ] identifier 正确
- [ ] clientTracker 冷却已清除
- [ ] AddClient / TryAddClient 调用成功
- [ ] 货物在 `SetEndAction` 或 `StartMainDialogue` 时添加（而非创建时）
- [ ] 台词在 `DisplayClientText` 时修改（`plainText` 和 `text` 均需修改）
- [ ] 仅修改第一句（使用静态标记控制）

### 4.8 已验证的不可行路径

- `OnUpdate` 轮询 → **导致游戏卡死**
- `StoreClientManager.OnNewDay` 手动 Patch → 不触发
- `HandleInspectionClient` 直接调用 → clientStack 为 0
- 手动 `new StoreClient()` + `AddClient()` → 货物不初始化

---

## 五、特性系统（StartingPerk）—— 参考 ExtraPerks / TradePerks

### 5.1 特性类型与颜色

| 值 | 含义 | UI 颜色 |
| --- | --- | --- |
| `POSITIVE = 0` | 正面 | 绿 |
| `NEGATIVE = 1` | 负面 | 红 |
| `NEUTRAL = 2` | 中性 | 黄 |

- **设置 type 必须直接赋值**：`perk.type = (StartingPerk.StartingPerkType)Type;`
- **注意事项**：通过反射 `typeField.SetValue(perk, int)` 赋值会被 Il2Cpp 的 `catch{}` 静默吞掉，导致 type 始终为 0。
- 负面特性的 Cost 使用负数以返还点数。

### 5.2 特性注册（推荐方式，可解决"选择界面点不动"问题）

```csharp
// Patch 游戏原生初始化方法，带去重
[HarmonyPatch(typeof(StartingPerkList), "InitStartingPerk")]
static void Postfix() { PerkRegistry.EnsureRegistered(); }

internal static void EnsureRegistered() {
    List<StartingPerk> perks = StartingPerkList.Perks;
    if (perks == null) return;
    foreach (var def in AllPerks) {
        bool exists = perks.Any(p => p.id == def.Id);
        if (!exists) perks.Add(def.Create());
    }
}
```

### 5.3 特性图标（两种方式）

**方式 A：外部文件（当前项目采用，稳定性较好）**
- 32×32 PNG，背景透明、前景白色
- 放置于 `Mods\JacksonPerks\Icons\`，通过 `Texture2D.LoadImage` 加载
- 游戏根据特性类型自动应用滤镜上色，**无需手动配色**

**方式 B：内嵌资源 + 注入游戏原生字典（TradePerks 方式，发布更干净）**
```csharp
// csproj: <EmbeddedResource Include="Icons\*.png" />
Assembly asm = typeof(Core).Assembly;
string resName = asm.GetManifestResourceNames()
    .FirstOrDefault(n => n.EndsWith("icon.png", StringComparison.OrdinalIgnoreCase));
using Stream stream = asm.GetManifestResourceStream(resName);
byte[] bytes = new byte[stream.Length];
stream.Read(bytes, 0, bytes.Length);

Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) {
    filterMode = FilterMode.Point,      // 像素风不模糊
    wrapMode = TextureWrapMode.Clamp,   // 边缘不重复
    hideFlags = HideFlags.HideAndDontSave  // ★ 必须设，否则场景切换时销毁
};
ImageConversion.LoadImage(tex, (Il2CppStructArray<byte>)bytes);
Sprite sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height),
    new Vector2(0.5f,0.5f), 100f);
sprite.hideFlags = HideFlags.HideAndDontSave;

// ★ 注入游戏原生图标字典
StartingPerkIconLoader.perkIcons[perkId] = sprite;
```

**颜色正常的关键：不要手动设置任何颜色**——`el.icon.color`、`el.icon.material`、`SetPixels` 均会覆盖 PNG 原始颜色。选中 / 未选中 / 悬停状态的颜色变化由原版 `StartingPerkElement` 自动处理。

### 5.4 特性 UI 接入

```csharp
[HarmonyPatch(typeof(PerkUIController), "OpenUI")]
static void Postfix(PerkUIController __instance) {
    EnsureRegistered();
    foreach (var def in AllPerks) {
        var existing = __instance.availablePerks.GetComponentsInChildren<StartingPerkElement>(true)
            .FirstOrDefault(el => el.id == def.Id);
        if (existing == null) {
            GameObject obj = Object.Instantiate(__instance.perkElementPrefab,
                __instance.availablePerks.transform);
            StartingPerkElement el = obj.GetComponent<StartingPerkElement>();
            el.id = def.Id;
            el.isSelected = false;
            obj.SetActive(true);
        }
    }
    __instance.SortPerkContainer(__instance.availablePerks);
}
```

### 5.5 特性本地化

```csharp
[HarmonyPatch(typeof(LocHelper), "GetLocalizedPerkTable")]
static bool Prefix(string key, ref string __result) {
    // key 格式：perk_{id}_name 或 perk_{id}_desc
    if (!key.StartsWith("perk_")) return true;
    bool isName = key.EndsWith("_name");
    bool isDesc = key.EndsWith("_desc");
    if (!isName && !isDesc) return true;
    string id = key.Substring(5, key.Length - 5 - 5);
    var def = FindCustom(id);
    if (def == null) return true;
    __result = isName ? def.DisplayName : def.Description;
    return false;  // 拦截，不走原版本地化
}
```

### 5.6 特性选择界面点不动（已解决，完整根因链 [L1] ISIL 实锤）

- **现象**：安装 Mod 后开新存档，特性选择界面所有特性无法点击（原版特性同样点不了）。
- **直接根因**：`StartingPerkElement.OnPointerClick` 被调用，但内部 `NewGameData.Instance[0x30]`（= `isInMainMenu`）== 0，直接跳"无操作"分支 return，**不执行 CanSelect/SelectPerk**。
- **完整点击决策链**（ISIL 实锤，StartingPerkElement.OnPointerClick）：
  1. `NewGameData.Instance == null` → return
  2. **`NewGameData[+0x30]`（isInMainMenu）== 0 → return（无操作）** ← 本次点不动的拦截点
  3. `PerkUIController.Instance == null` → return
  4. `PerkUIController[+0x216]`（私有状态字段）!= 0 → return
  5. `MainMenuUIController.Instance == null` → return
  6. `elem[+0x20]`（isSelected）!= 0 → 走 DeselectPerk 分支
  7. `CanSelect()` == false → return
  8. `PerkUIController.SelectPerk(element)` + `elem.isSelected = true`
- **修复（保留在 Patches.cs，PrefixOnPointerClick）**：在 OnPointerClick 前强制恢复正确状态——`Marshal.ReadByte(ng.Pointer, 0x30)==0` 时 `Marshal.WriteByte(ng.Pointer, 0x30, 1)`（恢复 isInMainMenu=1，特性选择界面应有的状态）。这是恢复游戏原生期望状态，不是绕过 UI。
- **为什么 isInMainMenu 会是 0**：NewGameData 构造时 `.ctor` 设 isInMainMenu=1，运行时被某流程置 0；Mod 不直接碰该字段，是游戏原生状态在新档流程中被置 0（版本相关），用 Prefix 在点击前恢复即可。
- **关键教训：Cpp2IL ISIL 的偏移是十进制**！`[rax+48]` = 偏移 0x30（十进制48=十六进制0x30）= dump.cs 的 `isInMainMenu`。**不是十六进制 0x48**！曾因把 48 当十六进制读到错误字段，Prefix 写错位置无效，浪费多轮。判据：ISIL 的 `[rbx+32]`（十进制）= dump.cs `isSelected 0x20` 吻合；若按十六进制 0x32 则无对应字段。**后续读 ISIL 偏移一律先 ×1 转十进制对照 dump.cs 十六进制字段**。
- **运行时内存读写**：`System.Runtime.InteropServices.Marshal.ReadByte/WriteByte(Il2CppObject.Pointer, offset)` 可直接读改 Il2Cpp 对象私有字段（无需 unsafe），是定位此类"私有字段拦截"的决定性手段。
- **排查路径复盘**（为什么走了很多弯路）：①静态对比 20:29 vs 当前 DLL 代码差异穷尽无果（Mod 不直接碰该字段）→ ②运行时 Postfix 确认 OnPointerClick 被调用 → ③ISIL 读点击决策链 → ④Marshal 读内存确认字段值 → ⑤发现偏移进制陷阱 → ⑥Prefix 正确偏移修复。**对 IL2CPP 私有字段类 bug，静态 diff 无效，必须运行时读内存 + ISIL 决策链**。

### 5.7 特性效果触发

- **新游戏时**：Patch 新游戏初始化方法，通过 `StartingPerk.IsPerkActive(id)` 判断特性是否激活。
- **每天开始时**：`PlayerStore.BeginDay` Postfix + `OnNewDay` Postfix（双挂，参见 2.4 节）。
- **运行时效果**：每个特性独立编写 Harmony Patch。

---

## 六、物品与价格系统

### 6.1 物品创建

```csharp
// 普通物品
GameItem item = DirectoryMaster.Item("raw_meat", true);

// 箱子工厂（比 DirectoryMaster.Item 更完整）
GameItem box = ShipSystemDirectory.StorageBayLarge();  // 大箱子 3x3
GameItem ext = ShipSystemDirectory.MachineBayExt();     // 机器区扩建 4x4
```

**预构建战利品箱（PreBuiltItemHelper）—— 解决"箱子是空的"问题**：
```csharp
PreBuiltItemHelper.LootCrateEngineering();  // 工程箱
PreBuiltItemHelper.LootCrateEvidence();     // 物证箱（治安部）
PreBuiltItemHelper.LootCrateMedical();      // 医疗箱
PreBuiltItemHelper.LootCrateSecurity();     // 安保箱
PreBuiltItemHelper.LootCrateService();      // 后勤箱
```

**注射器工厂**：`InsInjectorHelper.CreateExpiredInjector()`

### 6.2 复制物品检查清单（缺一项即可能出问题）

- [ ] `itemTypes` 已复制（否则属性为 0）
- [ ] `itemModifiedShape` 已引用（否则占地错误，直接引用 template.shape，不要 new）
- [ ] `state` 已复制
- [ ] `ability` 已复制（否则无法放入机器）
- [ ] `contentWindow` 已复制（否则 UI 错误或无法打开）
- [ ] `name` / `description` / `value` 已复制
- [ ] `spritePath` / `spriteAtlasPath` 已复制

### 6.3 价格计算引擎

```
GameItem.GetCurrentValue(useRetailMarkup, withChild, includeEvents, forceMarkup, extraMarketMod)
  → AccumulateFeatureStages(ref innateMult, ref marketMult, ref finalFactor, skipEventFeatures)
  → ComposeStagedValue(totalBaseValue, innateMult, marketMult, finalFactor, miscValue, marketScaledValue)
```

**ItemFeature 关键字段**：`identifier`、`featureType`（Normale / Event / Special / Temporary）、`valueStage`（Innate / Market / Final）、`valueModifier`

### 6.4 价格修改两种方式

**方式 A：GetNegociatedValue Postfix（TradePerks 方式，直接修改最终价）**
```csharp
[HarmonyPatch(typeof(GameItem), "GetNegociatedValue")]
static void Postfix(ref long __result) {
    if (IsPlayerSelling && PerkRegistry.IsActive("perk_id"))
        __result = (long)Math.Round(__result * 1.2, MidpointRounding.AwayFromZero);
}
```
- 优点：直接修改最终议价结果，不污染物品本身属性。
- 缺点：不显示价格标签（需额外添加 ItemFeature 以显示标签）。

**方式 B：ItemFeature（当前项目采用，可显示标签）**
- 直接为物品添加 ItemFeature，`valueStage=Market`，`valueModifier=20`。
- 优点：游戏原生显示价格标签。
- 缺点：污染物品属性栏，可能出现"价格已变但标签不匹配"的情况。

**最优方案**：通过 `GetNegociatedValue` 修改价格 + 单独添加 ItemFeature 显示标签。

### 6.5 玩家买/卖区分（TradePerks 深度计数器，彻底解决混淆）

```csharp
private static int _playerSellingDepth;
internal static bool IsPlayerSelling => _playerSellingDepth > 0;

[HarmonyPatch(typeof(PlayerStore), "SellItem")]
static void Prefix() { _playerSellingDepth++; }
static void Postfix() { _playerSellingDepth--; }
static Exception Finalizer(Exception __exception) {  // 异常也兜底
    _playerSellingDepth--;
    return __exception;
}
```

### 6.6 三种价格修正

| 类型 | 机制 | 控制参数 |
| --- | --- | --- |
| 零售价加价 | ItemFeature `item_feature_retail_markup` + 客户 `acceptRetailMarkup` | `useRetailMarkup: true` |
| 违禁品加价 | `ContrabandHelper.InitContrabandItem(item, level)` | 物品标签 `contraband` + `contrabandLevel` |
| 事件加价 | `StoreEvent` 系统，`FeatureType.Event` | `includeEvents: true` |

### 6.7 违禁品标签字符串（从 metadata 提取）

`contraband`、`contrabandLevel`、`contrabandType`、`contrabandMarkupLow/Mid/High/Critial`、`contrabandFinePercentage`

### 6.8 赃物系统（StolenHelper）

```csharp
StolenHelper.IsStolenItem(item);           // 是否赃物
StolenHelper.SetHeat(item, int);           // 设置热度
item.GetTagReadonly("STOLEN_VALUE_INT").GetInt();  // 读取热度

// 赃物初始化时修改热度
[HarmonyPatch(typeof(StolenHelper), "InitStolenItem")]
static void Postfix(GameItem item) {
    var tag = item.GetTagReadonly("STOLEN_VALUE_INT");
    if (tag != null && tag.GetInt() > 0)
        StolenHelper.SetHeat(item, (int)Math.Ceiling(tag.GetInt() * 1.25));
}
```

### 6.9 安保检查（修改冷却而非强制触发）

```csharp
[HarmonyPatch(typeof(StoreClientManager), "ComputeInspectionCooldownRange")]
static void Postfix(ref int min, ref int max) {
    if (PerkRegistry.IsActive("security_target")) {
        min = Math.Max(1, (int)Math.Floor(min * 0.75));
        max = Math.Max(min, (int)Math.Floor(max * 0.75));
    }
}
```

### 6.10 玩家资金字段

- **资金字段为 `PlayerStore.playerCash`（int，offset 0x10）⚠️版本相关——而非 `playerMoney`！**

### 6.11 进度型机器实现模式（参考 XIAOWOHydroponicFix 更新版）

**完整的便携式机器后端实现**（以水培系统为例，可复用于熔炉 / 净水器等）：

```csharp
// 1. 初始化进度型机器
MachineProgressHelper.InitProgressTypeMachine(machine, 20, false);
// 参数：机器, 基础处理速度, 是否需要模块

// 2. 绑定周期结束回调（关键！机器每完成一个周期触发）
Action<GameItem, GameInventory, SlotMarker> cycleEnd = 
    DelegateSupport.ConvertDelegate<Action<GameItem, GameInventory, SlotMarker>>(
        (Action<GameItem, GameInventory, SlotMarker>)OnCycleEnd);
((GameItemFunc)machine).onCycleEndSlotItemFunc = cycleEnd;

// 3. 周期处理逻辑
void OnCycleEnd(GameItem machine, GameInventory parent, SlotMarker slot) {
    // 检查状态：STATE_READY / STATE_WORKING
    string state = MachineProgressHelper.GetMachineState(machine);
    if (state == "STATE_READY") {
        // 开始新周期：消耗资源 + 开始进度
        if (ConsumeResources(machine)) {
            MachineProgressHelper.StartProgressTypeMachine(machine, inputItem);
            GeneralHelper.LockItem(inputItem);           // 锁定输入物品
            MachineHelper.LockModuleInv(machine);          // 锁定模块槽
            inputItem.EnableTag("GROWING_TAG", false);    // 标记生长中
        }
    } else if (state == "STATE_WORKING") {
        // 继续周期：消耗资源 + 推进进度
        if (ConsumeResources(machine)) {
            MachineProgressHelper.ContinueProgressTypeMachine(machine);
            if (MachineProgressHelper.IsProgressTypeMachineFinished(machine)) {
                FinishProduction(machine);  // 产出物品
            }
        }
    }
}

// 4. 完成产出
void FinishProduction(GameItem machine) {
    GameInventory outputSlot = MachineHelper.GetSlot(machine, 5);  // 第5槽=产出
    for (int i = 0; i < quantity; i++) {
        GraphUtils.TryAcceptAll(
            new GraphNodeStorage(((Il2CppObjectBase)outputSlot).Pointer),
            DirectoryMaster.Item("output_item_id", true), -1);
    }
    MachineProgressHelper.FinishProgressTypeMachine(machine);
    MachineHelper.UnlockModuleInv(machine);
}
```

**关键 API 速查**：

| API | 说明 |
|-----|------|
| `MachineProgressHelper.InitProgressTypeMachine(machine, speed, needModule)` | 初始化进度型机器 |
| `MachineProgressHelper.GetMachineState(machine)` | 获取状态（STATE_READY / WORKING） |
| `MachineProgressHelper.StartProgressTypeMachine(machine, input)` | 开始进度 |
| `MachineProgressHelper.ContinueProgressTypeMachine(machine)` | 推进进度 |
| `MachineProgressHelper.IsProgressTypeMachineFinished(machine)` | 是否完成 |
| `MachineProgressHelper.FinishProgressTypeMachine(machine)` | 完成进度 |
| `MachineHelper.CanPower(machine)` | 是否有电力 |
| `MachineHelper.TryDrawCyclePower(machine, batterySlot)` | 消耗周期电力 |
| `MachineHelper.GetSlot(machine, slotIndex)` | 获取指定槽位 |
| `MachineHelper.GetBatterySlot(machine)` | 获取电池槽 |
| `MachineHelper.SetupBatterySlot(slot, machine)` | 设置电池槽 |
| `MachineHelper.LockModuleInv(machine)` / `UnlockModuleInv` | 锁定 / 解锁模块槽 |
| `MachineHydroponic.GetInputSlot(machine)` | 获取输入槽（水培专用） |
| `MachineHydroponic.CanWater(machine)` | 是否有水 |
| `MachineHydroponic.GetWaterSlotItem(machine)` | 获取水槽物品 |
| `WaterHelper.Remove(waterItem, ml)` | 消耗水（单位 ml×1000） |
| `GeneralHelper.LockItem(item)` | 锁定物品（处理中不可移动） |
| `GeneralHelper.SetItemOwned(item, isOwned)` | 设置拥有状态 |
| `HydroponicHelper.ApplyBonusModuleYield(qty, machine, moduleInv)` | 应用加成模块产量 |
| `GraphUtils.TryAcceptAll(storage, item, -1)` | 添加物品到库存 |
| `PermitHelper.OnNewItemCreated(item)` | 新物品创建回调（许可系统） |

**配方从 TagSystem 读取**（种子 / 原料物品上的标签）：
- `PROGRESS_ITEM_TARGET_TAG`（int）—— 需要的进度值
- `HARVEST_QUANTITY`（int）—— 产出数量
- `HARVEST_ITEM_ID`（string）—— 产出物品 ID
- `WATER_USE_RATE`（int）—— 每周期耗水量（ml）

**多年生 / 重复结果**：
- `PERENNIAL_TAG` —— 多年生植物（收获后不消失）
- `REFRUIT_PROGRESS_INT` —— 重新结果的进度值（收获后设置为目标进度）
- `GROWING_TAG` —— 生长中标记

### 6.12 UI 窗口重建（GridPixelElement + GameSlotInventory）

**从零构建机器的交互窗口**（6 列 2 行：标签行 + 槽位行）：

```csharp
PixelWindow window = new PixelWindow(true, (string)null);
GridPixelElement grid = new GridPixelElement(6, 2, true);  // 6列2行

// 第一行：标签
TagElement label = new TagElement(-1, true).SetText("电池", 10000, (ColorPalette)12822958);
grid.Attach(new PixelElement(((Il2CppObjectBase)label).Pointer), 0, 0);  // 列0,行0

// 第二行：槽位
GameSlotInventory batterySlot = new GameSlotInventory();
grid.Attach(new PixelElement(((Il2CppObjectBase)batterySlot).Pointer), 0, 1);  // 列0,行1

// 槽位背景图
waterSlot.SetBackgroundFadeSprite("Items/water_container4", "xl", 0.5f, 0.5f);

// 槽位只接受特定标签物品
ContainerHelper.AllowOnlyTaggedItemsOr(
    (GameInventory)(object)waterSlot,
    new Il2CppStringArray(new string[1] { "LIQUID_CONTAINER_TAG" }),
    true);  // true=只接受列表内标签

window.Attach(new PixelElement(((Il2CppObjectBase)grid).Pointer));
item.SetContentWindow(window);
MachineHelper.SetupBatterySlot(batterySlot, item);  // 电池槽需要特殊设置
```

**从已有窗口提取槽位并重建**（修复旧存档中窗口布局变化的物品）：
```csharp
GridPixelElement root = new GridPixelElement(((Il2CppObjectBase)item.contentWindow.childElement).Pointer);
PixelElement oldBatteryCell = root.GetElement(0, 0);  // 列0行0
GridPixelElement oldBatteryGrid = new GridPixelElement(((Il2CppObjectBase)oldBatteryCell).Pointer);
PixelElement oldSlot = oldBatteryGrid.GetElement(0, 1);  // 提取原槽位
oldBatteryGrid.Detach(oldSlot);  // 从旧布局分离
// ... 然后 Attach 到新布局
```

### 6.13 反射调用可能不存在的工厂方法

当游戏版本更新导致工厂方法可能不存在时，采用反射 + 缓存实例的方式：

```csharp
private static MethodInfo factoryMethod;
private static UnusedDirectory retainedDirectory;

internal static GameItem Create(bool isOwned) {
    if (factoryMethod == null) {
        factoryMethod = AccessTools.Method(typeof(UnusedDirectory), "HydroponicSystem");
        if (factoryMethod == null || factoryMethod.ReturnType != typeof(GameItem))
            throw new MissingMethodException("HydroponicSystem not found");
    }
    if (retainedDirectory == null)
        retainedDirectory = new UnusedDirectory();  // 缓存实例，避免每次创建

    try {
        object result = factoryMethod.Invoke(retainedDirectory, null);
        GameItem item = (GameItem)(result is GameItem ? result : null);
        if (item == null) return null;
        // ... 手动设置 identifier/name/description/sprite/tags
        return item;
    } catch (TargetInvocationException ex) when (ex.InnerException != null) {
        throw ex.InnerException;  // 解包真实异常
    }
}
```

### 6.14 防刷屏日志模式

**UI刷新触发的Postfix必须加去重**（2026-09-02 实战踩坑）：

问题：NegociationUIManager.InitUIWithItemSellMode 的Postfix被频繁触发（每秒约5次），导致日志刷屏和重复处理。

原因：交易UI每次刷新都会调用InitUIWithItemSellMode，而Postfix没有做去重。

修复：用静态变量记录上一次处理的item的uniqueId，只在item变化时才处理和打日志：

`csharp
private static long _lastSellModeItemUid = -1;

public static void PostfixUIInitSellMode(GameItem item)
{
    CurrentUITradeMode = 2;
    if (item == null) return;
    long uid = 0;
    try { uid = item.uniqueId; } catch { }
    // 去重：同一个item重复触发（UI刷新）只处理一次
    if (uid == _lastSellModeItemUid) return;
    _lastSellModeItemUid = uid;
    FixTradeItemName(item);
    TryAddTradeFeatureByUI(item);
}
`

同时在PostfixUIClose中重置去重变量，确保下次打开交易UI能重新处理：
`csharp
public static void PostfixUIClose()
{
    CurrentUITradeMode = 0;
    _lastSellModeItemUid = -1; // 重置去重
    _lastBuyModeItemUid = -1;
    RemoveFriendDiscountFeatures();
    RemoveTradeFeatures();
}
`

**通用原则**：任何挂在UI刷新/OnUpdate/每帧调用上的Postfix，都必须加去重或节流，否则会日志刷屏+重复处理+性能损耗。

```csharp
// 按物品唯一ID去重，每个物品只记一次错误/迁移
private static readonly HashSet<long> LoggedFailures = new HashSet<long>();
private static readonly HashSet<long> LoggedMigrations = new HashSet<long>();

long itemId = (machine.uniqueId != 0) ? machine.uniqueId : 
    ((Il2CppObjectBase)machine).Pointer.ToInt64();

if (LoggedFailures.Add(itemId))  // Add返回true=首次记录
    Core.Log.Error("Production failed: " + ex);
```

### 6.15 未实装机器修复模式（以枪械打印机为例）

**问题**：`3d_printer` 等未实装机器物品通过 `DirectoryMaster.Item("3d_printer", true)` 创建后显示为问号——原因在于该物品 ID 在 DirectoryMaster 中仅有 name，缺少 desc / flavor / short 字段，且正确的创建方式应为专用工厂方法。

**根因**（经 metadata + dump.cs 确认）：
- `item_3d_printer_name` 存在，但 `item_3d_printer_desc / flavor / short` 不存在。
- 正确的工厂方法为 `PreBuiltItemHelper.CreatePrinter()`（与水培机 `CreateHydroponic()` 同一模式）。
- 机器逻辑类为 `MachinePrinter`，UI 窗口通过 `MachinePrinter.CreateMachineInventoryWindow()` 创建。

**PreBuiltItemHelper 中的未实装机器工厂方法**：

| 方法 | 物品 | 状态 |
|------|------|------|
| `CreateHydroponic()` | 水培机 | XIAOWO 已修复 |
| `CreatePrinter()` | 3D 打印机 / 枪械打印机 | 未实装（问号） |
| `CreateMoistureFarm()` | 湿度农场 | 未实装 |
| `CreateFurnace()` | 熔炉 | 未实装 |
| `CreateAlarm()` | 报警器 | 未实装 |

**MachinePrinter 类（3D 打印机逻辑）**：

| 方法 | 说明 |
|------|------|
| `Printer()` | 创建打印机物品 |
| `FinishPrinting(item, chipInv, itemInvRight, itemInvModule)` | 完成打印 |
| `HandlePrintedItemQuality(printedItem, printer, moduleInv)` | 处理打印质量 |
| `CreateMachineInventoryWindow(w, h, ...)` | 创建 6 槽 UI 窗口 |
| `CreateNote()` | 创建便签 |

**修复方案（参照水培机做法）**：

```csharp
internal static GameItem CreatePrinter(bool isOwned) {
    // 1. 用专用工厂方法创建基础物品（不要用 DirectoryMaster.Item）
    GameItem item = PreBuiltItemHelper.CreatePrinter();
    if (item == null) return null;

    // 2. 手动补全字段（因为未实装，这些字段为空）
    item.identifier = "3d_printer";
    item.identifierName = "3d_printer";
    item.name = "3D打印机";
    item.shortDescription = "紧凑型3D打印机，可使用打印芯片制造枪械、配件和其他物品。";
    item.flavorText = "在空间站的有限空间里，一把自己打印的枪就是生存的底气。";

    // 3. 设置图标和形状
    item.SetSpriteAndShape("Items/items_tool", "3d_printer");

    // 4. 重建UI窗口（6槽：芯片槽+物品槽+模块槽+便签槽等）
    var windowData = MachinePrinter.CreateMachineInventoryWindow(300, 200);
    item.SetContentWindow(windowData.Item1);  // PixelWindow

    // 5. 设置机器标签
    item.EnableTag("IS_BATTERY_POWERED_TAG", false);
    item.EnableTag("STANDARD_MACHINE_TAG", false);
    item.SetGameItemType("MACHINE");

    // 6. 设置拥有状态和唯一ID
    GeneralHelper.SetItemOwned(item, isOwned);
    if (item.uniqueId == 0 && PlayerStore.Instance != null)
        item.uniqueId = PlayerStore.Instance.GetUniqueID();

    return item;
}
```

**拦截 DirectoryMaster.Item（全局修复）**：

```csharp
[HarmonyPatch(typeof(DirectoryMaster), "Item")]
static bool Prefix(string identifier, bool isOwned, ref GameItem __result) {
    if (identifier == "3d_printer") {
        __result = PrinterFactory.Create(isOwned);
        return false;  // 跳过原方法
    }
    return true;  // 其他物品走原逻辑
}
```

**旧存档迁移**（读档时修复已有问号物品）：

```csharp
[HarmonyPatch(typeof(PlayerStore), "LoadGame")]
static void Postfix() {
    var allItems = PlayerStore.Instance.FindAllItem(true);
    foreach (var item in allItems) {
        if (item.identifier == "3d_printer" && string.IsNullOrEmpty(item.name)) {
            // 重新创建完整物品替换旧物品
            var fixedItem = PrinterFactory.Create(true);
            // ... 替换库存中的物品
        }
    }
}
```

**关键区分**：
- ~~`MachinePrinter.Printer()`~~ —— **0.46D 已删除该方法**（0.46D MachinePrinter 类只剩 `CreateNote()`），测试项 `MachinePrinter.Printer() 方法未找到` 即因此 FAIL。不要再引用。
- `PreBuiltItemHelper.CreatePrinter()` —— 预构建版本（与水培机同模式，推荐用于修复）
- `DirectoryMaster.Item("3d_printer")` —— 禁止使用，创建结果为问号

**同类未实装机器**（可采用相同模式修复）：
- 湿度农场 `CreateMoistureFarm()`
- 熔炉 `CreateFurnace()`
- 报警器 `CreateAlarm()`
- 生物打印机 `UnusedDirectory.BioPrinterSystem()`（有完整 UI 窗口 `CreateBioPrinterSystemWindow()`）
- 水回收器 `UnusedDirectory.WaterRecyclerSystem()`
- 自主医疗系统 `UnusedDirectory.AutonomousMedicalSystem()`

---

## 七、IL2CPP 互操作必守规则

### 7.1 委托转换（必须）

IL2CPP 环境下不能直接将 C# 委托赋值给 IL2CPP 委托字段，必须使用 `DelegateSupport.ConvertDelegate`。

```csharp
// 重写客户的 willAcceptItem（直接给属性赋值，✅ 可行）
Func<GameItem, bool> prev = client.willAcceptItem;
client.willAcceptItem = DelegateSupport.ConvertDelegate<Func<GameItem, bool>>(
    (Func<GameItem, bool>)delegate(GameItem item) {
        if (item != null && item.identifier == "xxx") return false;
        return prev == null || prev.Invoke(item);
    });
```

> **2026-09-04 踩坑补充（妙妙箱爆红根因）**：`DelegateSupport.ConvertDelegate<Il2CppSystem.Func<...>>` 转换出的委托**不能再用反射 `PropertyInfo.SetValue(inv, converted)` 回写**——IL2CPP 包装类型与反射目标不匹配，每次调用即抛 `TargetException: Object does not match target type`（MelonLoader 日志爆红，拖拽交互全失效）。
> **正确做法**：优先调用游戏原生 API 设置校验（如 `ContainerHelper.AllowOnlyOwnedItems(inv)` 设置"只允许已拥有物品入库"），不要自定义委托+反射回写。

### 7.2 对象指针去重

```csharp
HashSet<IntPtr> processed = new HashSet<IntPtr>();
IntPtr ptr = ((Il2CppObjectBase)item).Pointer;
if (!processed.Add(ptr)) return;  // 已处理过
```

### 7.3 类型转换使用 TryCast

```csharp
// as 操作符在 IL2CPP 下不可靠
GameGridInventory grid = inv is GameGridInventory g ? g :
    ((Il2CppObjectBase)inv).TryCast<GameGridInventory>();
```

### 7.4 反射取字段使用实例遍历

```csharp
// ❌ typeof(PlayerStore).GetField("storeClientManager") → 返回 null
// ✅ 用实例的 GetType() 遍历
foreach (var f in ps.GetType().GetFields(
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
    if (f.Name == "storeClientManager") { scm = f.GetValue(ps); break; }
}
```

### 7.5 结构体数组转换

```csharp
Vector3[] corners = new Vector3[4];
rectTransform.GetWorldCorners((Il2CppStructArray<Vector3>)corners);
ImageConversion.LoadImage(tex, (Il2CppStructArray<byte>)pngBytes);
```

### 7.6 线程静态防递归

```csharp
[ThreadStatic] private static bool _restoringType;
void SyncType(GameItem item) {
    if (_restoringType) return;
    _restoringType = true;
    try { /* 可能触发递归的操作 */ }
    finally { _restoringType = false; }
}
```

### 7.7 拦截物品创建（工厂模式）

```csharp
[HarmonyPatch(typeof(DirectoryMaster), "Item")]
static bool Prefix(string identifier, bool isOwned, ref GameItem __result) {
    if (identifier != "hydroponic") return true;
    try {
        __result = CustomFactory.Create(isOwned);
        return false;  // 跳过原方法
    } catch {
        __result = null;
        return true;   // 失败兜底
    }
}
```

### 7.8 缺失物品接入原版子系统

遇到"游戏有系统但物品缺失"的情况时——优先查找游戏原生 Helper（如 `WineHelper.InitBerry` / `MachineProgressHelper.InitProgressTypeMachine`），不要自行重写。使用 TagSystem 标签做幂等检查。

---

## 八、对话系统（Wage's Perks 实现）

### 8.1 架构

- **全局对话**（所有玩家均可见）：`GlobalModifyDialogue`
- **蛙哥专属对话**（仅激活对应特性的玩家可见）
- 跳过列表：博士 / 王尔德 / 胡安 / 鉴定委员会 / 奥丁·哈拉尔德森（保留剧情对话）

### 8.2 对话修改必守规则

1. **台词需同时修改两个字段**：`plainText`（实际显示使用）和 `text`（RichTextBuilder，富文本备用）。
2. **修改时机**：在 `DisplayClientText` 的 Prefix 中修改（生效稳定），不要在 `StartMainDialogue` 中修改（可能被覆盖）。
3. **仅修改第一句**：使用静态标记 `_firstLine` 控制，否则每句都会被修改。
4. **台词修改与货物初始化分离**：放在同一个 Patch 中会互相干扰，应分离到不同 Patch。
5. **RichTextBuilder 没有 Append 方法**：使用 `Clear()` + `AddLine()`。

### 8.3 对话池分类（40+ 数组，约 718 条）

| 分类 | 数组 |
| --- | --- |
| 玩家阶段 | Newbie / Growth / Famous |
| 通用 | Seller / Buyer / General / Special |
| 职业 | Gunsmith / Scav（4 类背景）/ LowerDoctor / Miner（买+卖）/ WaterMerchant / AlcoholMerchant / Farmer（买+卖） |
| 阵营 | Lower / Upper（买+卖）/ BankruptUpper（买+卖）/ Tourist（买+卖）/ Security / Rev / BlackMarket / Cartel / Church |
| 上层职业 | UpperPharma / UpperChef / UpperWaterMerchant |
| 玩家状态 | Poor / Rich / Newbie / Veteran / Shady / Reputable |
| 动态买卖 | BuildDynamicTradeLine + GetClientMainItemCategory |

### 8.4 对话风格原则

- 底层人：麻木、疲惫、直接，符合赛博朋克空间站环境
- 上层人（天龙人）：傲慢、刻薄、虚伪、炫富、居高临下
- 破产上层：落魄、无奈、回忆过去
- 游客：好奇、容易被宰
- 矿工：刚下井、咳嗽、购买烟酒止痛药、出售矿石
- 农场主：空间站水培背景，仅出售营养果、仅收购种子，朴实麻木

---

## 九、DebugMode 与测试工具

### 9.1 DebugMode 机制

```csharp
// Core.cs
public static bool DebugMode = true;  // 开发版 true，发布版 false

// TestHelper.Update 开头
if (!Core.DebugMode) return;
```

- **F11**：加钱 100000 + 测试包
- 发布版编译时设置 `DebugMode = false`
- **F11 功能仅限开发环境使用**，发布版默认移除

### 9.2 调试方法论

1. **先诊断再改代码**——编写诊断 Mod 输出日志，确认问题后再修改。
2. **一次只改一个变量**——修改后立即测试验证。
3. **失败两次即换思路**——不要在同一问题上反复尝试相同方案。
4. **参考已有 Mod**——解决方案可能已存在于 ExtraPerks / TradePerks 中。
5. **失败路径不要静默 catch**——添加日志以便定位问题。
6. **每个失败仅记录一次**——使用 HashSet 防止日志刷屏。

### 9.3 日志查看命令

```powershell
# 看特定 mod 的输出
Select-String -Path "Latest.log" -Pattern "modname" | Select-Object -Last 30
# 看错误
Select-String -Path "Latest.log" -Pattern "ERROR|Failed|Exception" | Select-Object -Last 20
# 看日志末尾
Get-Content "Latest.log" -Tail 30
```

---

### 9.4 RuntimeInspector 运行时检视器（鼠标悬停显示对象key/id）

**作用**：游戏内用鼠标悬停任何UI元素，自动输出名称/路径/组件/key字段到日志，开发AI读取日志就能"看到"用户在点什么。

**实现要点**：
- 用 EventSystem.current.RaycastAll(pointerData, results) 检测鼠标下的UI元素
- IL2CPP下必须用 Il2CppSystem.Collections.Generic.List<RaycastResult>，不能用System.Collections.Generic.List
- IL2CPP下这个游戏**没有IMGUI模块**（UnityEngine.GUI类不存在），不能在屏幕上画框，改用日志输出
- 每0.5秒输出一次当前悬停对象（避免日志刷屏），对象变化时立即输出
- 按F4 dump当前悬停对象的完整字段（所有公开/私有字段+属性+值）到 Mods/inspector_dump.txt

**关键发现（用RuntimeInspector发现的真实UI路径）**：
`
主菜单场景: EmporiumMenu
存档面板: Canvas/_LoadPanel
存档槽位: Canvas/_LoadPanel/Scroll View/Viewport/Content/SaveSlot(Clone)/LoadButton
加载黑屏: Canvas/_Loading_Blackscreen
游戏场景: InventoryScene
`

**使用方法**：
1. 用户鼠标悬停在任何UI元素上
2. 开发AI读取 MelonLoader/Latest.log 中的 [Inspector] 标签
3. 就能看到对象名称、层级路径、挂载组件、key/id字段

---

### 9.5 AutoLoadSave 自动进存档（基于真实UI路径）

**作用**：自动化测试时自动进入存档，不需要手动操作。

**实现流程（状态机）**：
`
WaitingForMenu → 等待主菜单场景(EmporiumMenu)
LookingForLoadPanel → 查找存档面板(_LoadPanel)，如果没找到就点击主菜单的LoadButton打开它
ClickingSaveSlot → 查找所有SaveSlot(Clone)/LoadButton，选择目标索引点击
WaitingForGame → 等待进入游戏场景(InventoryScene)或PlayerStore.Instance.playerCash>0
Done/Failed
`

**关键踩坑**：
1. **onClick.Invoke()在主菜单按钮上有效**——之前以为无效，实际上是因为没找到正确的按钮（主菜单有44个按钮，其中一个叫LoadButton，点击后打开存档面板）
2. **存档槽位按钮是SaveSlot(Clone)/LoadButton**——不是主菜单的按钮，需要先打开存档面板才能看到
3. **存档顺序会动态变化**——游戏会把最近玩的存档自动排到前面，第一次可能进错档，第二次就正常了
4. **支持存档索引选择**——通过 Mods/autoload_config.txt 配置 save_index=数字（0=第一个），日志会输出所有存档槽位信息方便确认
5. **进入游戏的判断条件**：场景是InventoryScene，或PlayerStore.Instance不为null且playerCash>0（新游戏界面现金为0）

**配置文件示例**（Mods/autoload_config.txt）：
`
# 指定要加载的存档索引（0=第一个，1=第二个...）
save_index=0
`

---

### 9.6 自动化测试框架（TestRunner + PowerShell）

**组成**：
1. **TestRunner.cs**——通过 -runtests 命令行参数触发，正常启动完全不触发
2. **run_tests.ps1**——启动游戏+超时强杀+心跳检测+读取结果
3. **build_and_test.ps1**——编译→部署DLL→调用run_tests

**TestRunner核心机制**：
- 命令行参数触发：Environment.GetCommandLineArgs() 包含 -runtests
- 协程执行测试：MelonCoroutines.Start(RunTestsCoroutine())
- 等待AutoLoadSave进存档（最多60秒），然后执行测试用例
- 结果写入 Mods/test_results.txt（格式：用例名|PASS/FAIL|详情|耗时ms）
- 心跳文件 Mods/test_heartbeat.txt（每用例更新，脚本检测是否卡住）

**四层退出兜底**（IL2CPP下Application.Quit()经常不生效）：
`
L1: Application.Quit()
L2: System.Environment.Exit(0)
L3: System.Diagnostics.Process.GetCurrentProcess().Kill()
L4: 外部脚本超时强杀（120秒）
`

**PowerShell脚本关键参数**：
- TimeoutSeconds=180：总超时
- HeartbeatTimeoutSeconds=30：心跳停止30秒认为卡住
- StartupGraceSeconds=60：启动宽限（IL2CPP游戏首次启动需生成互操作程序集，至少60秒）
- 心跳检测在启动宽限结束后才开始，避免误判

**关键踩坑**：
1. **游戏直接启动exe失败（退出码53）**——需要创建 steam_appid.txt（内容为游戏AppID）放在游戏根目录
2. **PowerShell中文编码乱码**——脚本和结果文件名全用英文
3. **IL2CPP游戏首次启动慢**——启动宽限至少60秒，心跳检测在宽限结束后才开始
4. **心跳初始时间问题**——$lastHeartbeatTime初始化为脚本启动时间，宽限结束后立即重置，避免误判
5. **测试用例在UI未初始化时执行会失败**——需要等待AutoLoadSave进存档后再执行需要UI的测试

**当前测试用例（6个）**：
- Mod加载状态（必跑）
- 关键类存在性（必跑）
- 关键方法存在性（必跑，~~MachinePrinter.Printer()~~ **0.46D 已删除该方法，预期失败**）
- 蛙哥物品池完整性（必跑）
- 枪械打印机关联物品（必跑，18个全缺失预期）
- 物品创建测试_3d_printer（需要存档）
- 枪械打印机字段完整性（需要存档）

---

### 9.7 未实装物品处理经验（以枪械打印机3d_printer为例）

**现象**：
- 本地化表有名字和描述，但运行时DirectoryMaster未注册
- 18个关联物品（printer_chip系列、printer_plastic、printed_gun等）全部NullReferenceException
- ~~MachinePrinter.Printer()~~ **0.46D 已删除该方法**（方法找不到）
- 强行创建后：图标用其他物品sprite兜底、双击无页面（contentWindow不存在）

**判断物品是否未实装的快速方法**：
1. DirectoryMaster.Item("id", true) 返回null或抛NullReferenceException
2. 本地化表有name/desc但运行时创建失败
3. 关联物品（同系列）全部创建失败
4. 工厂方法反射找不到或调用失败（如 0.46D 后 ~~MachinePrinter.Printer()~~ 已删除）

---
## 十、随机物品系统

### 10.1 物品池

- 约 150+ 物品 ID，从全物品库中筛选不常见物品
- 排除：水净化类、食材、工具、招牌、常见物品
- 包含：武器 / 弹药 / 模组 / 化学 / 电子 / 药品 / 箱子 / 卡 / 特殊物品

### 10.2 触发对象

- 所有普通 NPC 均携带随机物品
- 治安部（买家 / 卖家）不携带随机物品
- 特殊 NPC（博士 / 枪匠 / 水商 / 酒商）有专属商品列表

### 10.3 箱子内容

- `expedition_box` / `toolbox` / `trashcan`：塞入通用内容
- 治安箱 / 医疗箱 / 工程箱：使用 `PreBuiltItemHelper` 原版内容

---

## 十一、反编译经验

### 11.1 反编译方法对比

| 方法 | 可靠性 | 适用场景 | 推荐度 |
|------|--------|----------|--------|
| `Assembly.LoadFrom` | ❌ 低 | 无依赖的简单 DLL | ⭐ |
| `dotnet-ildasm` | ❌ 不稳定 | 不推荐 | ⭐ |
| `Mono.Cecil` | ⚠️ 中 | 简单 DLL，需 try-catch | ⭐⭐ |
| **MetadataReader 只读名称** | ✅ 高 | 仅需类型 / 方法 / 字段名 | ⭐⭐⭐⭐⭐ |
| **ILSpy GUI / ilspycmd** | ✅ 高 | 需要完整反编译代码 | ⭐⭐⭐⭐⭐ |
| **运行时反射调用** | ✅ 高 | DLL 已加载，仅需调用 | ⭐⭐⭐⭐⭐ |

### 11.2 MetadataReader 最小可用代码（只读名称，100% 可靠）

```csharp
using var fs = File.OpenRead("target.dll");
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();
foreach (var th in md.TypeDefinitions) {
    var td = md.GetTypeDefinition(th);
    string name = md.GetString(td.Name);
    if (name == "<Module>") continue;
    foreach (var mh in td.GetMethods()) {
        var m = md.GetMethodDefinition(mh);
        Console.WriteLine(md.GetString(m.Name));
    }
}
```

### 11.3 反编译游戏本身

- 游戏代码位于 `GameAssembly.dll` 中，并非 .NET 程序集
- 使用 Il2CppDumper 导出 dump.cs（仅含签名，方法体为空壳）
- 方法体逻辑位于原生代码中，静态反编译无法获取
- **需通过运行时诊断补丁**确认内部逻辑

### 11.4 核心原则

**能不反编译就不反编译，运行时反射调用更为直接高效。**

### 11.5 IL2CPP 游戏 4 层数据源结构（拆包前必读）

任何 IL2CPP 游戏的信息分布在 4 个独立层，**不要假设只有一个 dll**：

| 层 | 文件 | 内容 | 工具 | 能拿到什么 |
|---|---|---|---|---|
| 第1层 原生代码 | `GameAssembly.dll`（43MB） | IL2CPP 编译后的原生机器码，含方法体实现 | Cpp2IL / Ghidra / IDA | 方法体逻辑、算法、调用关系（汇编级） |
| 第2层 托管签名 | `MelonLoader\Il2CppAssemblies\*.dll` | IL2CPP 托管壳，类/方法/字段签名，**方法体为空** | dnSpy / ILSpy / ilspycmd | 类结构、方法签名、字段类型、继承关系 |
| 第3层 代码字符串 | `Probably Stolen_Data\il2cpp_data\Metadata\global-metadata.dat`（9.4MB） | 代码中的字符串字面量（常量字符串、错误信息、物品ID、标签名） | metadata 解析脚本 / 十六进制搜索 | 搜索字符串引用、定位方法、确认常量名 |
| 第4层 资源数据 | `Probably Stolen_Data\StreamingAssets\aa\` | AssetBundle（.bundle），含本地化表、游戏配置、预制体 | UnityPy / AssetStudio | ID类数据的真正来源、数值配置、文本内容 |

**关键认知**：
- 物品/角色/配置等游戏数据 ID **不在** 第3层（metadata），而在 **第4层**（AssetBundle 中的本地化表）
- 方法体逻辑 **不在** 第2层（Il2CppAssemblies），而在 **第1层**（GameAssembly.dll）
- 第2层的 DLL 用 ilspycmd 反编译后，所有方法体都是 `throw null` 或空 `{ }`——这是正常的，不是工具坏了

### 11.6 Il2CppDumper 完整流程（签名层提取）

**用途**：从 GameAssembly.dll + global-metadata.dat 提取所有类/方法/字段签名，输出为可读的 C# 伪代码。

```powershell
# 1. 准备文件（Il2CppDumper.exe 与 GameAssembly.dll、global-metadata.dat 同目录）
#    Il2CppDumper.exe 路径：_dumper\Il2CppDumper.exe

# 2. 运行（交互式，依次输入）
.\Il2CppDumper.exe
# 提示输入 executable path → GameAssembly.dll 的完整路径
# 提示输入 metadata path → global-metadata.dat 的完整路径
# 提示选择模式 → 选 1（Auto）或 2（Manual，需指定 RVA）

# 3. 输出文件（_dumper\ 目录下）
#    dump.cs          ← 主产物，18.6MB / 43.4万行，所有类的签名
#    script.json      ← 61.9MB，方法地址与符号的映射（供 Ghidra/IDA 使用）
#    il2cpp.h         ← 32.8MB，C++ 头文件（结构体定义）
#    stringliteral.json ← 1.9MB，所有字符串字面量
#    ida.py / ghidra.py ← 反汇编器导入脚本
```

**dump.cs 阅读方法**：
- 每个类以 `// Namespace: xxx` 开头，后跟 `public class ClassName : BaseClass`
- 字段标注偏移：`public int fieldName; // 0x24`
- 方法标注 RVA：`public void MethodName() { } // RVA: 0x568790`
- RVA 是方法在 GameAssembly.dll 中的相对虚拟地址，可用于 Cpp2IL / Ghidra 定位方法体

**常用搜索命令**：
```powershell
# 找特定类
Select-String -Path dump.cs -Pattern "class StoreClientManager" -Context 0,50

# 找特定方法（含 RVA）
Select-String -Path dump.cs -Pattern "HandleJacksonStorage"

# 找所有 Manager 类
Select-String -Path dump.cs -Pattern "class \w+Manager" | ForEach-Object { $_.Line.Trim() }

# 找字段偏移
Select-String -Path dump.cs -Pattern "jacksonInvestment" -Context 0,2
```

### 11.7 ilspycmd 反编译 Mod DLL（完整 C# 源码）

**用途**：反编译普通 .NET DLL（其他 Mod 的 DLL），获取完整 C# 源码（含方法体）。

```powershell
# 安装（仅一次）
dotnet tool install -g ilspycmd

# 基本反编译（输出到目录）
ilspycmd "D:\path\to\Mod.dll" -o "_src" -p

# 参数说明：
#   -o <dir>   输出目录
#   -p         生成 .csproj 项目文件（方便用 VS 打开）
#   -t         反编译为单个文件（不生成目录结构）

# 实际使用示例
ilspycmd "D:\谷歌\EmptyNukeBarrel.dll" -o "_emptybarrel_src" -p
ilspycmd "D:\谷歌\TagHover.dll" -o "_taghover_src" -p
ilspycmd "D:\谷歌\Contactless_Calendar.dll" -o "_calendar_src" -p
```

**输出结构**：
```
_src\
├── ModName.csproj          ← 可直接用 Visual Studio 打开
├── ModName\
│   ├── Core.cs             ← 入口类
│   ├── Patches.cs          ← Harmony 补丁
│   └── Properties\
│       └── AssemblyInfo.cs
```

**注意事项**：
- ilspycmd 8.2.0 对 IL2CPP 生成的 DLL（Il2CppAssemblies 下的）反编译后方法体为空——这是正常的，因为 IL2CPP 把方法体编译成了原生代码
- ilspycmd 对**普通 .NET DLL**（其他 Mod 编译的 DLL）可以完美反编译出完整方法体
- 升级 ilspycmd 可能失败（NuGet 源问题），8.2.0 足够使用

### 11.8 global-metadata.dat 字符串提取

**用途**：搜索代码中的字符串字面量（物品ID、标签名、错误信息、本地化键），定位方法和常量。

```powershell
# 方法1：Il2CppDumper 自动生成 stringliteral.json
# 格式：[{"value":"contraband","context":...}, ...]
$strings = Get-Content "_dumper\stringliteral.json" -Raw | ConvertFrom-Json
$strings | Where-Object { $_.value -match "jackson" } | ForEach-Object { $_.value }

# 方法2：直接二进制搜索（快速定位）
$bytes = [System.IO.File]::ReadAllBytes("global-metadata.dat")
$text = [System.Text.Encoding]::ASCII.GetString($bytes)

# 搜索所有物品ID
[regex]::Matches($text, "item_(\w+)_name") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

# 搜索违禁品相关字符串
[regex]::Matches($text, "contraband\w*") | ForEach-Object { $_.Value } | Sort-Object -Unique

# 搜索特性ID
[regex]::Matches($text, "perk_(\w+)_name") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
```

**已确认的关键字符串**（从 metadata 提取）：
- 违禁品标签：`contraband`、`contrabandLevel`、`contrabandType`、`contrabandMarkupLow/Mid/High/Critial`、`contrabandFinePercentage`
- 赃物标签：`STOLEN_VALUE_INT`、`TAG_NOT_PURCHASED`、`not_purchased`
- 机器标签：`IS_BATTERY_POWERED_TAG`、`STANDARD_MACHINE_TAG`、`GROWING_TAG`、`PROGRESS_ITEM_TARGET_TAG`、`HARVEST_QUANTITY`、`HARVEST_ITEM_ID`

### 11.9 反编译游戏本体 vs 反编译 Mod DLL（本质区别）

| 维度 | 游戏本体（Assembly-CSharp.dll） | Mod DLL（如 ExtraPerks.dll） |
|---|---|---|
| 编译方式 | IL2CPP（C# → C++ → 原生机器码） | 普通 .NET（C# → IL 字节码） |
| 方法体 | 在 GameAssembly.dll 中，DLL 里只有签名 | 完整保留在 DLL 中 |
| ilspycmd 结果 | 方法体为空 `{ }` 或 `throw null` | 完整 C# 源码 |
| 获取方法体 | Cpp2IL 汇编 / Ghidra 反编译 / 运行时诊断补丁 | ilspycmd 直接反编译 |
| 字段偏移 | 有（`// 0x24`） | 无（普通 .NET 布局） |
| RVA | 有（`// RVA: 0x568790`） | 无 |

**结论**：研究游戏逻辑用 Il2CppDumper + Cpp2IL；研究其他 Mod 怎么写用 ilspycmd。两者不可互相替代。

### 11.10 第4层 AssetBundle 拆解（UnityPy，ID类数据的真正来源）

**用途**：解包 `StreamingAssets\aa\` 下的 AssetBundle，提取本地化表（物品ID、角色名、对话文本）、游戏配置、预制体。这是所有 ID 类数据的权威来源。

**本游戏的 AssetBundle 结构**：
```
Probably Stolen_Data\StreamingAssets\aa\
├── catalog.json              ← Addressables 目录（所有 bundle 的索引）
├── settings.json
├── AddressablesLink\link.xml
└── StandaloneWindows64\
    ├── localization-assets-shared_assets_all.bundle        ← SharedTableData（明文 key，68.5KB）
    ├── localization-locales_assets_all.bundle               ← 语言列表（2.9KB）
    ├── localization-string-tables-english(en)_assets_all.bundle     ← 英文文本（142.8KB）
    ├── localization-string-tables-chinese(simplified)(zh)_assets_all.bundle  ← 中文文本（149.7KB）
    └── ... 其他 9 种语言
```

**UnityPy 解包脚本**（关键：StringTable 的 key 不直接存在 m_TableData 里，而是通过 m_Id 关联 SharedTableData）：

```python
import UnityPy, json, re

def extract_shared_data(bundle_path):
    """SharedTableData: 提取 id -> key 映射"""
    env = UnityPy.load(bundle_path)
    id_to_key = {}
    for obj in env.objects:
        if obj.type.name == 'MonoBehaviour':
            tree = obj.read_typetree()
            for e in tree.get('m_Entries', []):
                if isinstance(e, dict) and e.get('m_Id') and e.get('m_Key'):
                    id_to_key[e['m_Id']] = e['m_Key']
    return id_to_key

def extract_string_table(bundle_path, id_to_key):
    """StringTable: m_TableData 是 [{m_Id, m_Localized}]，通过 m_Id 查 key"""
    env = UnityPy.load(bundle_path)
    result = {}
    for obj in env.objects:
        if obj.type.name == 'MonoBehaviour':
            tree = obj.read_typetree()
            name = re.sub(r'_(en|zh|fr|de|it|ja|ko|ptbr|ru|es)$', '', tree.get('m_Name', ''))
            table = {}
            for e in tree.get('m_TableData', []):
                if isinstance(e, dict):
                    key = id_to_key.get(e.get('m_Id', 0), f'unknown_{e.get("m_Id")}')
                    table[key] = e.get('m_Localized', '')
            result[name] = table
    return result

# 使用
id_to_key = extract_shared_data('localization-assets-shared_assets_all.bundle')
en = extract_string_table('localization-string-tables-english(en)_assets_all.bundle', id_to_key)
zh = extract_string_table('localization-string-tables-chinese(simplified)(zh)_assets_all.bundle', id_to_key)
```

**本游戏 14 个本地化表及条目数**（[L1] 已确认）：

| 表名 | 条目数 | 内容 |
|---|---|---|
| Item | 809 | 物品名称/描述/风味文本 |
| Dialogue | 2321 | 剧情对话 |
| Mechanic | 1136 | 游戏机制说明 |
| UI | 568 | 界面文本 |
| ClientName | 193 | 客户名/公司名/派系名/地点名 |
| RepPerkTable | 138 | 声望特性 |
| NetworkUpgrade | 77 | 王尔德升级/恩惠 |
| PhoneDialogue | 87 | 电话对话 |
| MainMenu | 76 | 主菜单 |
| NewGame | 74 | 开局专精（14种） |
| PerkTable | 73 | 初始特性（36个） |
| Hint | 34 | 提示 |
| Tutorial | 24 | 教程 |
| Interactable | 11 | 可交互物提示 |
| **总计** | **5621** | |

**物品ID提取**：从 Item 表的 key 中用正则 `^(item_[a-z0-9_]+?)_(name|desc|flavor)$` 提取，本游戏共 **383 个唯一物品ID** [L1]。注意部分物品前缀不是 `item_`（如 `husbandry_`），需单独处理。

**王尔德升级完整列表**（从 NetworkUpgrade 表提取，[L1] 已确认）：
JACKSON1/JACKSON2、RETIRED_CHEMIST、RETIRED_FARMER、RETIRED_GUNSMITH、RETIRED_RANCHER、WINEMAKER、WATER_TRADER、BREWER、CHEMIST、PHARMA、CRIMINEL_NETWORK、GUN_PERMIT_I/II/III、SHOWCASE_I/II、RENOVATION1/2/3、MARKETPLACE_REQUESTS、RUINED_MACHINE_UNLOCK、MINER、COMMERCIAL_POWER、POWER_HIJACK、GUTTERFLOW_TAP、RUSTWATER_TAP、INSPECTION_INFORMANT、EVIDENCE_REMOVAL、SEC_GRACE、LANDLORD_GRACE、WILD_FIXER、BUY_REFERAL、SELL_REFERAL 等。

**14 种开局专精**（从 NewGame 表提取）：
generalist（学徒）、junker（废料客）、water_merchant（水商）、fence（销赃人）、hydroponic_farmer（水培农场主）、chemist（化学家）、pharmacist（药商）、gunsmith（枪械匠）、moonshiner（私酒贩子）、organ_dealer（器官贩子）、rancher（养鼠人）、street_food_vendor（街头小吃摊贩）、rock_bottom（一贫如洗）、advanced_start（进阶开局）。

### 11.11 从签名到方法体的验证路径（L1 → L2 → L3）

```
L1（签名确认）    Il2CppDumper dump.cs → 知道类名、方法签名、字段类型、RVA
    ↓
L2（逻辑确认）    三选一：
                  ① Cpp2IL ISIL 汇编 → 阅读 x86 指令推断分支和调用
                  ② Ghidra + script.json → 反编译为 C 伪代码
                  ③ 运行时诊断补丁 → Harmony Patch 打日志，观察参数/返回值/字段变化
    ↓
L3（运行验证）    编写实际 Mod，调用该方法，确认产生预期效果、无报错
```

**跳过 L2 直接从 L1 到 L3 = 盲试**，是大部分试错时间的来源。对于关键方法（价格计算、客户生成、库存管理），务必先拿到 L2 再写代码。

---

## 十二、参考 Mod 核心价值总结

| Mod | 核心价值 | 最值得借鉴 |
|-----|----------|-----------|
| **ExtraPerks** | 特性系统参考实现 | 自定义天赋 6 步实现、图标内嵌 + 原生字典注入 |
| **MoreEvents** | 游戏事件系统 | StoreEventManager 注册事件、NegociationData 动态价格、SetEndAction 中添加货物 |
| **TradePerks** | 交易机制 | GetNegociatedValue 改价格、买 / 卖深度计数器、ComputeInspectionCooldownRange 改检查冷却、InitStolenItem 改赃物热度 |
| **XIAOWOWeeklyVisitors** | 客户定期到访 | QueueFuturClient + HandleContentUnlockClient Postfix + TryAddClient 去重 |
| **XIAOWOHydroponicFix** | 物品创建拦截 | DirectoryMaster.Item Prefix + TagSystem 运行时状态 + 工厂模式 + 旧存档迁移 |
| **XIAOWO-WRDUnlock** | 网络升级解锁 | IsLockedInDemo Prefix + QueueFuturClient + 一次性账本防重复 |
| **AugPresenceGuard** | 数值守护（Aug 改造人存在度） | 多入口统一 Clamp(store,source) 钳制 + 防御性 Patch（AccessTools+warning）+ 引出 Aug 系统字段 |

---

## 十三、按症状快速诊断表

| 症状 | 90% 的原因 | 解决方案 |
|------|----------|---------|
| 物品属性全是 0 | itemTypes 没复制 | 复制 itemTypes |
| UI 空白 / 错误 | contentWindow 为 null | 复制 contentWindow |
| 占地大小不对 | shape 没正确引用 | 直接引用 template.shape |
| 放不进机器 | ability 没复制 | 复制 ability |
| 复制物品打不开 | child / 功能字段没复制 | 完整复制所有字段 |
| 上门客户没货 | Handle 调用时机不对 | StartMainDialogue 时调用，或 SetEndAction 中添加 |
| 台词改了不显示 | 只改了 text 没改 plainText | 两个都改 |
| 每句都被改 | 没有 _firstLine 标记 | 加静态标记 |
| 改完台词不卖货 | Patch 互相干扰 | 分离到不同 Patch |
| 晚上博士不卖 | 物品没加进 barterSellInventory | 检查库存 items.Count |
| 游戏卡死 | 反射太多 / OnUpdate 重操作 | 缓存结果 + 事件驱动 |
| Mod 不生效 | 旧 dll 没删 / 游戏没重启 | 删旧 dll + 完全重启 |
| 特性选择界面点不动 | 注册时机不对 | 用 InitStartingPerk Postfix 根治 |
| 图标空白 | hideFlags 没设 | 设 HideFlags.HideAndDontSave |
| 图标偏色 / 模糊 | filterMode 不是 Point | 设 filterMode=Point, wrapMode=Clamp |
| 买的时候也触发卖的效果 | 没有区分买 / 卖 | 用 _playerSellingDepth 深度计数器 |
| 价格变了但没标签 | 只用了 GetNegociatedValue | 另外加 ItemFeature 显示标签 |
| IL2CPP 委托赋值崩溃 | 没用 ConvertDelegate | 用 DelegateSupport.ConvertDelegate |
| typeof().GetField 返回 null | IL2CPP 类型系统不同 | 用实例.GetType().GetFields() 遍历 |

---

## 十四、文件结构

```
Probably Stolen Playtest\Mods\
├── WagesPerks.dll              # 开发版（DebugMode=true）
├── JacksonPerks\               # 源码工程
│   ├── Core.cs                 # DebugMode 开关
│   ├── Patches.cs              # Harmony 补丁
│   ├── FrogPowerPerk.cs        # 蛙哥牛逼特性
│   ├── PerkRegistration.cs     # 特性注册
│   ├── TestHelper.cs           # F11 测试工具
│   ├── Icons\                  # 特性图标
│   └── JacksonPerks.csproj
└── JacksonPerks_EN\            # 英文版源码

WagesPerks_发布版\
├── Mods\WagesPerks.dll         # 中文版发布版（DebugMode=false）
├── 英文版\Mods\WagesPerks.dll  # 英文版发布版
├── README.md
└── README_en.md
```

---

## 十五、半成品与未实装内容完整清单

### 15.1 什么是半成品？如何判断？

游戏中存在大量物品 / 系统已定义 ID、名称甚至完整逻辑，但最终未在游戏中启用。判断方法如下：

| 判断维度 | 半成品特征 | 已实装特征 |
|---------|-----------|-----------|
| 物品导出 name | 为空 | 有中文名 |
| metadata 本地化 | 只有 `item_xxx_name`，没有 `_desc / _flavor / _short` | name / desc / flavor 齐全 |
| dump.cs 引用 | 只有工厂方法定义，没有调用方 | 有多处调用 |
| 游戏内获取 | 不会自然出现 | 可通过正常途径获得 |
| 运行时行为 | 创建后字段不全（问号 / 属性 0） | 完整可用 |

**四类 name 为空的物品**（不要全部归为半成品）：
1. **真半成品**：有工厂方法 + 逻辑，但游戏未启用
2. **合成产物**：需要玩家制作，制作时才动态设置 name
3. **未汉化**：英文原版有名字，汉化包未覆盖
4. **条件触发型**：未鉴定 / 损坏状态下 name 为空，操作后显示真名

---

### 15.2 物品层：UnusedDirectory 类（200+ 个未实装物品）

`UnusedDirectory`（dump.cs 行 10775，TypeDefIndex 163）是开发者存放"已编写但未启用"物品的目录，继承自 `ItemDirectory`。

#### 15.2.1 飞船系统模块（19 个，游戏早期设计的飞船玩法）

以下为 `*System()` 方法，可能是空间站 / 飞船的可安装模块：

| 物品ID（推测） | 工厂方法 | 说明 |
|---------------|---------|------|
| navigation_system | `NavigationSystem()` | 导航系统 |
| propulsion_system | `PropulsionSystem()` | 推进系统 |
| power_system | `PowerSystem()` | 动力系统 |
| hydroponic_system | `HydroponicSystem()` | 水培系统（XIAOWO 已修复） |
| armor_conditioner_system | `ArmorConditionerSystem()` | 装甲调节系统 |
| bio_printer_system | `BioPrinterSystem()` | 生物打印系统（打印心 / 肺 / 肾） |
| water_recycler_system | `WaterRecyclerSystem()` | 水回收系统 |
| autonomous_medical_system | `AutonomousMedicalSystem()` | 自主医疗系统 |
| makeshift_hull_system | `MakeShiftHullSystem()` | 临时船体系统 |
| civilian_hull_system | `CivilianHullSystem()` | 民用船体系统 |
| heavy_duty_hull_system | `HeavyDutyHullSystem()` | 重型船体系统 |
| armored_hull_system | `ArmoredHullSystem()` | 装甲船体系统 |
| hull_repair_system | `HullRepairSystem()` | 船体修复系统 |
| mining_drill_system | `MiningDrillSystem()` | 采矿钻头系统 |
| salvage_gantry_system | `SalvageGantrySystem()` | 打捞龙门系统 |
| railgun_system | `RailgunSystem()` | 轨道炮系统 |
| point_defense_system | `PointDefenseSystem()` | 点防御系统 |
| xray_artillery_system | `XrayArtillerySystem()` | X 光火炮系统 |

**修复价值**：⭐⭐⭐ 玩法独特但工程量大，需理解飞船模块系统

#### 15.2.2 机器类（有完整逻辑可修复，最有价值）⭐⭐⭐⭐⭐

| 物品ID | 工厂方法 / 类 | UI窗口 | 说明 | 状态 |
|--------|------------|--------|------|------|
| `3d_printer` | `PreBuiltItemHelper.CreatePrinter()` / `MachinePrinter.Printer()` | `MachinePrinter.CreateMachineInventoryWindow(w,h)` → 6 槽 | 枪械打印机，芯片决定打印内容，模块决定质量 | 有 name 无 desc，问号 |
| `hydroponic` | `PreBuiltItemHelper.CreateHydroponic()` / `UnusedDirectory.HydroponicSystem()` | `CreateHydroponicSystemWindow(w,h)` → 3 槽 | 水培机，种植作物 | XIAOWO 已修复 |
| `evaporator` | 待确认 | 待确认 | 蒸发器（化工） | 半成品 |
| `alarm` | 待确认 | 待确认 | 报警器（防盗） | 半成品 |
| `blender` | 待确认 | 待确认 | 搅拌机（食物加工） | 半成品 |
| `heating_plate` | 待确认 | 待确认 | 加热板（食物加工） | 半成品 |
| `vending_fountain` | 待确认 | 待确认 | 自动售货饮水机 | 半成品 |
| `vending_machine` | 待确认 | 待确认 | 自动售货机 | 半成品 |
| bio_printer | `UnusedDirectory.BioPrinterSystem()` | `CreateBioPrinterSystemWindow()` → 3 槽 | 生物打印机，打印心 / 肺 / 肾 | 连 ID 都没注册 |
| water_recycler | `UnusedDirectory.WaterRecyclerSystem()` | `CreateWaterRecyclerSystemWindow()` → 3 槽 | 水回收器，废水净化 + 性能升级 | 连 ID 都没注册 |

**机器类通用修复模式**（详见 6.15 节）：
```csharp
// 1. 用专用工厂方法创建（不要用 DirectoryMaster.Item）
GameItem item = PreBuiltItemHelper.CreatePrinter();
// 2. 手动补全字段
item.identifier = "3d_printer";
item.name = "3D打印机";
item.shortDescription = "...";
item.flavorText = "...";
// 3. 设置图标和形状
item.SetSpriteAndShape("Items/items_tool", "3d_printer");
// 4. 重建UI窗口
var windowData = MachinePrinter.CreateMachineInventoryWindow(300, 200);
item.SetContentWindow(windowData.Item1);
// 5. 初始化进度系统
MachineProgressHelper.InitProgressTypeMachine(item, speed);
// 6. 设置电池槽和标签
MachineProgressHelper.SetupBatterySlot(...);
item.EnableTag("IS_BATTERY_POWERED_TAG", false);
```

#### 15.2.3 武器 / 枪械变种（20+ 个）

**左轮手枪变种**：
- `CreateRevolverLong()` — 长枪管左轮
- `CreateRevolverShort()` — 短管左轮
- `CreateRevolverShortTactical()` — 战术短管左轮

**枪械接收器（可组装枪械的部件）**：
- `CreateM16Receiver()` — M16 接收器
- `CreateMP9Receiver()` — MP9 接收器
- `CreateUMPReceiver()` — UMP 接收器
- `CreateMagnumReceiver()` — 马格南接收器
- `CreateGlockReceiver2()` — 格洛克接收器 2
- `CreateShotgunReceiver()` — 霰弹枪接收器
- `CreateShotgunStock()` — 霰弹枪枪托
- `CreateShotgunGrip()` — 霰弹枪握把
- `CreatSTANAG(sprite, size)` — STANAG 弹匣

**其他武器**：
- `CreateHeavyHandmadePistolSuppressed()` — 消音重型手工手枪
- `ArmorTitan9()` / `ArmorTitan10()` / `ArmorTitan10L/H/S()` — 泰坦护甲（6 种）

**修复价值**：⭐⭐⭐ 丰富武器选择，但需理解枪械组装系统

#### 15.2.4 药品 / 化学品（15+ 个）

**药片**：
- `BicarsolePill()` / `BicarsolePillBottle()` — Bicarsole 药片 / 药瓶
- `OcutolPill()` / `OcutolPillBottle()` — Ocutol 药片 / 药瓶
- `FixalinPill()` / `FixalinPillBottle()` — Fixalin 药片 / 药瓶
- `OxycodonePill()` / `OxycodonePillBottle()` — 羟考酮药片 / 药瓶
- `PhagimycinPillBottle()` / `UnlicensedPhagimycinPillBottle()` — 噬菌霉素药瓶（已有无证版）

**其他药品**：
- `HeroinPowder()` — 海洛因粉
- `HemostaticAgent()` — 止血剂
- `AloeVera()` / `AloeVeraGel()` — 芦荟 / 芦荟胶
- `Lifeweed()` / `LifeweedSeed()` / `LifeweedSeedPacket()` — 生命草 / 种子 / 种子包
- `HighweedSeed()` / `HighweedLeaf()` — 高级杂草 / 种子 / 叶子

**修复价值**：⭐⭐ 丰富药品系统，但很多可能已有类似物品

#### 15.2.5 家具 / 设施（10+ 个）

- `CreateCraftingTable()` — 合成台
- `CreateIndustrialFridge()` — 工业冰箱
- `CreateSafe()` / `Safe()` — 保险箱
- `CreateGunSafe()` — 枪柜
- `CreateBed()` — 床
- `CreateMirrorCabinet()` — 镜柜
- `CreateEndTable()` — 边桌
- `CreateDesk()` — 书桌
- `CreateSecuritySign()` — 安全标识
- `CreateTestVendingMachine()` — 测试自动售货机
- `LargeStorageBay()` — 大储物格

**修复价值**：⭐⭐⭐ 丰富店铺经营和基地建设

#### 15.2.6 容器 / 包装（30+ 个）

**箱子 / 盒**：
- `LootboxSmall/Medium/Large()` — 战利品箱（小 / 中 / 大）
- `OrganTransportContainer()` — 器官运输容器
- `CardboardBox()` / `LargeCardboardBox()` — 纸箱（普通 / 大）
- `FirstAidBox()` — 急救箱
- `BearPawBox()` — 熊掌盒
- `LunchBox()` — 午餐盒
- `AmmoBox()` — 弹药箱
- `NutrifruitCrate()` — 营养果箱
- `BoxTeaBag()` — 茶包盒
- `BoxNail()` — 钉子盒

**袋 / 包**：
- `InjectorPouch()` / `InjectorPouchProto()` / `InjectorPouchLarge()` / `InjectorPouchLargeDouble()` — 注射器袋（4 种）
- `FirstAidPouch()` — 急救袋
- `PouchGeneral()` / `PouchGeneralLarge()` — 通用袋（2 种）
- `PouchHydration()` — 水合袋
- `PacketCigaretteBlack()` — 黑色香烟包
- `NutrifruitPackaged()` — 包装营养果

**药瓶**（7 种）：
- `PillBottle()`、`BicarsolePillBottle()`、`OcutolPillBottle()`、`FixalinPillBottle()`、`PhagimycinPillBottle()`、`UnlicensedPhagimycinPillBottle()`、`OxycodonePillBottle()`

**修复价值**：⭐⭐ 丰富储物和包装，但很多可能功能重复

#### 15.2.7 爆炸物（4 种）

- `MolotovCocktail()` — 燃烧瓶
- `AntiqueGrenade()` — 古董手雷
- `FragGrenade()` — 破片手雷
- `IncendiaryGrenade()` — 燃烧手雷

**修复价值**：⭐⭐ 可能已有类似物品，或属于未实装的战斗系统

#### 15.2.8 情报 / 任务物品（10+ 个）

- `IntelItem()` — 情报物品
- `NavDisk()` — 导航盘
- `IntelSpawner()` — 情报生成器
- `EventDebugger()` — 事件调试器
- `CombatScavengeItem()` — 战斗拾荒物品
- `CodeBook()` — 密码本
- `VisualInspectionBook()` — 视觉检查手册
- `LoanContract(title, body, signature)` — 贷款合同（可自定义内容）
- `SecCard()` — 安全卡
- `CreateSpectralAnalyser()` — 光谱分析仪
- `MirageGenerator()` — 幻象生成器

**修复价值**：⭐⭐⭐ 可用于任务线和剧情扩展

#### 15.2.9 背包 / 装备（10+ 个）

- `EquippableBack()` — 可装备背部
- `TechnicianBackpack()` — 技师背包
- `BackpackSmallModular()` / `BackpackMediumModular()` / `BackpackLargeModular()` — 模块化背包（3 种）
- `TacitalEntryDevice()` — 战术进入装置
- `Lockpicks()` — 开锁器
- `Wrench()` — 扳手
- `SheetSteel(count)` — 钢板
- `ExoticFuel()` — 异域燃料
- `TrashPile()` — 垃圾堆

#### 15.2.10 其他杂项（10+ 个）

- `Coin(count)` / `TestCoin()` — 硬币 / 测试硬币
- `EnergyCreditCounterfeit()` — 伪造能量币
- `Wall()` / `WallStripped()` / `WallMold()` / `WallWindow()` / `WallWindowSmashed()` — 墙壁（5 种状态）
- `PassengerCabin()` — 乘客舱
- `Soap()` — 肥皂
- `Bedsheet()` — 床单
- `TestWater()` / `TestWater2()` / `TestWater3()` — 测试水（3 种，调试用）

---

### 15.3 特性层：原版 StartingPerk（标题称 22 个，表格实际列出 23 项 ⚠️不一致）

`StartingPerkList` 类（dump.cs 行 29975）定义了原版游戏的开局特性：

| 特性ID（推测） | 工厂方法 | 类型（推测） | 说明 |
|---------------|---------|-------------|------|
| saving | `Saving()` | 正面 | 储蓄 |
| bad_lease | `BadLease()` | 负面 | 糟糕的租约 |
| convict | `Convict()` | 负面 | 罪犯 |
| model_citizen | `ModelCitizen()` | 正面 | 模范公民 |
| well_connected | `WellConnected()` | 正面 | 人脉广泛 |
| alert | `Alert()` | 正面 | 警觉 |
| sawyer_crew | `SawyerCrew()` | 正面 | 索耶船员 |
| diplomatic | `Diplomatic()` | 正面 | 外交 |
| intimidating | `Intimidating()` | 正面 | 恐吓 |
| side_street | `SideStreet()` | 中性 | 小巷 |
| nicotine_addiction | `NicotineAddiction()` | 负面 | 尼古丁成瘾 |
| narcotic_addiction | `NarcoticAddiction()` | 负面 | 麻醉剂成瘾 |
| alcohol_addiction | `AlcoholAddiction()` | 负面 | 酒精成瘾 |
| secret_admirer | `SecretAdmirer()` | 正面 | 暗恋者 |
| colorful_character | `ColorfulCharacter()` | 中性 | 多彩角色 |
| blackmail | `Blackmail()` | 负面 | 敲诈 |
| blabbermouth | `Blabbermouth()` | 负面 | 大嘴巴 |
| minimalist | `Minimalist()` | 正面 | 极简主义者 |
| sentinel | `Sentinel()` | 正面 | 哨兵 |
| welfare_program | `WelfareProgram()` | 正面 | 福利计划 |
| proficient_scavenger | `ProficientScavenger()` | 正面 | 熟练拾荒者 |
| gambling_addiction | `GamblingAddiction()` | 负面 | 赌博成瘾 |
| tapehead | `Tapehead()` | 中性 | 磁带迷 |

**注意**：以上为原版特性。本 Mod 额外添加了 12 个自定义特性（蛙哥牛逼、博士之友等）。自定义特性的注册方法详见 5.2 节。

---

### 15.4 房间 / 区域层：RoomFactory.RoomPrefab（14+ 个房间）

`RoomFactory.RoomPrefab` 枚举（dump.cs 行 12198）定义了空间站的房间类型：

| 枚举值 | 值 | 说明 |
|--------|---|------|
| `None` | 0 | 无 |
| `hydroponics` | 1 | 水培室 |
| `crew_quarter` | 2 | 船员舱 |
| `kitchen` | 3 | 厨房 |
| `cafeteria` | 4 | 自助餐厅 |
| `infirmary` | 5 | 医务室 |
| `workshop` | 6 | 车间 |
| `supply_depot` | 7 | 补给站 |
| `kitchen_freezer` | 8 | 厨房冰箱 |
| `power_bay` | 9 | 动力舱 |
| `checkpoint` | 10 | 检查站 |
| `bridge` | 11 | 舰桥 |
| `engineering_bay` | 12 | 工程舱 |
| `security` | 13 | 安保室 |
| ... | 14+ | 更多（需继续读取枚举） |

**市场区域**（此前反编译发现）：
- `marketplace` = 25 — 市场
- `marketplace_clinic` = 26 — 市场诊所（博士所在）
- `marketplace_trader` = 27 — 市场商人
- `marketplace_military_surplus` = 28 — 市场军用品商店

**修复价值**：⭐⭐ 可能有些房间未实装或未开放，可用于扩展可探索区域

---

### 15.5 事件层：StoreEvent（6+ 个事件）

`StoreEvent` 类（dump.cs 行 39888）和相关工厂方法：

| 事件 | 工厂方法 | 说明 |
|------|---------|------|
| 交租日 | `CreateRentDayEvent()` | 交租日事件 |
| 抵押贷款还款 | `CreateMortgagePaymentEvent()` | 抵押贷款还款事件 |
| 卡特尔访问 | `CreateCartelVisitEvent()` | 卡特尔访问事件 |
| 通缉 3 捕获 | `Wanted3CapturedEvent()` | 通缉 3 捕获事件 |
| 彩票 | `HandleLotteryEvent()` | 彩票事件 |
| 调试 | `HandleDebugEvent()` | 调试事件 |

**事件系统核心类**：
- `StoreEventManager` — 事件管理器（行 40142）
- `StoreEventActionDict` — 事件动作字典（行 40079）
- `StoreEventBlueprint` — 事件蓝图（行 40091）

**添加自定义事件的方法**：详见 3.1 节（参考 MoreEvents）

---

### 15.6 商人 / 商店层：TraderFactory

`TraderFactory` 类（dump.cs 行 12162）定义了各种商人：

| 方法 | 说明 |
|------|------|
| `CreateDoctor()` | 博士商人（市场诊所） |
| `CreateTrader()` | 通用商人 |
| `CreateArmyVendor()` | 军火商 |
| `CreateFoundryTrader()` | 熔炉商人 |
| `CreateFenceTrader()` | 销赃商人 |

**商店方法**（`TradeSheet` 类）：
- `GetGenericMilitaryShop()` — 通用军用品商店
- `GetGenericMedicalShop()` — 通用医疗商店
- `GetGenericTraderShop()` — 通用杂货商店
- `GetFoundryShop()` — 熔炉商店
- `GetEnergyFarmShop()` — 能源农场商店
- `GetNutrifruitFarmShop()` — 营养果农场商店
- `GetFenceShop()` — 销赃商店

**修复价值**：⭐⭐⭐ 可能有些商人 / 商店未实装，可用于扩展交易选项

---

### 15.7 修复优先级总表

| 优先级 | 内容 | 理由 | 修复难度 |
|--------|------|------|---------|
| ⭐⭐⭐⭐⭐ | `3d_printer` 枪械打印机 | 前述问号显示问题，逻辑完整 | 中 |
| ⭐⭐⭐⭐ | `BioPrinterSystem` 生物打印机 | 打印人体器官，玩法独特 | 中高 |
| ⭐⭐⭐⭐ | `WaterRecyclerSystem` 水回收器 | 废水净化 + 性能升级 | 中 |
| ⭐⭐⭐ | 武器变种（M16 / MP9 / UMP 等） | 更多枪械选择 | 高（需理解组装系统） |
| ⭐⭐⭐ | 家具设施（合成台 / 工业冰箱 / 床） | 丰富店铺经营 | 中 |
| ⭐⭐⭐ | 情报 / 任务物品 | 可用于任务线和剧情 | 中 |
| ⭐⭐ | 药品 / 化学品 | 丰富药品系统 | 低中 |
| ⭐⭐ | 容器 / 包装（30+ 个） | 丰富储物，但功能可能重复 | 低 |
| ⭐⭐ | 飞船系统模块（19 个） | 玩法独特但工程量大 | 高 |
| ⭐ | 爆炸物 / 杂项 | 可能已有类似物品 | 低 |

---

### 15.8 自行扫描半成品的方法

```powershell
# 1. 从物品导出找 name 为空的物品
$content = Get-Content "物品属性导出_完整.json" -Raw | ConvertFrom-Json
$content | Where-Object { $_.name -eq "" } | ForEach-Object { $_.id }

# 2. 从 metadata 找只有 name 没有 desc/flavor 的物品
$bytes = [System.IO.File]::ReadAllBytes("global-metadata.dat")
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
$nameMatches = [regex]::Matches($text, "item_(\w+)_name")
foreach ($m in $nameMatches) {
    $id = $m.Groups[1].Value
    $hasDesc = $text -match "item_$($id)_desc"
    $hasFlavor = $text -match "item_$($id)_flavor"
    if (-not $hasDesc -and -not $hasFlavor) { Write-Host $id }
}

# 3. 从 dump.cs 找 UnusedDirectory 中的所有工厂方法
Select-String -Path "dump.cs" -Pattern "private GameItem \w+\(\)" | 
  Where-Object { $_.LineNumber -gt 10775 -and $_.LineNumber -lt 11200 } |
  ForEach-Object { $_.Line.Trim() }

# 4. 从 dump.cs 找有工厂方法但零引用的物品
# （搜索物品ID字符串，如果只在工厂方法定义处出现一次，就是未启用）
```

---

## 十六、ExtraPerks + XIAOWO 深度拆解补充（ilspycmd 11 重新反编译，2026-09-02）

> 使用 ilspycmd 11.0.0.9375（需 .NET 10 运行时）重新反编译 ExtraPerks.dll（65KB，10 个自定义特性）+ XIAOWOUnlimitedBayExpansion.dll，发现以下文档此前缺失的关键经验。

### 16.1 特性系统补充

#### 16.1.1 特性本地化必须挂载 4 个 Patch 点（此前仅记录 1 个）

```csharp
// ① 显示名称（特性选择界面 + 状态栏）
[HarmonyPatch(typeof(StartingPerk), "GetLocalizedDisplayName")]
static bool Prefix(StartingPerk __instance, ref string __result) {
    if (!CustomPerks.TryGetLoc(__instance.id, out string name, out _)) return true;
    __result = name; return false;
}

// ② 描述
[HarmonyPatch(typeof(StartingPerk), "GetLocalizedDescription")]
static bool Prefix(StartingPerk __instance, ref string __result) {
    if (!CustomPerks.TryGetLoc(__instance.id, out _, out string desc)) return true;
    __result = desc; return false;
}

// ③ perk_{id}_name / perk_{id}_desc 格式的本地化表查询
[HarmonyPatch(typeof(LocHelper), "GetLocalizedPerkTable", new[] { typeof(string), typeof(Il2CppReferenceArray<Object>) })]
static bool Prefix(string key, ref string __result) {
    if (!key.StartsWith("perk_")) return true;
    // 解析 id，从自定义特性字典取文本
}

// ④ ★ Tooltip 文本直接设置（最关键，此前遗漏）
[HarmonyPatch(typeof(StartingPerkElement), "SetTooltipContent")]
static void Postfix(StartingPerk perk) {
    if (!CustomPerks.TryGetLoc(perk.id, out string name, out string desc)) return;
    PerkUIController ui = PerkUIController.Instance;
    if (ui.startingPerkTooltipTitle != null)
        ((TMP_Text)ui.startingPerkTooltipTitle).text = name;
    if (ui.startingPerkTooltipDescription != null)
        ((TMP_Text)ui.startingPerkTooltipDescription).text = desc;
}
```

**注意事项**：仅挂载 `LocHelper.GetLocalizedPerkTable` 不足——特性选择界面的 tooltip 走的是 `SetTooltipContent`，不挂载此点则 tooltip 为空。

#### 16.1.2 特性 ID 必须追加 `\0` 字符（此前完全未提及）

```csharp
internal override string Id => "董事会内线\0\0\0\0\0\0\0";  // 后面跟7个\0
internal override string DisplayName => "董事会内线\0\0\0\0\0\0\0\0";
```

**原因**：IL2CPP 对字符串有长度对齐要求，不追加 `\0` 将导致 `StartingPerk.IsPerkActive(id)` 匹配失败、`StartingPerkList.Perks` 去重失败。不同特性所需的 `\0` 数量可能不同，建议统一追加 7–8 个 ⚠️经验性建议，未形成统一规则。

#### 16.1.3 特性栏位扩展（奇葩人物特性实现）

```csharp
// 选中时 +6 栏位，取消时还原
[HarmonyPatch(typeof(PerkUIController), "OnChange")]
static void Postfix(PerkUIController __instance) {
    int target = IsSelectedIn(__instance) ? 6 : 0;
    int delta = target - extraApplied;
    if (delta != 0) {
        __instance.maxPerkCount += delta;
        extraApplied = target;
    }
}
```

#### 16.1.4 特性效果触发的精确 Hook 点

```csharp
// 新游戏时（比 OnNewGame 更精确）
[HarmonyPatch(typeof(StartingPerkContent), "HandleNewGamePerk")]
static void Postfix() { CustomPerks.NotifyNewGame(); }

// 每天开始时
[HarmonyPatch(typeof(PlayerStore), "BeginDay")]
static void Postfix() { CustomPerks.NotifyNewDay(); }

// 基类虚方法分发
internal abstract class CustomStartingPerk {
    internal abstract void OnNewGame();
    internal virtual void OnNewDay() { }  // 默认空实现
}
```

### 16.2 InventoryGrant 工具类（完整实现，此前仅有片段）

```csharp
internal static class InventoryGrant {
    // 检查背包是否已有，没有则创建并添加到后台库存
    internal static bool TryGrant(string identifier) {
        EmporiumEntry e = EmporiumEntry.Instance;
        if (Contains(e.GetInvItems(), identifier)) return true;
        if (TryAdd(MarkOwned(DirectoryMaster.Item(identifier, true))))
            return Contains(e.GetInvItems(), identifier);
        return false;
    }

    // 添加到后台库存（backpack）
    internal static bool TryAdd(GameItem item) {
        MarkOwned(item);
        EmporiumEntry e = EmporiumEntry.Instance;
        e.backInvinvElement.TryFindOneValidInventorySlot(item, false);
        if (!((GameInventory)e.backInvinvElement).UncheckedAccept(item)) return false;
        e.TransferOwnershipBackInv();
        e.TransferOwnedItemBackToInv();
        return true;
    }

    // 添加到前台库存（柜台上，玩家直接可见）
    internal static bool TryAddOutside(GameItem item) {
        GameGridInventory front = EmporiumEntry.Instance.frontInvinvElement;
        front.TryFindOneValidInventorySlot(item, false);
        return ((GameInventory)front).UncheckedAccept(item);
    }

    // 标记为已拥有（移除"未购买"标签）
    internal static GameItem MarkOwned(GameItem item) {
        item.DisableTag("TAG_NOT_PURCHASED", true);
        item.DisableTag("not_purchased", true);
        return item;
    }

    private static bool Contains(List<GameItem> items, string id) {
        for (int i = 0; i < items.Count; i++)
            if (items[i].identifier == id) return true;
        return false;
    }
}
```

### 16.3 客户系统补充

#### 16.3.1 自定义客户创建的 `??` 链方式（比 `new StoreClient()` 更可靠）

```csharp
// 用游戏已有工厂创建基础客户，??链确保非空
StoreClient val = StoreClientList.CreateStampSeller()
         ?? StoreClientList.CreateJunkerSellOnly()
         ?? StoreClientList.CreateShadyMerchant();

// 设置阵营（黑市）
StoreClientFactionSetup.InitBlackMarket(val);

// ★ 必须先 CompleteClientCreation，再改字段
val.CompleteClientCreation(true);

// 然后手动覆盖 identifier / displayName / 资金 / 收购列表
val.identifier = "robin_banks";
val.displayName = "罗宾·班克斯\0\0\0\0\0";
val.isRegularSellDisable = false;
val.useClientBudget = false;
val.clientCash = 0;
val.SetClientBudget(0, 0);
val.OnArrival = null;
val.clientBuyingIdList?.Clear();
val.clientBuyingTagList?.Clear();
val.clientItemFeatureBuying?.Clear();

// 复制另一个客户的外观/阵营
StoreClient look = StoreClientList.CreateShadyMerchant();
val.possibleSprites = look.possibleSprites;
val.spriteName = look.spriteName;
val.clientFaction = look.clientFaction;

// 最后 AddClient
storeClientManager.AddClient(val);
```

**关键顺序**：`CreateXxx()` → `InitBlackMarket()` → `CompleteClientCreation(true)` → 修改字段 → `AddClient()`。顺序错误将导致货物不初始化或字段被覆盖。

#### 16.3.2 对话结束回调的可靠方式（ExecuteEndAction Postfix）

```csharp
// ① 设置对话结束回调（用 DelegateSupport 转换）
client.mainDialogue.SetEndAction(
    DelegateSupport.ConvertDelegate<Action>(new Action(OnGreetingDone)));

// ② ★ Patch Dialogue.ExecuteEndAction，在回调执行后添加货物
[HarmonyPatch(typeof(Dialogue), "ExecuteEndAction")]
static void Postfix() {
    if (!ShouldStockAfterGreeting()) return;
    StockCrate(CurrentClient());  // 在这里添加货物到柜台
}

// ③ 防重复标记
private static bool awaitingGreetingEnd = false;
private static bool crateStocked = false;
```

**为何不在 `SetEndAction` 中直接编写代码**：`SetEndAction` 的 delegate 在 IL2CPP 下有时不触发或触发时机不对，`ExecuteEndAction` Postfix 是 100% 可靠的方式。

#### 16.3.3 战利品箱创建（PreBuiltItemHelper）

```csharp
// 4 种官方战利品箱，内容随机但都是正常物品
GameItem crate = Random.Range(0, 4) switch {
    0 => PreBuiltItemHelper.LootCrateSecurity(),     // 安保箱
    1 => PreBuiltItemHelper.LootCrateMedical(),      // 医疗箱
    2 => PreBuiltItemHelper.LootCrateEngineering(),  // 工程箱
    _ => PreBuiltItemHelper.LootCrateService(),      // 后勤箱
};

// 设置赃物状态
crate.EnableTag("TAG_NOT_PURCHASED", true);
crate.EnableTag("not_purchased", true);
StolenHelper.InitStolenItem(crate, 100);  // 100=全赃
StolenHelper.SetHeat(crate, 100);          // 热度100

// 添加到柜台
PlayerStore.Instance.AddDirectSellingItemToTable(crate, false, true, false, 100);

// ★ 刷新交易 UI（不刷新则价格 / 物品不更新）
NegociationUIManager.Instance.DispatchUpdateForCurrentIntent();
NegociationUIManager.Instance.RefreshUI();
```

#### 16.3.4 强制客户买卖意图（4 个 Patch 点）

```csharp
// 强制只卖不买
[HarmonyPatch(typeof(StoreClient), "IsClientSelling")]
static void Postfix(StoreClient __instance, ref bool __result) {
    if (IsMyClient(__instance)) __result = true;
}

[HarmonyPatch(typeof(StoreClient), "IsClientBuying")]
static void Postfix(StoreClient __instance, ref bool __result) {
    if (IsMyClient(__instance)) __result = false;
}

[HarmonyPatch(typeof(PlayerStore), "GetCurrentClientIntent")]
static void Postfix(ref ClientIntent __result) {
    if (IsMyClient(CurrentClient())) __result = (ClientIntent)2;  // SELL
}

// 禁止以物易物
[HarmonyPatch(typeof(StoreClient), "SetBarterOffer")]
static bool Prefix(StoreClient __instance) {
    return !IsMyClient(__instance);  // 是我的客户就跳过
}
```

#### 16.3.5 客户对话 Patch 点全集（ExtraPerks 用了 9 个）

| Patch 点 | 用途 |
|---|---|
| `StoreClientDialogList.CreateAfterBuyingFromClientDialog` | 玩家买完后的对话 |
| `StoreClientDialogList.CreateDefaultAllDoneDialog` | 全部交易完成的对话 |
| `StoreClientDialogList.CreateAlreadyBoughtDialog` | 已买过的对话 |
| `StoreClientDialogList.GetRandomDialogAccepted` | 接受报价的随机对话 |
| `StoreClientDialogList.GetRandomDialogDone` | 交易完成的随机对话 |
| `StoreClientDialogList.GetRandomDialogAcceptedString` | 接受报价的随机文本 |
| `StoreClientDialogList.GetRandomDialogDoneString` | 交易完成的随机文本 |
| `PlayerStore.OnItemBought` | 玩家买入物品后 |
| `StoreClient.OnItemSold` | 客户卖出物品后 |

### 16.4 博士专属 Hook 点（HandleJacksonStorage）

```csharp
// ★ 博士（inventorStorage）专属的存储处理方法，比 HandleContentUnlockClient 更精准
[HarmonyPatch(typeof(StoreClientManager), "HandleJacksonStorage")]
static void Postfix(StoreClientManager __instance) {
    int day = StoreStation.GetDayCounter();
    if (day <= 0 || day % 7 != 3) return;  // 每周三

    // 防重复：用 day + manager 指针双重判断
    if (lastScheduledDay == day && lastScheduledManagerPointer == ptr) return;

    // 检查冷却
    if (StoreClientManager.IsClientOnCooldown("inventorStorage")) return;

    // 创建并添加
    StoreClient jackson = StoreClientList.CreateInventorStorage();
    __instance.clientStack.Add(jackson);  // 直接 Add，不用 TryAddClient
    __instance.TrackClient(jackson);
}
```

### 16.5 闭包方法 Hook（`__c._xxx_b__N_M`）

```csharp
// 编译器生成的闭包类方法，用于修改博士库存扩充
[HarmonyPatch(typeof(__c), "_CreateInventorStorage_b__31_0")]
static bool Prefix() {
    // 在博士创建库存时，额外添加大箱子
    GameItem box1 = DirectoryMaster.Item("storage_bay_large", true);
    PlayerStore.Instance.AddDirectSellingItemToTable(box1, false, false, false, 0);

    GameItem box2 = DirectoryMaster.Item("machine_bay_ext", true);
    PlayerStore.Instance.AddDirectSellingItemToTable(box2, false, false, false, 0);

    return false;  // 跳过原方法
}
```

**如何查找闭包方法名**：在 dump.cs 中搜索 `__c` 类，其中方法名格式为 `_<原方法名>b__<闭包编号>_<lambda编号>`。

### 16.6 对象指针去重（详细实现）

```csharp
private static int lastScheduledDay = int.MinValue;
private static long lastScheduledManagerPointer;

// 获取 Il2Cpp 对象的原生指针
long ptr = ((Il2CppObjectBase)manager).Pointer.ToInt64();

// 双重判断：同一天 + 同一个 manager 实例 = 重复
if (lastScheduledDay == day && lastScheduledManagerPointer == ptr) return;

// 记录
lastScheduledDay = day;
lastScheduledManagerPointer = ptr;
```

**为何需要指针判断**：同一天内 `HandleJacksonStorage` 可能被调用多次（不同 manager 实例或重入），仅用 day 判断会导致重复添加客户。

### 16.7 声望系统修改（惹毛安保 / 势利眼特性实现）

```csharp
// 限制声望上限（Prefix 修改 modValue）
[HarmonyPatch(typeof(StoreReputation), "ModReputation", new[] { typeof(double) })]
static void Prefix(StoreReputation __instance, ref double modValue) {
    if (IsActive() && __instance.factionId == StoreClient.FACTION_SECURITY) {
        double cap = -45.0 - __instance.GetReputationExact();
        if (modValue > cap) modValue = cap;  // 超过上限就截断
    }
}

// 交易声望倍率（Postfix 修改 __result）
[HarmonyPatch(typeof(BargainUIManager), "ComputeRepPer1000Credits")]
static void Postfix(string factionId, ref double __result) {
    if (factionId == StoreClient.FACTION_UPPER) __result *= 1.2;
    else if (factionId == StoreClient.FACTION_LOWER) __result *= 0.8;
}
```

### 16.8 橙汁概念验证 Mod（OrangeJuice）补充

```csharp
// ★ 物品创建方式：ItemDirectory.CreateEmptyItem + 链式调用（不是 DirectoryMaster.Item Prefix）
GameItem val = ItemDirectory.CreateEmptyItem((string)null)
    .SetName("Orange Juice")
    .SetSpriteAndShape("items/items_medical", "jar_orange");  // ★ 路径全小写！

// 注册方式：FoodItemDirectory.InitDirectory Postfix
[HarmonyPatch(typeof(FoodItemDirectory), "InitDirectory")]
static void Postfix(ref FoodItemDirectory __instance) {
    ((Directory<GameItem>)(object)__instance).Add("orange",
        Func<GameItem>.op_Implicit((Func<GameItem>)(() => Orange())));
}
```

**关键发现**：
- Sprite 路径**全小写** `items/items_medical`，而非 `Items/items_tool`（大写 I 可能导致 sprite 加载失败）
- 使用 `ItemDirectory.CreateEmptyItem(null)` 创建空物品，比 `PreBuiltItemHelper.CreateXxx()` 更干净（不会残留错误的 sprite 字段）
- 注册到 `FoodItemDirectory`（或对应类型的 Directory）而非拦截 `DirectoryMaster.Item`

---

## 十七、Cpp2IL 拆包经验（2026-09-02）

### 17.1 工具版本与 metadata 限制（关键）

| 工具 | 版本 | 对本游戏（metadata 31 / Unity 2021.3.45f2） |
|------|------|------|
| Cpp2IL（MelonLoader 自带） | 2022.1.0-pre-release.21 | ✅ 支持，77402 方法全部解析 |
| Cpp2IL 正式版（已下载） | 2022.0.7 | ❌ 仅支持 metadata 24-29，本游戏为 31，运行卡死或拒绝执行 |

**关键结论**：本游戏为 IL2CPP metadata 31，在当前测试范围内唯一能解析它的 Cpp2IL 版本是 MelonLoader 自带的 pre-release.21 ⚠️当前测试范围内结论。不建议下载正式版尝试。

### 17.1.1 Cpp2IL 命令行参数（pre-release.21）

```powershell
# 完整命令（在游戏根目录下执行）
$cpp2il = "MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\Cpp2IL.exe"
$gameAsm = "GameAssembly.dll"
$metadata = "Probably Stolen_Data\il2cpp_data\Metadata\global-metadata.dat"

# 生成所有输出格式（diffable-cs + isil + dll_il_recovery）
& $cpp2il --exe-path $gameAsm --metadata-path $metadata `
    --output-dir "_cpp2il_out" `
    --output-as diffable-cs `
    --output-as isil `
    --output-as dll_il_recovery

# 参数说明：
#   --exe-path <path>       GameAssembly.dll 路径
#   --metadata-path <path>  global-metadata.dat 路径
#   --output-dir <dir>      输出目录
#   --output-as <format>    输出格式，可多次指定：
#                           diffable-cs  C# 签名（类/方法/字段，方法体空）
#                           isil          汇编级反汇编（每个类一个 .txt）
#                           dll_il_recovery  尝试恢复 IL 字节码为 DLL（本游戏失败）
#                           c             C 伪代码（pre-release.21 可能不支持）
```

**实际执行结果**（本游戏）：
- `diffable-cs`：7344 个 .cs 文件，Assembly-CSharp 640 个 ✅
- `isil`：9086 个 .txt 文件，Assembly-CSharp 1505 个 ✅（**唯一有方法体的输出**）
- `dll_il_recovery`：生成 DLL 但方法体为 `throw null`（Code size: 2）❌

### 17.1.2 diffable-cs 与 dump.cs 的区别

| 维度 | Il2CppDumper dump.cs | Cpp2IL diffable-cs |
|---|---|---|
| 格式 | 单个大文件（43万行） | 每类一个 .cs 文件 |
| 字段偏移 | 有（`// 0x24`） | 无 |
| 方法 RVA | 有（`// RVA: 0x...`） | 无 |
| 属性展开 | 展开为 get_/set_ 方法 | 保留 C# 属性语法 |
| 泛型处理 | 较完整 | 可能遗漏泛型类 |
| 适用场景 | 全局搜索、找 RVA | 单类精读、对比签名 |

**建议**：两个都生成，dump.cs 用于搜索和定位 RVA，diffable-cs 用于单类结构精读。

### 17.2 输出格式对比

| 格式 | 内容 | 可读性 | 价值 |
|------|------|--------|------|
| `diffable-cs` | 类 / 方法 / 字段签名（比旧 dump.cs 更全） | ✅ 高 | 签名补充 |
| `isil` | 每个类一个 .txt，含方法从 GameAssembly.dll 反汇编的真实指令（寄存器、调用目标、分支、字符串地址） | ⚠️ 汇编级 | **首次获取真实方法体** |
| `dll_il_recovery` + ilspycmd | 尝试恢复 IL 字节码再反编译成 C# | ❌ 方法体空壳 | 本游戏失败 |

### 17.3 ISIL 汇编的价值（本次真正的新收获）

ISIL 是汇编级输出，每个类对应一个 .txt 文件，包含每个方法的真实反汇编指令。此前 Il2CppDumper 仅能获取签名（方法体为空壳），现在首次可以查看方法内部的真实逻辑。

**已确认的关键方法分支结构**（从 ISIL 汇编中读取）：
- `StoreClientManager.HandleJacksonStorage` / `HandleContentUnlockClient` / `HandleInspectionClient` 的真实分支结构
- `GameItem.GetCurrentValue` 的完整调用序列（约 30 次方法调用）
- `StoreClientDialogList` 系列类确认了对话按类型分派（Story / Barter / Tier / Wanted / Information 等 20+ 子表）

### 17.3.1 ISIL 汇编阅读方法（如何从 x86 指令提取逻辑）

ISIL 文件每个方法包含两段：`Disassembly`（原始 x86 汇编）和 `ISIL`（中间表示，可读性更好）。**优先读 ISIL 段**。

**ISIL 关键指令模式**：

| ISIL 指令 | 含义 | 对应 C# 逻辑 |
|---|---|---|
| `Move rcx, rdx` | 寄存器传参 | 方法参数传递（rcx=this, rdx=arg1, r8=arg2, r9=arg3） |
| `Call "方法名"` | 调用方法 | 直接调用，方法名已符号化 |
| `Call 0x180XXXXX` | 调用未符号化方法 | 需查 script.json 或 Ghidra 解析 |
| `Compare rax, 0` / `JumpIfEqual` | 比较 + 条件跳转 | if (x == null) / if (x == 0) |
| `Compare rax, 0` / `JumpIfNotEqual` | 比较 + 条件跳转 | if (x != null) / if (x != 0) |
| `Move [rcx+0x24], rdx` | 写字段 | `this.field = value`（偏移 0x24） |
| `Move rdx, [rcx+0x24]` | 读字段 | `value = this.field`（偏移 0x24） |
| `Move rcx, typeof(ClassName)` | 加载类型 | `typeof(ClassName)` / new ClassName() |
| `Call "il2cpp_codegen_object_new"` | 创建对象 | `new SomeClass()` |
| `Call "il2cpp_codegen_initialize_runtime_metadata"` | 初始化元数据 | 静态构造函数 / 首次访问静态字段 |
| `Return` | 返回 | return |

**阅读步骤**：
1. 找到方法的 ISIL 段（不是 Disassembly 段）
2. 看前几条 `Move rcx/rdx/r8/r9` 确认参数
3. 跟踪 `Call` 指令，记录调用了哪些子方法
4. 看 `Compare` + `JumpIfEqual/NotEqual` 确认分支条件
5. 看 `Move [rcx+偏移]` 确认设置了哪些字段
6. 忽略 `il2cpp_codegen_*` 系列调用（这些是 IL2CPP 运行时基础设施，不是游戏逻辑）

**示例**：`NetworkUpgrade..ctor(string id)` 的 ISIL 关键段：
```
015 Move rcx, rdi          ; rdi = this
016 Call 0x1802F47B0       ; 调用基类构造函数
017 LoadAddress rcx, [rdi+32]  ; this + 0x20 = id 字段
018 Move rdx, rbx          ; rbx = 参数 id
019 Move [rcx], rbx        ; this.id = id
021 Move rcx, typeof(List`1<String>)  ; 加载 List<string> 类型
022 Call "il2cpp_codegen_object_new"   ; new List<string>()
029 LoadAddress rcx, [rdi+56]  ; this + 0x38 = 某个 List 字段
031 Move [rcx], rbx        ; this.xxxList = new List<string>()
```
→ 结论：构造函数设置 `id`（偏移 0x20）并初始化一个 `List<string>` 字段（偏移 0x38）。

**ISIL 的局限性**：
- 字符串引用显示为地址（如 `[0x1825E6180]`），需结合 stringliteral.json 或 Ghidra 解析
- 未符号化的方法调用显示为地址，需查 script.json 映射
- 复杂算法（循环、数学运算）阅读困难，此时优先用运行时诊断补丁

### 17.4 已生成的拆包资产（游戏根目录下）

```
_cpp2il_isil\IsilDump\                    ← 新！汇编级方法体
  ├─ Assembly-CSharp\                      1505 个类文件
  │   ├─ StoreClientManager.txt            612 KB
  │   ├─ PlayerStore.txt                   958 KB
  │   ├─ StoreClientList.txt              1088 KB
  │   ├─ StoreClientListSpec.txt           228 KB
  │   ├─ GameItem.txt                      645 KB
  │   ├─ NegociationUIManager.txt          372 KB
  │   └─ TraderFactory.txt                   63 KB
  └─ 全部程序集                             9086 个类文件

_cpp2il_diffable\DiffableCs\Assembly-CSharp\   640 个 C# 签名文件
_cpp2il_out\  +  _cpp2il_src\                   签名 DLL + 639 个 C# 签名文件
```

### 17.5 C# 级方法体恢复失败的原因

Cpp2IL 的 IL → C# 恢复在本游戏失败（方法体为空壳），这是 **IL2CPP metadata 31 + Unity 2021.3 组合下的已知限制**，并非参数设置问题。

pre-release.21 没有额外的 IL 恢复参数；2022.0.7 正式版具备 IL 恢复但不支持 metadata 31。这是工具版本的客观限制。

### 17.6 替代方案（无法获取 C# 方法体时的应对）

1. **ISIL 汇编 + Ghidra**：将 ISIL 汇编导入 Ghidra，使用反编译器得到可读伪代码（工作量集中在关键方法，适合深度逆向）
2. **运行时诊断补丁（Harmony / MelonLoader）**：对特定方法添加日志观察——这是历史上验证 L2 逻辑最有效的方式，比静态反编译快得多
3. **参考已成功 Mod 的反编译**：使用 ilspycmd 11 反编译其他 Mod 的 DLL（如 XIAWO 系列、ExtraPerks），查看它们如何调用这些方法——这比直接逆向游戏方法体更高效

### 17.7 枪械打印机修复中的 Cpp2IL 应用

在修复 3d_printer（枪械打印机）时，Cpp2IL 的 ISIL 汇编帮助确认了：
- `MachinePrinter.Printer()` 是 public static 方法，返回完整的 GameItem（含内部状态连接）
- `MachinePrinter.CreateMachineInventoryWindow()` 返回 7 元素 ValueTuple（窗口 + 芯片槽 + 左物品槽 + 右物品槽 + 电池槽 + 模块槽 + 说明槽）
- `MachinePrinter.FinishPrinting(item, chipInv, itemInventoryRight, itemInventoryModule)` 的参数顺序
- `MachineProgressHelper.InitProgressTypeMachine(item, speed, reset)` 的参数含义

这些信息直接指导了 GunPrinterFix.cs 的重写：首选 `MachinePrinter.Printer()` 创建（不清除其内部状态），参考 XIAOWOHydroponicFix 从原 contentWindow 提取槽位重建 UI。

---

## 十八、完整拆包工作流速查（从 0 到产出可用信息）

> 本章是第十一、十七章的实操汇总，按执行顺序排列。每次拆新游戏或新版本时照此流程走一遍。

### 18.1 工具清单（已全部安装，2026-09-02 确认）

| 工具 | 版本 | 路径 | 用途 |
|---|---|---|---|
| Il2CppDumper | 6.7+ | `_dumper\Il2CppDumper.exe` | 签名层提取（dump.cs） |
| Cpp2IL | 2022.1.0-pre-release.21 | `MelonLoader\Dependencies\...\Cpp2IL.exe` | 汇编层提取（ISIL），**唯一支持 metadata 31** |
| ilspycmd | 8.2.0 | `C:\Users\1\.dotnet\tools\ilspycmd.exe` | Mod DLL 反编译（完整 C# 源码） |
| UnityPy | 1.25.3 | Python 3.14 包 | 第4层 AssetBundle 解包（本地化表、配置） |
| dnSpyEx | 6.6.0 | `D:\谷歌\tools\dnSpy\dnSpy.exe` | GUI 版 IL2CPP 托管 DLL 浏览（比 ilspycmd 方便） |
| AssetRipper | 2.0.0 | `D:\谷歌\tools\AssetRipper\AssetRipper.GUI.Free.exe` | Unity 资源批量提取（预制体、场景、贴图） |
| Ghidra | 12.1.3 | `D:\谷歌\tools\ghidra_12.1.3_PUBLIC\ghidraRun.bat` | 原生代码深度反编译（需 JDK 21） |
| HxD | 2.5.0 | `C:\Program Files\HxD\HxD.exe` | 十六进制编辑器（二进制补丁、metadata 查看） |
| JDK | 21.0.12 LTS | `D:\jdk\`（JAVA_HOME 已设） | Ghidra 运行依赖 |

**Python 注意**：UnityPy 装在 `C:\Users\1\AppData\Local\Python\pythoncore-3.14-64\python.exe`，`python` 命令可能指向其他版本，用完整路径调用。

**Ghidra + JDK 配置要点**（实测经验）：
1. JDK 用 `microsoft-jdk-21.0.12.1-windows-x64.msi` 安装，**默认装在 `D:\jdk\`**（不是 Program Files）
2. Ghidra 12 需要 `JAVA_HOME` 环境变量指向 JDK 根目录（`D:\jdk`），`JAVA_HOME_OVERRIDE` 只对 GUI 版生效，命令行版必须用 `JAVA_HOME`
3. 设置方法：`[System.Environment]::SetEnvironmentVariable("JAVA_HOME", "D:\jdk", "User")`（用户级永久生效，无需管理员）
4. 当前会话需手动 `$env:JAVA_HOME = "D:\jdk"` 才能立即生效
5. 命令行测试：`analyzeHeadless.bat <projectDir> <projName> -import <file> -analysisTimeoutPerFile 300`，能输出帮助即成功
6. **Ghidra 分析 IL2CPP DLL 的局限**：Il2CppAssemblies 下的托管 DLL 只有签名没有方法体，Ghidra 分析得到的是空壳；真正有价值的是分析 **GameAssembly.dll**（43MB 原生代码）配合 Il2CppDumper 的 `script.json` 做符号恢复，需要较长时间（10-30分钟）

### 18.2 标准拆包流程（8 步）

```
步骤1：数据源探查
  │  扫描游戏根目录，确认 4 层数据源的实际路径
  │  确认 GameAssembly.dll / global-metadata.dat / Il2CppAssemblies / StreamingAssets
  ▼
步骤2：签名层提取（Il2CppDumper）
  │  输入：GameAssembly.dll + global-metadata.dat
  │  输出：dump.cs（43万行）+ script.json + stringliteral.json
  │  验证：搜索已知类名（如 StoreClientManager）确认输出完整
  ▼
步骤3：汇编层提取（Cpp2IL）
  │  输入：同上
  │  输出：_cpp2il_isil\（9086个 .txt，含方法体汇编）
  │        _cpp2il_diffable\（7344个 .cs，签名补充）
  │  验证：检查 Assembly-CSharp 目录下文件数 ≥ 1500
  ▼
步骤4：Mod DLL 反编译（ilspycmd）
  │  输入：参考 Mod 的 DLL（ExtraPerks / XIAOWO 系列等）
  │  输出：完整 C# 源码（含方法体）
  │  验证：打开 Core.cs，确认 OnInitializeMelon 方法完整
  ▼
步骤5：第4层资源提取（UnityPy）
  │  解包 StreamingAssets\aa\StandaloneWindows64\ 下的 .bundle
  │  提取 SharedTableData（明文 key）+ StringTable（各语言值）
  │  输出：物品ID清单、特性ID、客户名、对话文本、王尔德升级列表
  ▼
步骤6：字符串提取（metadata / stringliteral.json）
  │  搜索代码中的字符串字面量（标签名、错误信息、常量名）
  │  与第4层的本地化表交叉验证
  ▼
步骤7：目标系统定位
  │  从 dump.cs 搜索目标类 → 记录方法签名和 RVA
  │  从 ISIL 文件读取关键方法的汇编 → 推断逻辑（L2）
  │  从参考 Mod 源码查找同类调用 → 交叉验证
  ▼
步骤8：运行时验证（诊断补丁）
  │  编写最小诊断 Mod，Patch 目标方法打日志
  │  确认参数、返回值、字段变化、调用时机
  │  输出：L3 验证结论
```

### 18.3 各步骤的验证标准（避免"以为拆完了实际没拆完"）

| 步骤 | 通过标准 | 失败表现 |
|---|---|---|
| 步骤2 | dump.cs 中能搜到 `class PlayerStore` 且有字段偏移 | 文件为空 / 类名缺失 |
| 步骤3 | ISIL 目录下 `StoreClientManager.txt` 存在且 > 100KB | 文件数 < 1000 / 方法只有签名无 Disassembly |
| 步骤4 | 反编译出的 .cs 有方法体（不是 `throw null`） | 所有方法体为空 |
| 步骤5 | 本地化表提取出 ≥14 个表、≥5000 条文本；物品ID ≥300 个 | bundle 无法解析 / 表为空 |
| 步骤6 | 能搜到 `contraband`、`MODULE_TAG` 等关键标签字符串 | 正则匹配为空 |
| 步骤7 | 目标方法的 ISIL 中能看到 `Call` 指令和分支 | 方法体只有 `ret`（内联或被优化掉） |
| 步骤8 | 日志中出现目标方法被调用的记录 | Patch 不触发 / 日志无输出 |

### 18.4 常见问题排查

| 问题 | 原因 | 解决 |
|---|---|---|
| Il2CppDumper 报 "Can't find metadata" | metadata 路径错误或文件损坏 | 确认路径，重新从游戏目录复制 |
| Il2CppDumper 报 "No executable" | GameAssembly.dll 路径错误 | 确认是游戏根目录下的 GameAssembly.dll |
| Cpp2IL 报 "Unsupported metadata version" | 用了正式版 2022.0.7 | 改用 MelonLoader 自带的 pre-release.21 |
| Cpp2IL 输出的 DLL 方法体为空 | IL 恢复对 metadata 31 失败 | 这是正常的，改用 ISIL 输出 |
| ilspycmd 反编译 IL2CPP DLL 方法体为空 | IL2CPP DLL 本来就没有方法体 | 这是正常的，用 Il2CppDumper 看签名 |
| dump.cs 搜索不到某个类 | 类在其他程序集中 | 搜索全部 dump 文件，或检查命名空间 |
| ISIL 中方法调用全是地址 | 符号未恢复 | 用 script.json 映射地址→方法名，或导入 Ghidra |

### 18.5 实战案例：Contactless_Calendar.dll 反编译

**目标**：拆解一个 15KB 的小型 Mod，学习其 Patch 模式。

```powershell
# 反编译
ilspycmd "D:\谷歌\Contactless_Calendar.dll" -o "_calendar_src" -p

# 输出（9 个文件）
_calendar_src\ContactlessCalendar\
├── Core.cs              ← 入口，定义 QueueFuturEventSmart 工具方法
├── CaulsonPatch.cs      ← Patch 某个 NPC（考尔森）
├── CaulsonFreePatch.cs  ← 免费版考尔森 Patch
├── OdinPatch.cs         ← Patch 奥丁·哈拉尔德森
├── RedImpPatch.cs       ← Patch 红小鬼 NPC
├── RedImpFreePatch.cs   ← 免费版红小鬼 Patch
├── colorPatch.cs        ← UI 颜色修正
├── ContactEventList.cs  ← 自定义事件列表
└── Properties\AssemblyInfo.cs
```

**核心模式提取**（从反编译源码中总结）：

1. **智能事件排队**：`QueueFuturEventSmart` 检查 `storeState`（0/1/2 三种状态），在任意状态下都安全调用 `QueueFuturEvent`
2. **NPC 专属 Patch**：每个剧情 NPC 独立一个 Patch 类，Patch 其对话或行为方法
3. **免费版/完整版分离**：`XxxFreePatch` 是免费版功能，`XxxPatch` 是完整版功能
4. **颜色修正**：`colorPatch`  Patch UI 颜色方法，解决特性图标偏色问题（与第 5.3 节呼应）

**反编译价值**：15KB 的 DLL 反编译出 9 个清晰的 .cs 文件，完整展示了"小型功能 Mod"的标准架构——比从游戏本体逆向快 10 倍。

### 18.6 拆包产出物归档规范

每次拆包后，在游戏根目录下建立以下结构，便于后续查阅：

```
Probably Stolen Playtest\
├── _dumper\              ← Il2CppDumper 输出
│   ├── dump.cs           ← 签名主文件（43万行）
│   ├── script.json       ← 地址→符号映射
│   └── stringliteral.json ← 字符串字面量
├── _cpp2il_isil\         ← Cpp2IL 汇编输出
│   └── IsilDump\Assembly-CSharp\  ← 1505 个类的方法体汇编
├── _cpp2il_diffable\     ← Cpp2IL 签名输出
│   └── DiffableCs\Assembly-CSharp\  ← 640 个类的 C# 签名
├── _mod_decompiled\      ← 参考 Mod 反编译源码
│   ├── ExtraPerks\
│   ├── XIAOWOWeeklyVisitors\
│   ├── Contactless_Calendar\
│   ├── ModuleUpgradeKit\
│   └── ...
├── _localization\        ← 第4层本地化表提取
│   ├── _localization_bilingual.json  ← 14表5621条中英文对照
│   ├── _item_ids.txt                 ← 383个物品ID清单
│   └── _localization_keys.json       ← 明文key清单
└── _analysis\            ← 分析笔记（按系统分文件）
    ├── 物品系统.md
    ├── 客户系统.md
    ├── 博士系统.md
    └── 价格系统.md
```

---

## 十九、ModuleUpgradeKit 深度拆解：从零做一个"拖拽升级"类 Mod（2026-09-02）

> 反编译来源：`D:\谷歌\ModuleUpgradeKit.dll`（15KB，5 个 .cs 文件）
> 核心价值：展示了如何在**不修改游戏原生代码**的前提下，给已有物品添加一套完整的升级系统——拖拽交互、进度追踪、属性修改、Tooltip 显示、配置化，全部通过 Harmony Patch + Tag 系统 + 反射实现。

### 19.1 这个 Mod 做了什么

给游戏中的**模块类物品**（MODULE / SHIP_MODULE_TAG）添加升级系统：

| 操作 | 拖什么到模块上 | 效果 |
|---|---|---|
| 累积进度 | 电子零件（common_electronic / electronic_component / advanced_electronic） | 模块进度 +N（默认每次+1，需3个完成） |
| 常规升级 | 普通提取器（module_extractor） | 进度满时触发：最高维度属性（性能/效率/品质）+10%，升级次数+1，进度清零 |
| 高级升级 | 高级提取器（module_extractor_advanced） | 满级后触发：添加随机模块效果（最多2个槽位） |
| 重随效果 | 普通提取器（满级后） | 消耗耐久，随机替换已有效果 |

**完全不新增物品、不修改预制体**，所有状态存在物品的 TagSystem 里。

### 19.2 架构总览（5 个文件各司其职）

```
Mod.cs                    ← 入口，OnInitializeMelon 中初始化配置 + 应用 Patch
Config.cs                 ← MelonPreferences 配置，7 个可调参数
UpgradePatch.cs           ← Harmony Patch 定义（拖拽捕获 + Tooltip 追加 + 启动探测）
ModuleUpgradeHelper.cs    ← 核心逻辑（29KB，所有升级/效果/Tag 操作）
Properties/AssemblyInfo.cs ← 程序集信息
```

**调用链**：
```
玩家拖拽物品到模块上
  → ItemMouseDragHandler.EndDrag (Prefix) 或 TreeNodeRender.OnDrop (Postfix)
  → 识别 source（拖的什么）和 target（模块）
  → TryAddComponentProgress() 或 TryApplyTool()
  → 操作 TagSystem（进度/次数/维度/效果）
  → 反射调用 ModuleEffectHelper 重算属性
  → Tooltip Patch 在鼠标悬停时显示进度
```

### 19.3 核心技术点拆解

#### 19.3.1 双路径拖拽捕获（关键创新）

游戏有**两套独立的拖拽系统**，这个 Mod 同时 Patch 了两者，确保在任何界面都能触发：

```csharp
// 路径1：ItemMouseDragHandler（物品栏拖拽）
// Prefix EndDrag，在原生逻辑执行前捕获 source 和 target
harmony.Patch(method, new HarmonyMethod("PrefixEndDrag"), null, null, null, null);

// 路径2：TreeNodeRender（技能树/模块树拖拽）
// Postfix OnBeginDrag 记录 source，Postfix OnDrop 识别 target 并执行
harmony.Patch(method2, null, new HarmonyMethod("PostfixOnBeginDrag"), ...);
harmony.Patch(method3, null, new HarmonyMethod("PostfixOnDrop"), ...);
```

**为什么需要双路径**：不同 UI 界面用不同的拖拽处理器。只 Patch 一个会导致"在仓库里能升级，在模块树里不能升级"。

**source 追踪技巧**：TreeNodeRender 没有直接暴露"当前拖拽的物品"，所以用静态字段 `_dragSource` 在 OnBeginDrag 时记录，在 OnDrop 时读取。

#### 19.3.2 TagSystem 作为数据存储（不新增字段）

所有升级状态都存在物品原生的 `item.state.dict`（TagSystem）里，用自定义 tag 名：

| Tag 名 | 类型 | 用途 |
|---|---|---|
| `MODULE_UPGRADE_PROGRESS_INT` | int | 当前电子零件进度（0~3） |
| `MODULE_UPGRADE_COUNT_INT` | int | 已完成常规升级次数（0~3） |
| `BONUS_PERCENTAGE_PERFORMANCE_INT` | int | 性能维度加成（%） |
| `BONUS_PERCENTAGE_EFFICIENCY_INT` | int | 效率维度加成（%） |
| `BONUS_PERCENTAGE_QUALITY_INT` | int | 品质维度加成（%） |
| `MODULE_EFFECT1_TAG` | string | 效果槽1（存效果ID） |
| `MODULE_EFFECT2_TAG` | string | 效果槽2（存效果ID） |

**Tag 读写封装**（统一处理 null 和异常）：
```csharp
internal static int GetTagInt(GameItem item, string tag) {
    TagSystem state = item.state;
    Dictionary<string, TagState> val = state?.dict;
    if (val == null || !val.ContainsKey(tag)) return 0;
    return val[tag]?.valueInt ?? 0;
}

internal static void SetTagInt(GameItem item, string tag, int value) {
    TagSystem state = item.state;
    Dictionary<string, TagState> val = state?.dict;
    if (val != null) {
        if (!val.ContainsKey(tag)) item.EnableTag(tag, false);  // 不存在则先启用
        val[tag].valueInt = value;
    }
}
```

**关键认知**：`item.EnableTag(tag, false)` 是给物品添加新 tag 的标准方式，第二个参数 `false` 表示不立即触发效果计算。

#### 19.3.3 反射调用游戏方法（版本兼容）

对于可能在版本更新中签名变化的方法，用反射调用而非直接引用：

```csharp
// 重算模块临时属性（添加/移除效果后必须调用）
private static void CallComputeTemporaryStat(GameItem module, string context) {
    MethodInfo method = typeof(ModuleEffectHelper).GetMethod(
        "ComputeTemporaryStat", BindingFlags.Static | BindingFlags.Public);
    if (method != null) {
        method.Invoke(null, new object[1] { module });
    } else {
        Mod.Log.Warning("ComputeTemporaryStat not found");  // 版本不兼容时只报警告
    }
}
```

**用反射的场景**：
- `ModuleEffectHelper.ResetAllTempStat(module)` — 清除所有临时属性
- `ModuleEffectHelper.ComputeTemporaryStat(module)` — 重新计算属性
- `DurabilityHelper.GetRealDurability(item)` — 获取真实耐久
- `DurabilityHelper.ChangeDurability(item, amount)` — 修改耐久

**为什么不直接调用**：IL2CPP 游戏中这些方法可能被内联、重命名或签名变化。反射调用在方法不存在时只返回 null，不会导致 Mod 崩溃。

#### 19.3.4 效果系统的"添加-重算"模式

给模块添加效果时，不能只改 tag——必须按正确顺序调用游戏原生方法：

```
添加效果：
1. SetTagString(module, "MODULE_EFFECT1_TAG", "MODULE_EFFECT_PREMIUM")  // 先写 tag
2. ApplyEffectBaseModifier(module, effectId)                             // 手动应用基础修正
3. CallComputeTemporaryStat(module)                                      // 通知游戏重算

移除/替换效果：
1. CallResetAllTempStat(module)         // 先清除所有临时属性
2. RevertEffectBaseModifier(module, oldEffectId)  // 撤销旧效果的基础修正
3. SetTagString(module, tag, newEffectId)        // 写新 tag
4. ApplyEffectBaseModifier(module, newEffectId)   // 应用新效果基础修正
5. CallComputeTemporaryStat(module)               // 重算
```

**4 种效果的基础修正**（硬编码在 Mod 中）：

| 效果 ID | 添加时 | 移除时 |
|---|---|---|
| `MODULE_EFFECT_PREMIUM` | 三维度各 +20 | 三维度各 -20 |
| `MODULE_EFFECT_DEGRADING` | 三维度 ×2 | 三维度 ÷2 |
| `MODULE_EFFECT_NEGATIVE_FEEDBACK` | 三维度 ×1.25 | 三维度 ÷1.25 |
| `MODULE_EFFECT_OVERVOLTED` | 三维度 ×3 | 三维度 ÷3 |

**注意**：这些基础修正是 Mod 作者逆向游戏效果系统后总结的，不是游戏原生 API。添加效果后调用 `ComputeTemporaryStat` 会让游戏根据 tag 重新计算临时属性，但基础修正（维度加成）需要 Mod 手动维护。

#### 19.3.5 Tooltip Patch（Postfix + Finalizer 双保险）

```csharp
// Patch 两个 tooltip 方法（普通模块和复杂模块）
harmony.Patch(method, null, 
    new HarmonyMethod("PostfixCreateModuleTooltip"),   // 追加进度文本
    null, 
    new HarmonyMethod("FinalizerCreateModuleTooltip"), // 吞掉异常
    null);

// Finalizer：如果 Postfix 抛异常，返回 null 抑制，不让 UI 崩溃
public static Exception? FinalizerCreateModuleTooltip(Exception? __exception) {
    if (__exception != null) {
        Mod.Log.Warning("suppressed tooltip exception: " + __exception.Message);
        return null;  // 返回 null = 抑制异常
    }
    return null;
}
```

**Finalizer 的作用**：Tooltip 方法在 UI 线程高频调用，如果 Mod 的追加逻辑抛异常，会导致游戏界面卡死。Finalizer 捕获异常并返回 null，保证原生 Tooltip 正常显示。

#### 19.3.6 启动探测（Probe）模式

在 `Apply()` 方法中，Patch 完所有目标后，立即调用一系列 Probe 方法打印目标类的结构：

```csharp
ModuleUpgradeHelper.ProbeScavHelper();       // 探测 ScavHelper 方法
ModuleUpgradeHelper.ProbeModuleEffects();    // 探测 ModuleEffectHelper 字段和效果列表
ProbeItemMouseDragHandler();                 // 打印 ItemMouseDragHandler 所有方法/字段
ProbeGameItemElement();                      // 打印 GameItemElement 中 Drag/Drop 相关方法
```

**Probe 输出示例**（日志中可见）：
```
ModuleUpgradeKit: ItemMouseDragHandler methods:
  method EndDrag : Void params=
  method GetCurrentItem : GameItem params=
ModuleUpgradeKit: ItemMouseDragHandler fields:
  field currentItem : GameItem
  field lastItem : GameItem
```

**价值**：游戏更新后，运行一次就能知道目标方法/字段是否还在、签名是否变化，不需要重新反编译。

### 19.4 从零做一个类似 Mod 的完整步骤

#### 步骤1：确定目标交互和数据存储

```
问自己：
- 玩家用什么操作触发功能？（拖拽/点击/按键/右键菜单）
- 状态存在哪里？（TagSystem / 静态字典 / 存档扩展）
- 哪些游戏原生方法需要调用？（先从 dump.cs 找签名）
```

ModuleUpgradeKit 的选择：
- 交互：拖拽（最自然，符合"把材料用到物品上"的直觉）
- 存储：TagSystem（随物品存档，不需要额外存档逻辑）
- 原生方法：ModuleEffectHelper（属性重算）、DurabilityHelper（耐久）

#### 步骤2：反编译确认目标类和方法

```powershell
# 从 dump.cs 搜索目标类
Select-String -Path dump.cs -Pattern "class ItemMouseDragHandler" -Context 0,40
Select-String -Path dump.cs -Pattern "class ModuleEffectHelper" -Context 0,60
Select-String -Path dump.cs -Pattern "class TreeNodeRender" -Context 0,40

# 确认方法签名和参数
Select-String -Path dump.cs -Pattern "ComputeTemporaryStat"
Select-String -Path dump.cs -Pattern "ResetAllTempStat"
Select-String -Path dump.cs -Pattern "EndDrag"
```

**记录**：类名、方法名、参数类型、返回类型、是否静态。这些决定了 Patch 的写法。

#### 步骤3：搭建项目骨架

```
MyMod\
├── MyMod.csproj          ← 参考第1.2节模板
├── Mod.cs                ← 入口（OnInitializeMelon）
├── Config.cs             ← MelonPreferences 配置
├── MyPatch.cs            ← Harmony Patch 定义
└── MyHelper.cs           ← 核心逻辑
```

**Mod.cs 最小模板**：
```csharp
using MelonLoader;
namespace MyMod;
public sealed class Mod : MelonMod {
    internal static Instance Log;
    public override void OnInitializeMelon() {
        Log = ((MelonBase)this).LoggerInstance;
        Config.Init();
        MyPatch.Apply(((MelonBase)this).HarmonyInstance);
        Log.Msg("MyMod ready.");
    }
}
```

#### 步骤4：写 Patch 类（防御性优先）

```csharp
internal static class MyPatch {
    internal static void Apply(Harmony harmony) {
        // 每个 Patch 都用 try-catch + null 检查
        var method = AccessTools.Method(typeof(TargetClass), "TargetMethod", 
            new[] { typeof(ParamType) });
        if (method != null) {
            harmony.Patch(method, new HarmonyMethod("Prefix"), 
                new HarmonyMethod("Postfix"), null, 
                new HarmonyMethod("Finalizer"), null);
            Mod.Log.Msg($"patched {method}");
        } else {
            Mod.Log.Warning("TargetClass.TargetMethod not found");
        }
        // 启动探测
        ProbeTargetClass();
    }
    
    // Finalizer 吞异常（UI 相关 Patch 必须有）
    public static Exception? Finalizer(Exception? __exception) {
        if (__exception != null) {
            Mod.Log.Warning("suppressed: " + __exception.Message);
            return null;
        }
        return null;
    }
}
```

#### 步骤5：写 Helper 类（逻辑与 Patch 分离）

**原则**：Patch 方法只做"捕获事件 + 调用 Helper"，所有业务逻辑在 Helper 中。

```csharp
internal static class MyHelper {
    // 每个方法 try-catch，异常只记日志不抛出
    internal static void DoSomething(GameItem item) {
        try {
            // ... 逻辑 ...
        } catch (Exception ex) {
            Mod.Log.Warning("DoSomething: " + ex.Message);
        }
    }
    
    // Tag 读写统一封装
    internal static int GetTagInt(GameItem item, string tag) { /* ... */ }
    internal static void SetTagInt(GameItem item, string tag, int value) { /* ... */ }
    
    // 反射调用游戏方法
    private static void CallGameMethod(GameItem item) {
        var method = typeof(GameHelper).GetMethod("MethodName", 
            BindingFlags.Static | BindingFlags.Public);
        method?.Invoke(null, new object[] { item });
    }
}
```

#### 步骤6：配置化（MelonPreferences）

```csharp
internal static class Config {
    internal static readonly MelonPreferences_Category Prefs = 
        MelonPreferences.CreateCategory("MyMod");
    
    internal static MelonPreferences_Entry<int> SomeValue = null;
    
    internal static void Init() {
        SomeValue = Prefs.CreateEntry<int>("SomeValue", 3, "说明文字");
    }
}
```

#### 步骤7：编译部署 + 日志验证

```powershell
# 编译
dotnet build -c Release

# 部署（参考附录命令）
Copy-Item bin\Release\net6.0\MyMod.dll "游戏目录\Mods\"

# 启动游戏，检查日志
# MelonLoader\Latest.log 中搜索 "MyMod:"
# 确认：所有 Patch 成功、Probe 输出了目标结构、无异常
```

### 19.5 这个 Mod 值得学习的 7 个模式

| # | 模式 | 为什么好 | 怎么学 |
|---|---|---|---|
| 1 | **双路径 Patch** | 覆盖游戏所有 UI 交互场景 | 找到游戏中所有处理同类交互的类，全部 Patch |
| 2 | **TagSystem 存储** | 随物品自动存档，无需自定义存档逻辑 | 用 `item.EnableTag(name, false)` 添加 tag，用 `state.dict` 读写 |
| 3 | **反射调用原生方法** | 版本更新时 graceful degrade（方法没了只报警告） | 对不确定的方法用 `GetMethod + Invoke`，不用直接引用 |
| 4 | **Postfix + Finalizer** | UI Patch 不会导致游戏崩溃 | 所有 UI 相关 Patch 都加 Finalizer 吞异常 |
| 5 | **启动 Probe** | 游戏更新后一键确认目标结构是否变化 | 在 Apply() 末尾打印目标类的所有方法/字段 |
| 6 | **Patch 与 Helper 分离** | Patch 只捕获事件，逻辑集中在 Helper，易测试和维护 | Patch 方法 ≤10 行，只做参数提取 + Helper 调用 |
| 7 | **全方法 try-catch** | 任何异常都不会导致 Mod 崩溃或影响游戏 | Helper 中每个 public 方法都有 try-catch，异常记日志 |

### 19.6 这个 Mod 的局限性（可改进点）

1. **效果基础修正硬编码**：4 种效果的维度加成写死在代码里，如果游戏新增效果类型不会自动支持。改进：从 `ModuleEffectHelper.moduleEffects` 静态字典读取效果定义（Probe 方法已经在探测这个字段）。
2. **随机数用 `new Random()`**：每次调用都新建实例，在快速连续操作时可能产生相同序列。改进：用静态 Random 实例或游戏的 RNGNeeds。
3. **进度 tag 不持久化验证**：虽然存在 TagSystem 里会随存档保存，但没有验证读档后 tag 是否正确恢复。改进：加一个 OnSceneWasLoaded 后的验证日志。
4. **没有本地化**：Tooltip 文本硬编码中文。改进：用 `LocHelper.GetLocalizedItem` 或 `GetLocalizedMechanic`（代码中已有 `LogEffectLocalizations` 方法探测了这个 API）。

---

## 二十、自动化测试框架（2026-09-02 实现）

### 20.1 架构总览

目标：改完代码后自动编译→启动游戏→跑测试→关游戏→输出结果，无需手动开关。

组件：
- **TestRunner.cs**：Mod内测试运行器，通过`-runtests`命令行参数触发，正常启动完全不触发
- **run_tests.ps1**：PowerShell脚本，启动游戏+超时强杀+心跳检测+读取结果
- **build_and_test.ps1**：编译→部署DLL→调用run_tests.ps1

### 20.2 关键踩坑与解决方案

**坑1：游戏直接启动exe失败（退出码53）**
- 原因：Steam游戏需要Steam验证，直接启动exe会失败
- 解决：在游戏根目录创建`steam_appid.txt`，内容为游戏的Steam App ID（本游戏：4603730）
- 验证：创建后直接启动exe能正常进入游戏

**坑2：PowerShell脚本中文编码导致语法错误**
- 原因：PowerShell 5.1默认编码不是UTF-8，中文字符会乱码导致括号不匹配
- 解决：脚本全部用英文编写，包括变量名、注释、输出信息
- 结果文件名也用英文（test_results.txt），避免中文路径乱码

**坑3：TestRunner.OnUpdate()中访问PlayerStore.Instance导致日志刷屏**
- 原因：游戏早期访问PlayerStore.Instance会触发单例创建，而单例创建需要GameGridInventory，GameGridInventory需要TreeNodeRender.Instantiate，在游戏启动早期会失败
- 解决：改用场景名称判断加载完成，不访问PlayerStore.Instance
```csharp
string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
if (string.IsNullOrEmpty(sceneName)) return;
if (sceneName.ToLower().Contains("load") || sceneName.ToLower().Contains("boot")) return;
// 游戏加载完成，开始测试
```

**坑4：测试用例在游戏UI未完全初始化时执行会失败**
- 原因：DirectoryMaster.Item("3d_printer")在UI未初始化时会失败（GameItemElement构造需要PixelWindow）
- 解决：测试用例需要等待游戏完全进入存档后再执行，或测试不需要UI的纯逻辑功能

### 20.3 四层退出兜底（解决"有时关不掉游戏"）

IL2CPP+MelonLoader下Application.Quit()经常不生效（被hook、被退出弹窗拦住、或测试卡住根本没执行到退出）。需要多层兜底：

```
L1 温和退出：Application.Quit()
  ↓ 等1秒还没退出
L2 暴力退出：Environment.Exit(0)
  ↓ 等0.5秒还没退出
L3 强杀进程：Process.GetCurrentProcess().Kill()
  ↓ 等0.5秒还没退出
L4 二次强杀：再Kill一次
```

外部脚本再加两层：
- **超时强杀**：脚本启动游戏后等待120秒，进程还没退出就强杀
- **心跳看门狗**：mod每5秒写心跳文件，脚本检测15秒没更新就强杀（比固定超时更智能，能告诉你卡在哪一步）

### 20.4 心跳文件格式

```
用例名|时间戳|耗时ms
```

脚本检测逻辑：
```powershell
$lastHeartbeat = Get-Item $heartbeatFile | Select-Object -ExpandProperty LastWriteTime
if ((Get-Date) - $lastHeartbeat).TotalSeconds -gt 15 {
    Stop-Process -Id $gamePid -Force
    Write-Host "测试卡住，已强杀。卡在用例：$(Get-Content $heartbeatFile)"
}
```

### 20.5 Ghidra安装与使用经验（2026-09-02）

**关键坑：Ghidra安装路径不能含中文**
- 问题：Ghidra装在`D:\谷歌\tools\ghidra...`时，headless模式启动失败，log4j配置XML解析错误
- 原因：Java的URL编码对中文路径处理有问题，导致log4j找不到配置文件
- 解决：把Ghidra复制到不含中文的路径，如`D:\ghidra\`

**JAVA_HOME配置**
- JDK装在非默认路径（如`D:\jdk\`）时，必须设置JAVA_HOME环境变量
- 注意：`JAVA_HOME_OVERRIDE`只对GUI版生效，headless模式必须用`JAVA_HOME`
- 设置用户级环境变量（永久生效，不污染系统）：
```powershell
[Environment]::SetEnvironmentVariable("JAVA_HOME", "D:\jdk", "User")
```

**Ghidra headless分析IL2CPP游戏**
- GameAssembly.dll（43MB）完整分析需要10-30分钟
- 需要配合Il2CppDumper生成的script.json做符号恢复
- 分析完成后可用脚本导出特定函数的反编译结果

## 二十一、存储/容器/UI 类参考 Mod 拆解（2026-09-03）

> 本节拆解 3 个包：MoreStorageColors（动态贴图上色）、HyperMinimalist（极简专精缩小网格）、QOL 4-in-1（全大箱/嵌套箱/日期暂停/特质点上限）。
> 全部为 Harmony Patch + 运行时修改，无任何游戏文件改动。

### 21.1 存储箱（storage bay）家族——`ShipSystemDirectory`

**容器工厂方法全集（[L1] 已在 dump.cs / ISIL 确认）**
| 方法 | 返回 | RVA | unitValue（价格） | 内部网格（CreateInventoryWindow） |
|---|---|---|---|---|
| `MakeshiftStorageBay()` | GameItem | 0x816560 | 待查 | 待查 |
| `StorageBay()` | GameItem | 0x817890 | `[rsi+0x200]=0xFA=250` | 待查 |
| `StorageBayLarge()` | GameItem | 0x817550 | 待查 | 待查 |
| `GunsmithStorageBay()` | GameItem | 0x815700 | 待查 | 待查 |
| `ChemistStorageBay()` | GameItem | 0x815390 | 待查 | 待查 |
| `MachineBay()` | GameItem | 0x816140 | 待查 | 待查 |
| `MachineBayExt()` | GameItem | 0x815E70 | 待查 | 待查 |
| `SmugglerBay()` | GameItem | 0x8170C0 | `[rsi+0x200]=0xFA=250` | **5×5** |
| `SmugglerBayMod()` | GameItem | 0x816C20 | `[rsi+0x200]=0x190=400` | **8×8** |
| `MiniSmugglerBay()` | GameItem | 0x816770 | `[rsi+0x200]=0x96=150` | **3×3** |

> **重要纠正**：`[rsi+0x200]` 是 `GameItem.unitValue`（long，单价/价格），**不是容量**。三档走私者暗格的价格不同（150/250/400），真正的内部存储大小由 `DirectoryUtils.CreateInventoryWindow(width, height, true)` 决定。

**内部存储大小判定（[L1] ISIL 确认）**
- 每个容器工厂方法内部调 `DirectoryUtils.CreateInventoryWindow(int width, int height, bool isDraggable=true)` → 返回 `ValueTuple<PixelWindow, GameInventory>`
- 三档走私者暗格的 ISIL 参数：Mini=`CreateInventoryWindow(3,3,true)` / SmugglerBay=`(5,5,true)` / SmugglerBayMod=`(8,8,true)`
- 返回的 Item1=PixelWindow 通过 `GameItem.SetContentWindow()` 绑定；Item2=GameInventory（实际为 `GameGridInventory` 子类）是内部库存
- 制造路径：`DirectoryMaster.Item(string identifier, bool isOwned=true)`（RVA:0x490A60）→ 按 identifier 查 Directory → 调对应工厂方法

**物品 identifier（[L1] 本地化表 + Mod 源码交叉确认）**
- `storage_bay`（小箱）、`storage_bay_large`（大箱）
- 前缀判定：`id.StartsWith("storage_bay")` 且非 `storage_bay_large` = 小箱

### 21.2 容器内部存储大小机制——CreateInventoryWindow + GameGridInventory

**核心创建方法（[L1] dump.cs 确认）**
```csharp
// DirectoryUtils
public static ValueTuple<PixelWindow, GameInventory> CreateInventoryWindow(int width, int height, bool isDraggable = True)
public static ValueTuple<PixelWindow, GameInventory> CreateInventoryWindow(string data, int width, bool isDraggable = True)
```
- 返回 `ValueTuple`：Item1=PixelWindow（UI 窗口，绑到 GameItem.contentWindow）；Item2=GameInventory（内部库存，实际为 GameGridInventory 子类）

**GameGridInventory 类结构（[L1] dump.cs 确认）**
| 字段 | 偏移 | 类型 | 作用 |
|---|---|---|---|
| `inventoryShape` | 0x1B0 | GridShape | 内部网格形状（决定多少格子） |
| `widthPixels` | 0x138 | int | 像素宽 |
| `heightPixels` | 0x13C | int | 像素高 |
| `items` | 0x198 | List\<GameItem\> | 存储的物品 |
| `_lastShapeWidth` | 0x1B8 | int | 缓存宽 |
| `_lastShapeHeight` | 0x1BC | int | 缓存高 |

| 方法 | 签名 | 作用 |
|---|---|---|
| `.ctor(int width, int height)` | 构造 | 创建时指定网格尺寸 |
| `SetShape(int width, int height)` | → GameGridInventory | **运行时改网格尺寸** |
| `SetShape(string shape, int width)` | → GameGridInventory | 用形状字符串设置 |
| `Validate()` | abstract（继承自 GameInventory） | **改完必须调，刷新 UI** |

**容器工厂方法完整链路（MiniSmugglerBay 为例，[L1] ISIL 确认）**
```
1. DirectoryUtils.CreateInventoryWindow(3, 3, true) → (PixelWindow, GameInventory)
2. GameItem.SetContentWindow(PixelWindow)           ← 绑窗口到物品
3. GameItem.SetSpriteAndShape("Items/items_ship", "smuggler_bay_mini")  ← 设外观+外形
4. GameInventory.identifier = "smuggler_bay"        ← 内部库存标识
5. GameItem.unitValue = 150                         ← 价格（偏移0x200，不是容量！）
6. ContainerHelper.InitSmugglerBay(GameInventory, GameItem)  ← 设标签+校验委托
7. 标签：CONTAINER_TAG, ITEM_HIDDEN_TAG, SYSTEM_TAG, SYSTEM_TAG_UTILITY
8. GameItem.SetGameItemType("STORAGE")
```

**ContainerHelper 静态方法集（[L1] dump.cs 确认，纯方法类无字段）**
| 方法 | 作用 |
|---|---|
| `InitContainerItem(GameInventory, GameItem, List<string> allowedTags, List<string> allowedId)` | 通用容器初始化 |
| `InitSmugglerBay(GameInventory, GameItem)` | 走私者暗格初始化（设 IMPORTANT_TAG+CONTAINER_TAG + mayInventoryAddItemFunc 委托） |
| `InitContainerItemTagOnly(GameInventory, GameItem)` / `(GameItem)` | 仅标签初始化 |
| `InitBackpackItem(GameInventory, GameItem)` | 背包初始化 |
| `InitPouchItem(GameItem)` | 小袋初始化 |
| `InitModularBackpack(GameItem)` | 模块化背包 |
| `AllowOnlyTaggedItem(GameInventory, string, bool ownedOnly, bool excludeContainer)` | 限制只放指定标签物品 |
| `AllowOnlyOwnedItems(GameInventory)` / `ExcludeContainers` | 限制只放已有物品 |
| `IsNestableDevice(GameItem)` → bool | 是否可嵌套 |

**运行时改容器内部大小（[L1] 签名确认，[L0] 调用路径）**
1. 拿到容器的内部 `GameGridInventory`（从 CreateInventoryWindow 返回的 Item2，或从 GameItem.contentWindow 关联的 inventory）
2. `inventory.SetShape(newWidth, newHeight)`
3. `((GameInventory)inventory).Validate()`
4. 若窗口已打开，也调 `window.Validate()`

> 与 HyperMinimalist 改商店库存模式完全一致（`invElement.SetShape(3,3)` + `Validate()`），区别仅在对象从商店库存换成容器内部库存。

### 21.3 AllLargeStorage（所有箱子变大型）—— 工厂替换 + 存档改写

**Hook 点（[L1] ilspycmd 反编译确认）**
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `ShipSystemDirectory.StorageBay()` | Prefix | 改为返回 `StorageBayLarge()` |
| `ShipSystemDirectory.MakeshiftStorageBay()` | Prefix | 同上 |
| `ShipSystemDirectory.GunsmithStorageBay()` | Prefix | 同上 |
| `ShipSystemDirectory.ChemistStorageBay()` | Prefix | 同上 |
| `DirectoryMaster.Item(string,bool)` | Prefix | identifier 前缀 `storage_bay`（非large）→ 改 `storage_bay_large` |
| `PlayerStore.LoadGame()` | Postfix | 读档后遍历改 identifier |
| `PlayerStore.SaveGame()` | Prefix | 存档前强制全部改为 large |

**遍历全部物品的反射手法（[L1]）**
- `AccessTools.Field(typeof(PlayerStore),"instance")` 拿单例（静态字段，非 Instance 属性）
- `AccessTools.Field(typeof(PlayerStore),"gridInv")` 拿库存
- 递归 `GetChildren(obj)`：反射读 `childItems` 属性 → `Count` → `get_Item(i)`，按 `uniqueId` 去重（HashSet）
- 改 `item.identifier` 和 `item.spritePath` 两处

### 21.4 NestedStorage（嵌套储物）—— 放开容器校验

**Hook 点（[L1]）**
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `GeneralHelper.CanAddItemToInv` | Postfix | 双 Storage → `__result=true` |
| `ContainerHelper.AllowOnlyOwnedItemsExcludeContainers` | Prefix | 改调 `AllowOnlyOwnedItems` 并 return false |
| `ContainerHelper.AllowOnlyTaggedItem` | Prefix | `excludeContainer=false` |
| `ContainerHelper.AllowOnlyTaggedItemOwn` | Prefix | 同上 |
| `GraphUtils.CanAccept` | Postfix | 物品是 Storage → true |
| `GraphUtils.CanInsertToActiveContainer` | Postfix | 同上 |
| `ContainerHelper.InitContainerItem` | Postfix | `RelaxInv` 清空校验委托 |
| `ContainerHelper.InitContainerItemTagOnly` | Postfix | 同上 |
| `ContainerHelper.InitSmugglerBay` | Postfix | 同上（暗格也放开嵌套） |

**关键 API（[L1]）**
- `IsStorage(item)`：`item.itemTypes` 含字符串 `"STORAGE"` 或 identifier 含 `storage_bay`
- `RelaxInv()`：`((GameInventoryFunc)inv).mayInventoryAddItemFunc = null` —— 容器"能放什么"的委托置空即放开限制

### 21.5 DatePauseMod（日期暂停）—— F1 阻断新的一天

**Hook 点（[L1]）**
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `StoreStation.StartDay()` | Prefix | `DatePaused` 时 return false 阻止开新的一天 |

- 输入监听：`Input.GetKeyDown(KeyCode 282)`（F1）
- 状态绘制：`OnGUI` 用 `GUIStyle` 画左上角中文状态
- 间接确认：**`StoreStation.StartDay()` 是"新的一天开始"入口方法**

### 21.6 PerkPointMod（特质点数上限）—— 反射写属性

**Hook 点（[L1]）**
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `PerkUIController.OpenUI` | Postfix | `ForceMaxValues` |
| `PerkUIController.OnChange` | Postfix | `ForceMaxValues` |
| `PerkUIController.SelectPerk` | Prefix | `ForceMaxValues` |

- `ForceMaxValues`：反射 `AccessTools.Property(type,"maxPerkPoint"/"maxPerkCount")` 写 50，属性不可写时回退调 `set_xxx` setter 方法
- 配置：`MelonPreferences_Category` + `CreateEntry<int>`（MaxPerkPoint / MaxPerkCount）
- 确认类：**`PerkUIController`**（特质 UI 控制器），属性 `maxPerkPoint` / `maxPerkCount`

### 21.7 MoreStorageColors（存储箱动态上色）—— 运行时重绘 sprite

**用途**：给 storage bay 加 10 金属色 + 7 主题皮肤，不碰游戏文件，**读图集像素→按亮度重映射→生成新 sprite**。

**Hook 点（[L1]）**
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `ToggleAppearanceHelper.MakeSpriteCyclable(GameItem,string,Il2CppStringArray)` | Prefix | 变体数组尾部追加 `::msc::t<theme>`×7 + `::msc::c<0-9>`×10 |
| `GameItemElement.ResolveItemSprite()` | Prefix | spritePath 含 `::msc::` → `SpriteFactory.Resolve` 命中则替换并 return false |
| `GameItemElement.ResolveSpriteByName(string)` | Prefix | 同上 |
| `GameItem.SetSprite(string,string)` | Postfix | 写配色后缀到 TagSystem |
| `GameItem.ToggleSlotItem()` | Prefix | 按住 Ctrl/Alt 点物品 → 重置原色 |
| `GameItemElement.Validate()` | Postfix | 读档后从 TagSystem 恢复配色 |

**核心算法（Colorize，[L1]）**
1. `GetReadableAtlas`：RenderTexture.Blit 图集 → 新 Texture2D → ReadPixels（解决原图集不可读）
2. 读 sprite 的 textureRect 像素块
3. 跳过透明（a<0.5）和暗部（luma≤0.22，保留描边/轮廓）
4. 剩余像素按亮度排序 → 均匀映射 `RampLerp(Shadow→Mid→Highlight)`
5. 生成新 Texture2D+Sprite，`hideFlags=61`（DontSave），`_colorCache` 缓存

**存档方案（[L1]）**：复用游戏 `TagSystem` 的 `EnableTag/GetTag/DisableTag`，标签 key `MSC_COLOR`（存后缀）、`MSC_BASE`（存基础名），`valueString` 存数据——不新增任何字段。

**主题皮肤（[L1]）**：`Mods/MoreStorageColors/Textures/<theme>/<shape>.png`，首次运行自动生成原图模板（`MakeTemplate` 从图集读真形状），玩家覆盖 PNG 即可换肤；`SanitizeShape` 把非字母数字字符替换为 `_` 做文件名。

**游戏内工具**：F8 dump 所有加载 sprite 到 Reference 文件夹。

### 21.8 HyperMinimalist（极简专精）—— 动态缩网格 + 注入专精

**用途**：新增"Hyper-Minimalist"专精，选中后商店库存/展柜空间被大幅缩小。

**Hook 点（[L1]）**
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `EmporiumEntry.GetFrontInvPosition(int,int)` | Prefix | 全改写布局，return false |
| `EmporiumEntry.SetSmallInv()` | Postfix | 库存 `SetShape(3,3)`、后库存 `(5,3)`、垃圾桶 `(5,3)` |
| `EmporiumEntry.SetSmallInv()` | Prefix | 极简时展柜 `SetShape(2,2)` + 覆盖展柜贴图 |
| `EmporiumEntry.UpgradeDisplayCaseI()` | Postfix | 极简时展柜 `SetShape(3,3)` |
| `EmporiumEntry.UpgradeDisplayCaseII()` | Prefix | 极简时 `SetShape(4,3)` 且 return false 阻止原升级 |

**专精注入（[L1]）**：协程等 `LocalizationSettings.InitializationOperation.IsDone` 后，遍历所有 Locale，对 `PerkTable` 调 `RemoveEntry`+`AddEntry` 注入 `perk_minimalist_name` / `perk_minimalist_desc`——**往游戏 StringTable 动态加本地化条目**的完整示例。

**网格修改模式（[L1]）**：`invElement.SetShape(w,h)` + `((GameInventory)invElement).Validate()` + `invWindow.Validate()`——**改网格尺寸后必须调 Validate 刷新**。

**门控判断**：所有行为由 `StartingPerk.IsMinimalist()` 返回 bool 控制。

### 21.9 本节可复用结论

1. **容器工厂在 `ShipSystemDirectory`**，内部存储大小由 `DirectoryUtils.CreateInventoryWindow(width,height,true)` 创建时决定（返回 ValueTuple\<PixelWindow, GameInventory\>）；`GameItem.0x200` 是 `unitValue`（价格），**不是容量**
2. **改容器内部大小 = 拿到内部 GameGridInventory → `SetShape(w,h)` → `Validate()`**，与改商店库存模式一致
3. **`DirectoryMaster.Item(id,isOwned)` 是所有物品创建的统一入口**，Prefix 改 identifier 即可"狸猫换太子"
4. **`ContainerHelper` 是所有容器校验的集中地**（InitContainerItem / InitSmugglerBay / AllowOnlyOwnedItems*），改容器行为优先 Patch 此类
5. **`GameInventoryFunc.mayInventoryAddItemFunc` 委托置 null = 放开该容器的一切添加限制**
6. **改网格尺寸后必须 `Validate()`**，否则 UI 不刷新
7. **往游戏注入本地化文本**：等 LocalizationSettings 初始化 → `StringDatabase.GetTable(tableRef, locale)` → `RemoveEntry` + `AddEntry`，可全 Locale 生效
8. **存档数据不新增字段**：用游戏自带 `TagSystem` 的 `EnableTag/GetTag/DisableTag` + `valueString`
9. **游戏内快捷键/状态显示**：`Input.GetKeyDown(KeyCode.xxx)` + `OnGUI` 手画 GUIStyle
10. **ISIL 偏移归属必须交叉验证**：`[rsi+0x200]` 看似容量，实际读 dump.cs 字段表才确认是 unitValue；ISIL 中的寄存器指向哪个类要追来源，不能凭数值大小猜语义

## 附：编译部署命令

```bash
# 编译
cd "D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks"
dotnet build -c Release

# 部署开发版
copy /y "bin\Release\net6.0\JacksonPerks.dll" "..\WagesPerks.dll"

# 部署发布版（先改 Core.cs DebugMode=false）
copy /y "bin\Release\net6.0\JacksonPerks.dll" "D:\谷歌\深空当铺汉化包+王尔德补丁\WagesPerks_发布版\Mods\WagesPerks.dll"
```

---

## 十九、蛙哥妙妙箱与开局给物经验（2026-09-03）

### 19.1 OnUpdate 禁用经验 [L1 已验证]

**问题**：OnUpdate 每帧轮询会导致游戏严重卡顿，尤其是在特性UI选择阶段，反射调用会干扰UI点击。

**解决方案**：
- **禁用 OnUpdate**，改用事件驱动方式（Patch BeginDay / StartMainDialogue / AddClient 等）
- 开局给物用 Patch PlayerStore.BeginDay 的 Postfix，在进存档第一天触发
- 客户对话/物品用 Patch StoreClient.StartMainDialogue 的 Postfix，在客户进店对话开始时触发

**关键代码**：
`csharp
// 【已禁用】OnUpdate会导致游戏卡顿，改用Patch BeginDay方式处理
internal static void OnUpdate()
{
    return; // 直接返回，不执行任何逻辑
}
`

### 19.2 开局给物的正确方式（参考 ExtraPerks 的 InventoryGrant.TryGrant）[L1 已验证]

**错误方式**：
`csharp
// ❌ 用 PlayerStore.Instance.playerInventory，可能不存在或不是后背包
PlayerStore.Instance.playerInventory.UncheckedAccept(item);
`

**正确方式（参考 ExtraPerks 的 InventoryGrant.TryGrant）**：
`csharp
// ✅ 用 EmporiumEntry.Instance.backInvinvElement（后背包）
EmporiumEntry emporium = EmporiumEntry.Instance;
if (emporium != null && emporium.backInvinvElement != null)
{
    // 1. 标记为已拥有
    item.DisableTag("TAG_NOT_PURCHASED", true);
    item.DisableTag("not_purchased", true);
    
    // 2. 找一个有效槽位
    emporium.backInvinvElement.TryFindOneValidInventorySlot(item, false);
    
    // 3. 添加到后背包
    if (((GameInventory)emporium.backInvinvElement).UncheckedAccept(item))
    {
        // 4. 转移所有权（关键！否则物品可能显示为未购买）
        emporium.TransferOwnershipBackInv();
        emporium.TransferOwnedItemBackToInv();
    }
}
`

**InventoryGrant 完整实现（ExtraPerks 反编译）**：
`csharp
internal static bool TryGrant(string identifier)
{
    EmporiumEntry instance = EmporiumEntry.Instance;
    if (Contains(instance.GetInvItems(), identifier)) return true; // 已拥有则不重复给
    if (TryAdd(MarkOwned(DirectoryMaster.Item(identifier, true))))
    {
        return Contains(instance.GetInvItems(), identifier);
    }
    return false;
}

internal static bool TryAdd(GameItem item)
{
    MarkOwned(item);
    EmporiumEntry instance = EmporiumEntry.Instance;
    instance.backInvinvElement.TryFindOneValidInventorySlot(item, false);
    if (!((GameInventory)instance.backInvinvElement).UncheckedAccept(item)) return false;
    instance.TransferOwnershipBackInv();
    instance.TransferOwnedItemBackToInv();
    return true;
}
`

### 19.3 蛙哥妙妙箱实现经验 [L1 已验证]

**物品参数**：
- 物品ID：custom_storage_box
- 物品名称：蛙哥妙妙箱
- 外部占地：2×2（32×32像素 sprite，pixelsPerUnit=100）
- 内部库存：52×10=520格
- 什么都能放（清除 mayInventoryAddItemFunc 委托）

**关键实现步骤**：
1. **创建内部库存窗口**：DirectoryUtils.CreateInventoryWindow(52, 10, true)
2. **创建空物品**：ItemDirectory.CreateEmptyItem(null)
3. **绑定内容窗口**：container.SetContentWindow(contentWindow)
4. **设置外观**：container.SetSpriteAndShape("custom_atlas", "custom_storage_box_sprite")
5. **初始化容器**：ContainerHelper.InitContainerItem(internalInv, container)
6. **清除添加限制**：mayInventoryAddItemFunc 属性设为 null（允许放箱子、机器等任何物品）
7. **设置标签**：CONTAINER_TAG, ITEM_HIDDEN_TAG, SYSTEM_TAG, SYSTEM_TAG_UTILITY
8. **设置类型**：SetGameItemType("STORAGE")

**清除添加限制的代码**：
`csharp
var mayAddProp = internalInv.GetType().GetProperty("mayInventoryAddItemFunc", 
    BindingFlags.Public | BindingFlags.Instance);
if (mayAddProp != null)
{
    mayAddProp.SetValue(internalInv, null); // 设为null = 允许放任何物品
}
`

### 19.4 随机上锁箱子与钥匙卡对应关系 [L1 已验证]

| 箱子 identifier | 钥匙卡 identifier | 名称 |
|---|---|---|
| sec_box | sec_keycard | 治安战利品箱 |
| med_box | med_keycard | 医疗战利品箱 |
| eng_box | eng_keycard | 工程战利品箱 |
| service_box | ser_keycard | 服务战利品箱 |
| evidence_box | cmd_keycard | 证物箱 |

> **2026-09-04 修正（过时经验）**：原表写的是 `sec_loot_box`/`medical_keycard`/`engineer_keycard`/`service_keycard`/`command_keycard`，经 0.46D `_cpp2il_isil\IsilDump\Assembly-CSharp\MiscItemDirectory.txt` 核实全部过时/错误——正确 ID 见上表。箱子走 `PreBuiltItemHelper.LootCrate*`（内部用 `sec_box` 等）；钥匙卡在 MiscItemDirectory 成批注册的是 `sec_keycard`/`med_keycard`/`eng_keycard`/`ser_keycard`/`cmd_keycard`/`sci_keycard`/`sup_keycard`。
> - **指挥卡 = `cmd_keycard`**（不是 `command_keycard`，后者仅出现在字符串常量区、未注册到目录，用 `DirectoryMaster.Item("command_keycard")` 会得到问号物品）。
> - **研发卡 = `sci_keycard`**（旧表曾配给 evidence_box，但用户已要求妙妙箱不放研发卡，改放指挥卡）。

**往容器里放物品的代码**：
`csharp
// 获取容器的内部库存
var contentWindow = storageBox.contentWindow;
var invProp = contentWindow.GetType().GetProperty("inventory", BindingFlags.Public | BindingFlags.Instance);
GameInventory internalInv = invProp.GetValue(contentWindow) as GameInventory;

// 往内部库存放物品
internalInv.UncheckedAccept(lockedBox);
internalInv.UncheckedAccept(keycard);
`

### 19.5 自定义 sprite 显示经验 [L1 已验证]

**问号根因**：RenderHandler.LoadFromAtlasRaw 返回 null 时，返回 RenderHandler.unknownSprite（就是问号）。

**正确实现方式**：
1. **静态构造函数创建 sprite**：C# 保证在任何方法调用之前执行
2. **Patch RenderHandler.LoadFromAtlas**：当 name 是自定义 sprite 名称时返回自定义 sprite
3. **pixelsPerUnit=100**：32×32像素 sprite 显示为 0.32×0.32 单位，正好对应 2×2 格子
4. **嵌入像素数据**：用 	ex.SetPixels(pixels) 设置像素，不需要外部 PNG 文件（避免 System.Drawing.Common 依赖）

**sprite 显示大小机制**：显示大小 = 纹理宽度 / pixelsPerUnit。不是由 shape 决定的，shape 是由 sprite 尺寸自动计算的（32/16=2）。

## 第二十二章：XIAOWOTradePerks（更多特性-小窝）—— 起始特性 Mod 教科书实现

**来源**：`XIAOWOTradePerks.dll` v1.1.2（41KB，41 个 cs 文件），作者"遗忘的小窝"
**功能**：10 个原创起始特性（9 交易特性 + 业主密约）+ 独立图标 + 确定性调度 + 买断门店
**反编译**：ilspycmd -p -o

### 22.1 整体架构

```
Core (MelonMod 入口)
  └─ OnInitializeMelon → HarmonyInstance.PatchAll(Assembly)
  └─ OnUpdate → OwnerDeal.TickPendingWeeklyVisitorCheck()

PerkIds            → 10 个特性 ID 常量（xiaowo_trade_*）
PerkDefinition     → 特性数据类（id/name/description/cost/type/iconFile）
PerkRegistry       → 注册核心（EnsureRegistered/EnsurePicker/GetOrCreate/ApplyDefinition）
TradeEffects       → 纯静态数值计算（所有特性的倍率/修正逻辑）
DeterministicSchedule → 确定性调度（贵客来访日 + 现金流行情，FNV-1a hash）
OwnerDeal          → 业主密约（买断结局拦截 + 房东/商人每周来访 + 库存生成）
IconLoader         → 嵌入资源 PNG → Texture2D → Sprite（Dictionary 缓存）
各种 Patch         → 每个特性 1-3 个 Harmony Patch
```

### 22.2 特性注册模式（核心可复用）

**注册入口**：`StartingPerkList.InitStartingPerk` Postfix → `PerkRegistry.EnsureRegistered()`

```csharp
// EnsureRegistered 核心逻辑
List<StartingPerk> perks = StartingPerkList.Perks;
foreach (PerkDefinition def in All) {
    StartingPerk existing = FindPerk(perks, def.Id);
    if (existing != null) {
        ApplyDefinition(existing, def);  // 已存在则覆盖字段
        Created[def.Id] = existing;
    } else {
        perks.Add(GetOrCreate(def));     // 不存在则 new + Add
    }
}

// GetOrCreate
StartingPerk perk = new StartingPerk();
ApplyDefinition(perk, def);

// ApplyDefinition 设置字段
perk.id = def.Id;
perk.cost = def.Cost;
perk.maxSlot = 0;
perk.type = def.Type;           // StartingPerkType 枚举：0=正面, 1=负面, 2=中性
perk.rawName = def.Name;        // 直接写 rawName，不走本地化表
perk.rawDescription = def.Description;
perk.incompatiblePerks = new List<string>();  // 初始化为空
```

**UI 注入**：`PerkUIController.OpenUI` Postfix → `PerkRegistry.EnsurePicker(ui)`
- 遍历 `ui.availablePerks` 和 `ui.selectedPerks`，找不到则 `Object.Instantiate(ui.perkElementPrefab)`
- 设置 `element.id = def.Id`、`element.isSelected = false`、`element.perk = GetOrCreate(def)`
- `element.icon.sprite = IconLoader.Get(def)`
- 最后 `ui.SortPerkContainer(ui.availablePerks)`

**文本替换**（不走本地化表，直接 Postfix 改返回值）：
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `StartingPerk.GetLocalizedDisplayName` | Postfix | 替换特性名 |
| `StartingPerk.GetLocalizedDescription` | Postfix | 替换特性描述 |

**判断激活**：`StartingPerk.IsPerkActive(string id)`（静态方法，try-catch 包裹防版本不兼容）

### 22.3 10 个特性效果与 Patch 点对照表

| 特性 | ID | 成本 | 效果 | Patch 点 | 计算方法 |
|---|---|---|---|---|---|
| 金牌鉴定师 | xiaowo_trade_gold_appraiser | +2 | 鉴定费+20% | `AppraisalServiceHelper.GiveAppraisalFee` Prefix/Postfix | AppraisalBonus(fee)=fee×0.2 |
| 鉴定招牌 | xiaowo_trade_appraisal_sign | +2 | 鉴定客户出现率+30% | `StoreClientManager.HandleNormalClient` Postfix | TryAddAppraisalClient（hash%100<30） |
| 灰色保单 | xiaowo_trade_grey_insurance | +1 | 每周首位赃物商人热度减半 | `PlayerStore.OnItemBought` Postfix | ApplyGreyInsurance（PlayerPrefs 持久化） |
| 停电掮客 | xiaowo_trade_blackout_broker | +1 | 停电时售价+10% | `GameItem.GetNegociatedValue` Postfix + `PlayerStore.SellItem` Prefix/Postfix/Finalizer | ApplySaleModifiers（_playerSellingDepth 标记） |
| 贵客预约 | xiaowo_trade_luxury_appointment | +2 | 每周额外奢侈品商，预算×150%，收购价-10% | `StoreClientManager.HandleNormalClient` Postfix | TryAddLuxuryClient（StoreClientList.CreateUpperLowerVisitorLuxury） |
| 现金流赌徒 | xiaowo_trade_cashflow_gambler | 0 | 每周1天行情：繁荣+20%/紧缩-20% | `StoreEventManager.OnDayStart` Postfix + 4 个 StoreEvent/ItemFeature Patch | EnsureCashflowCalendar（NegociationData） |
| 安保眼中钉 | xiaowo_trade_security_target | -3 | 赃物热度+25%，检查间隔-25% | `StolenHelper.InitStolenItem` Postfix + `StoreClientManager.ComputeInspectionCooldownRange` Postfix | SecurityTargetHeat(heat)=ceil(heat×1.25), ShortenedCooldown(days)=floor(days×0.75) |
| 门面失修 | xiaowo_trade_rundown_storefront | -2 | 吸引力-150，翻新费+20% | `PlayerStore.GetCurrentStoreAttractiveness` Postfix + `NetworkUpgrade.GetCost` Postfix | RundownAttractiveness(attr)=attr-150, RenovationCost(cost)=ceil(cost×1.2) |
| 熟客赊账 | xiaowo_trade_regular_credit | -1 | 普通回头客预算-25% | `StoreClientManager.TrackClient` Prefix | ReturningBudget(budget)=floor(budget×0.75)，Pointer 去重 |
| 业主密约 | xiaowo_trade_owner_deal | 0 | 买断门店不结束，每周房东85折卖货+商人收账 | 6 个 Patch（见 22.6） | OwnerDealRules + OwnerStockRoll |

### 22.4 图标注入模式

**PNG 打包**：作为 Embedded Resource 编译进 DLL（csproj 中 `<EmbeddedResource Include="icons\*.png" />`）

**加载流程**（IconLoader.Get）：
```
Assembly.GetManifestResourceNames() → 匹配 EndsWith(iconFile)
→ GetManifestResourceStream → byte[]
→ new Texture2D(2,2, RGBA32, false, false) { filterMode=Point, wrapMode=Clamp, hideFlags=HideFlags.HideAndDontSave }
→ ImageConversion.LoadImage(texture, bytes, false)
→ Sprite.Create(texture, Rect(0,0,w,h), pivot(0.5,0.5), pixelsPerUnit=100)
→ Dictionary<string, Sprite> 缓存
```

**两条注入路径**：
1. `StartingPerkIconLoader.Start` Postfix → `IconLoader.InjectAll()` → 写入 `StartingPerkIconLoader.perkIcons` 字典
2. `StartingPerkElement.Start` Postfix → 直接设 `element.icon.sprite = IconLoader.Get(def)`

> hideFlags=HideFlags.HideAndDontSave(61) 防止 Unity 卸载纹理

### 22.5 确定性调度模式（防读档重随机）

**核心**：用 `runID + 类型 + day/week` 做 FNV-1a hash，所有随机结果由 hash 决定，读档不重新随机。

```csharp
// FNV-1a hash
int StableHash(string value) {
    int num = -2128831035;  // FNV offset basis
    for (int i = 0; i < value.Length; i++)
        num = (num ^ value[i]) * 16777619;  // FNV prime
    return num;
}
int PositiveModulo(int hash, int mod) => ((hash % mod) + mod) % mod;
```

**贵客来访日**：`PositiveModulo(StableHash(runID+"|luxury|"+week), 7) + 1` → 每周第几天来
**现金流 boom/bust**：hash 奇偶决定
**现金流起始日**：hash 决定周内第几天
**防重复**：`_lastDailyGenerationKey = runID+"|"+day`，同一天只触发一次
**持久化**：灰色保单用 `PlayerPrefs.SetString("XIAOWOTradePerks.GreyInsurance."+runID+"|"+week, clientId)`

### 22.6 业主密约模式（买断门店不结束）

**6 个 Patch**：
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `PlayerStore.CanBuyOutStore` | Postfix | 现金≥买断价 → `__result=true`（出现买断选项） |
| `PlayerStore.ExecuteGameOver` | Prefix | ending=="buyout" 且已激活 → return false（**游戏不结束**） |
| `PlayerStore.LoadGame` | Postfix | ArmWeeklyVisitorCheck（延迟 90 帧调度） |
| `StoreClientManager.HandleNormalClient` | Postfix | ScheduleWeeklyVisitors（每周 day%7==0 生成房东+商人） |
| `__c._LandlordWholesale_b__31_0` | Prefix | 替换房东库存生成（TrySpawnStock） |
| `PlayerStore` 相关 | — | 房东 SetWholesale(15) = 85 折 |

**每周来访逻辑**（ScheduleWeeklyVisitors）：
- 条件：`IsEnabledAndOwned(store)` = store.IsPropertyPaid && 特性激活
- 时机：`OwnerDealRules.IsFormerRentDay(day)` = day % 7 == 0（原租金日）
- 同时生成两个客户：
  - 商人：`StoreClientList.CreateMerchant()`（identifier="merchant"）
  - 房东：`StoreClientUniqueList.LandlordWholesale()` → 改 identifier="xiaowo_trade_owner_landlord"，`SetWholesale(15)`（85折），CanReturn=false
- 防重复：`PlayerPrefs.SetInt("XIAOWOTradePerks.OwnerDeal.Pair."+runID, day)`

**房东库存**（OwnerDealStockPatch）：
- Patch 编译器生成的闭包方法 `__c._LandlordWholesale_b__31_0`
- `TrySpawnStock` 用 `OwnerDealRules.GetVisitSeed(runID, day)` 做 Random seed
- `OwnerStockRoll`：Count=1-4 件，5% 概率受限神经核心（system_capped_neural_core），1% 概率不受限神经核心（system_uncapped_neural_core）

**延迟调度**：`ArmWeeklyVisitorCheck()` 设 `_pendingFrames=90`，`OnUpdate` 里逐帧递减，到 0 时调 ScheduleWeeklyVisitors（避免 LoadGame 时 StoreClientManager 未初始化）

### 22.7 现金流行情模式（全物品价格事件）

**事件创建**（EnsureCashflowCalendar）：
```csharp
StoreEvent evt = new StoreEvent();
evt.InitNormalEvent();
evt.identifier = "xiaowo_trade_cashflow_"+hash+"_"+week+"_"+(boom?"boom":"bust");
evt.displayName = boom ? "繁荣" : "紧缩";
evt.eventType = EventType.Normal(0);
evt.eventArea = EventArea.Market(2);
evt.duration = 1; evt.totalDuration = 1;
evt.startDay = cashflowStartDay;
evt.IsKnown = true;
// 关键：NegociationData 实现全物品价格影响
NegociationData nd = new NegociationData("XIAOWO_ALL_ITEMS", boom ? 20 : -20, desc, displayName);
nd.identifier = evt.identifier + "_price";
evt.negociationDatas.Add(nd);
// 排队或激活
if (startDay <= CurrentDay) manager.ActivateEvent(evt, false, true);
else manager.QueueFuturEvent(evt, startDay - CurrentDay, true);
```

**4 个配套 Patch**：
| Patch 目标 | 类型 | 作用 |
|---|---|---|
| `StoreEventManager.OnDayStart` | Postfix | EnsureCashflowCalendar（确保事件在日历上） |
| `StoreEvent.IsItemAffectedByThisEvent` | Postfix | 现金流事件 → `__result=true`（影响所有物品） |
| `ItemFeature.CreateAndAddFeatureFromEvent` | Prefix | 自定义特征显示文本（繁荣/紧缩 ±20%） |
| `StoreEvent.GetEventPriceEffect` | Postfix | 自定义价格效果文本 |

### 22.8 本节可复用结论

1. **起始特性注册 = Patch `StartingPerkList.InitStartingPerk` Postfix**，new StartingPerk() + 设置 id/cost/type/rawName/rawDescription + Add 到 Perks 列表
2. **特性文本不走本地化表**：直接写 `rawName`/`rawDescription`，再 Patch `GetLocalizedDisplayName`/`GetLocalizedDescription` Postfix 替换返回值
3. **特性激活判断用 `StartingPerk.IsPerkActive(string id)`**（静态方法，try-catch 包裹防崩溃）
4. **UI 注入 = Patch `PerkUIController.OpenUI` Postfix**，Instantiate perkElementPrefab + 设 id/icon/perk + SortPerkContainer
5. **图标 = Embedded Resource PNG → Texture2D.LoadImage → Sprite.Create**，hideFlags=HideFlags.HideAndDontSave，Dictionary 缓存；双路径注入（perkIcons 字典 + element.icon.sprite）
6. **确定性随机 = FNV-1a hash(runID+类型+day/week)**，读档不重随机；防同一天重复用 `_lastKey` 字符串比较
7. **价格类特性 = Patch `GameItem.GetNegociatedValue` Postfix**，用 `_playerSellingDepth` 计数器标记"玩家正在卖"（Prefix++/Postfix--/Finalizer-- 三件套）
8. **全物品价格事件 = 创建 StoreEvent + NegociationData("XIAOWO_ALL_ITEMS", ±20)**，配合 Patch IsItemAffectedByThisEvent 返回 true
9. **买断不结束 = Patch `PlayerStore.ExecuteGameOver` Prefix return false**（ending=="buyout" 时拦截）
10. **延迟初始化 = 帧计数器（_pendingFrames=90）在 OnUpdate 里递减**，避免 LoadGame 时依赖的管理器未就绪
11. **编译器生成闭包方法也能 Patch**：`__c._LandlordWholesale_b__31_0` 这种 lambda 方法名可直接做 Harmony Patch 目标
12. **回头客预算修改 = Patch `StoreClientManager.TrackClient` Prefix**，用 `((Il2CppObjectBase)client).Pointer.ToInt64()` 做 HashSet 去重，避免同一客户多次被改

---

*本文档汇总了 Wage's Perks 开发过程中所有经实践验证的问题与解决方案（100+ 次编译部署测试），以及反编译 8+ 个参考 Mod 所得的可复用经验。*
*核心原则：先诊断再修复，一次只改一个变量，失败两次即换思路，优先参考已有 Mod。*
*拆包原则：4 层数据源不混淆，签名层用 Il2CppDumper，方法体用 Cpp2IL ISIL，Mod 源码用 ilspycmd，L1→L2→L3 不跳级。*
*Mod 设计原则：Patch 只捕获事件，逻辑集中在 Helper；TagSystem 做存储，反射调用做版本兼容；UI Patch 必有 Finalizer，启动必有 Probe。*
---

## 二十三、代码优化实战经验（v5 迭代：2026-09-03）

### 23.1 随机数统一优化

**问题**：项目中存在 103 处随机数调用，分散在 9 个文件中，每个文件各自维护静态 _rng / _tradeRng 实例，快速连续调用时 seed 相同导致随机结果重复。

**解决方案**：
1. 在 Core.cs 添加全局静态 Random 实例：
`csharp
public static readonly System.Random Rng = new System.Random();
`
2. 所有 
ew Random() 调用（11处）替换为 Core.Rng
3. 所有文件的 _rng / _tradeRng 静态实例（92处）替换为 Core.Rng 并删除定义

**效果**：快速连续调用随机数时不再出现 seed 相同导致的重复结果。

### 23.2 FNV-1a hash 确定性随机数

**问题**：按天计算的随机事件（霉运丢钱、宿醉概率、治安检查）读档后结果会改变，因为 Random.Next() 是伪随机序列，读档后序列位置不同。

**解决方案**：参考 XIAWOTradePerks 的核心实现，创建 DeterministicRandom.cs 工具类：
- 用 FNV-1a hash（offset basis=2166136261，prime=16777619）计算 hash(runID + 类型 + day)
- 提供 NextDouble(type, day) / Next(type, day, max) / NextBool(type, day, probability) / Choose<T>(type, day, array) 等方法
- GetRunId() 用反射查找 PlayerStore 的 saveID/runID 等字段

**已替换的确定性随机数**：
- 霉运缠身丢钱金额（50-200，按天计算）
- 好酒之徒宿醉概率（30%，按天计算）
- 招贼体质治安检查概率（100%→20%，按天计算）

**注意**：每次交易/收购触发的随机数（拾荒直觉奖励、笑面虎议价翻盘等）不适合用按天确定性随机，因为同一天多次调用会得到相同结果，保留 Core.Rng。

### 23.3 特性状态持久化

**问题**：FrogPowerPerk、WineLoverPerk、JuanStory 等特性的所有状态都是静态变量，读档后全部重置，导致玩家等级、声誉、宿醉状态、剧情进度丢失。

**解决方案**：创建 PerkStatePersistence.cs 工具类，基于 UnityEngine.PlayerPrefs：
- key 格式：WagesPerks_<runID>_<perkId>_<key>，带 runID 避免不同存档混淆
- 提供 SetInt/GetInt / SetFloat/GetFloat / SetBool/GetBool / SetString/GetString / HasKey / ResetCache / ClearPerkState 等方法

**持久化时机**：
1. **新游戏**：PostfixOnNewGame → ResetState()（重置所有状态）
2. **读档**：PostfixOnLoadGame → LoadState()（从 PlayerPrefs 恢复）
3. **状态变化**：关键节点自动 SaveState()（如蛙哥每10次购买、胡安阶段变化、酒商宿醉时）

**已实现持久化的特性**：
| 特性 | 状态数量 | 关键状态 |
| --- | --- | --- |
| 蛙哥牛逼 | 7个 | _totalPurchases、_totalSales、_inspectionCount、_contrabandSold、_gameDay、_hasBeenRobbed、_hasBribed |
| 好酒之徒 | 1个 | _hungover（宿醉状态） |
| 胡安剧情 | 13个 | _juanStage、_juanTrust、_juanTradeCount、_juanMet、_juanKeepsake、_juanProtected、_juanBetrayed、_juanNeutralExit、_juanLedger、_juanNightGiftCount、_fameTestDone、_stage3TriggerDay、_lastVisitDay |

**效果**：读档后玩家等级、声誉、宿醉状态、4阶段长线剧情进度不再丢失。

### 23.4 发布版与开发版分离

**问题**：开发版需要调试日志（DebugMode=true），发布版需要关闭调试日志（DebugMode=false），但每次手动修改 Core.cs 再编译容易出错。

**解决方案**：
1. 开发版保持 DebugMode = true，编译后部署到 Mods\WagesPerks.dll
2. 发布版临时把 DebugMode 改成 alse，编译后部署到 D:\谷歌\深空当铺汉化包+王尔德补丁\WagesPerks_发布版\Mods\WagesPerks.dll
3. 编译完成后立即把 DebugMode 改回 	rue，重新编译开发版

**注意**：发布版和开发版的源码是同一个，只是编译时 DebugMode 不同。

### 23.5 反射优化的限制

**问题**：项目中有 9 处反射遍历程序集找类（ImageConversion、PowerHelper、ShipSystemDirectory 等），性能较差。

**结论**：IL2CPP 中 ImageConversion 等类不在标准 UnityEngine 命名空间下，必须用反射查找，不能直接引用。直接引用会报 CS0234: 命名空间"UnityEngine"中不存在类型或命名空间名"ImageConversion"。

**已验证**：
- 	ypeof(UnityEngine.ImageConversion) → 编译失败（CS0234）
- 反射遍历 AppDomain.CurrentDomain.GetAssemblies() 找 ImageConversion → 编译成功，运行正常

**建议**：IL2CPP 游戏中，不确定类型在哪个命名空间时，先用反射查找，确认后再考虑直接引用。

### 23.6 编译警告清理的注意事项

**问题**：项目中有约 40 个编译警告，主要是 catch 块中 ex 变量未使用（CS0168）、重复 using（CS0105）、未使用变量（CS0219）、无法访问代码（CS0162）等。

**注意事项**：
1. **catch 块中的 ex 变量不能简单删除**：有些 catch 块中的 ex 变量在日志输出中使用（如 Core.LogMsg("失败: " + ex.Message)），简单把 catch (Exception ex) 改成 catch (Exception) 会导致 CS0103: 当前上下文中不存在名称"ex"。
2. **正确做法**：先检查 ex 变量是否在 catch 块中使用，未使用的才改成 catch (Exception)，使用的保持原样。
3. **无法访问代码**：
eturn; 后面的代码可以安全删除，但要确认是故意禁用的功能（如胡安剧情已禁用），删除前加注释说明。

**已清理的警告**：
- FrogPowerPerk.cs 重复 using System
- AlcoholMerchantPerk.cs 未使用变量 shellFallback
- catch 警告保持原样（部分 ex 变量在使用中）

### 23.7 代码重复抽取的风险

**问题**：三个商人特性（酒商、水商、退休枪匠）的代码 95% 重复，可以抽基类 WeeklyVisitorPerk。

**风险**：
1. 三个特性虽然结构相似，但售卖物品数组、NPC 类型名、访问间隔天数等参数不同，抽基类需要仔细设计构造函数。
2. 修改大量代码可能引入新 bug，需要充分测试。
3. IL2CPP 游戏中，基类和派生类的 Harmony Patch 可能有兼容性问题。

**建议**：在稳定版本中不急于抽基类，先保持三个独立文件，等功能完全稳定后再考虑重构。

---

> **v5 迭代总结**：本次优化完成了随机数统一（103处）、确定性随机数工具类、特性状态持久化工具类及3个特性的实现（蛙哥7状态、酒商1状态、胡安13状态）、招贼体质参数调整（100%→20%）、发布版分离、反射限制验证、编译警告部分清理。所有优化均经过编译验证，0错误。
---

### 23.8 v1.0.8 测试反馈修复经验（2026-09-03）

**背景**：整理了一份6+1条测试反馈，逐条定位修复。这些是"玩家实测发现问题"的典型，直接复用。

**Bug1：未选特性时仍被改对话（特性门控缺失）**
- 现象：没选"蛙哥牛逼"天赋，普通NPC台词仍被mod改写，看不出买卖意图。
- 根因：PostfixSpecialNpcStartDialogue 的 else 分支（未激活时）调用 GlobalModifyDialogue 全局改写对话。全局对话本应属于蛙哥牛逼特性。
- 修复：删除 else 分支。**经验：所有对话/商品改写必须做特性门控（IsActive 检查），否则未选特性的玩家被误伤。**
- 残留：GlobalDialogueUpdate 方法体被注释禁用（死代码），未选特性时对话完全保持原版。

**Bug2：读档后重复发箱（初版 hack 已废弃，根治见第25章）**
- 现象：读档后出现两个蛙哥妙妙箱，第一个打不开。
- 初版修复用 PlayerPrefs 持久化防重（static 标记持久化），但该方案随后被证明会导致"开新档不给箱"——OnNewGame 重置不可靠，持久化标记残留 true 误拦发箱（见 25.1 根因）。此 hack 已废弃。
- **根治**：开局给物走 NewGameData.HandleInitialItem 原生钩子（只在开新档触发）+ 进程内静态标志防重；读档恢复走 DirectoryMaster 工厂表注册（见第25章）。

**Bug3：箱内物品显示 (未拥有)/问号**
- 现象：钥匙卡显示"(未拥有)"，物品图标问号。
- 根因：DirectoryMaster.Item 创建物品后带有 TAG_NOT_PURCHASED / not_purchased 标签，未标记已拥有。
- 修复：对创建的箱子和钥匙卡 DisableTag("TAG_NOT_PURCHASED", true) + DisableTag("not_purchased", true)。
- **经验：DirectoryMaster.Item 创建的任何物品都要显式 DisableTag 已拥有标记，否则 UI 显示未拥有。参考 TryGiveStorageBox 对主箱子的处理。**

**Bug4：简体中文时特性界面变英文（本地化误伤）**
- 现象：中文环境下特性界面文本变英文。
- 排查：PostfixGetLocalizedPerkTable / PerkTableLocPatch.Prefix 拦截 LocHelper.GetLocalizedPerkTable。代码逻辑只在 TryGetLoc 匹配自定义特性时覆盖，理论上不误伤原版。但为保险加白名单：先 CustomStartingPerks.Find(perkId) 确认是自定义特性才覆盖。
- **经验：本地化覆盖补丁必须加白名单校验（Find 确认特性ID属于自己），绝不能只靠 TryGetLoc 的隐式匹配。原版特性走游戏本地化，保持原语言。**

**Bug5：对话开头空白文本**
- 现象：部分对话开头有一段空白。
- 根因：Dialogue.SetText(name, text) 的 name 可能为空/含空白，或文本为空。
- 修复：SetText 前检查 string.IsNullOrWhiteSpace(newDialogueText) 则跳过；name 用 (name ?? "").Trim()。
- **经验：对话替换三连防护——文本空则跳过、说话人名字去空白、生成文本非空才替换。**

**Bug6：与 More Storage Color 共存崩溃**
- 现象：多mod共存时启动 Fatal error: Internal CLR error (0x80131506)，崩在 DynamicMethodDefinition.Reload。
- 排查：崩在 Harmony 创建补丁代理（MonoMod 将IL编译为DynamicMethod）。这是 IL2CPP + 多mod 同时 patch 相同方法时的 Harmony 兼容冲突，属于运行时CLR级崩溃，try-catch 救不了。
- 现状：ManualPatcher 每个 Patch 已有 try-catch。发布时只放 WagesPerks.dll（不打包 MonoMod/Harmony 依赖，用 MelonLoader 内置），避免 DLL 版本冲突。
- **经验：①发布包只放 mod 本体 DLL，依赖用 MelonLoader 内置；②崩溃在 DynamicMethodDefinition.Reload 属于 Harmony/IL2CPP 底层冲突，需实测复现定位是哪个 mod 的 patch 触发；③多mod共存问题需要"单开本mod正常 + 加其他mod复现"的方式定位。**

**Bug7：版本号显示错误**
- 现象：游戏内 mod 版本显示 1.0.7（用户说"dll还是1.07"）。
- 根因：Core.cs 第8行 [assembly: MelonInfo(...)] 的版本号还是 "1.0.7"，日志字符串已是 "v1.0.8" 但 MelonLoader 显示版本用的是 MelonInfo 程序集属性。
- 修复：MelonInfo 版本号 1.0.7→1.0.8。
- **经验：改版本号必须同步改两处——MelonInfo 程序集属性 + 日志字符串。游戏/MelonLoader 显示的是 MelonInfo 里的版本。**

---

## 24. 自动双语切换（Mod文本随游戏设置语言变化）[L1 实测]

### 需求
玩家希望一个 DLL 同时支持中英文：游戏设置中文显示中文、设置英文显示英文，不用维护两个版本。

### 语言检测（核心）
游戏引用了 Unity Localization 包（csproj 已有 UnityEngine.LocalizationModule.dll + Unity.Localization.dll），可以直接检测当前语言：

`csharp
using UnityEngine.Localization.Settings;

public static bool IsEnglish()
{
    try
    {
        var locale = LocalizationSettings.SelectedLocale;  // 跟随游戏内设置
        if (locale != null)
        {
            string code = locale.Identifier.Code ?? "";
            if (!string.IsNullOrEmpty(code))
                return !code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
    }
    catch { }
    // 兜底：系统语言
    return Application.systemLanguage != SystemLanguage.ChineseSimplified
        && Application.systemLanguage != SystemLanguage.ChineseTraditional
        && Application.systemLanguage != SystemLanguage.Chinese;
}

public static string T(string zh, string en) => IsEnglish() ? en : zh;
`

### 用法
- **DisplayName/Description 属性**：internal override string DisplayName => LangHelper.T("蛙哥牛逼", "Frog Power");
- **const 常量改动态**：玩家可见的 const 字符串（如箱子名）必须改成 static string xxx => LangHelper.T(...)，const 不能运行时求值。const→property 后调用处不变（SetName(CONTAINER_NAME) 照常编译）。

### 覆盖范围（本项目）
- 12 个特性的 DisplayName（蛙哥牛逼/博士之友/退休枪匠之友/水商之友/酒商之友/捡漏直觉/刀尖舔血/好酒之徒/笑面虎/招贼体质/霉运缠身/信誉扫地）
- NewTraits.cs 7 个特性的 Description
- 蛙哥妙妙箱名称 CONTAINER_NAME 和箱子名

### 关键经验
1. **游戏引用了 Unity.Localization**（csproj 里 UnityEngine.LocalizationModule.dll + Unity.Localization.dll），LocalizationSettings.SelectedLocale.Identifier.Code 是最准的语言检测（跟随游戏内设置），比 Application.systemLanguage（系统语言）准确。
2. **SelectedLocale 可能为 null**（Localization 未初始化时），必须 try-catch + 系统语言兜底。
3. **const 字符串无法运行时切换语言**，玩家可见文本一律用 static property + T()。
4. **验证方法**：反编译 DLL 确认 LangHelper.T("蛙哥牛逼", "Frog Power") 真的在 IL 里（用 ilspycmd -t 指定类）。不要用 UTF8 字节搜索——.NET 字符串常量是 UTF-16 存储，英文搜索会漏。
5. **发布版只需一个 DLL**：不再需要单独英文版，英文版目录/英文zip可清理。README 注明"自动双语"。

### 24.5 语言检测改进 + 蛙哥妙妙箱新档误判修复 [L1 实测]

**问题1：双语切换没落实**
- 现象：游戏切英文后特性名仍中文。
- 根因：LocalizationSettings.SelectedLocale 在 IL2CPP 下可能返回 null/抛异常，走系统语言兜底（中文系统→中文）。
- 修复：优先用游戏自己的 LocHelper.GetCurrentLocaleCode()（IL2CPP 静态方法，返回当前语言代码），再兜底 SelectedLocale，最后系统语言。
- 经验：**游戏有自带的 GetCurrentLocaleCode()，比 Unity Localization 的 SelectedLocale 可靠**。

**问题2：新档没有蛙哥妙妙箱**
- 现象：开新档背包没箱子，日志却显示"读档恢复：该存档已给过蛙哥妙妙箱，跳过"。
- 根因：PerkStatePersistence._cachedRunId 缓存 runID 后不刷新。ResetState() 只设 _storageBoxGiven=false，没调 ResetCache()。新档时 PlayerPrefs 检查用旧 runID → 误判"已给过"→ 跳过发箱。
- 修复：ResetState() 里加 PerkStatePersistence.ResetCache()（刷新 runID）+ SetBool(perkId,"storageBoxGiven",false)（强制清除持久化标记）。
- 经验：**PlayerPrefs 持久化带 runID 时，新游戏必须 ResetCache + 重置标记，否则新档沿用旧档 runID 导致状态串档**。


## 二十五、蛙哥妙妙箱「开局不给箱 + 读档打不开」根因与原生修复（2026-09-03）[L1 ISIL 实测]

### 25.1 问题1：开新档第一天不给箱子——根因

**现象**：开新档背包没箱子，日志显示 `HandleInitialItem原生钩子触发，给予蛙哥妙妙箱...` 之后**没有任何创建日志**（在 TryGiveStorageBox 最前面的无日志 return 退出）。

**根因**（日志实锤）：
1. 之前"读档补发"方案测试时把 `storageBoxGiven` **持久化标记**写成了 true（PlayerPrefs，带 runID）。
2. `FrogPowerPerk.OnNewGame()`（负责重置持久化标记为 false）在开新档时**根本没触发**（日志无"重置发箱标记"）。
3. 于是 `HandleInitialItem` 给箱子时，TryGiveStorageBox 里的**持久化防重检查**发现 `storageBoxGiven==true` → 直接 return → 箱子不创建。

**核心矛盾**：持久化防重是治"读档多刷"留下的，但它依赖 OnNewGame 重置；OnNewGame 不可靠 → 标记残留 true → 把箱子误拦。

**修复**：删掉持久化防重检查，只保留进程内静态标志 `_storageBoxGiven`（每次启动自动重置为 false）。
- `HandleInitialItem`（NewGameData 原生钩子）**只在开新档调用**（读档走 LoadGame 分支）→ 天然不会多刷。
- 静态标志防同一次运行内重复给。
- 不需要持久化防重。

**教训**：两套机制互相打架时，删掉不可靠的那套（持久化防重），保留语义正确的那套（静态标志 + 原生钩子只在开新档触发）。

### 25.2 问题2：读档后箱子打不开——根因（ISIL 实锤）

**现象**：读档后箱子在背包里（图标正常），但双击无反应。

**根因链路**：
1. **双击打开容器**走 `ItemMouseDoubleClickHandler.OpenContentAction`，方法体第一段：
   ```
   016 Move rax, [rbx+560]        ← GameItem.contentWindow（字段偏移 0x230 = 560）
   018 JumpIfEqual {40}           ← contentWindow == null → 直接跳转返回（无反应！）
   038 Call PixelWindow.ToFront   ← contentWindow 非空才打开/前置窗口
   ```
   → **contentWindow 为 null 时双击直接 return，打不开**。
2. **游戏原生容器**（storage_bay 等）读档后 contentWindow 能恢复，是因为读档时游戏按 identifier 走工厂 `SetContentWindow` 创建物品。
3. **自定义容器** `custom_storage_box` 的 identifier 不在游戏工厂表 → 读档时游戏**不重建窗口** → contentWindow 为 null → 打不开。

### 25.3 游戏读档物品恢复机制（SaveManager.DecodeNodes ISIL 实锤）

- `SaveItemNode`（ISerializationCallbackReceiver）序列化物品：identifier / uuid / itemState(TagSystem) / itemShape / itemModifiedShape / itemType / name / spriteAtlasPath / spritePath / itemTypes / **childItems(List<long> uuid)** / **childItemInventoryNode(List<int>)** / **tempLink(GameItem)**。
- 容器内容以 **uuid 列表**保存（childItems），读档后按 uuid 链接回内部库存。
- `SaveManager.DecodeNodes(List<SaveItemNode>)`：遍历节点 → `DirectoryMaster.Item(identifier, true)` 按 identifier 创建物品 → 恢复字段（tags/sprite/itemTypes/value）→ 递归恢复 childItems 内容 → 返回 GraphNodeStorage。
- **读档时物品是"按 identifier 走 DirectoryMaster 工厂重新创建"**，不是反序列化已有对象。

### 25.4 原生修复：把自定义物品注册进游戏物品目录（参考 EmptyNukeBarrel）

**核心思路**：把 `custom_storage_box` 注册进 `DirectoryMaster` 的工厂表，读档时游戏走原生路径 `DecodeNodes → DirectoryMaster.Item("custom_storage_box")` → 我们的工厂创建带 contentWindow 的完整箱子 → 能打开；内容由 DecodeNodes 按 uuid 恢复。

**机制拆解**：
- `Directory<T>`（MonoBehaviour 抽象类）：`factoryDictionary = Dictionary<string, Func<T>>`，方法 `Add(string, Func<T>)` / `Set(...)` / `Has(string)` / `Create(string)`。物品目录 `ItemDirectory : Directory<GameItem>`，子类 ContainerItemDirectory / AmenitiesItemDirectory / ModItemDirectory 等。
- **注册方式**（EmptyNukeBarrel 模式）：
  1. Patch `ItemDirectory` 子类的 `InitDirectory` Postfix（游戏启动时目录初始化触发），拿到目录实例。
  2. `((Directory<GameItem>)(object)dir).Has(identifier)` 防重。
  3. 构造 Il2Cpp 委托：`System.Func<GameItem> f = () => Factory(); _factory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)f);`（`using Il2CppInterop.Runtime;`）。
  4. `((Directory<GameItem>)(object)dir).Add(identifier, _factory)`。
- **关键警告**：`DirectoryMaster.Item` 创建出带 childItems 的物品会打警告并**移除 childItems**（"GameItem 'xxx' created with DirectoryMaster contains child items, this should not happen... Removing all child items"）。所以**工厂只返回空箱子（含窗口/标签/类型/sprite，不带内容）**，内容由读档 DecodeNodes 按 uuid 恢复；开新档时给箱子后再手动 FillWithRandomLockedBoxes 填充（不经过 DirectoryMaster.Item，不受此限制）。

**本项目落地**：
- `CustomStorageContainer.RegisterToDirectory(ItemDirectory dir)`：Has 防重 + ConvertDelegate + Add。
- `Patches.PostfixInitDirectory(ItemDirectory __instance)`：转发注册。
- Core.cs：patch `ContainerItemDirectory` / `AmenitiesItemDirectory` / `ModItemDirectory` 三个目录的 `InitDirectory`。
- 日志确认：`★ 已注册 custom_storage_box 到 XxxItemDirectory（读档原生恢复窗口）`。

### 25.5 可复用结论

1. **双击打开容器**：`GameItem.contentWindow`（0x230）必须非 null，否则 `OpenContentAction` 直接 return。这是"打不开"的唯一判定点。
2. **自定义物品读档恢复**：必须注册进 `DirectoryMaster` 工厂表，游戏才走原生路径重建。不注册 = 读档后窗口丢失。
3. **`DelegateSupport.ConvertDelegate<Il2CppSystem.Func<T>>`** 把 C# 委托转 Il2Cpp 委托（`using Il2CppInterop.Runtime;`），是往游戏泛型方法传委托的唯一可靠方式。
4. **DirectoryMaster 工厂创建不允许带 childItems**（会被移除并警告）——工厂返回空壳，内容靠读档恢复。
5. **开局给物**：NewGameData.HandleInitialItem 是原生开局物品分发中心（拾荒者信物就走这），只在开新档调用；给物状态用进程内静态标志防重即可，不要用 PlayerPrefs 持久化防重（OnNewGame 重置不可靠，残留 true 会误拦）。
6. **延迟+遍历+重建是 hack**（被用户明确否决）；注册工厂表走原生路径才是根治。


---

## 二十六、与"物品添加类"mod 的兼容性（2026-09-03）[L1 实测]

**背景**：用户反馈 Wage's Perks 与 NEI Item Browser、Item Manager - Add or Remove Item To You 等"物品添加/浏览"类 mod 不兼容。

### 26.1 根因：GunPrinterFix 拦截了全局物品创建入口

- `GunPrinterFix` 用**独立 Harmony 实例**（`new HarmonyLib.Harmony("JacksonPerks.GunPrinterFix")`）Patch 了 `DirectoryMaster.Item(string, bool)`——**游戏所有物品创建的唯一入口**。
- 该入口被一切"物品添加类"mod（浏览物品库、给玩家加物品）高度依赖。多 mod 叠加 Patch 同一核心方法 + 独立 Harmony 实例管理，与它们不兼容。
- `EnsureRelatedItems()` 启动时还会调用 `DirectoryMaster.Item` 批量创建 printer_chip 等培芯片半成品 → 物品库（NEI 浏览）出现"培芯片相关物品"。
- 经验文档早有"枪械打印机保留代码但禁用"，但 Core.cs 里 `GunPrinterFix.Register()/EnsureRelatedItems()` 仍无条件调用——**"禁用"只写了注释没落实到代码**。

### 26.2 修复：发布版门控 + 工厂非null降级

**修复1（Core.cs）**：`GunPrinterFix.Register()/EnsureRelatedItems()` 包进 `if (DebugMode) { }`——发布版（DebugMode=false）完全禁用，开发版保留继续调试枪械打印机。

**修复2（CustomStorageContainer）**：`CreateRegisteredContainer()` 工厂被外部工具（NEI/ItemManager）调用时，可能因 UI 上下文未就绪使 `CreateContainer()` 返回 null → 外部工具拿 null 崩溃。降级：失败时返回原生 `storage_bay` 保证调用方永远拿不到 null。

### 26.3 全局 Patch 兼容性审计结论（本项目）

| 补丁 | 风险 | 说明 |
|---|---|---|
| GunPrinterFix.ItemPrefix（DirectoryMaster.Item） | 🔴 高 | 全局物品创建入口，发布版已门控禁用 |
| 本地化 Postfix（GetLocalizedItem/Name/UI/GenerateLocalizedString） | 🟢 低 | 白名单 + 只在结果空/"?"时覆盖，不误伤他mod物品 |
| PrefixLoadFromAtlas（RenderHandler） | 🟢 低 | 精确匹配自定义 sprite 名，其余放行 |
| 价格 Patch（GetNegociatedValue/GetCurrentValue/GetValue） | 🟢 低 | 特性 IsActive 门控 + 交易模式判断 |
| GetDisplayName Postfix | 🟢 低 | 白名单 ID 精确匹配 |
| 3× ItemDirectory.InitDirectory 注册箱子 | 🟢 低 | 往目录 Add 自己的物品，与其他 mod 共存正常 |

### 26.4 可复用结论

1. **凡是 Patch 全局物品创建入口（DirectoryMaster.Item）的代码，发布版必须门控**——所有物品类 mod 都依赖它，是最高风险共享点。
2. **"保留代码但禁用"必须落实到 Core 调用处**（if DebugMode 或注释掉调用），只改注释/文档等于没禁用。
3. **Patch 应统一用同一个 Harmony 实例**（ManualPatcher），避免独立 `new Harmony()` 实例分散管理。
4. **外部工具调用你的物品工厂时可能 UI 未就绪**——工厂方法必须保证永不返回 null（失败降级返回原生同类物品）。
5. **做兼容性审计时按"是否作用于所有物品"分级**：白名单精确匹配=低风险；作用于所有物品的全局拦截（尤其创建入口）=高风险必须门控。

### 26.5 发布版刷诊断日志——统一日志入口门控

**背景**：发布版（DebugMode=false）仍持续刷诊断日志（如交易方向 [SellMode]、客户判定 [诊断判定]）。

**根因**：`Core.LogMsg` 定义只是 `Log?.Msg(msg)`，**没有 DebugMode 门控**——发布版里 479 处 `Core.LogMsg` 全部照常输出；且纯诊断 Patch（`StoreClient.IsClientBuyingThisItem` 的 Postfix）在发布版仍注册、每次触发都打日志。

**修复**：
1. `Core.LogMsg` 开头加 `if (!DebugMode) return;`——发布版所有 Core.LogMsg 静默（一处门控管住全部 479 处）
2. 纯诊断 Patch（`IsClientBuyingThisItem`）包进 `if (DebugMode)` 才注册——发布版完全不挂这个补丁（不只是日志静默，是连 Patch 都不注册）

**验证**：反编译发布版确认 `DebugMode=false`、`LogMsg` 被 `if(DebugMode)` 包裹、诊断 Patch 在 `if(DebugMode)` 内。

**可复用结论**：
1. **日志必须统一走一个入口**，并在该入口做 DebugMode 门控——一劳永逸，不要在几百个调用点逐个加判断。
2. **纯诊断 Patch 发布版不注册**（用 `if(DebugMode)` 包住 TryPatch），比"日志静默"更彻底（省去每帧/每次触发的调用开销）。
3. 发布版保留的少量直接 `Log.Msg`（仅初始化/异常，约 18 处）低频不刷屏，可保留用于玩家反馈问题时的初步定位。

### 26.6 诊断统一管理——Diagnostics 总入口 + Diagnostics/ 文件夹

**背景**：诊断/测试/调试代码曾分散在 9 个文件（约 150KB），散落注册在 Core.cs 的 5 个 `if(DebugMode)` 块 + OnUpdate/OnGUI/ApplyAllPatches 里，发布版打包要逐个找、逐个删，容易乱。

**方案**（v1.0.8 落地）：
1. **物理归类**：诊断文件集中到 `Mods/JacksonPerks/Diagnostics/` 子文件夹（SDK 通配自动包含，无需改 csproj）
2. **总入口 `Diagnostics.cs`**：`Register()` / `OnUpdate()` / `OnGUI()` / `ApplyPatches()` 四个静态方法，统一注册/分发所有诊断工具，内部全 try-catch
3. **Core.cs 收敛**：OnInitializeMelon 里 5 个分散 Init 块 → 一行 `Diagnostics.Register()`；OnUpdate/OnGUI 各一行；诊断 Patch（IsClientBuyingThisItem）移入 `Diagnostics.ApplyPatches()`
4. **门控统一**：Diagnostics 四个方法开头都 `if (!Core.DebugMode) return;`——发布版（DebugMode=false）整个诊断体系 no-op，一个开关管住全部

**诊断新增/删除流程（写进文档供以后照做）**：
- **新增诊断**：1) 在 Diagnostics/ 文件夹写类  2) 在 Diagnostics.cs 的 Register/OnUpdate/OnGUI 对应位置加一行
- **删除诊断**：从 Diagnostics.cs 删对应行 → 删文件 → 编译（若有引用编译报错会立刻暴露）

**本次清理删除的无用诊断**（均无外部有效引用）：
| 文件 | 原因 |
|------|------|
| AutoLoadSave.cs | 自动进存档已放弃；TestRunner 对它的依赖（等 IsDone）已改为纯等待 |
| TestHelper.cs | F6-F9 测试快捷键，临时禁用且无 public static 方法 |
| MainMenuButtonDumper.cs | 一次性主菜单按钮 dump，使命完成 |
| ShapeSizeDumper.cs | 一次性形状尺寸 dump，使命完成 |

**保留并纳入统一管理的诊断**：DebugOverlay（F10 覆盖层）、TestRunner（-runtests 自动化测试）、RuntimeInspector（F4 检视器）、ContainerDiagnostics（容器结构 dump）、GunPrinterDiagnostics（F9 枪械打印机 dump）。

**验证**：反编译发布版确认 `DebugMode=false`，Diagnostics 四个方法全被 `if(Core.DebugMode)` 包裹，发布版不注册任何诊断工具/Patch。

**可复用结论**：
1. **诊断类物理集中 + 逻辑总入口**，新增/删除都在一处，不再满项目找。
2. **发布版打包 = 只切一个 `DebugMode=false`**，诊断体系自动整体下线，不用逐个注释。
3. **删除诊断前先 grep 引用**（AutoLoadSave 曾被 TestRunner 引用，直接删会编译错）；删依赖前先解依赖。
4. SDK-style csproj 通配包含子目录 .cs，物理归档无需改工程文件。

### 26.7 经验反哺代码——精炼/优雅/通用化（v1.0.8 落地）

**原则**：拆包经验不只是"记下来"，要回写进代码让实现更精炼优雅通用。本次落地的两处：

**26.7.1 ManualPatcher 通用化（对照 AugPresenceGuard 27.4/27.6 防御性 Patch）**
- 删除 `TryPatchAllOverloads` 里的空 if/else 死代码（`if(count>0){}else{}`）
- 成功挂载打确认日志 `[Patch] X.Y 已挂载` / `已挂载 N 个重载`——开发版排查"哪个补丁没挂上"一清二楚（发布版 Core.LogMsg 静默不受影响）
- 新增 `patchHost` 参数（默认 `typeof(Patches)`）——**不再硬编码 Patch 宿主类**，任何类都能当 Patch 宿主（Diagnostics 等诊断类可直接挂），一行改动零破坏（调用点全用默认值）

**26.7.2 特性激活统一入口 `Core.PerkActive(id)`**
- 问题：12 个 Perk 各写一行 `IsActive() => StartingPerk.IsPerkActive(PerkId)`，完全重复，且无 try-catch（游戏更新签名变则崩）
- 方案：`Core.PerkActive(string)` 带 try-catch（异常返回 false），12 个 Perk 的 IsActive 统一 `return Core.PerkActive(PerkId);`
- 收益：一处改逻辑全生效 + 防崩溃 + 后续新特性直接调用

**可复用结论**：
1. **跨文件批量替换先想"会不会自伤"**——正则 `IsPerkActive( → PerkActive(` 把 Core.PerkActive 自己的实现也替换了，导致无限递归（`Core.PerkActive → Core.PerkActive`）。批量替换后必须 grep 自引用检查。
2. **通用化的正确姿势**：加"带默认值的参数"（patchHost=null）优于"新建方法"——调用点零改动，能力增强。
3. **统一入口放 Core（静态单例宿主）**：所有跨文件共享的小工具（特性激活、日志、随机数）都收敛到 Core，新代码直接 `Core.Xxx` 不再各自造轮子。
---

## 二十七、Aug 改造人系统 + 数值钳制模式（AugPresenceGuard 参考，2026-09-03）

**AugPresenceGuard（v1.0.0，6.6KB）** 是个极小的数值守护 mod：把 `PlayerStore.augPresence`（改造人存在度）钳制在合法范围，防止数值溢出卡流程。它引出了此前 26 章从未覆盖的 **Aug 改造人系统**。

### 27.1 Aug 系统核心字段 [L1]

| 字段 | 类型 | 含义 |
|------|------|------|
| `PlayerStore.augPresence` | int | 改造人存在度（可读写） |
| `PlayerStore.isHardMode` | bool | 是否硬核模式 |
| `PlayerStore.isAugIntroduced` | bool | aug 系统是否已引入 |
| `PlayerStore.isAugEnded` | bool | aug 系统是否已结束 |
| `PlayerStore.IsInstanceExist()` | static bool | 单例是否存活 |

### 27.2 Aug 相关方法签名 [L1]

| 类 | 方法 | 参数 | 说明 |
|----|------|------|------|
| AugHelper | OnGoodKill | 无 | 杀掉 aug 后回调 |
| AugHelper | OnAugLeftAlive | 无 | 放走 aug 后回调 |
| StoreClientManager | HandleAugClient | bool | 检查 aug 访客（带 bool 参数） |

### 27.3 数值钳制模板（多入口守护）

```csharp
// 核心：所有入口统一点到 Clamp，source 进日志
internal static void Clamp(PlayerStore store, string source)
{
    int orig = store.augPresence;
    int num = orig;
    if (store.isHardMode) num = Math.Min(99, num);            // 硬核上限
    if (store.isAugIntroduced && !store.isAugEnded) num = Math.Max(1, num);  // 下限（仅进行中）
    if (num != orig) { store.augPresence = num; Mod.Log.Msg($"Protected after {source}: {orig} -> {num}"); }
}
```

**5 个入口**（全 Postfix/Prefix 一行调用）：`AugHelper.OnGoodKill` / `AugHelper.OnAugLeftAlive` / `PlayerStore.LoadGame` / `PlayerStore.BeginDay` / `StoreClientManager.HandleAugClient(bool)`。

**可复用要点**：
1. 数值守护类 mod 的标准结构：多个入口 → 统一 `Clamp(store, source)` → 变了才写 + 日志定位来源。
2. "系统进行中"判断模式：`isXxxIntroduced && !isXxxEnded` 判断子系统是否激活。
3. 守护任何可能被玩坏的数值（存在度/好感/现金/声望）都可用这个模板。

### 27.4 防御性 Patch 模式（比 [HarmonyPatch] 特性更稳）

```csharp
MethodInfo mi = AccessTools.Method(targetType, targetName, targetArguments, null);
MethodInfo pi = AccessTools.Method(patchType, isPrefix ? "Prefix" : "Postfix", null, null);
if (mi == null || pi == null) { Mod.Log.Warning("Could not patch " + targetType.FullName + "." + targetName); return; }
harmony.Patch(mi, isPrefix ? new HarmonyMethod(pi) : null, isPrefix ? null : new HarmonyMethod(pi), null, null, null);
Mod.Log.Msg("Patched " + targetType.Name + "." + targetName);
```

**优势**：目标方法签名变了 → warning 而非启动崩溃；每个 Patch 成功/失败都有日志。多 Patch mod 强烈推荐，排查挂载失败一清二楚。

### 27.5 可复用结论
- Aug 系统：核心数值 augPresence + isAugIntroduced/isAugEnded 生命周期开关 + AugHelper 两个回调 + StoreClientManager.HandleAugClient(bool)。
- 数值钳制 = 多入口 Clamp + 变了才写 + source 日志，是最安全的"保护数值"手段。
- 防御性 Patch（AccessTools.Method + warning）应作为多 Patch mod 的默认挂载方式。

### 27.6 源码级补充（反编译确认，补齐 27.3/27.4 遗漏细节）

1. **`ClampCurrent(string)` 单例封装**：钳制有两个入口——`Clamp(store, source)` 传实例 + `ClampCurrent(source)` 自动取当前单例（先 `if (PlayerStore.IsInstanceExist())` 再取 `Instance`）。凡"守护当前玩家数值"的场景都应复制这个双入口。
2. **aug 结束后下限不生效（精确语义）**：`if (store.isAugIntroduced && !store.isAugEnded) num = Math.Max(1, num)` —— 只有 aug 系统"引入且未结束"时下限 1 才生效；**aug 结束后存在度可归 0**。之前 27.3 的"仅进行中"不够精确，此处明确。
3. **来源字符串具体值**（日志定位入口用）：`"killing an aug"` / `"letting an aug leave"` / `"loading the save"` / `"beginning the day"` / `"checking aug visitors"`。每个 Patch 一行 `ClampCurrent("来源")`，出问题日志直接看出是哪个入口触发钳制。
4. **一个 Patch 一个独立小类**：GoodKillPatch/AugLeftAlivePatch/LoadGamePatch/BeginDayPatch/HandleAugClientPatch 各一个类、每个只有一个方法，配合 `PatchPrefix/PatchPostfix/Patch` 三个 helper 封装"找方法+warning+try-catch+打日志"。5 个 Patch 在 `Apply()` 里各一行，清晰可维护。
5. **try-catch 兜底**：Patch 查找/挂载整体 try-catch，异常用 `Mod.Log.Error($"Failed to patch {target}.{name}: {ex}")`——最坏也只是 Error 日志，不崩溃。



## 二十八、成瘾警官巡查事件——拆包技术证据（6 任务，2026-09-03）

> 来源：Il2CppDumper dump.cs（签名 [L1]）+ Cpp2IL ISIL 汇编（方法体 [L2]）。
> 数据来源层：dump.cs = 第2层签名；`_cpp2il_isil\IsilDump\Assembly-CSharp\<类>.txt` = 第1层方法体（ISIL）。
> 只列反编译确认事实，证据等级已标注。

### 28.1 麻醉品物品体系（任务1，最高优先——判定总卡点）

**判定机制（[L1 签名 + L2 ISIL 方法体实锤]）**：麻醉品不是独立分类字段，是**标签 + 化学意图枚举**双重判定。

| 判定方法 | 签名 | 机制 | 证据 |
|-|-|-|-|
| `ChemicalFeedbackHelper.IsNarcotic(GameItem)` | `public static bool IsNarcotic(GameItem item)` RVA:0x8810D0 | 调用 GetChemicalIntent 比较是否 == NARCOTIC | [L2] ChemicalFeedbackHelper.txt |
| `ChemicalFeedbackHelper.GetChemicalIntent(GameItem)` | `public static ChemicalIntent GetChemicalIntent(GameItem item)` RVA:0x880620 | 读 `CHEMICAL_INTENT_TAG` 标签值 + 纯度等级修正（GetPurityGradeDifference） | [L2] ChemicalFeedbackHelper.txt |
| `GameItem.IsTag(item, "NARCOTIC")` | 物品类型标签（SetGameItemType 设置） | NARCOTIC 是物品类型 hash→标签映射中的一种 | [L1] TypeHelper.txt（hash 0x3DFBE034→"NARCOTIC"） |
| `ContrabandHelper.IsContraband(GameItem)` | RVA:0x8906D0 | 读 `CONTRABAND`/`CONTRABAND_ITEM_TAG` 标签 | [L1] |

**ChemicalIntent 枚举（dump.cs 行13849）**：NONE=0 / HYDRATION=1 / NUTRITION=2 / **NARCOTIC=3** / MEDICAL=4 / POISON=5 / BIRTH_CONTROL=6 / SYNAPREST=7。

**麻醉品初始化（[L2]）**：`ConsumableHelper.InitNarcotic(GameItem gameItem, int level)`（dump.cs 行15046，RVA:0x88CF80）设置 CHEMICAL_INTENT_TAG=NARCOTIC + 等级；`ConsumableHelper.InitAlcohol(GameItem,int)` 同理。

**麻醉品物品清单（[L2 ISIL 实锤，MedsItemDirectory.txt）——全部同时是违禁品**：

| 工厂方法 | 物品 identifier | InitNarcotic 等级 | InitContrabandItem 等级 | 物品类型 |
|-|-|-|-|-|
| `FentanylPill()` | `oxycodone_pill` | 3 | 3 | MEDICAL+NARCOTIC+SUBSTANCE |
| `DreamDust()` | `dream_dust` | 1 | 2 | MEDICAL+NARCOTIC+SUBSTANCE |
| `Injector(color)` 粉色分支 | `injector_pink` | 1 | - | NARCOTIC+SUBSTANCE |
| `Injector(color)` 纯白分支 | `injector_pure_white_s` | 无（仅类型） | - | NARCOTIC |
| ShipSystemDirectory 化学补给物品 | 含 NARCOTIC+MEDICAL+CHEMICAL_SUPPLIES+LIQUID_CONTAINER_TAG+WATER_PURIFICATION_SUPPLY | - | - | 多标签 |

> 说明：FentanylPill 本地化 key 为 `item_fanta_pill_name/desc/flavor`（模板遗留），sprite 名 `oxycodone_pill`；判定以标签为准，不依赖名字。

**麻醉品物品 = 违禁品（[L2]）**：FentanylPill/DreamDust 都同时调用 `InitContrabandItem(item, level)`——麻醉品必然携带 CONTRABAND 标签。**巡查时直接 `IsNarcotic(item)` 即可判麻醉品，`IsContraband(item)` 判广义违禁品，二者有交集但不等价**（非麻醉违禁品如 cyanide_pill 也带 CONTRABAND）。

### 28.2 违禁品打击事件——完整调用链（任务2）

**原生事件：`OperationListSec.Crackdown()`（dump.cs 行39762，RVA:0x68A650）[L2 ISIL 实锤]**：

| StoreEvent 字段 | 偏移 | 值 | 证据 |
|-|-|-|-|
| `identifier` | 0x18 | `"crackdown"` | [L2] OperationListSec.txt |
| `eventType` | 0x10 | NORMALE(0) | [L2] |
| `eventArea` | 0x14 | ALL(2) | [L2] |
| `duration` | 0x3C | 2（天） | [L2] |
| `cooldown` | 0x38 | 3（天） | [L2] |
| `importance` | 0x80 | 6 | [L2] |
| `isFactionOperation` | 0x92 | true | [L2] |
| `sourceFaction` | 0x98 | SEC（安全部） | [L2] |
| `addClientFromEventActionId` | 0x78 | `"crackdownaddClientAction"` | [L2] |
| `bmPowerMod` | 0xB0 | -40（黑市势力） | [L2] |
| `secPowerMod` | 0xA8 | -20（安全部势力） | [L2] |
| `civilUnrestMod` | 0xA0 | -20 | [L2] |

**事件动作：`crackdownaddClientAction` lambda（StoreEventActionDict_NestedType___c.txt b__3_13）[L2]**：
```
StoreClientList.CreateInspectionClient()          // 创建检查客户
client.eventSourceId = "crackdown"                // [rax+568] = eventSourceId
PlayerStore.Instance.StoreClientManager.AddClient(client)  // 加入客户管理器
```

**事件调度（StoreEventManager，dump.cs 行40142）**：`QueueEvent()` → `HandleLotteryEvent()` → `PickRandomEventByType(EventType)` → `ActivateEvent(StoreEvent,bool,bool)` → 激活时触发 `addClientFromEventActionId`。事件池 = 静态 `normalEventBlueprints/threatEventBlueprints/cosmeticEventBlueprints`（List<StoreEventBlueprint>）。StoreEvent 生命周期：`OnEventActive/OnDayStart/OnDayEnd/OnEventRemove`。

**检查客户处理：`StoreClientManager.HandleInspectionClient()`（[L2 ISIL 实锤]）**：
1. `StoreReputation.IsPerkUnlocked("SEC_VIP")` → true 跳过（安全部 VIP 免查）
2. `PlayerStore.Instance.secData.GetTotalTraffickingCrimes()`（[rax+688]=secData）
3. 走私犯罪 < 50 → 不触发（常量 `INSPECTION_MIN_TRAFFICKING_CRIME = 50`，dump.cs 行39260）
4. `inspectionSeeded`（0x60）为 false 不推进
5. 按犯罪总数分级算 `dayUntilInspection`（0x5C）：>=750→2天、450-750→2天、150-450→3天、<150→4天；某事件 active→+4，另一事件 active→-1（clamp 到 ≥2）
6. `dayUntilInspection` 递减归零 → `StoreClientList.CreateInspectionClient(某参数)` + `TryAddClient` + `ResetInspection`

**检查执行：`ContrabandHelper.StartInspection(int personality, bool fromShowcase)`（RVA:0x8915C0，方法体2721行）[L2]**，内部调用链：
`RecursiveInspect`(×4 递归容器) + `InspectItem`(×2) + `IsConcealedFromInspection`(暗格隐藏) + `AddConcealed`/`CollectConcealed` + `GetHighestContrabandLevel` + `GetFineAmount`。StartInspection 在检查客户对话的 EndAction 触发（StoreClientDialogList_NestedType___c.txt 行872/887）。

**没收/罚款（[L1] 签名）**：`ContrabandHelper.DisposeContraband(List<GameItem>)`（没收）、`GetFineAmount(List<GameItem>)`（罚款）、`GetBribeAmount()`（贿赂，在 StoreClientDialogList_NestedType___c__DisplayClass22_0.txt 贿赂分支调用）、`IsSeriousContraband(GameItem)`、`GetReportReward(List<GameItem>)`（举报奖励）。

### 28.3 SmugglerBay 暗格系统（任务3）

**创建（[L2] ContainerHelper.InitSmugglerBay(GameInventory, GameItem)）**：设置标签 `IMPORTANT_TAG` + `CONTAINER_TAG` + 挂放行委托（Func<GameItem,GameInventory,bool>，Delegate.Combine 到 inventory 字段）。**不设尺寸**——尺寸由外层 CreateInventoryWindow 决定。

**暗格物品 ID（stringliteral.json）**：`smuggler_bay` / `smuggler_bay_mini` / `smuggler_bay_mod` / `smuggler_bay_large`（本地化 key `item_smuggler_bay_*`）。

**尺寸与窗口（[L1] cheatsheet 21.2）**：`DirectoryUtils.CreateInventoryWindow(int width, int height, bool isDraggable=true)` → `ValueTuple<PixelWindow, GameInventory>`；`GameGridInventory.SetShape(int,int)` + `inventoryShape`(0x1B0) + `Validate()`。三档：Mini=3×3/150、SmugglerBay=5×5/250、SmugglerBayMod=8×8/400。

**遍历/移除（[L1] GameInventory 抽象类，dump.cs 行55729）**：
- 遍历暗格内物品：`GameInventory.childItems`（抽象属性，List<GameItem>）
- 移除指定物品：`GameInventory.Expel(GameItem)`（abstract）
- 放入物品：`GameInventory.UncheckedAccept(GameItem)` / `UncheckedAcceptAll(List<GameItem>)`
- 校验：`Validate()` / `TryInventorySlot(GameItem,...)` / `IsInsertLocked()/IsRemoveLocked()/LockInv()/UnlockInv()`

**检查时隐藏（[L2]）**：`ContrabandHelper.IsConcealedFromInspection(GameItem)`（暗格内物品不被检查发现）+ `AddConcealed`/`CollectConcealed` 收集隐藏物。检查结果上报 `OnInspectedEvent`（Event 子类，参数 key：`outcome_type` / `has_smuggler_bay` / `total_lost_item_value` / `total_lost_item_count`）。

### 28.4 客户生成与伪装可行性（任务4）

**检查客户工厂（[L2] StoreClientList.CreateInspectionClient(int personality = 0, bool fromShowcase = False)）**：按 personality 分支设置 displayName/外观/对话；personality==2 时设 `isLazyInspector`(0x218)=1、`clientIntent`(0x244)=4、`dismissable`(0x68)=0。

**客户处理入口（[L1] StoreClientManager，dump.cs 行39213）**：`GenerateClient()/PickClient()/PickRandomClient(string faction)/PickRandomValidClient(string type)/AddClient(StoreClient)/TryAddClient(StoreClient,bool noDuplicate=true)/HandleClientFromEvent()/RemoveDuplicateClientsByIdentifier(string)`。去重/冷却字段：`clientTracker`(0x20)/`permanentClientTracker`(0x28)/`uniqueClientTracker`(0x30)。

**StoreClient 完整字段（[L1] dump.cs 行38782，伪装/身份相关加粗）**：

| 字段 | 类型 | 偏移 | 用途 |
|-|-|-|-|
| **`spriteName`** | string | 0x20 | 外观 sprite（伪装改这里） |
| **`possibleSprites`** | List\<string\> | 0x28 | 多外观候选 |
| **`realName`** | string | 0x1A0 | 真名（区分于 displayName） |
| **`clientFaction`** | string | 0x180 | 阵营（FACTION_SECURITY / FACTION_LOWER_LEVEL 等常量见类头） |
| **`isSecurity`** | bool | 0x198 | 是否安全部 |
| **`isWanted`** | bool | 0x199 | 是否通缉 |
| `displayName` | string | 0x10 | 显示名 |
| `identifier` | string | 0x18 | 唯一 ID |
| `bubbleColor` | string | 0x188 | 对话气泡色 |
| `tag` | List\<string\> | 0x190 | 自定义标签 |
| `arrestable`/`nonDismissableArrestable`/`nonShootable` | bool | 0x69/0x6A/0x6B | 可逮捕/不可解雇/不可射击 |
| `isStealthWanted` | bool | 0x1B0 | 隐形通缉 |
| `reportReward`/`reputationPenalty` | int | 0x1A8/0x1AC | 举报奖励/声望惩罚 |
| `onCaptureAction`/`onReportAction` | Action | 0x1B8/0x1C0 | 捕获/举报回调 |
| `reported` | bool | 0x1D0 | 是否已举报 |
| `eventSourceId` | string | 0x238 | 事件来源标记 |
| `clientIntent` | enum | 0x244 | 客户意图 |
| `isLazyInspector` | bool | 0x218 | 懒惰检查员 |
| `isAug`/`augTier`/`isAugScanned` | bool/int/bool | 0x219/0x21C/0x220 | Aug 改造人 |
| `noAcceptingContraband`/`AcceptingContrabandOverride`/`IsPersonalConsumption`/`IsClientAddict`/`CanExposeChem`/`acceptedChemicalProductGrade` | bool×5+int | 0x200-0x208 | 化学/违禁品接受 |
| `mainDialogue`/`acceptDealDialogue`/`allDoneDialogue`/`interogationDialogue`/`customOnArrestDialogue` | Dialogue | 0x70-0xC8 | 对话槽 |

**伪装可行性结论（[L1]）**：StoreClient 有独立 `spriteName`（外观）+ `clientFaction`（阵营）+ `isSecurity`（安全部标志）+ `realName`（真名）——**外观与身份解耦**，可创建"平民外观 + 安全部职能"客户（isSecurity 用于隐藏的执法行为）。游戏已有伪装概念：`StoreClientManager.AUG_DISGUISE_CHANCE = 80`（常量，dump.cs 行39275）。

### 28.5 游戏现有执法概念完整盘点（任务5）

**游戏有完整"警官 NPC"系统，mod 不应硬造警徽 UI**：

| 系统 | 类/方法 | 证据 |
|-|-|-|
| **案件警官** | `SecData.assignedCaseOfficer`(string,0x30) + `SecData.SendOfficer()` + `SecData.GetCaseOfficer(int totalCrimeValue)` | [L2] SecData.txt |
| **警官分级（GetCaseOfficer 方法体实锤）** | <=100 无警官（sec_data_none）；100-300 Jeff；300-850 Valentine；850-1500 Huang；1500-2500 Page；>=2500 Carlson | [L2] SecData.txt，本地化 key `sec_data_officer_jeff/valentine/huang/page/carlson` |
| **犯罪统计** | `SecData.GetTotalTraffickingCrimes()`/`GetTotalCrimeValue()`/`GetContrabandTotalCrimeValue()`/`GetBiggestCrime()`/`GetEvidenceLevelDisplay()`/`GetProjectedSentence()`/`GetInspectorPersonality()`/`GetBriberyAmount()`/`GetFineValue()` | [L1] dump.cs 行1466 |
| **犯罪提交** | `SecData.CommitCrime(string crimeID, int amount)`/`CommitBribery(int)`/`SellDirtyWater(int)`/`SellBadAmmo(int,bool)`/`CheckSoldContraband(GameItem,StoreClient)`/`CheckSoldChem(GameItem,SoldItem)` | [L1] |
| **检查客户** | `StoreClientList.CreateInspectionClient(int,bool)` + `StoreClientManager.HandleInspectionClient()` | [L2] |
| **违禁品检查** | `ContrabandHelper` 26 方法（IsContraband/StartInspection/RecursiveInspect/DisposeContraband/GetFineAmount/GetBribeAmount/GetReportReward/SecurityCheckCounter/SecurityCheckDisplayCase） | [L1] |
| **通缉系统** | `StoreClientListWanted` + `StoreClient.isWanted/isStealthWanted` + `WantedElement`(MonoBehaviour) + `WantedUIManager`(MonoBehaviour, dump.cs 行49902) + `WantedNotReportedEvent` | [L1] |
| **安全部客户** | `StoreClientListSecurity`（dump.cs 行36293） | [L1] |
| **执法 UI/单据** | `WantedUIManager` + `SecuritySlip`(MonoBehaviour, 行48624) + `NotebookUIManager.contrabandType`(0x50, UI 分类图标字段) | [L1] |
| **被捕记录** | `StoreClientManager.arrestedClients`(List\<string\>, 0x38) | [L1] |
| **安全部事件** | `OperationListSec`：IncreasePatrols/Crackdown/RaidBlackMarket/MartialLaw/BudgetIncrease（全部返回 StoreEvent） | [L1] 行39752 |
| **安全部声望** | `StoreReputation.IsPerkUnlocked("SEC_VIP")` | [L2] |
| **阵营常量** | `StoreClient.FACTION_SECURITY` = "FACTION_SECURITY" | [L1] |

### 28.6 本地化 key 机制（任务6）

**本地化架构（[L1] dump.cs 行452573/452613/452760/452809）**：Unity Localization 系统——`StringTable : DetailedLocalizationTable<StringTableEntry>`（值表）、`SharedTableData : ScriptableObject`（明文 key↔long id 映射，m_KeyDictionary+m_IdDictionary）、`TableEntryData`（Id(long)+Localized+Metadata）、`TableReference`（Guid/Name 引用）。

**读取入口 `LocHelper`（dump.cs 行19110，15 个静态方法）**：按域分 key 前缀——`GetLocalizedItem`(item_)、`GetLocalizedMechanic`(sec_data_officer_ 等)、`GetLocalizedClientName`、`GetLocalizedDialogue`、`GetLocalizedDialoguePhone`、`GetLocalizedNetworkUpgrade`、`GetLocalizedPerkTable`、`GetLocalizedRepPerkTable`、`GetLocalizedUI`、`GetLocalizedName` 等。

**key 命名规则（stringliteral.json + 本地化表实测）**：`item_`（物品名/desc/flavor）、`sec_data_`（警官/犯罪）、`event_op_`（事件）、`sec_`（安全部）等前缀 + 语义名。

**mod 新增 key 的已知路径（[L1] cheatsheet 5.5 + 参考 mod）**：`WagesPerks` 用 `HarmonyPatch(typeof(LocHelper), "GetLocalizedPerkTable")` Prefix 拦截自定义 key，返回自定义字符串并 `return false`（不走原版表）。**对话文本在代码里硬编码英文、不走本地化表**（cheatsheet 行297），需汉化要 Patch 或改源码。

### 28.7 对"成瘾警官巡查"mod 的拆包结论（决策依据）

- **判定麻醉品**：`ChemicalFeedbackHelper.IsNarcotic(GameItem)` 是唯一权威入口（[L2]），不需要自己实现标签逻辑；麻醉品=违禁品子集。
- **"成瘾警官"做成"检查客户 + 事件"组合有原生骨架**：检查客户（CreateInspectionClient/HandleInspectionClient）+ crackdown 事件（crackdownaddClientAction）都是现成机制；原生警官分级（GetCaseOfficer 5 级）可作为"成瘾"升级参照。
- **伪装已可行**：StoreClient.spriteName/clientFaction/isSecurity 解耦，可做"平民外观+安全部职能"；游戏自带 AUG_DISGUISE_CHANCE=80 伪装先例。
- **暗格隐藏已存在**：IsConcealedFromInspection 天然让暗格内物品逃过检查——巡查"查暗格"是新增行为（原生只查显式违禁品）。


### 28.8 暗格"躲检查"机制实锤 + 穿透暗格的拆包依据（2026-09-03 补充）

**暗格完整创建规格（[L2] ShipSystemDirectory.SmugglerBay() 方法体实锤）**：

| 步骤 | 调用 | 参数/值 | 证据 |
|-|-|-|-|
| 1 | `DirectoryUtils.CreateInventoryWindow` | **5×5**、isDraggable=true | [L2] ShipSystemDirectory.txt 行~3160 |
| 2 | `GameItem.SetContentWindow(inventory)` | 挂内容窗口 | [L2] |
| 3 | `GameItem.SetSpriteAndShape` | `"Items/items_ship"` + `"smuggler_bay"` | [L2] |
| 4 | `identifier` | `"smuggler_bay"`（[rbp+288]） | [L2] |
| 5 | `unitValue` | 250（[rsi+512]） | [L2] |
| 6 | `ContainerHelper.InitSmugglerBay(inv,item)` | 设 IMPORTANT_TAG+CONTAINER_TAG+放行委托 | [L2] |
| 7 | `EnableTag` | **`CONTAINER_TAG` + `ITEM_HIDDEN_TAG`** | [L2] 行~3230 |
| 8 | `SetGameItemType` | `"STORAGE"` | [L2] |
| 9 | 挂回调 | Action<GameItem,GameInventory,SlotMarker>（放行/处理） | [L2] |

**暗格变体**：`SmugglerBay()` / `SmugglerBayMod()`（identifier smuggle_bay_mod，unitValue=400）/ `MiniSmugglerBay()`（smuggler_bay_mini）/ `StorageBayLarge()`（smuggler_bay_large，7×7 档）。三档尺寸：Mini=3×3、SmugglerBay=5×5、SmugglerBayMod=8×8。

**躲检查机制（[L2] ContrabandHelper 实锤，这是规格"穿透暗格"的根因）**：
- `IsConcealedFromInspection(GameItem)`：`IsFlaggableItem` 通过后，凡带 `LOCKED_TAG` / `ITEM_HIDDEN_TAG` / `ITEM_BEHIND_LOCK_TAG` 三者任一 → 返回 true（隐藏）
- `RecursiveInspect(item, contraband, concealed)`：对带 **`ITEM_HIDDEN_TAG`** 的容器 → `CollectConcealed(node, concealed)` **把整个容器的子物品全部记为隐藏** + **直接 return 不进容器内部** → 暗格内的违禁品**不会被 InspectItem 检查、不会被没收**
- 结论：**原版巡查（StartInspection→RecursiveInspect）对暗格一件都收不到** —— 规格要求"没收暗格2件"必须专门写穿透逻辑，不能复用 StartInspection

**mod 识别 smuggler_bay 容器的三种依据（[L1/L2]）**：按 identifier 前缀 `smuggler_bay`（smuggler_bay/smuggler_bay_mod/smuggler_bay_mini/smuggler_bay_large），或 `IsTag("ITEM_HIDDEN_TAG") && IsTag("CONTAINER_TAG")`，或 `GetGameItemType()=="STORAGE"`。

**穿透暗格的遍历/移除 API（[L1] GameInventory 抽象类，dump.cs 行55729）**：`childItems`（List<GameItem>）遍历 + `Expel(GameItem)` 移除 + `UncheckedAccept` 放入。按 `ContrabandHelper.GetContrabandLevel(GameItem)`（RVA:0x88F2E0 附近，或 `GetContrabandLevelInInt`）取违禁等级排序，优先移高等级。

### 28.9 麻醉品物品清单（最终修正版，2026-09-03）——injector 系列不能全算

**已实锤麻醉品（[L2] MedsItemDirectory，判据=InitNarcotic 调用或 NARCOTIC 类型）**：

| 物品 identifier | 工厂 | InitNarcotic | InitContrabandItem | 类型 | 价格 |
|-|-|-|-|-|-|
| `oxycodone_pill` | FentanylPill() | 3 | 3 | MEDICAL+NARCOTIC+SUBSTANCE | 12 |
| `dream_dust` | DreamDust() | 1 | 2 | MEDICAL+NARCOTIC+SUBSTANCE | 18 |
| `injector_pink` | Injector(粉色) | 1 | - | NARCOTIC+SUBSTANCE | 50 |
| `injector_pure_white_s` | Injector(纯白) | 无（仅 NARCOTIC 类型） | - | NARCOTIC | 30 |

**injector_* 全系列对照（[L2] 逐分支实锤）——大部分不是麻醉品**：

| injector identifier | 功能 | NARCOTIC? |
|-|-|-|
| `injector_white` | white_stim_heal 刺激治疗 | 否 |
| `injector_red` | red_stim_heal+red_stim_bleed | 否 |
| `injector_green` | green_stim_heal+antitoxin 解毒 | 否 |
| `injector_turquoise` | combat_stim_acc_buff+cc_buff | 否 |
| `injector_purple` | 多刺激治疗组合 | 否 |
| `injector_blue` | blue_stim_heal+vision | 否 |
| `injector_blood_red` | blood_red_max_health+heal | 否 |
| `injector_black` | Synaprest 注射器（SYNAPREST 意图，独立于 NARCOTIC） | 否 |
| `injector_pink_alt` | MEDICAL 类型 | 否 |
| `injector_purple_large` | InsInjectorHelper.Init 免疫注射器 | 否 |
| `injector_pure_white_s` | NARCOTIC 类型 | **是** |
| `injector_pink` | NARCOTIC+SUBSTANCE+InitNarcotic(1) | **是** |

**给规格开工确认的答复**：
1. **麻醉品清单不建议"全算 injector_*"**——12 个 injector 里只有 2 个（pink/pure_white_s）是麻醉品，其余是战斗刺激剂/治疗剂/免疫剂/合成剂；误算会导致巡查没收正常医疗品。权威判据始终是 `ChemicalFeedbackHelper.IsNarcotic(item)`（读 CHEMICAL_INTENT_TAG），名字含 injector 不代表麻醉品。
2. **穿透暗格冲突面**：覆盖的是 `ITEM_HIDDEN_TAG`（SmugglerBay/隐藏袋/锁柜类容器共用的隐藏机制）。原版只有检查客户会触发检查，Mod 端需确认 WagesPerks/XIAOWO 等 mod 未 patch RecursiveInspect/StartInspection（拆包未见任何 mod 触碰 ContrabandHelper）。


## 第29章 拾荒系统/打烊外出/探寻器/稀有物品 —— 拆包技术证据（2026-09-03）

数据来源：ISIL 方法体（`_cpp2il_isil\IsilDump\Assembly-CSharp\`）= [L2]；dump.cs 签名 = [L1]；
mod 源码（`D:\谷歌\_wages_v107\_src\JacksonPerks\`）= [L3]。

### 29.1 打烊外出玩法 —— 存在，完整链路 [L2]

**核心结论：游戏有完整的"打烊后外出拾荒"玩法，探寻器 = 原版 metal_scanner（金属探测器）。**

**外出全链路（[L2] MapUIManager + ScavHelper 方法体实锤）**：

| 步骤 | 方法 | 调用 | 证据 |
|-|-|-|-|
| 1 | 外出确认UI | `MapUIManager.OpenGoOutsideConfirm()` / `OnGoOutsideConfirm()` | [L2] MapUIManager |
| 2 | 确认后进入afterhours响应 | `StoreResponse.ShowAfterhourResponse(rcx=0)` | [L2] MapUIManager 行797 |
| 3 | 拾荒按钮 | `MapUIManager.ScavengeButtonClick()` → `ScavHelper.ScavengeDumpingGrounds()` → `MapUIManager.UpdateScavUI()` | [L2] 行3103 |
| 4 | 返回 | `TutorialUIManager.OnReturnFromAfterhours` | [L2] |

**地点**：
- `ExpeditionLocationList.DumpingGrounds()` —— 垃圾场（拾荒唯一地点）[L2]
- `ExpeditionPoiList`：JunkPile() / FreshScrapPile() / ScrapPile() / CreateExpeditionPoi(id) [L2]
- 独立远征系统（雇佣兵带出）：`Expedition` 类（.ctor(Hireling, ExpeditionLocation)）、`ExpeditionData.UnlockExpedition`、`ExpeditionLocationList`（含 Rare Components 奖励）、`ExpeditionHelper.GetExpeditionBackpack(hirelingId)`、`EquipmentDirectory.ExpeditionBox()` [L2]

**"本日已外出次数"状态字段（PlayerStore）**：
- `[0x536]` 今日已拾荒次数（`CanScavenge` 判定用，`ResetScavenging()` 每日归零）[L2]
- `[0x540]` / `[0x544]` 受伤恢复计数（每次拾荒 -1，归零后才再次判定伤害）[L2]
- `GetMaxScavAttempts()`：基础 5 次；`StartingPerk.IsProficientScavenger()` → 7 次；另有一特性按周递减（`(day-1)/7`），<3 则不可拾荒 [L2]
- `CanScavenge()`：检查 `store[0x282]` + 剩余次数 `(max - 0x536) > 0` [L2]
- `GetScavTimeLeft()`：剩余可拾荒时间 [L2]

**ScavengeDumpingGrounds() 完整逻辑（[L2] 拾荒核心）**：
```
if !CanScavenge(): return
if 大伤恢复已清零 → 判定小伤 ReceiveMinorWound()（概率基于 0x536 累计；持有"flashlight"手电筒 → 概率降低）
if 拾荒次数已清零 → 判定大伤 ReceiveMajorWound()
否则拾荒成功：
  StoreUIManager.Notify("mech_scav_success")
  items = GetRandomScavengedItem()   // 随机物品列表
  逐个放入 EmporiumEntry[0x328]（afterhourInventory，与博士夜晚商店同一容器）
收尾：0x540/0x544 递减、0x536 递增
```
**挂钩点推荐（结算/修改点）**：`ScavengeDumpingGrounds()`（每次拾荒入口+结算）、`UpdateScavUI()`（拾荒后刷新）、`ResetScavenging()`（每日重置）、`GetRandomScavengedItem()`（物品生成）。

### 29.2 探寻器 = 原版 metal_scanner（被动持有，无需右键）[L2]

**MetalScanner() 物品规格（[L2] AmenitiesItemDirectory 行4846-5027）**：
- identifier=`metal_scanner`；sprite `"Items/items_amenities"`（1×1 小工具）
- `SetGameItemType("TOOL")` —— 工具类型
- `SetValue(item, 250)`；`EnableTag("IMPORTANT_TAG")`
- **`ScavHelper.InitScavScanner(item)`** —— 挂 SCAV_SCANNER_DOUBLE_CHANCE 标签回调
- `AudioHelper.InitSmallMachine` 音效

**使用方式 = 被动持有**：拾荒时 `GetRandomScavengedItem()` 检查 `IsAfterhourHaveOwnedItems("metal_scanner")`，拥有则按 SCAV_SCANNER_DOUBLE_CHANCE 回调概率**再得 1 件**垃圾场物品（数量双倍，非价值翻倍）。无右键交互。

**InitScavScanner(GameItem)（[L2] ScavHelper 行2320）**：`ModifyTag("SCAV_SCANNER_DOUBLE_CHANCE", 回调)`——chance 存 tag[48]。

**UpgradeScavScanner(item, upgradeItem) / CanUpgradeScavScanner(item)（[L2]）**：检查 item identifier=="metal_scanner"；tag[48]（chance）<100 时可升级：**chance += 5（封顶100）、unitValue += 15**，`ModifyTag` 更新回调。

**其他"可交互/使用"先例**：`PortableWaterPurifier` 系（`OnPortableWaterPurifierUsed` 挂回调）[L2] —— 游戏"使用物品"= 挂回调 + 类型标记，无统一"右键使用"入口。

### 29.3 拾荒物品生成与价值（任务3核心）[L2]

**GetRandomScavengedItem() 完整逻辑（[L2] ScavHelper 行1213-1545）**：
```
必得：ItemSpawner.SpawnFromTableGroup("dumpingGroundTG")   // 垃圾场战利品表随机1件
3%：未拥有 satchel → 掉落 satchel（挎包）
1%：CreateUnownedDegradedCassette（旧磁带）
1%：mouse_trap（捕鼠夹）
有 screwdriver +10% → nuts_metal
有 wire_cutter +10% → wire
有 welder +10% → scrap_metal
有 metal_scanner → 按 SCAV_SCANNER_DOUBLE_CHANCE 概率再得1件 dumpingGroundTG
```
- `dumpingGroundTG` 战利品表定义在 **ExpeditionPoiList**（垃圾场POI）[L2]
- **价值修改 API**：`GameItem.SetValue(object value, int index)` [L1]；`GetValue()` / `GetCurrentValue(bool,bool,bool,bool,long)` / `GetNegociatedValue()` 已被 WagesPerks Prefix/Postfix 拦截（加价）[L3]
- **"隐藏价值"与 ITEM_HIDDEN_TAG 不是一回事**：ITEM_HIDDEN_TAG 是容器/物品的检查隐藏标签（见 28.8），与"价值翻倍/隐藏价值"无关 [L2]
- 游戏本体**无"价值翻倍"机制**，metal_scanner 是数量双倍；"1.5~3倍价值"需自行 `SetValue` 修改或复用 dumpingGroundTG 表生成后改值

### 29.4 便携容器（任务4）[L2]

| 容器 | 尺寸 | 价值 | 特征 | 工厂 |
|-|-|-|-|-|
| `satchel`（挎包） | **6×3=18格** | 150 | EquippableBack 可装备背包、InitBackpackItem | ContainerItemDirectory.Satchel() |
| `smuggler_bay` | 5×5=25格 | 250 | ITEM_HIDDEN_TAG+CONTAINER_TAG+STORAGE | ShipSystemDirectory.SmugglerBay() |
| `smuggler_bay_mini` | 3×3=9格 | - | 同上 | MiniSmugglerBay() |
| `smuggler_bay_mod` | 8×8=64格 | 400 | 同上 | SmugglerBayMod() |
| `expedition_box` | 8×12 | - | 远征战利品箱 | EquipmentDirectory.ExpeditionBox() |

- **`CreateInventoryWindow(w,h,isDraggable)` 可定义任意尺寸**（satchel 6×3、smuggler_bay 5×5 都经它创建）[L2]
- 便携容器（放背包可打开）关键调用：`DirectoryUtils.CreateInventoryWindow(w,h,true)` → `GameItem.SetContentWindow(inv)` → `ContainerHelper.InitBackpackItem`（或 InitBackpack）→ `EquippableBack` → SetSpriteAndShape → EnableTag("backpack") [L2]
- 游戏无现成 5 格容器，但任意尺寸可自定义

### 29.5 稀有物品系统（任务5）[L2]

- **DropTable 系统**：`DropTableDirectory.DropTableMedicalRare(DropTableSize)` / `DropTableValuable(DropTableSize)` —— 稀有掉落表（按 DropTableSize 生成战利品池）[L2]
- 稀有材料：`MaterialDirectory.RareMetalOre()`（`rare_ore`）/ `RareElectronic()`（`rare_electronic`）[L2]
- 稀有商人：`StoreClientList.CreateLowerLevelRareMerchant()`（clientFaction@0x180、clientId@0x244=2、三组买卖列表）[L2]；`rareLowerLevelChemist`（StoreClientListDict）[L1]
- "RARE" 标签：TypeHelper 行2046 [L1]
- 远征奖励含 "Rare Components"（ExpeditionLocationList）[L2]

### 29.6 "捡漏直觉"特性现状 —— 重做前必查（[L3] WagesPerks v1.0.7 mod 源码）

**特性本体（LuckScoutPerk.cs）**：
- `PerkId = "捡漏直觉"`（中文ID）、`DisplayName="捡漏直觉"`、Cost=2、Type=0
- `LUCK_CHANCE = 0.05f`（5%）
- `TryTriggerLuck(out string reward)`：`Core.Rng.NextDouble() > 0.05` 判定；8 个随机提示（现金/稀有模组/旧彩票/古代遗物/稀有矿石/藏宝图/高级药品/好枪）

**注册（CustomStartingPerks.cs）**：
- `CustomStartingPerks.All[5]`（12 个自定义特性第 6 个）
- `EnsureRegistered()` → `StartingPerkList.Perks.Add(GetOrCreate(custom))`；由 `PostfixInitStartingPerks`（Patch StartingPerkList.InitStartingPerk）触发

**生效 Patch（Core.cs ApplyAllPatches）—— 捡漏直觉只有 1 个**：
- `ManualPatcher.TryPatch(typeof(PlayerStore), "BuyItem", null, "PostfixPlayerStoreBuyItem", new Type[1]{typeof(GameItem)})`（Core.cs 行282）
- PostfixPlayerStoreBuyItem：`IsActive() && IsJunk(item)` → TryTriggerLuck → 直接加现金 50-200

**死代码（不生效，可忽略/顺手删）**：`TraitLuckScoutPatch.cs`（internal static class，无任何注册点引用）——参数 (GameItem, int quantity) 的旧版 Patch（50%现金/50%稀有物品 AddDirectSellingItemToTable），v1.0.7 未挂载。

**老实现拆除清单**：
1. 删 `LuckScoutPerk.cs` 特性类
2. Core.cs 删 `ManualPatcher.TryPatch(typeof(PlayerStore), "BuyItem", null, "PostfixPlayerStoreBuyItem", ...)` 注册行 + 删 `PostfixPlayerStoreBuyItem` 方法体
3. CustomStartingPerks.cs 删 `new LuckScoutPerk()` 项
4. 删图标 `JacksonPerks.Icons.06_捡漏直觉.png`（PerkIconLoader 加载）
5. 本地化 PostfixGetLocalizedPerkTable 分支清理（perk_ 前缀）
6. 可选：删死代码 TraitLuckScoutPatch.cs
7. 注意：Core.ApplyAllPatches 里 `GetCurrentValue/GetValue/GetNegociatedValue` 的加价 Patch 是别的特性共用，**勿误删**（需逐条核对归属）

### 29.7 本地化 key 新增（任务7）[L1]

- Unity Localization：StringTable : DetailedLocalizationTable<StringTableEntry>、SharedTableData（key↔long id）
- `LocHelper` 15 个静态入口按域分前缀：GetLocalizedItem(item_)、GetLocalizedMechanic(mech_)、GetLocalizedPerkTable(perk_)、GetLocalizedClientName 等 [L1]
- 拾荒相关现成 key：`mech_scav_success`（StoreUIManager.Notify 用，GetLocalizedMechanic）[L2]；`item_metal_scanner_name/desc/flavor` [L2]
- mod 新增 key 先例：WagesPerks 用 `HarmonyPatch LocHelper.GetLocalizedPerkTable` Prefix 拦截 return false，返回自定义 StringTable 条目 [L3]
---

## 28. 成瘾警官巡查事件（AddictOfficerEvent）——通用世界事件 [L1/L2]

### 28.1 机制（用户拍板口径）
- **归属**：Wage's Perks 本体通用事件模块，总是发生，不依赖任何特性
- **周期**：首次第 3~5 天自动来，之后每 2~4 天随机一次（Core.Rng）
- **双分支**：伪装成瘾警官进店 → 店铺当天有麻醉品库存（IsNarcotic）→ 正常买不记仇；无 → 记仇 flag=true
- **次日巡查**：记仇 flag=true → 全店总共没收 2 件违禁品（全局 contrabandLevel 降序取前2，不足2件收多少算多少）→ 清 flag
- **没收口径（用户拍板）**：全店总共 2 件，不是每个容器 2 件——避免惩罚玩家买多个暗格
- **防BUG**：同一天不重复触发；巡查日不刷新伪装进店（防连锁）；空暗格只提示不扣
- **持久化**：PerkStatePersistence（key 带 runID）
- **调试键**：F6 伪装进店 / Shift+F6 强制记仇 / F7 强制巡查 / F8 状态（Diagnostics 门控，发布版不响应）

### 28.2 关键实现（复用现有基础设施）
```
挂载点：ModHook.OnShutterOpenedEarly（每天开门）→ OnNewDay()
判定点：StoreUIManager.OnNextClientArrived Postfix（PostfixSpecialNpcStartDialogue）→ OnClientArrived
没收：EmporiumEntry.Instance.GetAllItems() 遍历 → 暗格容器 GetInnerInventory() → 收集违禁品
文案：LangHelper.T(中,英)；巡查文案 → Core.LastNightReportLine（晨报显示）
```

### 28.3 坑1：海报/隐藏区（玩家藏东西的位置）[L2 ISIL 实锤]
- 玩家"放在海报后边"的物品存在 **`EmporiumEntry.Instance.hiddenElement`**（GameGridInventory，隐藏区）
- 原生巡查（ContrabandHelper ISIL）在 **StorePoster 激活时**（`[rax+56]` 非0）从 EmporiumEntry 偏移 0xC0 取隐藏区，逐件 `InspectItem`
- **教训**：没收/检查违禁品必须同时覆盖 ①hiddenElement（海报隐藏区）②smuggler_bay 暗格容器内部——只扫 smuggler_bay 会漏掉海报后的违禁品
- 识别暗格容器：identifier 前缀 `smuggler_bay` 或 `IsTag("ITEM_HIDDEN_TAG") && IsTag("CONTAINER_TAG")`

### 28.4 坑2：CreateInspectionClient 伪装客户会触发原生检查 [L1 实测 + L2 ISIL]
- `StoreClientList.CreateInspectionClient()` 是**检查客户**，`clientIntent=4(INSPECTION)`
- **`isSecurity` 只是外观字段（偏移 0x198）**，强制 `isSecurity=false` + 平民外观**挡不住检查行为**——检查由 clientIntent/内部行为触发，实测进店就触发治安检查
- **伪装客户要用普通买家工厂**：`CreateFlexiBuyer()`（clientIntent=1 BUY，无检查行为）
- StoreClient 关键字段偏移：`isSecurity`=0x198、`clientIntent`=0x244、`dismissable`=0x68、`eventSourceId`=0x238、`isWanted`=0x199
- ClientIntent 枚举：UNDEFINED=0/BUY=1/SELL=2/SELLNBUY=3/**INSPECTION=4**/DIALOGUE=5/BARTER=6/...
- 常用客户工厂 intent（ISIL 确认）：CreatePoisonBuyer=1(BUY)、CreateFlexiBuyer=1(BUY)、CreateShadyMerchant=1、CreateMerchant=1、CreateLowerLevelChemist=3(SELLNBUY)、CreateScavGeneral=2(SELL)、CreateShadyPharmacist=2(SELL)

### 28.5 容器内部库存反查（已验证路径）
- 容器 GameItem → `contentWindow`（PixelWindow）→ 反射 `GetProperty("inventory")` → `GameInventory`（CustomStorageContainer 验证）
- `GameInventory.childItems`（Il2CppSystem.List<GameItem>）用 `for i < Count` 索引遍历
- 移除：`inventory.Expel(item)`
- 违禁等级：`ContrabandHelper.GetContrabandLevel(GameItem)` 返回 int

### 28.6 麻醉品判定链（全部确认）
- `ChemicalFeedbackHelper.IsNarcotic(GameItem)`；`ChemicalIntent.NARCOTIC=3`
- 4 个白名单（MedsItemDirectory ISIL 实锤）：oxycodone_pill / dream_dust / injector_pink / injector_pure_white_s
- 创建时 `ConsumableHelper.InitNarcotic(GameItem,int)` 设标签；同时 `ContrabandHelper.InitContrabandItem(GameItem,int)` 设违禁等级
- 店铺库存遍历：`EmporiumEntry.Instance.GetAllItems()`

### 28.7 可复用结论
1. **伪装 ≠ 改外观**：`isSecurity`/`spriteName`/`clientFaction` 是表现层字段，不控制行为；行为由 clientIntent 和内部回调决定——伪装身份必须用行为匹配的客户工厂
2. **没收范围要覆盖全部隐藏位**：海报隐藏区（hiddenElement）+ 暗格容器内部，缺一漏一
3. **容器内部库存反查**统一用 contentWindow.inventory 反射，一处封装全局复用
4. **事件状态统一 PerkStatePersistence**：防重复（KEY_LAST）、下次触发日（KEY_NEXT）、flag（KEY_GRUDGE），读档全恢复


### 29.8 补充拆包：探测器注册/默认概率/打烊枚举/掉落池（2026-09-04）

**① metal_scanner 注册机制 [L2]**：
- identifier 确认 = `"metal_scanner"`（`AmenitiesItemDirectory.MetalScanner()` 行4846：SetSpriteAndShape("Items/items_amenities","metal_scanner")）
- 注册走 `AmenitiesItemDirectory.InitDirectory()`：循环 `new 物品类() → 基类 InitDirectory(this) → this.Add(item, key)`（虚调用 [r10+198h]），每种物品一个嵌套类/工厂
- `DirectoryMaster.Item(identifier, isOwned=true)`（行2091）可从字典创建；`DirectoryMaster.Has(identifier)` 可判存在
- 结论：`DirectoryMaster.Item("metal_scanner")` 能创建成功 [L2]

**② SCAV_SCANNER_DOUBLE_CHANCE 默认概率 = 15% [L2]**：
- 回调 `ScavHelper_NestedType___c.<InitScavScanner>b__21_0(TagState state)`（方法体实锤）：
  ```
  b__21_0(state):
      if state == null: return
      tag = state.Enable()
      if tag == null: return
      tag.SetInt(15)      // 默认概率 = 15
  ```
- 即原版金属探测器默认**15%**双倍获得；`UpgradeScavScanner` 每次 **+5**（封顶 100）[L2]
- 用户 mod 设 35% 不会与原版冲突——**除非 mod 自己再 ModifyTag 覆盖同一标签**；原版创建时 SetInt(15) 是一次性写入

**③ 打烊场景玩家物品枚举入口 = EmporiumEntry.GetAllAfterhourOwnedItems() [L2]**：
- `IsAfterhourHaveOwnedItems(identifier)`（EmporiumEntry 行29778）＝ 调 `GetAllAfterhourOwnedItems()` → 遍历列表 → 比对 `item.identifier == 传入`
- `GetAllAfterhourOwnedItems()`（行29330）：遍历 `this[0x158]`（EmporiumEntry 持有的容器）的**子物品**（GetChildItems 枚举）→ 逐项判定 → 加入 List<GameItem>
- 结论：**打烊时玩家物品枚举入口是 `EmporiumEntry.GetAllAfterhourOwnedItems()`**，原版 ScavHelper 找 metal_scanner 正是走它。mod 升级时找探测器应复用同一入口，而非其它容器枚举 [L2]

**④ 拾荒掉落池 dumpingGroundTG 的三级结构与数据源 [L2]**：
- POI 引用：`ExpeditionPoiList.ScrapPile()` 的 `[rbx+56] = "dumpingGroundTG"`（ScrapPile 旧堆）；FreshScrapPile 用 `"dumpingGroundFreshTG"`（新堆）[L2]
- 生成链：`ScavHelper.GetRandomScavengedItem()` → `ItemSpawner.SpawnFromTableGroup("dumpingGroundTG")`
- `ItemSpawner.SpawnFromTableGroup(tableGroupID)` 完整逻辑（方法体实锤）：
  ```
  if !TableGroupMaster.Instance.tableGroups.TryGetValue(id, out tableGroup):
      Debug.LogWarning("Table Group ID '" + id + "' not found."); return null
  table = tableGroup.GetWeightedRandomTable()     // 表组→加权随机选表（0x18107F5A0）
  entry = table.GetWeightedRandomEntry()          // 表→加权随机选条目
  return ItemSpawner.Spawn(entry.itemId)          // 按 itemId 生成
  ```
- 数据结构：TableGroup = {Tables: List<加权表>}，Table = {Entries: List<加权条目(itemId, weight)>} [L2]
- **数据源：TableGroupMaster.Start()（方法体实锤）遍历 `this[0x24]`（tableGroups 序列化 Dictionary<string,TableGroup>）注册——即表组数据是 TableGroupMaster 场景/预制体的 Inspector 序列化字段，不在 Assembly-CSharp 代码里**。完整掉落池（物品+权重）需从资源层（第4层：TableGroupMaster 所在场景/预制体）用 UnityPy/AssetStudio 提取 [L2]

### 29.9 嵌套容器内的物品，原版打烊持有检查找不到（2026-09-04）

**场景**：捡漏直觉把加强探测器放在 5 格工具箱内部，原版 ScavengeDumpingGrounds 双倍掉落失效。

**根因** [L2]：
- `EmporiumEntry.GetAllAfterhourOwnedItems()` 返回打烊背包**直接子物品**（非递归）：
  - 遍历 `[r14+0x158]`（afterhourPocketSlotInvBackpack）+ `[r14+0x160]`（afterhourPocketSlotInvRight）两个容器
  - 每个 item 调 `0x18089B720`（IsOwned/已购买判断）为真才加入列表
- 原版探测检查链（ScavengeDumpingGrounds）：
  - `IsAfterhourHaveOwnedItems("metal_scanner")` → 基于 GetAllAfterhourOwnedItems 遍历比对 identifier
  - `GetAfterhourItemById("metal_scanner")` → 同样基于该列表
  - 两者 ISIL 都调用 `0x1807B10D0`（= GetAllAfterhourOwnedItems）
- **结论**：物品嵌套在背包内的容器里（如工具箱），原版这两个检查都找不到 → 持有判定失效

**解法（一处 Patch 全通）**：Patch `GetAllAfterhourOwnedItems` Postfix，把嵌套容器内的目标物品补进返回列表：
```csharp
public static void PostfixGetAllAfterhourOwnedItems(Il2CppSystem.Collections.Generic.List<GameItem> __result)
{
    if (!IsActive() || __result == null) return;
    GameItem scanner = FindScannerInOwned();   // 递归查工具箱内部
    if (scanner == null) return;
    for (int i = 0; i < __result.Count; i++)
        if (__result[i] == scanner) return;    // 防重复
    __result.Add(scanner);
}
// 注册：ManualPatcher.TryPatch(typeof(EmporiumEntry), "GetAllAfterhourOwnedItems", postfix:..., patchHost: typeof(LuckScoutPerk));
```
- 因为 IsAfterhourHaveOwnedItems / GetAfterhourItemById 都基于该列表，补一次列表，原版两条检查链同时生效
- Postfix 的 List 参数用 `Il2CppSystem.Collections.Generic.List<T>`（有先例 CustomStartingPerks.cs）
- 每次调用都要防重复 Add（该方法会被频繁调用）

**可复用结论**：
- "持有判定"类检查若基于某个列举方法，Patch 该列举方法补对象，一处改多处生效
- 打烊玩家物品的正确枚举入口 = `EmporiumEntry.GetAllAfterhourOwnedItems()`（不是 GetAllItems）
- 原版 metal_scanner 直接放背包（非嵌套）才被识别——凡"被动持有生效"的道具，放进自定义容器即失效，需此法补回



### 29.10 最小复现排查法 —— 版本坏了但不知道哪个改动引入（2026-09-04 沉淀）

**适用场景**：新版本整体坏了（如"特性界面点不了"），但改动点很多，不知道是哪个引入的。

**正确做法（最小复现，最多两三次锁定）**：
1. 回退到**最近一次"测过能用"的版本**（成瘾警官版）→ 确认现象消失（界面能点）
2. 只加**一个改动**（如捡漏直觉）→ 再测
   - 坏了 → 就是刚加的这个，锁定
   - 没坏 → 加下一个，重复
3. 不要在新版本上逐个"禁"功能去猜——坏版本可能有多个叠加问题，逐个禁会互相干扰，还慢

**为什么比"在坏版本上逐个禁"快**：
- 坏版本 = 多个改动叠加，现象可能是 2+ 个改动共同导致，逐个禁看不出哪个是关键
- 干净回退 + 单点加回 = 每次只引入一个变量，结论确定

**配套（让回退秒级完成）**：
- 每个阶段一个 git 提交（功能完成提交一次、清调试单独提交一次）
- 无 git 就留版本备份（zip 整个 Mods 目录 + 源码）
- 回退用 `git revert` / 对比，不要靠改代码猜

**纪律（避免重蹈"删调试误伤"覆辙）**：
- 排查前先 `git diff` 对比"能用版 vs 当前版"的改动文件清单，不从头猜
- 删调试 = 独立任务，删完先回归验证上个项目，再开新项目


### 29.11 外出拾荒系统完整流程 + 可改点（看板任务交付，2026-09-04）

#### ① 完整流程（玩家视角逐步 + 类/方法/UI）

| 步 | 玩家视角 | 入口类.方法 | 证据 |
|-|-|-|-|
| 1 | 打烊收摊 | `MapUIManager`（外出地图 UI 激活，`TutorialUIManager.OnReturnFromAfterhours` 返回） | [L2] |
| 2 | 地图出现"外出确认" | `MapUIManager.OpenGoOutsideConfirm()` → `OnGoOutsideConfirm()` → `StoreResponse.ShowAfterhourResponse()` | [L2] |
| 3 | 选地点（唯一：垃圾场） | `ExpeditionLocationList.DumpingGrounds()`；POI：`ExpeditionPoiList.JunkPile()/FreshScrapPile()/ScrapPile()` | [L2] |
| 4 | 点"拾荒"按钮 | `MapUIManager.ScavengeButtonClick()` → `ScavHelper.ScavengeDumpingGrounds()` → `UpdateScavUI()` | [L2] |
| 5 | 判定（次数+受伤） | `ScavHelper.CanScavenge()`；小伤 `RollMinorWound`/大伤 `RollMajorWound`（免疫计数 >0 则不判） | [L2] |
| 6 | 掉物生成 | `ScavHelper.GetRandomScavengedItem()` → `ItemSpawner.SpawnFromTableGroup("dumpingGroundTG")` + 工具联动 + metal_scanner 双倍 | [L2] |
| 7 | 物品去向 | 拾荒物入打烊背包 `EmporiumEntry.TryAddToAfterhourInv()`；打烊结束返回时 `MapUIManager` → `EmporiumEntry.TransferAfterhourToInv()` → `GraphUtils.TryAccept` 转回白天店铺库存（3 槽位兜底：`[0x152]/[0x48]/[0x64]`） | [L2] |
| 8 | 每日重置 | `ScavHelper.ResetScavenging()`：`PlayerStore.scavengingAttempts = 0` | [L2] |

**打烊后玩家物品枚举入口**：`EmporiumEntry.GetAllAfterhourOwnedItems()`（遍历 `[0x158]/[0x160]` 背包容器直接子物品，`0x18089B720` 判定已购买）→ `IsAfterhourHaveOwnedItems(id)` / `GetAfterhourItemById(id)` 都基于它 [L2]

#### ② 可改点清单（各带 Patch 目标 + 难易）

| # | 可改点 | 目标方法 | Patch 目标 | 难易 |
|-|-|-|-|-|
| 1 | 每日拾荒次数 | `ScavHelper.GetMaxScavAttempts()` | Prefix 改返回值（基础5/精通7-每周惩罚）；**必须同时 Patch `GetScavTimeLeft()`（独立实现，减 `PlayerStore.scavengingAttempts`），只 Patch 前者不生效** | 低 |
| 2 | 次数重置 | `ScavHelper.ResetScavenging()` | 改归零逻辑（如改为不清零/改每日上限） | 低 |
| 3 | 掉物内容 | `ScavHelper.GetRandomScavengedItem()` | Postfix 追加/替换物品（现：dumpingGroundTG 必得 + 工具联动 10% + scanner 双倍） | 中 |
| 4 | 掉物表 | `dumpingGroundTG`（TableGroupMaster 序列化数据） | **在资源层**（UnityPy 改 TableGroupMaster 场景/预制体），非代码 | 中 |
| 5 | 物品价值 | `GameItem.GetValue()/GetCurrentValue()/GetNegociatedValue()` | Prefix/Postfix 加价（WagesPerks 已有先例）；或创建后 `SetValue(item, n)` | 低 |
| 6 | 探测器基础概率 | `metal_scanner` 创建 → `ScavHelper.InitScavScanner()` | 回调 `b__21_0` 内 `TagState.SetInt(15)` → 改 15 为自定义；或 `ModifyTag` 覆盖同一标签 | 低 |
| 7 | 探测器升级 | `ScavHelper.UpgradeScavScanner(item, upgrade)` | 改 +5 / 封顶 100 / +15 价值参数 | 低 |
| 8 | 探测器持有判定（嵌套容器） | `EmporiumEntry.GetAllAfterhourOwnedItems()` | Postfix 补嵌套容器内目标物品（一处 Patch，IsAfterhourHaveOwnedItems/GetAfterhourItemById 全通） | 中 |
| 9 | 受伤系统 | `RollMinorWound/RollMajorWound` + 免疫字段 | 改概率/免疫计数（`scavengingAttempts` 累加影响概率；手电筒 flashlight 降伤） | 中 |
| 10 | 物品去向 | `TransferAfterhourToInv()` | 打烊结束回白天时被 MapUIManager 调；可挂钩改库存去向 | 中 |

#### ③ 边界坑确认（2026-09-04 实锤）

**坑1 · GetAfterhourOwnedItems 非递归** [L2]：
- `GetAllAfterhourOwnedItems()` 只枚举背包**直接子物品**（`[0x158]/[0x160]` 容器子项），**不递归进入容器内部**
- 原版 metal_scanner 直接放背包（非嵌套）才被识别；放工具箱内 → `IsAfterhourHaveOwnedItems` / `GetAfterhourItemById` 均失效
- 解法：Patch `GetAllAfterhourOwnedItems` Postfix 把嵌套目标补进返回列表（见 29.9）

**坑2 · 次数重置** [L2]：
- `ResetScavenging()` 每日把 `PlayerStore.scavengingAttempts` 归零（`[0x218]`）
- `GetMaxScavAttempts()`（基础5/精通7-每周惩罚）与 `GetScavTimeLeft()`（减 `scavengingAttempts`）是**两套独立实现**，改次数必须两个都 Patch
- `CanScavenge()`：`(maxAttempts - scavengingAttempts) > 0` 才可拾荒

**坑3 · 存档状态** [L1]：
- PlayerStore public 字段随存档保存：
  - `scavengingAttempts`（int @0x218）= 今日已拾荒次数
  - `dumpingGroundMajorWoundImmunitySearchesLeft`（int @0x21C）= 大伤免疫剩余
  - `dumpingGroundMinorWoundImmunitySearchesLeft`（int @0x220）= 小伤免疫剩余
- 这些字段在存档系统序列化范围内，**读档后保留**（mod 如需自定义计数持久化，存 `PerkStatePersistence` 或复用 PlayerStore 存档字段）


## 第30章 捡漏直觉重做 + 稀有率 + 物品拥有状态污染（2026-09-04 实战）

### 30.1 捡漏直觉"只有次数+2没有工具箱/探测器"根因：Patch 只定义未注册

**现象**：重做后只有拾荒次数+2 生效，工具箱/探测器/大背包全没发。

**根因 [L1 实测]**：`LuckScoutPerk.cs` 里定义了 4 个 Patch 方法（PostfixGetMaxScavAttempts / PostfixGetScavTimeLeft / PostfixScavengeDumpingGrounds / PostfixGetAllAfterhourOwnedItems），但 `Core.cs` 的 ApplyAllPatches 里**没有对应的 TryPatch 注册调用**。TryGiveKit 虽然被 HandleInitialItemPostfix 调用，但拾荒相关 Patch 全没挂上 → 只有次数+2 生效（说明那两处 Patch 挂到了），掉落/嵌套识别/工具箱发放全失效。

**教训**：ManualPatcher.TryPatch 是"定义 + 注册"两步。**新增 Patch 方法后必须同步在 Core.ApplyAllPatches 加 TryPatch 调用**，否则静默不生效。排查"某功能没效果"时先 grep Core 里是否注册了该方法。

### 30.2 加强探测器稀有率实现（recvubNSdZ1JvE）

**拆包结论 [L2]**：原版拾荒掉物只走 dumpingGroundTG 表组；`SCAV_SCANNER_DOUBLE_CHANCE` 标签只是"双倍再送1件"，**不是稀有率**。DropTableValuable/MedicalRare 属 DropDirectory 场景战利品表，不在拾荒链路，无法直接挂钩。

**实现（已部署）**：Patch `ScavHelper.GetRandomScavengedItem` Postfix（返回 `List<GameItem>`）：
1. 判定持有加强探测器（FindScannerInOwned，复用 GetAllAfterhourOwnedItems + 递归补回）
2. 读探测器 SCAV_SCANNER_DOUBLE_CHANCE 标签值作概率载体
3. roll 概率 → 追加稀有物（rare_ore / rare_electronic，MaterialDirectory 稀有材料，`DirectoryMaster.Item(id, true)` 创建 + DisableTag("not_purchased"/"TAG_NOT_PURCHASED")）

**通用模式**：`GetRandomScavengedItem` Postfix 是"拾荒掉物内容扩展"的通用挂载点——追加物品直接 `__result.Add(item)`。

### 30.3 AddDirectSellingItemToTable isOwned=false 污染共享实例（通用坑）

**现象**：购买酿酒师卖的无限酵母后，正规酵母（wine_yeast）变未拥有（owned=false）。类似蛙哥妙妙箱"(未拥有)"bug。

**根因 [L2 拆包]**：
- owned 状态 = `IS_OWNED_TAG` 标签（GeneralHelper.SetItemOwned/IsItemOwned）
- `DirectoryMaster.Item(id, true)` 从缓存返回**共享实例**并 SetItemOwned(true)
- 商人售卖 `AddDirectSellingItemToTable(item, isOwned=false, ...)` → SetItemOwned(false) → **把共享实例置未拥有** → 玩家已有同 id 物品全被污染

**签名实锤（2026-09-04 二修）**：
```
AddDirectSellingItemToTable(GameItem, bool isOwend, bool isStolen, bool ignorePerk, int forcedHeat)
```
- **isOwend=true → 物品被判"已拥有" → UI 显示赠送（免费）**（蛙哥好物变赠送的根因）
- isOwend=false → 正常购买，但对 DirectoryMaster 共享实例会 SetItemOwned(false) 污染玩家已有同 id 物品
- **isStolen=true → InitStolenItem(item, forcedHeat) → 100% 盗窃标记**（新坑，拆包确认）
- ignorePerk / forcedHeat 与特性判定、盗窃分档相关

**最终正解（已部署）**：**商人售卖统一用「克隆实例 + isOwend=false + isStolen=false」**：
```csharp
GameItem sellItem = item;
try { GameItem cl = item.CloneLinked(); if (cl != null) sellItem = cl; } catch { }
instance.AddDirectSellingItemToTable(sellItem, false, false, false, 100);
```
- **克隆实例**：isOwend=false 只影响克隆，不污染 DirectoryMaster 共享实例
- **isOwend=false**：恢复正常收费（购买）
- **isStolen=false**：消除 100% 盗窃标记
- 8 处统一修复：FrogPowerPerk 2 / AlcoholMerchantPerk 3（grape/w 克隆、clone 直接用）/ RetiredGunsmithPerk 1 / WaterMerchantPerk 1 / DrJacksonFriendPerk 1

**通用规则**：商人售卖物品**不要直接传共享实例**（DirectoryMaster.Item 创建）给 AddDirectSellingItemToTable——要么 isOwend=true（会赠送）、要么 isOwend=false（会污染）。**正确做法 = 先 CloneLinked 再传 (false, false, false, 100)**。

### 30.4 本次验证命令速查

```
# 编译
cd "D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks"; dotnet build -c Release
# 部署
Copy-Item "bin\Release
et6.0\JacksonPerks.dll" "...\Mods\WagesPerks.dll" -Force
# 反编译验证
dotnet "D:\DoubaoWork\ilspycmd_extracted	ools
et10.0ny\ilspycmd.dll" "...\WagesPerks.dll" -o "<输出目录>"
# 验证 isOwned（应 0 处残留 false）
# grep AddDirectSellingItemToTable 调用，检查 ", false, true, false, 100" 是否全为 ", true, ..."
```


## 第31章 外出拾荒背包：原版背包工厂链 + 打烊背包装配 + 升级功能（2026-09-04）

### 31.1 原版背包完整创建工厂链（BackpackMedium() 为模板，[L2] ContainerItemDirectory ISIL 实锤）

`ContainerItemDirectory.BackpackMedium()` 完整链（含中间产物 GameItem 复用）：
```
1. invWindow = DirectoryUtils.CreateInventoryWindow(6, 4, true)   // (PixelWindow, GameInventory)，尺寸定内部库存
2. item = ContainerItemDirectory.EquippableBack()                  // 挂 "backpack" + "equippable" 标签（外观层）
3. item.SetContentWindow(invWindow.Item1)                          // 绑定内容窗口
4. item.SetSpriteAndShape("Items/items_backpack2", "simple_backpack_medium")  // 外观+外形
5. AudioHelper.InitBackpack(item)                                  // 音效
6. item 价值 = 450（[rbx+512]）
7. SetName(LocHelper.GetLocalizedItem("item_backpack_medium_name"))
8. ContainerHelper.InitBackpackItem(inventory, item)              // ⭐ 挂 BACKPACK_TAG
```

**两种标签的区别（关键，易混淆）**：
- `EquippableBack()` 挂 **"backpack" + "equippable"**（小写，表现层：能装备到背部）
- `InitBackpackItem(inv,item)` 挂 **"BACKPACK_TAG"**（大写，功能层：决定打烊背包 [0x158] 槽位是否收）
- 原版背包工厂链两者都会挂；**新做出外拾荒背包必须走 InitBackpackItem 挂 BACKPACK_TAG**

其他背包工厂（尺寸）：BackpackSmall=CreateInventoryWindow(4,3?) / BackpackMedium=6×4 / BackpackLarge=更大 / Satchel=6×3=18格 / backpack_medium_military 等。尺寸一律由 CreateInventoryWindow 决定，不是 value 字段。

### 31.2 打烊背包装配（SetupAfterhourInv 完整结构，[L2] EmporiumEntry ISIL 实锤）

`EmporiumEntry.SetupAfterhourInv()` 装配 4 个容器（this 偏移 = ISIL 十进制）：
| 偏移 | 类型 | 角色 | 放行规则 |
|-|-|-|-|
| [0x140] (=320) | PixelWindow "Ground" | 打烊窗口根 | - |
| [0x148] (=328) | GameGridInventory | **afterhourInventory 网格库存（12×9）**，拾荒掉落物去处 | AllowOnlyOwnedItems |
| [0x150] (=336) | GameSlotInventory | 口袋 1（tooltip item_expedition_box_label_pocket1，pouch 贴图） | AllowOnlyOwnedItems |
| [0x158] (=344) | GameSlotInventory | **afterhourPocketSlotInvBackpack**（背景=simple_backpack_medium） | **AllowOnlyTaggedItem("BACKPACK_TAG")** |
| [0x160] (=352) | GameSlotInventory | 口袋 2（tooltip item_expedition_box_label_pocket2，pouch 贴图） | AllowOnlyOwnedItems |

**关键结论**：
- **打烊背包 [0x158] 槽位只接受带 BACKPACK_TAG 标签的物品**（`ContainerHelper.AllowOnlyTaggedItem(inv, "BACKPACK_TAG", ownedOnly=false, excludeContainer=false)`）
- 打烊时玩家把带 BACKPACK_TAG 的背包放 [0x158]，杂项放口袋 [0x150]/[0x160]
- 拾荒掉落物（ScavengeDumpingGrounds）进 **afterhourInventory [0x148]**（注意 0x148=328 就是 afterhourInventory，cheatsheet 29.1 说的 EmporiumEntry[0x328] 即此）
- `GetAllAfterhourOwnedItems()` 遍历 [0x158]+[0x160]（29.8/29.9 已录），**不遍历 [0x148]**
- 返回：`TransferAfterhourToInv()` 把 [0x148] + 口袋/背包内容转回白天库存

### 31.3 新做出外拾荒背包的必要条件（清单）

参考原版 BackpackMedium + 妙妙箱 CustomStorageContainer 先例：
1. `DirectoryUtils.CreateInventoryWindow(w,h,true)` 定内部尺寸
2. `ItemDirectory.CreateEmptyItem(null)` 创建空物品（比 PreBuiltItemHelper 干净，cheatsheet 2455/2470）
3. `SetContentWindow(invWindow.Item1)` 绑窗口
4. `SetSpriteAndShape(atlas, spriteName)` 外观（自定义 atlas 先例=CustomStorageContainer）
5. `AudioHelper.InitBackpack` + SetName/SetValue
6. **`ContainerHelper.InitBackpackItem(inv, item)` → 挂 BACKPACK_TAG（打烊可携带的关键）**
7. 标签：CONTAINER_TAG / ITEM_HIDDEN_TAG / SYSTEM_TAG / SetGameItemType("STORAGE")（参考妙妙箱）
8. **注册目录**：`RegisterToDirectory(ItemDirectory)`（CustomStorageContainer 已有先例，cheatsheet 25 章）——读档原生恢复窗口必需
9. 给玩家：放 `emporium.backInvinvElement` 后背包（FrogPowerPerk 先例）

### 31.4 容器升级功能实现参考（ContainerUpgrade 0.9.4 反编译，2026-09-04）

**核心机制**：按住升级修饰键（默认 `）+ 拖材料（junk/scrap_metal/metal_ingot）到容器 → 容器内部网格 SetShape 扩容 → tag 记录升级状态（读档重放）。

**交互 Patch（5 个）**：
```
[GameItem.MayHaveValidInventorySlot] Prefix：按住升级键时，材料→容器不当作"放入"，拦截并记 candidate
[GameItem.MayTarget] / [GameItem.CanTarget] Prefix：按住升级键时，材料→容器是否可升级
[ItemBehaviourManager.Target] Prefix：按住升级键拦截默认行为（return false）
[GameItem.Target] Prefix：核心——校验材料/容器归属（PlayerStore.FindAllItem 指针比对）→ PrepareUpgrade → apply() + ConsumeMaterial()
[ItemMouseDragHandler.StartDrag/AbortDrag/EndDrag]：拖拽清理 candidate / EndDrag 落定升级
```

**尺寸修改核心（PrepareGrid.apply 闭包）**：
```
List<GameGridInventory> ggis = CollectGgisFromWindow(container.contentWindow, ...)  // 遍历窗口内所有 GGI
foreach (ggi in ggis) { ForceRebuild(ggi); ggi.SetShape(nw, nh); }                    // 扩容
container.contentWindow.ResizePixels(...); container.contentWindow.Validate();
SetItemTag(container, "CONTAINER_UPGRADE_W_TAG", nw);  // 持久化到 tag
SetItemTag(container, "CONTAINER_UPGRADE_H_TAG", nh);
```
- `GameGridInventory.SetShape(int,int)` + `inventoryShape` + `Validate()`（cheatsheet 21.2 已录）
- **tag 持久化**：`CONTAINER_UPGRADE_BASE_W_TAG/H_TAG`（基础尺寸）+ `CONTAINER_UPGRADE_W_TAG/H_TAG`（当前）+ `CONTAINER_UPGRADE_SPENT_TAG`（升级次数）
- **读档重放**：`GameItem.ValidateShapeState` Postfix → ReapplyFromTags：有 W/H tag 就 SetShape 回去
- **tag 写入**：`item.EnableTag(tag,true)` + `DelegateSupport.ConvertDelegate<Action<TagState>>(st => st.SetInt(v))` + `item.ModifyTag(tag, del, false)`（IL2CPP 委托写法）
- **材料消耗**：`material.unitCount-1`，≤0 则 Destroy
- **升级键判定**：`Input.GetKey(KeyCode)`（传统 Input）

**关键 API 参考**（ContainerUpgrade 可用）：
- `item.IsRootedInBackInv()` —— 判定物品根在后背包
- `PlayerStore.Instance.FindAllItem(bool isOwn)` —— 玩家拥有/非拥有物品全列表（指针比对归属）
- `item.contentWindow`（PixelWindow）→ 遍历收集 GameGridInventory（反射或 CollectWindowTree 递归）
- `item.EnableTag/IsTag/GetTagReadonly(tag).GetInt()`
- `LockHelper.IsCurrentlyLocked(item)` —— 上锁容器不可升级

### 31.5 本次验证命令速查
```
# 反编译 ContainerUpgrade（参考升级实现）
& "C:\Users\1\.dotnet\tools\ilspycmd.exe" -p -o <out_dir> "Mods\ContainerUpgrade.dll"
# 反编译产物主类：ContainerUpgrade\Main.cs（61KB，全逻辑）
```


## 第32章 全物品价值分布 + 损坏机器机制（2026-09-05 运行时 dump 实测）

> 数据来源：WagesPerks mod 临时调试键 F9（DebugMode 门控）运行时枚举，[L3] 实测。
> 权威枚举：`DirectoryMaster.GetIdentifierList<GameItem>(null)` 返回全量 359 个物品（游戏自身 API，比遍历 directories 可靠）。
> 归档表：`Mods\all_item_values.txt`（identifier<TAB>unitValue）。

### 32.1 全物品价值 dump 方法（可复用）

- **权威枚举入口**：`DirectoryMaster.GetIdentifierList<GameItem>(null)` → `List<string>`，359 条全量（[L3] 实测）。
- **路径 B 教训**：`DirectoryMaster.directories` 是 `Dictionary<Type, List<object>>`（键是 Type 不是 string），且 Il2Cpp 集合 `as System.Collections.IDictionary` **恒为 null**（Il2Cpp 集合不实现 .NET 接口）——路径 B 反射遍历拿不到 keys（fdNull=25）。**直接用游戏自带 GetIdentifierList 绕开 Il2Cpp 集合问题**。
- 取价值：对每个 id `DirectoryMaster.Item(id, false)` → `.unitValue`（long，auto-property）。
- 输出文件：`Path.Combine(Application.dataPath, "..", "Mods", "xxx.txt")`。

### 32.2 价值分布总览（359 个物品）

- 范围：**0 ~ 1200**；302 个有价值（>0），57 个价值 0（任务品/容器/凭证）。
- 分段：0=57 | 1-20=61 | 21-50=91 | 51-100=55 | 101-200=46 | 201-400=42 | 401-600=4 | 601-1000=2 | 1000+=1。
- 顶级（≥1000）：仅 `machine_bay_ext`(1200)。
- ≥500（6 个）：machine_bay_ext 1200 / storage_bay_large 850 / backpack_large 650 / system_uncapped_neural_core 500 / skincare_cream 500 / custom_storage_box 500。
- **剔除大件容器/装备后（适合拾荒的高价值物）**：
  - ≥300：11 个 —— system_uncapped_neural_core 500 / skincare_cream 500 / mirage_projector 400 / canister_mbs 400 / rare_electronic 350 / desequencer 350 / black_injector 350 / bottled_water_premium 325 / shotgun 310 / turbo_booster_adv 300 / advanced_flux_agent 300
  - ≥250 扩展：+ smg 280 / wine_yeast_infinite 250 / water_filter_adv 250 / vacuum_robot 250 / system_capped_neural_core 250 / stun_gun 250 / chem_module 250 / c4_set 250 / c4 250 / blue_blood_bag 250

### 32.3 损坏机器机制（[L1] MachineBrokenHelper ISIL 实锤）

**核心结论：损坏机器不是独立物品 ID，是「正常机器 + BROKEN_MACHINE_TAG 标签」。**

| 正常机器 | unitValue | 破损外观资产（非物品） |
|---|---|---|
| security_alarm | 150 | broken_alarm |
| furnace | 200 | broken_furnace |
| moisture_farm | 225 | broken_moisture_farm |

- `broken_*` 3 个是 Addressable 资产 `Items/items_broken_machine` 里的破损贴图/外观，**不是 GameItem**（所以不在 GetIdentifierList 的 359 里）。
- 关键类 `MachineBrokenHelper`（6 个静态方法）：
  - `InitBrokenMachine(GameItem)` —— 对正常机器 3 处 `ModifyTag("BROKEN_MACHINE_TAG")`，损坏化。
  - `LoadBrokenMachine(GameItem)` —— 按机器类型加载对应 `broken_*` 破损外观（`Items/items_broken_machine` Addressable）。
  - `AddBrokenMachineItem(GameItem)` —— 损坏机附加：随机能量信用 + `ModuleHelper.AddStuckCorruptedModule`（卡住腐化模块，50%/25% 概率）。
  - `OnScrewdriverUsed/OnWireCutterUsed/OnWelderUsed(GameItem machine, GameItem tool)` —— 检查 `GetTagReadonly("BROKEN_MACHINE_TAG")`，工具拆解产出 `nuts_metal`/`wire`/`scrap_metal`。
- 标签常量：`BROKEN_MACHINE_TAG` / `BROKEN_MACHINE_WIRE_COUNT` / `BROKEN_MACHINE_SCREW_COUNT` / `BROKEN_MACHINE_WELD_COUNT`。
- 关联升级：`RUINED_MACHINE_UNLOCK`、`isBrokenMachineIntroduced`、废品商 `retired_junker`（day%7==3 来访）。

### 32.4 拾荒稀有物池最终方案（捡漏直觉重做，用户拍板）

- **高价值池（≥300，11 个）**：见 32.2 剔除大件清单 —— 随机掉落 1 件，SCAV_SCANNER_DOUBLE_CHANCE 标签概率触发。
- **损坏机器池（新增，3 个）**：`security_alarm`(150) / `furnace`(200) / `moisture_farm`(225)，掉落时走 `MachineBrokenHelper.InitBrokenMachine(item)` 打标签。
- 理由：损坏机器是垃圾场主题招牌掉落，虽单件 <300，但「可拆解出材料 + 随机腐化模块」的复合价值够格当稀有发现。
- 实现要点：替换 `PostfixGetRandomScavengedItem` 里固定 rare_ore/rare_electronic 追加逻辑 → 从「高价值池 ∪ 损坏机器池」随机；损坏机器用 `DirectoryMaster.Item(id,false)` + `InitBrokenMachine` 生成。

### 32.5 本次验证命令速查

# 运行时 dump（F9）数据已归档：Mods\all_item_values.txt
# 损坏机器反编译：_cpp2il_isil\IsilDump\Assembly-CSharp\MachineBrokenHelper.txt
# 权威物品枚举（L3）：DirectoryMaster.GetIdentifierList<GameItem>(null) -> 359 条


---

## 第三部分 · 社区素材与自研踩坑（2026-09-01 原始素材）

> 说明：三、五节 hooks 清单与第二部分"二、Hook 与事件系统"主题重叠，以本部分社区原帖时间线原貌保留；九、节为 Wage's Perks 自研踩坑（特性类型/资金字段/每日触发/调试方法论）。

# Probably Stolen · Mod 开发速查

> 来源：#⚙️ mod 论坛频道 · Modding Q&A / FAQ（Discord：Probably Stolen 官方社区）
> 整理日期：2026-09-01

---

## 一、开发环境与入坑门槛（最重要）

| 项目 | 说明 |
| --- | --- |
| 当前游戏构建 | **IL2CPP**（正式版将转为 **Mono**） |
| 官方加载器 | **MelonLoader 0.7.3**（IL2CPP / net6），用官方 installer 安装 |
| 反编译 | 需反编译 IL2CPP 定义来研究游戏内部逻辑 |
| 入坑门槛 | 官方口径：懂 **IL/Assembly**、能啃 IL2CPP 定义的人现在就能做；否则**等正式版 Mono** 再入坑 |
| 安装方式 | 把编译好的 DLL 放进游戏目录 `Probably Stolen Playtest\Mods\` |
| 存档位置 | `C:\Users\你的用户名\AppData\LocalLow\Questing Goose Studio\Probably Stolen\`，文件形如 `save_NUMBER.es3`（Easy Save 3 格式） |
| 下载安全 | 优先走 **Nexus Mods**（有病毒扫描）；MelonLoader 官方源安全，但 DLL mod 有恶意注入风险 |

**常用仓库：**
- MelonLoader: `https://github.com/LavaGang/MelonLoader`

**硬性要求：**
- 该游戏的 mod **只能用 C# 编写**（2Much 在 FAQ 帖子中明确："You can only mod this game with C#..."）
- Python/Java 等其他语言不行；要做复杂 mod（如 Archipelago 联机）需"中等或更好"的 C# 水平

---

## 二、FAQ 帖子完整讨论内容（Modding Q&A / FAQ 置顶帖，8/16 ~ 8/31）

> 这是你链接所指的那个帖子本体，下面按时间线整理全部问答，保留原文要点。

### 1. 帖子正文（作者：2Much，8/16 发帖）

**帖子定位**：这是所有 mod 相关问题的问答主阵地（"Is there X mod?" / "How hard would it be for Y?" / "Can you please make Z mod?"）。与已有工具/mod 相关的问题请去对应帖子问。

**FAQ 原文要点：**
- **How do I get into modding?** 取决于你会不会 IL/Assembly、能不能啃 IL2CPP 定义。会的话就反编译 IL2CPP 构建；不会就等正式版（转 Mono）。
- **Is there "x" mod?** 除非在这个论坛频道里，否则没有。
- **Can you make "x" mod?** 目前只有 2Much 一个核心 modder，且很少按请求做 mod——提问反而会降低被做的概率。
- **Can you unlock non-demo content?**（玩笑拒绝）不行，也不要自己尝试。

### 2. 8/16 核心技术问答（重点）

**Q（amyalt125）：怎么向玩家库存添加物品？**
**A（2Much）**——从 debug mod 里提取的代码：
```csharp
EmporiumEntry.Instance.TryAddToPlayerInv(DirectoryMaster.Item("mirage_projector"));
```
> 知道物品的 identifier/id 后，替换字符串即可。2Much 还补充：添加"正常物品"和"自定义物品"是两回事。

**Q（amyalt125）：有没有 HandleInitialItem 的现代等价物（新游戏开始时被调用的钩子）？**
**A（2Much）**：用 **Harmony** hook 一个游戏开始时使用的 handler 函数即可。
**补充（MinekPo1）**：没有完全等价的，但有 **day start hooks**（每天开始，多个，用于 customers）。

**Q（amyalt125）：有没有办法查看调用栈 / 打印所有被调用的东西？**
**A（MinekPo1）**：可以用调试器拿常规 stack 再交叉引用；可以尝试把 IL2CPP metadata 转成调试器能理解的东西。

**Q（Nagisa）：你是怎么学会 mod 这个游戏的？**
**A（2Much）**："Fucked around and found out"——瞎折腾试出来的，加上之前在 Iron Nest 的经验。

**Q（Nagisa）：如果我想自己学，需要什么？**
**A（2Much）**：取决于你的技能水平；建议先在别的游戏上练手（"learn on another game"）。

### 3. 8/16 反编译工具链干货（MinekPo1 提供）

- **Il2CppInspectorPro**（Il2CppInspector 的 fork 即可）：导出一个 c# 文件，包含**所有 class 和函数定义**，还能在 **Ghidra** 里设置符号（symbols）。
- **Ghidra 自带调试器**：可以告诉你部分调用栈细节（不过会被 Il2Cpp 和转成 C 标识符的过程弄乱一些）。
- 建议准备一份**当前版本的 decomp**（反编译结果），方便直接看现在构建里有什么。
- 2Much 提示：看 **il2cpp refs**（IL2CPP 引用/定义）。

### 3.5 8/16 完整 hooks 列表（ModHook 提供，重点中的重点）

> 这是社区玩家（MinekPo1）从游戏里扒出来的**全部可用事件 hooks**，是写 mod 时订阅事件的核心清单。这些是 `ModHook` 类里的静态事件（不是 Emporium 的）。

**客户生成（顾客刷新）：**
```csharp
OnGenerateCustomerVeryEarly  // 极早生成客户
OnGenerateCustomerEarly      // 早
OnGenerateCustomerNormal     // 正常
OnGenerateCustomerLate       // 晚
OnGenerateCustomerVeryLate   // 极晚
```
（都带 `Action<StoreClientManager>` 参数）

**游戏加载：**
```csharp
OnGameLoadedInit     // 游戏加载初始化
OnGameLoadedEarly    // 加载早阶段
OnGameLoadedNormal   // 加载正常
OnGameLoadedLate     // 加载晚阶段
```

**营业状态：**
```csharp
OnShutterOpenedEarly  OnShutterOpenedLate   // 卷帘门开启
OnShutterClosedEarly  OnShutterClosedLate   // 卷帘门关闭
OnLeavingStoreEarly   OnLeavingStoreLate    // 离开店铺
OnReturningStoreEarly OnReturningStoreLate  // 回到店铺
```

**日常作息：**
```csharp
OnGoingSleepEarly   OnGoingSleepLate    // 睡觉
OnWakingUpEarly     OnWakingUpLate      // 醒来
OnHandlingNightlyServicesEarly  OnHandlingNightlyServicesLate  // 夜间服务处理
```

**物品/UI：**
```csharp
OnModItemDirectoryInit                       // Action<ModItemDirectory> 物品目录初始化
OnPlaceInventorInventoryItemEarly / Late     // Action<List<GameItem>> 放置物品到库存
OnCreateTooltipEarly                         // Action<RichTextBuilder, GameItem> 创建提示框
```

> 附带知识点：事件用 `add/remove` 声明（`public static event Action<...> OnXxx { add; remove; }`），对应 IL2CPP 偏移（如 0x08、0x10…）。

### 3.6 8/16 实战调试经验（amyalt125 + MinekPo1 + 2Much）

- **ModHook 里**有这些 hooks；`emporium.start` 不生效时，可以在 `OnGameLoaded` 上动手。
- **PlayerStore::StartNewGame** 是新游戏开始的钩子（替代旧版 HandleInitialItem）。
- **PlayerStore::get_Instance** 被频繁调用（几乎到处用），可用来验证 hook 是否生效。
- **AddAlwayItems / AddStartingItem** 仍在现代版里（在 EmporiumEntry），但 2Much 确认部分已不被调用。
- **Harmony 大小写坑**：`PostFix` 和 `Postfix` 不一样——用户曾因写了 `PostFix` 而不是 `Postfix` 导致 hook 不触发。
- **MelonLoader 默认会自动 Harmony PatchAll**（不需要手动调 `Harmony.PatchAll`）。
- **判断 hook 是否真的被调用**：可以用 `ChangeLanguage`（语言切换）或 `PlayerStore::get_Instance` 这种高频函数先测自己的消息输出是否正常。

### 4. 8/17 ~ 8/21 讨论

- **ToxicPhoenix**：能否做"去掉顾客移动、改成小屏幕显示顾客呼吸"的 mod（晕动症问题）？→ 2Much：See FAQ（即不按请求做）。
- **DyrClone**（第一个 mod 求助）：做老鼠基因重命名 mod，目前问题是要用户**手改 cfg 文件**来设置"好老鼠"的阈值，想找更好的方案。→ 2Much：指去 **Technical Discussions**（技术讨论）板块。
- **drakbar**：想用 customUIManager 做一个可以打开查看所有肉的笔记本/纸张。→ DyrClone：发你了（已分享自定义 UI 用法）。
- **Supercopia** 提到 **UnityExplorer**：可用于检查游戏、切换状态、查看对象/函数并调用，mod 开发利器（注意：社区只担心工具会泄露正式版/封闭 playtest 内容，不担心单机作弊）。

### 5. 8/23 讨论（重要：正式版与 Archipelago）

**Q（Raxtus）：现在做的 mod 移植到正式版有多难？**
**A（2Much）**：取决于你 mod 的是什么内容、用的 hooks/functions 是什么。**正式版官方 mod 支持已确认**（2Much 原话："Official mod support is confirmed"）；**开发者 Amba 亲自确认：正式版会容易得多**（"yes much easier, no idea how you actually created the perk mod in il2cpp build"——连开发者都不明白 IL2CPP 下怎么做的）。

**关于 Archipelago（多人随机化联机）**：
- 需要锁/替换 Wilde 的所有服务、控制租金、控制事件、即时生成物品、控制声望等，预计 **3 个月项目**（含学 C# + 跟进更新）。
- 2Much 建议：先学 C# 并**等正式版**（"it's this year"——今年就出）。
- 若 Archipelago 有 C# 插件可直接引用做网络层；生成物品、控制租金容易，声望中等，其余较难。

### 6. 8/25 讨论（鼠标控制）

- **Kyrtos**：想要纯鼠标操作的 mod——右键切换"目标容器"，左键把物品放进指定容器；滚轮旋转。
- **amyalt125**：滚轮旋转物品已经是原版功能（beta 分支）；拖拽放物品到指定位置是目前能做到的最好方案。
- **Kyrtos**：能给右键菜单加一个"选择目标容器"按钮吗？→ amyalt125：技术上不难，但不知道怎么加新按钮。
- **Kyrtos**：那用中键点击选中？→ **DyrClone**：其实不需要 mod，买个带侧键可自定义的鼠标（如雷蛇 DeathAdder Essential，约 $25）。

### 7. 8/31 讨论（最新）

- **Jirikki**：把赃物物品背景做成微红色难吗？→ 2Much：不太确定，他不太碰 sprites（贴图），但指了相关频道/资料。

---

## 三、Technical Discussions 技术讨论（8/16 建立的深度技术板块）

> 这是 FAQ 帖子里 2Much 指路的"Technical Discussions"子区（楼主 MinekPo1），专门聊游戏代码底层，不适合问"怎么装 mod / 怎么入门"这类基础问题。地址：`https://discord.com/channels/1315752321975845004/1538294778121297992`

### 1. 游戏使用的第三方库清单（MinekPo1 置顶，写 mod 前必看）

> 这些库是游戏引擎依赖，mod 里可能直接引用或需要了解：

| 库 | 用途 / 官网 |
| --- | --- |
| **Easy Save 3** | 存档系统，docs.moodkie.com |
| **Febucci.TextAnimator** | 文本动画，textAnimatorForGames.com |
| **RNGNeeds** | 随机数生成（NPC 行为/掉落等），RNGNeeds.com |
| **Json.NET** | JSON 序列化，newtonsoft.com |
| **DOTween** | 补间动画，DOTween.demigiant.com |
| **Harmony** | 打补丁/hook 的核心库（配合 MelonLoader） |

### 2. 物品制造商（Manufacturer）显示的技术细节

- **amyalt125** 发现物品制造商名字不显示。
- **MinekPo1** 分析：物品名下方那行制造商文字（如 oxy/immun 注射器的 "Charmicheal Pharmaceutical"），可能需要 **patch `GameItem::DisplayManufacturer`** 并调用 **`GameItem::EnableManufacturer`**。
- **amyalt125** 实测结论：
  - `DisplayManufacturer` 看着该生效但实际不显示；
  - `EnableManufacturer` 跟 rich text builder 有关；
  - **正确做法**：`gameItem.EnableManufacturer("davre")` —— 传入厂商 id 字符串，会显示成 "Davre Manufacturing Ltd."。
  - 制造商显示机制在现在的构建里**有改动**（不能自定义任意厂商名，需要对应 id）。

### 3. Perk（特性）图标制作规范（I. González 分享，8/25）

> 做自定义 perk 时，要让图标正确显示，需遵守：
- **32x32 像素 PNG**，**背景透明、前景白色**
- 游戏本身会根据 perk 类型（积极 Positive / 消极 Negative / 中立 Neutral）**自动套滤镜上色**
- 也就是说：图标素材统一用白底图即可，颜色由游戏统一处理，不需要自己配色

### 4. 屏蔽董事会成员示例：完整 Harmony Patch 代码（2Much，8/25）

**背景**（I. González 实测 + Ghidra 反编译）：
- 董事会成员（Board Member）会在 `StoreStation.Instance.dayCounter == 8` 时条件性出现（客户约第 9 天开始出现）
- 想用 perk 禁用它，设 `PlayerStore.Instance.isAppraisalIntroduced = true` 不够，仍会显示
- 相关方法：`CreateStoryAppraisalBoard`（以及 "Come back later" 的 `CreateStoryAppraisalBoard2`）

**2Much 给的解法**——patch 让方法返回 null（`AddClient` 本身有错误处理，传 null 不会崩）：

```csharp
[HarmonyPatch(typeof(StoreClientListStory), "CreateStoryAppraisalBoard")]
public static class hiTwoMuchHereIAmInYourWallsIMeanYourSourceCode
{
    public static void Postfix(ref StoreClient __result)
    {
        if (StartingPerk.IsPerkActive("y"))   // "y" 换成你的 perk id
        {
            __result = null;
        }
    }
}
```

**配套知识点：**
- **Prefix vs Postfix 都能用**（amyalt125/2Much 确认）：Prefix 可以跳过原函数；Postfix 可以改最终结果。本例用 Postfix 改 `__result = null` 即可。性能上前缀略优但无所谓。
- `StartingPerk.IsPerkActive("perkId")` 是判断某 perk 是否激活的 API。

---

## 四、QoL 类 mod（已发布可参考）

| Mod | 作者 | 功能 | 热键 / 入口 |
| --- | --- | --- | --- |
| Inventory sort（储物整理） | DyrClone | 给所有储物加 Sort 按钮，一键整理散装物品，大件优先 + 同类型聚合 | F6 打开储物列表窗口 |
| Rat Genetic Renamer（老鼠基因重命名） | DyrClone | 新生老鼠按基因自动命名并打标 good/decent/normal/bad | F4 可拖拽面板，设各属性 min/max |
| TagHover（标签悬停高亮） | 我明了 | 悬停高亮物品，可穿透储存区（副作用：也会穿透未打开的工程/服务/医疗箱） | 悬停触发 |
| 寿命属性改进 | 我明了 | 把寿命改为延长成年阶段（原版 11 天进老年，寿命只加老年段） | — |
| Rent Configurator（租金配置） | 2Much | 主菜单 UI 自定义全部租金参数；初始租金只对新档生效 | F8 重开 UI |
| Don't Take My Shotty! | 2Much | 阻止守卫没收 12 号霰弹枪；对已没收的旧档无效 | — |
| HotterFix Bug 修补 | 2Much | 在官方 hotfix 前快速修补多种 bug（含自定义 UI 示例） | 主菜单自动弹出，F5 重开 |
| Autohotkey Tool Helper | Riael | F1 悬停选工具 → F2 自动拿起拖到鼠标处使用（切东西/灌瓶/喂药），F3 反向把物品放进指定箱子 | F1 / F2 / F3 |

---

## 五、内容类 mod（玩法扩展可参考）

| Mod | 作者 | 功能 |
| --- | --- | --- |
| MoreEvents（新增事件） | amyalt125 | 动态创建新游戏事件，字段模板见下 |
| Orange Juice（添加物品 POC） | amyalt125 | 概念验证：往游戏加新物品，要求 MelonLoader 0.7.3 |
| Fallacy of Control（老虎机） | 2Much | 自定义老虎机赌博 mod，G 键打开菜单 |
| Boombox（自定义磁带） | LastPenguin | 自定义录音带：`.mp3/.ogg/.wav` 放入 `Mods/Boombox/`，配同名 `.txt`；磁带可缩小 2x2→1x1 |
| 特性+特殊NPC+剧情 | 遗忘的小窝 + gwxxwg12332 | 9 个自定义起始特性、4 个特殊 NPC（博士/退休枪匠/水商/酒商）、胡安多阶段剧情线（A/B/C 三结局）、NPC 随机商品 + 智能推荐 |

### MoreEvents 事件字段模板

- **News name**：新闻标题（如 "Station-wide blackout!"）
- **News description**：新闻正文
- **Display name**：日历上显示的标签（如 "Water shortage"）
- **Odds**：出现概率，越高越常见（常见事件用 15）
- **Duration**：持续天数（可随机数）
- **Event type**：cosmetic（纯装饰）或 normal（食物/水短缺等）
- **Modifiers**：修正，如 "Food +50%, weapons +30%"
- **Bonus action**：可做任意事，例如召唤一个需要食物的客户

> 注：除 odds 外，其余字段均可动态变化（可随天数、净财富等改变）。

---

## 六、外部工具（网页 / 开源，不注入游戏）

| 工具 | 作者 | 功能 |
| --- | --- | --- |
| Module Optimization Tool | hoydoy | 模块布局优化器：选模块舱等级、模块和节点数，按性能/品质/效率自动排布；持续迭代找更优解，全开源 |
| Rat Colony Manager Tool | hiemas | 批量对比老鼠基因，给出绝育/繁殖建议；支持读取存档批量导入 |
| Save Editor（存档编辑器） | hiemas | 网页端编辑玩家、商店、声望与库存；注意改前备份，仅支持英文存档 |
| UnityExplorer | 社区推荐 | 检查游戏对象、切换状态、查看/调用函数，mod 开发调试利器 |

### Save Editor 可编辑项

- **玩家**：信用点（Credits）、野性声望（Wild Favor）、证据等级、各类成瘾天数（尼古丁/麻醉剂/酒精/哨兵注射）、哨兵池、伤口状态（是否稳定、是否新伤）、昨晚是否去 After Hours
- **商店**：是否已付租金、基础魅力、今日 gutterflow/rustwater 纯度、是否硬模式、无限模式、跳过开场
- **声望**：所有阵营的全部字段与 perk
- **库存**：每个物品可编辑、移动、复制、删除、复制为 JSON，也可从 JSON 新增物品
  - 提示：经 JSON 加物品时，`save_bag` 是容器的基础父物品，这是游戏构建方式的一个怪癖

---

## 七、中文汉化（本地化参考）

| 内容 | 作者 / 说明 |
| --- | --- |
| 汉化老虎机 | gwxxwg12332（Fallacy of Control 简中版） |
| 汉化箱子整理 | gwxxwg12332（Inventory sort 简中版） |
| 汉化租赁配置 | gwxxwg12332（Rent Configurator 简中版） |
| 汉化 MeatGazer | gwxxwg12332（drakbar 的肉类统计 mod 简中版，面板 UI/配置项/控制台提示全翻译） |
| 老鼠基因重命名 汉化版 | 社区汉化（Rat Genetic Renamer 中文翻译版） |
| 俄语本地化修复 | ComebackPlay（示范本地化修复 mod 写法） |

---

## 八、其他值得关注

- **Archipelago 多人随机化**（Raxtus）：把 Probably Stolen 接入 Archipelago（多人随机化联机框架），计划正式版发布后开发。
- **Modding Q&A / FAQ**：频道置顶帖，是社区问答主阵地；"Is there X mod?"、"How hard would it be for Y?" 类问题集中在此。
- **官方态度**：目前核心 modder 以 2Much 为主，rarely 基于请求做 mod；提问反而降低被做的概率（原话：Asking makes it less likely I'll do your mod）。

---

---

## 九、自研 Mod 实战踩坑（Wage's Perks / JacksonPerks，作者 gwxxwg12332）

> 以下均来自实际迭代验证（反复编译/部署/进游戏/查日志），可直接复用。

### 9.1 特性类型与负面标红（StartingPerkType）

- 游戏枚举 `StartingPerk.StartingPerkType`（dump.cs TypeDefIndex 746）：
  | 值 | 含义 | UI 颜色 |
  |---|---|------|
  | `POSITIVE = 0` | 正面 | 绿 |
  | `NEGATIVE = 1` | 负面 | 红 |
  | `NEUTRAL = 2` | 中性/混合 | 黄 |

- **设置 StartingPerk.type 必须直接赋值**：
  ```csharp
  perk.type = (StartingPerk.StartingPerkType)Type;   // ✅ 有效
  ```
- **坑**：反射 `typeField.SetValue(perk, int)` 会把 int 塞进枚举字段，Il2Cpp 下抛异常被 `catch {}` 吞掉 → type 一直是 0（正面绿），负面特性不标红。反射兜底要用 `Enum.ToObject(typeField.FieldType, Type)`，但**最稳是直接赋值**。
- 特性注册数组用 `new CustomStartingPerk[]`（自动推断），**不要写固定长度**（如 `[9]`）——加特性会 CS0847 报错。
- 负面特性 Cost 用负数返还点数（如招贼-2 / 霉运-2 / 信誉-1）。

### 9.2 玩家资金字段（PlayerStore）

- **钱字段是 `PlayerStore.playerCash`（int, offset 0x10）——不是 `playerMoney`！**
- 扣/改钱直接反射 `playerCash`（public 字段），或直接 `store.playerCash -= x;`
- 其他关键字段速查：
  | 字段 | 类型 | 偏移 | 用途 |
  |---|---|---|---|
  | `playerCash` | int | 0x10 | 玩家信用点 |
  | `wildFavor` | int | 0x14 | 荒原声望 |
  | `storeClientManager` | StoreClientManager | 0x80 | 客户管理器 |
  | `futurStoreClientIdQueue` | List<string> | 0xC0 | 未来客户队列 |
  | `futurStoreClientDayQueue` | List<int> | 0xC8 | 未来客户到达天数 |
  | `nightlyAction` | List<Action> | 0xE8 | 每晚执行动作 |
  | `lastTheftValue` / `lastInspectionLossValue` | int | 0xB0/0xA8 | 失窃/检查损失 |

### 9.3 每日效果触发时机（重要踩坑）

- **`OnNewDay`（跨天）在测试当天经常不触发**——进档只触发 `BeginDay`，不触发 OnNewDay。你测"第一天"功能时 OnNewDay 根本不会跑。
- **每日/负面效果必须双挂**：`BeginDay`（进档当天即生效）+ `OnNewDay`（跨天）
  ```csharp
  public static void PostfixOnBeginDay() { ForceInspectionToday(); ApplyBadLuck(); ... }
  public static void PostfixOnNewDay()    { ForceInspectionToday(); ApplyBadLuck(); ... }
  ```
- 日志佐证：某次测试 8 分钟，`BeginDay` 触发多次、`OnNewDay` 一条都没有 → 招贼体质/霉运"没生效"其实是没等到跨天。
- **诊断顺序**：先看日志区分「没触发」vs「触发了但逻辑失败」。例如霉运显示「扣钱失败：playerMoney 属性/字段均未找到」——说明触发正常，是字段名错（playerCash）。

### 9.4 调试方法论

- 失败路径**不要静默 catch**，加日志：`[特性] 直接赋值type失败: ...`、`[霉运缠身] 属性扣款失败: ...`，才能定位。
- 功能"没实现"时，先确认：① 补丁挂载是否成功 ② 触发时机是否到达 ③ 逻辑内部是否抛异常 ④ 字段/API 名是否正确（对照 dump.cs）。
- 找游戏字段/枚举直接查 `_dumper\dump.cs`（53 万行，用 python 逐行搜比 Get-Content 快）。

## 附：通用安装流程

1. 安装 MelonLoader 0.7.3（用官方 installer，IL2CPP / net6）。
2. 把编译好的 mod DLL 放进 `Probably Stolen Playtest\Mods\`。
3. 启动游戏验证；多数 mod 通过 F4/F5/F6/F8/G 等热键打开配置或功能面板。
4. 若需改存档：先备份原 `save_NUMBER.es3`，再使用存档编辑器或直接替换。

