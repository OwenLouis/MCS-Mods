using HarmonyLib;
using System;
using UnityEngine;
using Ventulus;
using UnityEngine.UI;

namespace Yueyehuiling.MCS.AllInOneMod.Features.BiGuanExpectedDate
{
    [HarmonyPatch(typeof(UIBiGuanXiuLianPanel))]
    internal class BiGuanExpectedDatePatch
    {
        private static Transform expectedDateTransform = null;
        private static Text expectedDateText = null;

        [HarmonyPostfix]
        [HarmonyPatch("RefreshUI")]
        public static void RefreshUI_Postfix(UIBiGuanXiuLianPanel __instance)
        {
            if (!Main.EnableBiGuanExpectedDate.Value) return;
            if (expectedDateTransform != null) return;

            Transform parent = __instance.TimeSlider.transform.parent;
            if (parent == null) return;
            Text yearText = __instance.YearText;
            if (yearText == null) return;

            GameObject template = yearText.gameObject;
            GameObject newObj = UnityEngine.Object.Instantiate(template, parent);
            newObj.name = "ExpectedDate";
            RectTransform rect = newObj.GetComponent<RectTransform>();
            // 下移更多
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y - 90f);

            Text text = newObj.GetComponent<Text>();
            text.text = "预计日期：";
            text.color = Color.black;      // 黑色
            text.fontSize = 22;            // 调小字体
            expectedDateText = text;
            expectedDateTransform = newObj.transform;

            UpdateExpectedDate(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnTimeSliderValueChanged")]
        public static void OnTimeSliderValueChanged_Postfix(UIBiGuanXiuLianPanel __instance)
        {
            if (!Main.EnableBiGuanExpectedDate.Value) return;
            UpdateExpectedDate(__instance);
        }

        private static void UpdateExpectedDate(UIBiGuanXiuLianPanel __instance)
        {
            if (expectedDateText == null) return;
            int biGuanTime = Traverse.Create(__instance).Field("biGuanTime").GetValue<int>();
            if (biGuanTime <= 0)
            {
                expectedDateText.text = "闭关0月";
                return;
            }
            DateTime now = VTools.NowTime;
            DateTime finish = now.AddMonths(biGuanTime);
            string dateStr = string.Format("{0}年{1}月{2}日", finish.Year, finish.Month, finish.Day);
            expectedDateText.text = $"预计日期：{dateStr}";
        }
    }
}
