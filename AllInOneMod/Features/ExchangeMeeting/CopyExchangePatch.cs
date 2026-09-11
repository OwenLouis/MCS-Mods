using Bag;
using HarmonyLib;
using script.ExchangeMeeting.Logic.Interface;
using script.ExchangeMeeting.UI.Ctr;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Yueyehuiling.MCS.AllInOneMod.Features.ExchangeMeeting
{
    [HarmonyPatch(typeof(PublishCtr))]
    public static class CopyExchangePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("CreatePlayerList")]
        public static void CreatePlayerListPostfix(PublishCtr __instance)
        {
            var parent = __instance.UI.ExchangeParent;
            if (parent == null) return;

            var playerList = IExchangeMag.Inst.ExchangeIO.GetPlayerList();
            if (playerList == null) return;

            var publishDataUI = __instance.UI.PublishDataUI;
            if (publishDataUI == null) return;

            int index = 0;
            int count = parent.childCount - playerList.Count;
            if (count < 0) count = 0;
            for (int i = count; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                // 跳过标题行
                if (child.name.Equals("已发布"))
                    continue;

                if (index >= playerList.Count) break;

                // 防止重复添加（如果之前添加过，先删除，保证每个条目只有一个按钮）
                Transform existingBtn = child.Find("CopyBtn");
                if (existingBtn != null)
                {
                    GameObject.Destroy(existingBtn.gameObject);
                }

                IExchangeData exchangeData = playerList[index];
                if (exchangeData == null)
                {
                    index++;
                    continue;
                }

                // 获取需求物品（取第一个）
                var needItems = GetNeedItems(exchangeData);
                if (needItems == null || needItems.Count == 0)
                {
                    index++;
                    continue;
                }
                BaseItem needItem = needItems[0];

                // 获取给出物品列表
                var giveItems = GetGiveItems(exchangeData);

                // 创建复制按钮
                GameObject copyBtn = new GameObject("CopyBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                copyBtn.transform.SetParent(child, false);

                // 设置背景颜色
                Image img = copyBtn.GetComponent<Image>();
                img.color = new Color(230f / 255f, 128f / 255f, 30f / 255f);

                // 添加边框（Outline）
                Outline outline = copyBtn.AddComponent<Outline>();
                outline.effectColor = Color.white;         // 边框颜色
                outline.effectDistance = new Vector2(1, -1); // 边框偏移量（宽度）

                RectTransform rect = copyBtn.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(50, 0); // 根据实际调整
                rect.sizeDelta = new Vector2(80, 40);

                // 2. 创建文字子物体（独立 GameObject）
                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(copyBtn.transform, false);

                // 文字组件设置
                Text text = textObj.GetComponent<Text>();
                text.text = "复制";
                text.font = GetGameFont();
                text.fontSize = 22;
                text.alignment = TextAnchor.MiddleCenter;

                // 文字填满按钮区域
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                Button btn = copyBtn.GetComponent<Button>();
                // 闭包捕获
                var capturedNeedItem = needItem;
                var capturedGiveItems = giveItems;
                var capturedPublishDataUI = publishDataUI;
                var capturedCtr = __instance;
                btn.onClick.AddListener(() =>
                {
                    // 1. 清空所有（需求和给出）
                    capturedPublishDataUI.Clear();
                    // 2. 设置需求
                    capturedCtr.PutNeedItem(capturedNeedItem.Clone());
                    // 3. 填充给出物品
                    if (capturedGiveItems != null)
                    {
                        int slotIndex = 0;
                        foreach (var giveItem in capturedGiveItems)
                        {
                            if (slotIndex >= capturedPublishDataUI.GiveItems.Count) break;
                            var slot = capturedPublishDataUI.GiveItems[slotIndex];
                            if (giveItem != null)
                            {
                                slot.SetSlotData(giveItem.Clone());
                            }
                            slotIndex++;
                        }
                    }
                    // 4. 刷新UI（更新按钮状态和抽成）
                    capturedPublishDataUI.UpdateUI();
                });

                index++;
            }
        }

        // 辅助方法：从 IExchangeData 获取需求物品列表
        private static List<BaseItem> GetNeedItems(IExchangeData data)
        {
            if (data == null) return null;
            Type t = data.GetType();
            // 尝试属性 NeedItems
            PropertyInfo prop = t.GetProperty("NeedItems");
            if (prop != null)
            {
                object val = prop.GetValue(data);
                if (val is List<BaseItem> list) return list;
            }
            // 尝试方法 GetNeedItems
            MethodInfo method = t.GetMethod("GetNeedItems");
            if (method != null)
            {
                object result = method.Invoke(data, null);
                if (result is List<BaseItem> list2) return list2;
            }
            // 尝试字段 NeedItems
            FieldInfo field = t.GetField("NeedItems");
            if (field != null)
            {
                object val2 = field.GetValue(data);
                if (val2 is List<BaseItem> list3) return list3;
            }
            return null;
        }

        // 辅助方法：从 IExchangeData 获取给出物品列表
        private static List<BaseItem> GetGiveItems(IExchangeData data)
        {
            if (data == null) return null;
            Type t = data.GetType();
            PropertyInfo prop = t.GetProperty("GiveItems");
            if (prop != null)
            {
                object val = prop.GetValue(data);
                if (val is List<BaseItem> list) return list;
            }
            MethodInfo method = t.GetMethod("GetGiveItems");
            if (method != null)
            {
                object result = method.Invoke(data, null);
                if (result is List<BaseItem> list2) return list2;
            }
            FieldInfo field = t.GetField("GiveItems");
            if (field != null)
            {
                object val2 = field.GetValue(data);
                if (val2 is List<BaseItem> list3) return list3;
            }
            return null;
        }

        private static Font GetGameFont()
        {
            Text[] allTexts = UnityEngine.Object.FindObjectsOfType<Text>();
            foreach (Text t in allTexts)
            {
                if (t.font != null)
                    return t.font;
            }
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) return font;
            try { return Font.CreateDynamicFontFromOSFont("Arial", 14); }
            catch { return null; }
        }
    }
}
