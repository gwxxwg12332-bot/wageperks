using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 狄仁杰之手：罪证收集速度 +50%，免疫王尔德销毁
internal sealed class DetectivePerk : CustomStartingPerk
{
    internal const string PerkId = "狄仁杰之手";
    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("狄仁杰之手", "Detective's Touch");
    internal override string Description => LangHelper.T(
        "罪证收集速度 +50%。卖违禁品/赃物每累计 5000 信用点，打烊时证据条额外 +15。免疫王尔德之手的证据销毁，购买清除证据服务也不会清除证据。（治安部复刻了那位神探的手段，你经手的每一件来路不正的货都被治安部记档在案——销赃越多，把柄越多。）",
        "Evidence collection speed +50%. Every 5000 credits of contraband/stolen goods sold adds +15 to the evidence bar at closing. Immune to Wilde's evidence destruction, and buying evidence removal service won't clear evidence either. (Security has replicated the great detective's methods — every shady deal you handle is logged; the more you fence, the more evidence they hold on you.)");
    internal override int Cost => -15;  // 09-22 用户拍板：-5→-15
    internal override int Type => 1;  // 09-22 用户拍板：红色

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    internal override void OnNewGame() { }
}