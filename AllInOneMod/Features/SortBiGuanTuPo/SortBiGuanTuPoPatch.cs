using GUIPackage;
using HarmonyLib;
using KBEngine;
using script.ExchangeMeeting.UI.Ctr;

namespace Yueyehuiling.MCS.AllInOneMod.Features.SortBiGuanTuPo
{
    [HarmonyPatch(typeof(UIBiGuanTuPoPanel))]
    public static class SortBiGuanTuPoPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("RefreshInventory")]
        public static void RefreshInventoryPrefix()
        {
            if (!Main.EnableSortBiGuanTuPo.Value)
            {
                return;
            }
            Avatar player = PlayerEx.Player;
            if (player != null && player.hasStaticSkillList != null)
            {
                // 先按突破层数降序，再按品级降序
                player.hasStaticSkillList.Sort((a, b) =>
                {
                    // 1. 突破层数降序（高等级在前）
                    int levelCompare = b.level.CompareTo(a.level);
                    if (levelCompare != 0) return levelCompare;

                    // 获取功法数据
                    var skillA = SkillStaticDatebase.instence.Dict[a.itemId][a.level];
                    var skillB = SkillStaticDatebase.instence.Dict[b.itemId][b.level];

                    // 2. SkillQuality 降序
                    int qualityCompare = skillB.SkillQuality.CompareTo(skillA.SkillQuality);
                    if (qualityCompare != 0) return qualityCompare;

                    // 3. typePinJie 降序（从 StaticSkillJsonData 获取）
                    int typePinJieA = jsonData.instance.StaticSkillJsonData[skillA.skill_ID.ToString()]["typePinJie"].I;
                    int typePinJieB = jsonData.instance.StaticSkillJsonData[skillB.skill_ID.ToString()]["typePinJie"].I;
                    return typePinJieB.CompareTo(typePinJieA);
                });
            }
        }
    }
}
