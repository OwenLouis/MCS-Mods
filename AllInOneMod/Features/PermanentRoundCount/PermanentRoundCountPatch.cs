using HarmonyLib;
using UnityEngine;
using YSGame.Fight;

namespace Yueyehuiling.MCS.AllInOneMod.Features.PermanentRoundCount
{
    [HarmonyPatch(typeof(UIFightRoundCount), "ShowRuond")]
    public static class PermanentRoundCountPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UIFightRoundCount __instance, int i)
        {
            // 直接设置文本和透明度，常驻显示
            __instance.RoundCountText.text = $"第{i}回合";
            __instance.RoundCountBG.color = Color.white;
            __instance.RoundCountText.color = Color.white;

            // 使用 Traverse 设置私有字段 nowMoving = false，防止状态锁定
            Traverse.Create(__instance).Field("nowMoving").SetValue(false);

            // 跳过原始方法（不执行动画）
            return false;
        }
    }
}
