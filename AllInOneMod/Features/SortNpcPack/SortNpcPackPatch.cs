using Bag;
using HarmonyLib;
using JSONClass;
using script.NewLianDan;
using System;
using System.Collections.Generic;

namespace Yueyehuiling.MCS.AllInOneMod.Features.SortNpcPack
{
    public static class SortNpcPackPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(BaseBag2), "CreateTempList")]
        public static bool PrefixMethod(BaseBag2 __instance)
        {
            try
            {
                if (!Main.EnableSortNpcPack.Value)
                {
                    return true;
                }
                // 炼丹界面不执行
                if (LianDanUIMag.Instance != null && LianDanUIMag.Instance.gameObject.activeSelf)
                    return true;
                SortNpcPack(__instance.NpcId);
            }
            catch (Exception ex)
            {
                Main.LogError($"[SortNpcPack] 执行异常: {ex}");
            }
            return true;
        }

        public static void SortNpcPack(int npcId)
        {
            if (jsonData.instance.AvatarBackpackJsonData[npcId.ToString()] == null) return;
            JSONObject jSONObject = jsonData.instance.AvatarBackpackJsonData[npcId.ToString()]["Backpack"];
            if (jSONObject == null || jSONObject.Count < 1) return;

            // 1. 将所有物品拷贝到一个列表中
            List<JSONObject> allItems = new List<JSONObject>();
            foreach (JSONObject item in jSONObject.list)
            {
                // 可选：校验数据完整性，如果缺少 ItemID 或无效，跳过或报错
                if (!item.HasField("ItemID") || item["ItemID"].I < 1)
                {
                    Main.LogError($"[SortNpcPack] 整理NPC背包异常, npcId={npcId}");
                    return;
                }
                allItems.Add(item.Copy()); // 拷贝副本，避免影响原始数据
            }

            // 2. 排序（使用官方逻辑）
            allItems.Sort((a, b) => CompareItems(a, b));

            // 3. 重建背包
            jsonData.instance.AvatarBackpackJsonData[npcId.ToString()]["Backpack"] = new JSONObject();
            jSONObject = jsonData.instance.AvatarBackpackJsonData[npcId.ToString()]["Backpack"];
            foreach (JSONObject item in allItems)
            {
                jSONObject.Add(item);
            }
        }

        private static int CompareItems(JSONObject a, JSONObject b)
        {
            try
            {
                int idA = a["ItemID"].I;
                int idB = b["ItemID"].I;
                _ItemJsonData dataA = _ItemJsonData.DataDict[idA];
                _ItemJsonData dataB = _ItemJsonData.DataDict[idB];

                JSONObject seidA = a.HasField("Seid") ? a["Seid"] : null;
                JSONObject seidB = b.HasField("Seid") ? b["Seid"] : null;

                // 基础品级 & 哈希（用于打破平局）
                int qualityA = dataA.quality;
                int qualityB = dataB.quality;
                int hashA = dataA.GetHashCode();
                int hashB = dataB.GetHashCode();

                // Seid 覆盖品级
                if (seidA != null && seidA.HasField("quality"))
                {
                    qualityA = seidA["quality"].I;
                    hashA += seidA.GetHashCode();
                }
                if (seidB != null && seidB.HasField("quality"))
                {
                    qualityB = seidB["quality"].I;
                    hashB += seidB.GetHashCode();
                }

                // 类型修正
                if (dataA.type == 3 || dataA.type == 4) qualityA *= 2;
                if (dataB.type == 3 || dataB.type == 4) qualityB *= 2;
                if (dataA.type == 0 || dataA.type == 1 || dataA.type == 2) qualityA++;
                if (dataB.type == 0 || dataB.type == 1 || dataB.type == 2) qualityB++;

                // ① 按最终品级降序
                if (qualityA != qualityB)
                    return qualityB.CompareTo(qualityA);

                // ② 按类型升序
                if (dataA.type != dataB.type)
                    return dataA.type.CompareTo(dataB.type);

                // ③ 按 ID 升序
                if (dataA.id != dataB.id)
                    return dataA.id.CompareTo(dataB.id);

                // ④ 按哈希升序（保证稳定排序）
                return hashA.CompareTo(hashB);
            }
            catch
            {
                // 异常时返回 1，与官方一致（将 a 视为大于 b）
                return 1;
            }
        }
    }
}