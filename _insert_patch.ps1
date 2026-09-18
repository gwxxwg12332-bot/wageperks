$p = "D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\Patches.cs"
$t = [IO.File]::ReadAllText($p)
$marker = "internal static class ModCannibalism"
$idx2 = $t.IndexOf($marker)
if ($idx2 -lt 0) { "MARKER_NOT_FOUND"; exit 1 }
$idx3 = $t.LastIndexOf("}", $idx2)

$newBlock = @'


	// 09-22 制卡降上城区声望根因修复：mod 违禁品跳过"客户曝光"链（ClientExposeFeature）
	// 曝光链（拆包实锤）：PlacedItemForBuying → CanClientExposeAnyFeature → ClientExposeFeature →
	//   CanClientExposeThisFeature → 对话 + StoreReputation.ModReputation(客户faction, -4, true) + ExposeFeature(词条移除)
	// mod 违禁品（wage_ 前缀 / 吞噬融合 CANNIBALISM_VALUE / 电池融合 BREEDER_POWER_SOURCE_ITEM_TAG）
	// 被客户浏览即曝光 → 扣该客户 faction 声望 -4 + 词条划掉。跳过曝光：不扣声望、词条保留、违禁品打标保留。
	public static bool PrefixClientExposeFeature(GameItem gameItem)
	{
		try
		{
			if (gameItem == null) return true;
			string id = "";
			try { id = gameItem.identifier ?? ""; } catch { }
			bool isWage = id.StartsWith("wage_") || gameItem.IsTag("CANNIBALISM_VALUE") || gameItem.IsTag("BREEDER_POWER_SOURCE_ITEM_TAG");
			if (isWage)
			{
				Core.LogMsg("[声望修复] " + id + " 是 mod 物品，跳过客户曝光（不再扣声望/划词条）");
				return false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[声望修复] Prefix异常: " + ex.Message);
		}
		return true;
	}
	// 诊断（用完删）：static ModReputation(String,int,bool) 日志——验证曝光扣声望走 static 版（value=-4）
	public static void PrefixModReputationStatic(string factionId, int value)
	{
		try { Core.LogMsg("[声望诊断] ModReputationStatic faction=" + factionId + " value=" + value); } catch { }
	}
'@

$t2 = $t.Substring(0, $idx3) + $newBlock + "}" + $t.Substring($idx2)
[IO.File]::WriteAllText($p, $t2, (New-Object Text.UTF8Encoding $false))
"OK 已插入 LEN=$($t2.Length)"
