# Helper 复用指南（HELPERS GUIDE）

> 所有通用工具类都在这里。新功能优先用现成Helper，不要重复造轮子。
> 原则：能复用就复用，能组合就组合，不为单个效果新建类。

---

## 一、基础工具类

### TagHelper — tag 读写统一
```csharp
// 读
int v = TagHelper.GetInt(item, "MY_TAG");
bool has = TagHelper.Has(item, "MY_TAG");

// 写
TagHelper.SetInt(item, "MY_TAG", 100);
TagHelper.AddInt(item, "MY_TAG", 10);  // 加/减
```
**什么时候用**：任何需要读/写物品tag的场景。

---

### ItemHelper — 物品类型判定
```csharp
ItemHelper.IsFood(item);      // 是不是食物
ItemHelper.IsDrink(item);     // 是不是饮料
ItemHelper.IsDailyNeed(item); // 是不是日用品
```
**什么时候用**：蛙娘喂食/偷东西判定、鲁滨逊吃喝逻辑。

---

### LangHelper — 本地化
```csharp
string s = LangHelper.T("中文", "English");
```
**什么时候用**：所有UI文本，必须走双语。

---

## 二、交互类

### NotifyHelper — 通知/夜报统一
```csharp
NotifyHelper.Notify("即时通知");           // 弹窗通知
NotifyHelper.NightLog("夜间报告", "#7FC97F"); // 夜报三件套（通知+夜报+日历）
```
**什么时候用**：所有玩家可见的提示。夜间状态统一绿色 #7FC97F。

---

### UIHelper — 面板构建
```csharp
// 统一流式布局 + 样式
UIHelper.CreateButton(...);
UIHelper.CreateWindow(...);
```
**什么时候用**：任何自定义面板/窗口。

---

### NpcHelper — NPC调度
```csharp
NpcHelper.Schedule("npc_id", daysFromNow);      // 预约NPC
NpcHelper.ScheduleOnce("npc_id", "tag", firstDay, currentDay); // 预约+防重
```
**什么时候用**：胡安/李北文等NPC调度、蛙娘回归。

---

### MerchantHelper — 交易/议价
```csharp
MerchantHelper.GetNegotiatedValue(...);  // 议价后价格
MerchantHelper.GetDealMakerBonus(...);   // 议价成功率
```
**什么时候用**：价格修正、议价相关。

---

### MerchantHelper — 商人相关
```csharp
MerchantHelper.GetCustomerBudget(...);  // 客户预算
MerchantHelper.ApplyRepMultiplier(...);  // 声望修正
```
**什么时候用**：蛙娘在场客户预算×4、议价+50。

---

## 四、配置类

### ConfigHelper — 配置管理
```csharp
ConfigHelper.ContainerUpgradeEnabled;        // 容器升级开关
ConfigHelper.MachineContainerTemplateUpgrade; // 机器模板升级开关
```
**什么时候用**：所有可配置功能，统一从这里读。

---

### ContainerShapeHelper — 容器形状
```csharp
ContainerShapeHelper.GetGrid(item);     // 获取容器网格
ContainerShapeHelper.GetSize(inv);      // 读尺寸
ContainerShapeHelper.SetSize(inv, w, h); // 写尺寸
```
**什么时候用**：容器升级改尺寸。

---

### GameItemShapeHelper — 物品形状
```csharp
GameItemShapeHelper.GetShape(item);  // 读物品占地形状
GameItemShapeHelper.SetShape(item, shape); // 写物品占地形状
```
**什么时候用**：蛙娘2×3占地等。

---

## 六、其他
