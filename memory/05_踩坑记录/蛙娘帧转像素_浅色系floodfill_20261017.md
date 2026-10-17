# 踩坑：浅色系角色转像素帧必须用 floodfill + 生成立绘注意脚底地面线

> 2026-10-17 ｜ 场景：蛙娘状态帧转像素（form3_64x96，52 帧）｜ 涉及：convert_to_pixel_v3_floodfill.py、image_edit 生成立绘

## 结论（≤3 行）
1. 浅色系角色（白鞋/浅肤色）转像素帧，背景去除一律用 `convert_to_pixel_v3_floodfill.py`（PIL C 实现 floodfill，23 秒/批 vs 纯 Python 数分钟）——**近白阈值（≤18）方案对"浅灰蓝地面线"类残留无效**，floodfill 才能连根清。
2. image_edit 生成立绘时，prompt 必须显式写「纯白背景、角色脚底无地面线/阴影线」——walk 立绘脚底自带浅灰蓝地面线（曼哈顿距离 145），是本次残留白块根因。
3. 重转后验收标准：脚下残留块尺寸与 idle 同量级（≈47px，白鞋本身）即算干净；皮肤/发饰等浅色特征不得被 floodfill 误删。

## 根因
walk 立绘（image_edit 生成）脚底带浅灰蓝地面线，颜色离纯白太远，旧"近白"方案覆盖不到 → 残留成脚下白块（425px→修复后 60px）。

## 规避（下次开工前）
- 转任何浅色系角色帧：直接用 floodfill 版脚本，不试旧方案
- 生成立绘：prompt 加「纯白背景，角色脚底无地面线/阴影线」
- 验收：按 idle 基线比残留量

## 产物
- `ai_states_v2\form3_64x96\` 52 帧全干净（walk_0~3 2026-10-17 重转）
- `ai_states_v2\convert_to_pixel_v3_floodfill.py`（PIL 快版，后续重转都用它）

---
（总控代笔 · 2026-10-17）
