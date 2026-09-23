# Top 10 踩坑记录（最容易复犯）

## 1. GameMaster.NewGame 是死方法
- **问题**：Postfix挂在GameMaster.NewGame上不触发
- **原因**：全ISIL dump 0调用点，游戏根本不调它
- **解决**：挂PlayerStore.StartNewGame Postfix（真实调用链）

## 2. GetCalorie() 读tag不对
- **问题**：卡路里读出来不对
- **原因**：CALORIE_VALUE_TAG
- **解决**：用RobinCrusoePerk.GetCalLeft()

## 3. shape被SetSpriteAndShape覆盖
- **问题**：设了2×3 shape又变回1×1
- **原因**：ApplyIcon里的SetSpriteAndShape会按sprite尺寸重算shape
- **解决**：ApplyIcon之后再补SetShape(2×3)

## 4. PlayerPrefs直写有问题
- **问题**：状态读档不对
- **解决**：统一走WageSaveStore，所有状态只在打烊落盘，读档清内存缓存

## 5. default_run残留
- **问题**：新档继承旧档状态
- **解决**：新档CleanDefaultRun()清残留

## 6. static缓存跨读档残留
- **问题**：喂食后不存档退出重进，饱食度不回退
- **解决**：PostfixOnLoadGame里ClearMemStats()

## 7. Harmony Postfix在原生存档之后跑
- **问题**：修改不落盘
- **解决**：必须改Prefix

## 8. node_small/medium是MODULE_TAG
- **问题**：吞噬季吞噬节点
- **解决**：加IsNodeModule排除

## 9. Copy-Item -Force不覆盖
- **问题**：DLL MD5不一致
- **解决**：先删旧文件再复制

## 10. 读档恢复挂点不可靠
- **问题**：ModHook事件不触发
- **解决**：挂PostfixOnLoadGame（确定性）
