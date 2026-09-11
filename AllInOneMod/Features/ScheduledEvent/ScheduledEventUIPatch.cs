using HarmonyLib;
using KBEngine;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Yueyehuiling.MCS.AllInOneMod.Features.ScheduledEvent
{
    [HarmonyPatch(typeof(UIMiniTaskPanel))]
    public static class ScheduledEventUIPatch
    {
        private const string BlockName = "AIO_ScheduledEvent";
        private const int MaxDisplay = 8;

        [HarmonyPostfix]
        [HarmonyPatch("RefreshTaskZhuiZong")]
        public static void RefreshTaskZhuiZongPostfix(UIMiniTaskPanel __instance)
        {
            Apply(__instance);
        }

        public static void Apply(UIMiniTaskPanel panel)
        {
            if (panel == null || panel.ZhuiZongObj == null) return;

            Transform original = panel.ZhuiZongObj.transform;
            Transform parent = original.parent;
            if (parent == null) return;

            // 获取传音列表中的预定事件
            var events = GetScheduledEvents();
            if (events.Count == 0)
            {
                var tempBlock = parent.Find(BlockName);
                if (tempBlock != null) tempBlock.gameObject.SetActive(false);
                return;
            }

            var displayList = events.Count > MaxDisplay ? events.GetRange(0, MaxDisplay) : events;
            int extra = events.Count - displayList.Count;

            // 查找或创建UI块
            Transform block = parent.Find(BlockName);
            if (block == null)
            {
                UnityEngine.GameObject go = UnityEngine.Object.Instantiate(original.gameObject, parent);
                go.name = BlockName;
                RectTransform rect = go.transform as RectTransform;
                if (rect != null)
                {
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                }
                block = go.transform;
            }
            block.gameObject.SetActive(true);

            // 设置标题
            var title = block.Find("Title");
            if (title != null)
            {
                var titleText = title.GetComponent<Text>();
                if (titleText != null) titleText.text = "传音预告";
            }

            // 构建内容
            var taskText = block.Find("TaskText");
            var text = taskText?.GetComponent<Text>();
            if (text == null)
            {
                block.gameObject.SetActive(false);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var evt in displayList)
            {
                if (sb.Length > 0) sb.Append('\n');
                string timeStr = $"{evt.SendTime.Year}年{evt.SendTime.Month}月{evt.SendTime.Day}日";
                sb.Append("时间：").Append(timeStr).Append("\n内容：").Append(evt.ItemName);
            }
            if (extra > 0)
            {
                sb.Append('\n').Append("<color=#B1A079>…还有 ").Append(extra).Append(" 个</color>");
            }

            string newText = sb.ToString();
            if (text.text != newText)
            {
                text.text = newText;
                Canvas.ForceUpdateCanvases();

                // 动态调整高度（参考原代码）
                float preferredHeight = text.preferredHeight;
                RectTransform bg = block.Find("Mask/TaskTextBG") as RectTransform;
                if (bg != null)
                {
                    bg.anchoredPosition = new Vector2(bg.anchoredPosition.x, -preferredHeight - 49f);
                    float y = Mathf.Max(149f, preferredHeight + 80f);
                    bg.sizeDelta = new Vector2(bg.sizeDelta.x, y);
                    RectTransform parentRect = bg.parent as RectTransform;
                    if (parentRect != null) parentRect.sizeDelta = new Vector2(parentRect.sizeDelta.x, y);
                }
            }

            // 调整位置：放在关注NPC块下方，或寻物志块下方，或原生任务块下方
            RectTransform blockRect = block as RectTransform;
            if (blockRect != null)
            {
                Canvas.ForceUpdateCanvases();

                float lowestY = 0f;
                Vector3[] corners = new Vector3[4];
                bool found = false;

                // 1. 查找关注NPC块（FindNpc_Watched）
                Transform watchedBlock = parent.Find("FindNpc_Watched");
                if (watchedBlock != null && watchedBlock.gameObject.activeSelf)
                {
                    Transform bg = watchedBlock.Find("Mask/TaskTextBG");
                    if (bg != null)
                    {
                        RectTransform bgRect = bg as RectTransform;
                        if (bgRect != null)
                        {
                            bgRect.GetWorldCorners(corners);
                            lowestY = parent.InverseTransformPoint(corners[0]).y;
                            found = true;
                        }
                    }
                }

                // 2. 如果没有关注NPC，查找寻物志块（XWZ_Track）
                if (!found)
                {
                    Transform xwzBlock = parent.Find("XWZ_Track");
                    if (xwzBlock != null && xwzBlock.gameObject.activeSelf)
                    {
                        Transform bg = xwzBlock.Find("Mask/TaskTextBG");
                        if (bg != null)
                        {
                            RectTransform bgRect = bg as RectTransform;
                            if (bgRect != null)
                            {
                                bgRect.GetWorldCorners(corners);
                                lowestY = parent.InverseTransformPoint(corners[0]).y;
                                found = true;
                            }
                        }
                    }
                }

                // 3. 如果都没有，使用原生任务块
                if (!found)
                {
                    foreach (Transform child in parent)
                    {
                        if (child == block || !child.gameObject.activeSelf) continue;
                        string childName = child.name;
                        if (childName.Contains("任务追踪"))
                        {
                            Transform bg = child.Find("Mask/TaskTextBG");
                            if (bg != null)
                            {
                                RectTransform bgRect = bg as RectTransform;
                                if (bgRect != null)
                                {
                                    bgRect.GetWorldCorners(corners);
                                    float y = parent.InverseTransformPoint(corners[0]).y;
                                    if (!found || y < lowestY)
                                    {
                                        lowestY = y;
                                        found = true;
                                    }
                                }
                            }
                        }
                        else if (childName.Contains("杀手追踪"))
                        {
                            RectTransform bgRect = child as RectTransform;
                            if (bgRect != null)
                            {
                                bgRect.GetWorldCorners(corners);
                                float y = parent.InverseTransformPoint(corners[0]).y;
                                if (!found || y < lowestY)
                                {
                                    lowestY = y;
                                    found = true;
                                }
                            }
                        }
                    }
                }

                if (!found)
                {
                    blockRect.anchoredPosition = new Vector2(blockRect.anchoredPosition.x, -24.5f);
                }
                else
                {
                    float targetTopY = lowestY - 25f;
                    blockRect.anchoredPosition = new Vector2(blockRect.anchoredPosition.x, targetTopY);
                }

                block.SetAsLastSibling();
            }

            // 确保父容器激活
            if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
        }

        private static List<ScheduledEventData> GetScheduledEvents()
        {
            List<ScheduledEventData> list = new List<ScheduledEventData>();
            Avatar player = PlayerEx.Player;
            if (player == null) return list;

            var dict = player.NoGetChuanYingList;
            if (dict == null || dict.Count == 0) return list;

            foreach (var key in dict.keys)
            {
                JSONObject obj = dict[key];
                if (obj == null) continue;

                //跳过非天衍阁管事的传音
                int avatarID = obj["AvatarID"].I;
                if (avatarID != 912) continue;

                string info = obj["info"].str;
                if (string.IsNullOrEmpty(info)) continue;

                // 从info中提取物品名称
                string itemName = ExtractItemName(info);
                if (string.IsNullOrEmpty(itemName)) continue;

                string sendTimeStr = obj["sendTime"].str;
                DateTime sendTime = ParseSendTime(sendTimeStr);

                list.Add(new ScheduledEventData(itemName, sendTime));
            }

            // 按时间升序排列
            list.Sort((a, b) => a.SendTime.CompareTo(b.SendTime));
            return list;
        }

        private static string ExtractItemName(string info)
        {
            if (string.IsNullOrEmpty(info)) return "未知物品";

            // 示例："道友，你委托我天机阁寻找的地心火芝已然寻获"
            int start = info.IndexOf("寻找的");
            if (start >= 0)
            {
                start += 3;
                int end = info.IndexOf("已然", start);
                if (end < 0) end = info.IndexOf("寻获", start);
                if (end > start)
                {
                    return info.Substring(start, end - start);
                }
            }
            return "未知物品";
        }

        private static DateTime ParseSendTime(string str)
        {
            string[] parts = str.Split(' ');
            string datePart = parts[0];
            string[] segs = datePart.Split('/');
            if (segs.Length == 3)
            {
                int year = int.Parse(segs[0]);
                int month = int.Parse(segs[1]);
                int day = int.Parse(segs[2]);
                return new DateTime(year, month, day);
            }
            return DateTime.MinValue;
        }
    }

    public class ScheduledEventData
    {
        public string ItemName { get; set; }
        public DateTime SendTime { get; set; }

        public ScheduledEventData(string itemName, DateTime sendTime)
        {
            ItemName = itemName;
            SendTime = sendTime;
        }
    }
}