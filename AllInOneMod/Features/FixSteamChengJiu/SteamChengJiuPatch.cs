using HarmonyLib;
using KBEngine;

namespace Yueyehuiling.MCS.AllInOneMod.Features.FixSteamChengJiu
{
    public static class SteamChengJiuPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamChengJiu), "FightSkillAllDamageSetStat")]
        public static bool Prefix(Avatar avatar, int skillType, int damage)
        {
            if (damage < 0)
            {
                // 负伤害（治疗）不统计到成就累计伤害中
                return false; // 跳过原方法
            }
            return true; // 允许正常执行
        }
    }
}
