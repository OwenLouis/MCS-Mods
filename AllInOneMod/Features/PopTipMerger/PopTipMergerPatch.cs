using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yueyehuiling.MCS.AllInOneMod.Features.PopTipMerger
{
    [HarmonyPatch]
    public static class PopTipMergerPatch
    {
        private static Dictionary<string, int> mergeDict = new Dictionary<string, int>();
        private static float mergeTimer = 0f;
        private static bool hasPending = false;
        private static bool isMerging = false;

        // 拦截不带音效的 Pop
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIPopTip), "Pop", new Type[] { typeof(string), typeof(PopTipIconType) })]
        public static bool PrefixPop(string msg, PopTipIconType iconType)
        {
            // 跳过包含“存档”字样的消息（不合并，直接显示）
            if (!string.IsNullOrEmpty(msg) && msg.Contains("存档"))
                return true;
            if (isMerging) return true; // 放行，避免递归
            if (string.IsNullOrEmpty(msg)) return true;

            string key = $"{(int)iconType}|{msg}";
            if (mergeDict.ContainsKey(key))
                mergeDict[key]++;
            else
                mergeDict[key] = 1;

            mergeTimer = Main.MergeInterval.Value;
            hasPending = true;
            return false; // 跳过原方法
        }

        // 拦截带音效的 Pop（不合并，直接放行）
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIPopTip), "Pop", new Type[] { typeof(string), typeof(string), typeof(PopTipIconType) })]
        public static bool PrefixPopSound(string msg, string sound, PopTipIconType iconType)
        {
            return true; // 放行
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIPopTip), "Update")]
        public static void UpdatePostfix(UIPopTip __instance)
        {
            if (!hasPending) return;

            mergeTimer -= Time.deltaTime;
            if (mergeTimer <= 0f)
            {
                isMerging = true;
                foreach (var kv in mergeDict)
                {
                    string[] parts = kv.Key.Split('|');
                    PopTipIconType iconType = (PopTipIconType)int.Parse(parts[0]);
                    string msg = parts[1];
                    if (kv.Value > 1)
                        msg = $"{msg} x{kv.Value}";
                    __instance.Pop(msg, iconType);
                }
                isMerging = false;
                mergeDict.Clear();
                hasPending = false;
            }
        }
    }
}
