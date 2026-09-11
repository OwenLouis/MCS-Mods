using HarmonyLib;

namespace Yueyehuiling.MCS.AllInOneMod.Features.RemoveSeaFog
{
    [HarmonyPatch(typeof(MapPlayerSeaShow))]
    public static class RemoveSeaFogPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Refresh")]
        public static void RefreshPostfix(MapPlayerSeaShow __instance)
        {
            if (__instance.SeaZheZhao != null)
                __instance.SeaZheZhao.gameObject.SetActive(false);
        }
    }
}