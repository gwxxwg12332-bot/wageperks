# 游戏API签名清单

> 所有签名均经 dump.cs / ISIL 实锤。按类分组，方便 grep。

---

## StoreClient（客户）

### 字段
```csharp
public int clientCash;        // 0x30 客户现金
private int clientBudget;     // 0x34 客户预算
public bool useClientBudget;  // 0x38 是否使用客户预算
```

### 方法
```csharp
public void SetBudget(int budget);
// 行为：写 clientBudget → 设 useClientBudget=1 → 调 ApplyBudgetModifier()

public void OverrideBudget(int budget);
// 行为：只写 clientBudget，不调 ApplyBudgetModifier

public void ModBudget(int budgetMod);
// 行为：clientBudget += budgetMod，不调 ApplyBudgetModifier

public int GetBudget();
// 行为：直接读 clientBudget 字段

public void SetClientBudget(int amount, int additionalRange);
// 行为：设置客户预算（含UI显示），创建客户时由 StoreClientList 调用

public void ApplyBudgetModifier();
// 行为：按特权+店铺吸引力分档修正预算，只加不减

public void OnItemSold(GameItem item, int soldAmount);
// 行为：卖出后预算增加（Add）

public void OnItemBought(GameItem item, int cost);
// 行为：买入后触发回调，不修改预算
```

---

## PlayerStore（店铺）

### 生命周期挂点
```csharp
public void StartNewGame();
// 新档初始化真实挂点（Postfix）

public void LoadGame();
// 读档确定性挂点（Postfix）

public void SaveGame();
// 打烊存档挂点（Postfix）

public int GetCurrentStoreAttractiveness();
// 获取店铺吸引力值（ApplyBudgetModifier 分档用）

public void QueueFuturClient(string id, int days);
// 叫客：N天后生成指定客户
```

---

## StoreClientManager（客户调度）

### 搜查相关
```csharp
public void CreateInspectionClient(int personality = 0, bool fromShowcase = false);
// 创建治安官客户（DebugTool一键触发用）

public void HandleInspectionClient();
// 处理治安官进店搜查
```

### 字段
```csharp
public int dayUntilInspection;   // 0x5C 距离下次搜查天数
public int daySinceInspection;   // 0x58 距离上次搜查天数
public bool inspectionSeeded;    // 0x60 是否已播种搜查
```

---

## StoreReputation（声望）

```csharp
public static bool IsPerkUnlocked(string perkId);
// 检查特权是否解锁（ApplyBudgetModifier 用）
// 已确认的特权ID：UL_TOP_REVIEW、SEC_RELIABLE_VENDOR

public void ModReputation(string factionId, int amount, bool something);
// 修改声望
// 已确认 factionId：LL/UL/SEC/BM/REV
```

---

## GameItem（物品）

### 形状
```csharp
public GridShape shape;           // 0x198 逻辑形状
public GridShape modifiedShape;    // 0x1A0 UI显示权威形状

public void SetShape(int width, int height);
// 只写 shape，不改 modifiedShape

public void SetSpriteAndShape(string atlas, string name);
// 成功路径：解析sprite → 从像素÷16算GridShape → 写modifiedShape
// 失败路径（LoadFromAtlas返回null）：不改modifiedShape，无异常
```

### 标签
```csharp
public bool IsTag(string tag);
public void EnableTag(string tag, bool state);
public void DisableTag(string tag);
```

### 拖拽
```csharp
public bool MayTarget(GameObject target);
public bool CanTarget(GameObject target);
public void Target(GameObject target);
```

---

## ContrabandHelper（违禁品）

```csharp
public static int GetContrabandLevel(GameItem item);
// 返回违禁品等级（0=合法，>0=违禁）
```

---

## WaterHelper（水）

```csharp
public static int GetWaterPurity(GameItem item);
// 返回水质（µl单位，越高越纯）

public static void Remove(GameItem item, int microLiters);
// 扣除水量（µl单位）
```

---

## ItemMouseDragHandler（拖拽）

```csharp
public static ItemMouseDragHandler current;
public bool IsDraggingItem { get; }
// 判定是否正在拖物品
```

---

## 不可用挂点（避雷）

| 方法 | 原因 |
|---|---|
| GameMaster.NewGame | 0 ISIL调用点，从不触发 |
| ModHook.OnGameLoadedNormal | 事件不可靠，多次测试不触发 |
| MelonLoader.OnUpdate | 每帧逻辑禁用，走InputActionManager.Update Postfix |

---

## 容器内部读取

### PixelWindow
```csharp
// PixelWindow 无 inventory 字段
// 正确读取：
var grid = pixelWindow.childElement.Cast<GameGridInventory>();

// GetAllItems() 不递归容器内部！
// 暗格/箱子内部物品要额外遍历 childElement
```
