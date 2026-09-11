using Bag;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using script.ExchangeMeeting.UI.Ctr;
using script.MenPaiTask;
using script.MenPaiTask.ZhangLao.UI;
using script.MenPaiTask.ZhangLao.UI.Base;
using script.MenPaiTask.ZhangLao.UI.Ctr;
using script.MenPaiTask.ZhangLao.UI.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace Yueyehuiling.MCS.AllInOneMod.Features.CopyMenpaiTask
{
    [HarmonyPatch(typeof(ElderTaskUI))]
    public static class CopyMenpaiTaskPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Show")]
        public static void ShowPostfix(ElderTaskUI __instance)
        {
            if (!Main.EnableCopyMenpaiTask.Value)
            {
                return;
            }
            if (__instance.任务列表列表 == null) return;

            // 从 Ctr 获取任务数据列表
            List<BaseElderTask> taskList = __instance.Ctr.TaskList;
            if (taskList == null || taskList.Count == 0) return;

            int taskIndex = 0;

            foreach (Transform child in __instance.任务列表列表)
            {
                // 跳过标题行
                if (child.name.Equals("已完成") || child.name.Equals("执行中") || child.name.Equals("待接取"))
                    continue;

                // 防止重复添加
                if (child.Find("CopyBtn") != null) continue;

                // 确保索引有效
                if (taskIndex >= taskList.Count) break;

                // 获取对应的任务数据
                BaseElderTask baseTask = taskList[taskIndex];
                if (baseTask == null || baseTask.ElderTask == null)
                {
                    taskIndex++;
                    continue;
                }

                List<BaseItem> needItems = baseTask.ElderTask.needItemList;
                if (needItems == null || needItems.Count == 0)
                {
                    taskIndex++;
                    continue;
                }

                int needCostTime = baseTask.ElderTask.NeedCostTime;
                int hasCostTime = baseTask.ElderTask.HasCostTime;

                // ====== 创建耗时显示文本 ======
                GameObject timeTextObj = new GameObject("TimeText", typeof(RectTransform), typeof(Text));
                timeTextObj.transform.SetParent(child, false);
                RectTransform timeRect = timeTextObj.GetComponent<RectTransform>();
                timeRect.anchoredPosition = new Vector2(190, -4); // 放在复制按钮左边或上方，需调整
                timeRect.sizeDelta = new Vector2(130, 30);

                Text timeText = timeTextObj.GetComponent<Text>();
                timeText.text = $"耗时：{hasCostTime}/{needCostTime}月";
                timeText.font = GetGameFont();
                timeText.fontSize = 22;
                timeText.alignment = TextAnchor.MiddleLeft;

                // ====== 创建复制按钮 ======
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
                rect.anchoredPosition = new Vector2(324, -4);  // 根据实际调整
                rect.sizeDelta = new Vector2(70, 35);

                // 2. 创建文字子物体（独立 GameObject）
                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(copyBtn.transform, false);

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
                var capturedNeedItems = needItems; // 闭包捕获
                btn.onClick.AddListener(() =>
                {
                    // 打开发布任务界面
                    ElderTaskUIMag.Inst.OpenCreateElderTaskUI();
                    CreateElderTaskCtr createCtr = ElderTaskUIMag.Inst.CreateElderTaskUI.Ctr;
                    createCtr.ClearItemList();

                    int slotIndex = 0;
                    foreach (BaseItem item in capturedNeedItems)
                    {
                        if (slotIndex >= createCtr.SlotList.Count) break;
                        ElderTaskSlot slot = createCtr.SlotList[slotIndex];

                        // ====== 使用 BaseItem.Create 创建物品 ======
                        // 参数：id, count, uuid（传null自动生成）, seid（传null）
                        BaseItem newItem = BaseItem.Create(item.Id, item.Count, null, null);
                        if (newItem != null)
                        {
                            slot.SetSlotData(newItem);
                            slotIndex++;
                        }
                    }

                    // 通过反射调用私有 UpdateData 方法
                    var method = createCtr.GetType().GetMethod("UpdateData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(createCtr, null);
                    }
                    createCtr.UI.UpdateUI();
                    UIPopTip.Inst.Pop($"已复制任务到发布界面，请确认后发布", 0);
                    createCtr.PublishTask();
                });

                taskIndex++;
            }
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