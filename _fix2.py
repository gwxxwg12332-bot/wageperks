# -*- coding: utf-8 -*-
# 鲁滨逊容器系统排除判定：CUSTOM_STORAGE_TAG 换 IsWageBox（老档蛙哥箱缺 tag 兜底）
p = 'RobinCrusoePerk.cs'
t = open(p, encoding='utf-8').read()

subs = [
    ('__1.IsTag("VOID_BEAD_TAG") || __1.IsTag("CUSTOM_STORAGE_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(__1)',
     '__1.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsWageBox(__1) || ContainerUpgradeV2.IsVoidBeadStorage(__1)'),
    ('item.IsTag("VOID_BEAD_TAG") || item.IsTag("CUSTOM_STORAGE_TAG") || ContainerUpgradeV2.IsVoidBeadStorage(item)',
     'item.IsTag("VOID_BEAD_TAG") || ContainerUpgradeV2.IsWageBox(item) || ContainerUpgradeV2.IsVoidBeadStorage(item)'),
]
for old, new in subs:
    if old in t:
        t = t.replace(old, new, 1)
        print('OK:', old[:70])
    else:
        print('MISS:', old[:70])
open(p, 'w', encoding='utf-8', newline='').write(t)
print('完成')
