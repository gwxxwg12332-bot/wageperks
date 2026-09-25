# Wage's Perks Bug 追踪

> 维护说明：本文件为 bug 追踪权威来源，随修复进度更新。
> 字段约定：
> - **状态**：待排查 → 排查中 → 已确认 → 已修复
> - **根因**：只填拆包/日志确认后的机制根因，**不写猜想**（猜想单独标在"初步猜想"）
> - **修复版本**：对应 git tag（如 v1.1.6）
> - 修复原则：先拆包确认根因再动手，不按猜想修

## 🔴 高优先级（卡死/严重功能失效）

### BUG-001 打开储藏箱卡顿卡死
- 优先级：🔴 高
- 状态：已修复（09-11 落地，commit 待填）
- 现象：打开容器/储藏箱时游戏直接卡顿卡死；hover 物品也卡；转移所有物品时格外卡（蛙哥箱子格外卡）
- 触发：打开容器、hover 物品（tooltip 显示价格）、批量转移物品
- 初步猜想：容器 UI 渲染/物品遍历循环、保存加载钩子无限迭代，打开容器触发大量状态计算（已拆包推翻：非死循环）
- 根因（[L1] 源码级实锤）：hover/批量转移 → 每件物品调 GetCurrentValue/GetNegociatedValue → Postfix TryApplyTradeMarkup（Patches.cs L1297）→ 鲁滨逊职业激活时每次价格计算都跑 TryAddNodeBuffFeature（L1691）→ GetTradeBuffDisplay（RobinCrusoePerk.cs L449）重活：
  - AllActiveNodes() 7 状态节点判定 → 每节点遍历 Lock 效果数组 + FxLabel switch
  - GetStoredNodeKey + GetNodeFx（读抽取状态）
  - GetSellBonusPct/GetBargainBonusPct/GetBudgetBonusPct 三个重算（各自调 GetMood/GetSocial/GetGranaryDays/GetElevCount/FxNum）
  - StringBuilder 全量拼接 → 每次价格计算重建整段文本
  - 叠加：itemFeatures 线性遍历防重复（O(N)）+ IsFood/IsMedicine 宽松判断
  - 批量转移几百件 / 鼠标扫过堆积物品 → 每件 × 每帧重跑整条链，物品越多越卡
- 修复方案（已按此落地 09-11）：
  1. GetTradeBuffDisplay 缓存：节点状态只在打烊结算/状态变化时变 → 7 个 Set* + SetCompBuffDays 写入时 InvalidateTradeCaches()，价格计算直接读缓存字符串
  2. GetSellBonusPct/GetBudgetBonusPct/GetBargainBonusPct 各自缓存（-999 哨兵），失效同 1
  3. TryAddNodeBuffFeature 物品指针 HashSet<long> 快速路径：同一缓存周期每件物品只处理一次（ClearNodeBuffItems 随缓存失效清空）
  - IsFood/IsMedicine 保持原样（哈希/tag 判断已快，指针缓存收益低且指针复用有误判风险，不缓存）
- 验证方法：非鲁滨逊职业（startType≠14）的档不卡 → 可用来验证修复效果
- 修复版本：v1.1.6（待发布）

### BUG-015 蛙娘好感度/六维状态读档清零
- 优先级：🔴 高（状态丢失，影响蛙娘玩法）
- 状态：已修复（09-26，开发版已部署 MD5 1D0041FF）
- 现象：喂蛙娘 → 好感上涨 → 打烊存档 → 关游戏重开 → 读档 → 好感度回到初始值。日志 [SaveStore] 显示键值（wage_girl.affection 等）已读到，但游戏内状态未生效
- 根因（源码级确认）：读档空窗期（OnLoadGame → LoadIfPending/TryDoLoad 完成前）特性代码 SetXxx 写入**默认值**，覆盖刚读入存档的键值——键值在日志可见但已被默认值污染
- 修复（WageSaveStore.cs 写入门控）：
  1. _loadingComplete 字段默认 true（新档/启动正常写入）
  2. OnLoadGame L473 复位 false（读档期间禁止写入）
  3. SetString L286 唯一写入口拦截 if (!_loadingComplete) return（SetInt/Bool/Float 全走 SetString）
  4. TryDoLoad L533/L541/L547 三出口（成功/无档/异常）统一放开 true
  5. 蛙娘预热核心 key（K_LEAVE/K_EXIST）在 OnGameLoadedReset 最先恢复
- 验证：正常旧档/连续读两档/存档损坏/新开局/读档后销赃/读档后蛙娘外出——6 场景全覆盖
- 修复版本：v1.2.10+（待重打包）

## 🟠 中优先级（机制逻辑错误，影响核心玩法）

### BUG-002 生存状态议价 buff：购买生效、出售不生效
- 优先级：🟠 中
- 状态：已关闭（09-12 用户确认：**设定**——买入加价、卖出不加是有意设计，不修）
- 现象：状态带来的议价加成，买东西能吃到加成，卖东西吃不到，买卖价格不对称。例：原价 30 物品，买入价 50~60，卖出上限仅 40
- 触发：交易出售场景
- 初步猜想：买卖两套独立议价函数，补丁只 Hook 了 OnBuy，漏掉 OnSell 分支（**待拆包确认**）
- 根因：非 bug——用户拍板"只有买入加价"为设定
- 修复版本：—（不修）

### BUG-003 纯水健康判定绑定容器（水质判定错误）
- 优先级：🟠 中
- 状态：已解决（09-12 用户确认）
- 现象：同样 100% 纯水，装在 A 瓶子饮用=脏水扣健康；换到 B 瓶子饮用=优质水不扣健康。水质判定绑定容器实例，不是水本身属性
- 触发：饮用不同容器中的水
- 初步猜想：水质读取的是容器组件的值，不是液体本身纯度（**待拆包确认**）
- 根因：（用户确认已解决，机制根因未记录）
- 修复版本：（未记录）

### BUG-004 地下交易清单获取异常
- 优先级：🟠 中
- 状态：已解决（09-12 用户确认）
- 现象：部分玩家第一天进入场景即可拿到清单；另有部分玩家无法获取清单
- 触发：地下交易场景进入时
- 初步猜想：触发条件/标记变量未正确 set，存档 flag、剧情触发时序问题（**待拆包确认**）
- 根因：（用户确认已解决，机制根因未记录）
- 修复版本：（未记录）

### BUG-005 绿衣革命军 NPC 重复发放名片
- 优先级：🟠 中
- 状态：已解决（09-12 用户确认）
- 现象：NPC 多次上门反复给名片，没有"已获得名片"的防重复判断
- 触发：NPC 上门时
- 初步猜想：缺少 bool 标记，不检测玩家是否已持有名片（**待拆包确认**）
- 根因：（用户确认已解决，机制根因未记录）
- 修复版本：（未记录）

## 🟢 低优先级（后续补充）

> 待用户继续提交测试反馈后追加条目。

### BUG-006 蛙娘带回的正品免疫宁无法使用
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）
- 现象：蛙娘带回的免疫宁点了没反应、无法消耗生效
- 根因（[L1] ISIL 实锤 InsInjectorHelper）：`DirectoryMaster.Item("large_purple_injector")` 等价 `Init`，只打 2 个基础标签；
  `SetGenuine` = ModifyTag x7 写入正品数据，才是"可用正品"的必要前提（SetExpired / SetCounterfeit / SetUnused 都先调 SetGenuine）。
  旧代码只调 DirectoryMaster.Item → 缺 7 项 → 不可用
- 修复：`WageGirlSystem.StealAI_Fence.cs` 新增 `CreateGenuineImmunivax()` = `InsInjectorHelper.CreateRealInjector()` + `SetGenuine()`（失败回退原路径）
- 修复版本：待发布

### BUG-007 物资箱可能为空箱
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）— 作者拍板：全部物资箱（不只流浪者）非空，1% 空箱保留
- 现象：开出来的物资箱是空的
- 根因：`CreateSupplyCrate()` 中 targetValue<1 时填充循环一次都不跑 → 空箱；此外违禁品偏好过滤可能把候选全滤掉 / UncheckedAccept 抛异常
- 修复：targetValue 下限 1 + 兜底循环（忽略违禁品偏好）强制至少 1 件；1% 空箱由上层概率控制
- 修复版本：待发布

### BUG-008 骰子对部分物品无法吞噬
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）
- 现象：夜晚拾荒带回的物品拖到骰子上吞不掉
- 根因：`IsNonPlayerOwned` 靠容器/位置推断归属，对夜间拾荒物品（afterhourInventory 混合已拥有/未拥有）判错
- 修复：改用 `GeneralHelper.IsItemOwned` ≡ `item.IsTag("IS_OWNED_TAG")`（原版归属标记，Patches.cs:1984 已用作买卖方向判据）。
  实锤：夜间拾荒走 `ItemSpawner.Spawn` → `DirectoryMaster.Item(id, isOwned:true)` → `SetItemOwned(true)`，确实带 IS_OWNED_TAG
- 修复版本：待发布

### BUG-009 蛙娘带回无法移动的场景物品
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）
- 现象：蛙娘带回 storage_bay / machine_bay_ext 之类建筑模块，只能摆场景、拖不进背包
- 根因：`FindItemNearValue` 第二轮遍历全物品缓存时只按类别 + 价值筛选，没有"场景固定物"过滤
- 修复：`EnsureItemCache()` 增加：`ContainerUpgradeV2.IsBuildingContainerId(id)` + `STANDARD_MACHINE_TAG` / `SYSTEM_TAG` / `SYSTEM_TAG_UTILITY` / `ITEM_HIDDEN_TAG` 全部跳过
- 修复版本：待发布

### BUG-010 没有饮品时蛙娘仍会偷喝
- 优先级：🟢 低
- 状态：已修复（09-23，commit 3121605）
- 现象：店里没有能喝的东西，蛙娘依然触发偷喝
- 根因：`DRINK_IDS` 含 `empty_beer_bottle` 等空容器，且部分饮品无水量/无卡路里记录 → `IsDrink` 判成饮品但实际一口都喝不到
- 修复：`StealItems()` drink 分支加 `HasDrinkContent()`（水量>0 或 卡路里>0）→ 候选为空则不偷不报
- 修复版本：待发布

### BUG-011 无卡路里值饮品喂蛙娘无效果
- 优先级：🟢 低
- 状态：已修复（09-23，commit 3121605）— 作者拍板：统一按价值恢复口渴
- 现象：酒/代饮品喂给蛙娘 → 物品消失但口渴一点不回
- 根因：`TryFeed()` 饮品分支 `int ml = GetWaterMl(item); if (ml <= 0) { item.Destroy(); return false; }`。
  酒类原生不写 `LIQUID_CONTAINER_CURRENT` → ml=0 → 直接销毁且不结算
- 修复：ml<=0 时改走"按价值恢复口渴"分支（当前价值/单位价值分档 25/18/12/6/2，与水纯度 5 档同量级），整件喝完消失。
  注：有卡路里的饮品会被上面的 `IsFood` 分支先接走，落到饮品分支且 ml=0 的基本都是无卡路里值饮品
- 修复版本：待发布

### BUG-012 蛙娘只有在不营业时才可被照顾
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）
- 现象：营业期间（柜台接客）无法喂食/照顾，只有打烊后才行
- 根因：`TryFeed()` 开头 `if (Patches.CurrentUITradeMode != 0) return false;` —— 营业期几乎全程开交易 UI
- 修复：拆成两段门禁。违禁品分支保留"交易中禁止"（原行为）；照顾分支放行，
  但新增 `IsPlayerOwnedForCare()`（`GeneralHelper.IsItemOwned` ≡ `IS_OWNED_TAG`）→ 买入模式下不会喂掉客户的货。
  `PostfixDoubleClickAction` 同样放行（面板内销赃/洗白按钮各自保留原有门禁）
- 修复版本：待发布

### BUG-013 吞噬季把节点也吞了
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）— 作者拍板：节点排除，不参与吞噬判定
- 现象：吞噬季把节点模组吃掉了
- 根因（[L1] ISIL 实锤 `ModuleDirectory.txt`）：`node_small`（:1051）/ `node_medium`（:1067）与 `system_module_*`、
  `chem_module`、`furnace_module_*` 在同一张模组注册表 → 节点在原生定义中就是模组（带 `MODULE_TAG`），
  会被 `ModCannibalism.CollectMachines()` 收进候选池
- 修复：`Patches.cs` 新增 `IsNodeModule(id)`（node / node_small / node_medium / `node_` 前缀），仅作用于吞噬季。
  刻意不改 `RobinCrusoePerk.IsExcludedModule`——那个还被炼蛊器与模组 tooltip 共用，改了会动到别的机制
- 修复版本：待发布

### BUG-014 命运骰子带有很多标签
- 优先级：🟠 中
- 状态：已修复（09-23，commit 3121605）
- 现象：骰子有时会变成"带一堆标签"的容器
- 根因：`CreateRegisteredDice()` 是 `destiny_dice` 的 DirectoryMaster 工厂（`DestinyDice.cs:2705` / `:2713`），
  创建失败时兜底返回 `DirectoryMaster.Item("simple_backpack")` —— 背包是容器，自带 CONTAINER_TAG 等一堆原生标签。
  读档反序列化骰子时也走这个工厂 → 拿到背包后再叠加存档里的骰子标签
- 修复：兜底改为 `CreateMinimalDice()`（只带 `DICE_ID` + `destiny_dice_tag` + 必需计数标签），工厂永不返回非骰子物品
- 修复版本：待发布
