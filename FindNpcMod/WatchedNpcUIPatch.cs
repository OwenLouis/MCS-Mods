using HarmonyLib;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using GameObject = UnityEngine.GameObject;

namespace Yueyehuiling.MCS.FindNpcMod
{
    [HarmonyPatch(typeof(UIMiniTaskPanel))]
    public static class WatchedNpcUIPatch
    {
        private const string BlockName = "FindNpc_Watched";
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

            // 移除已死亡的关注 NPC
            Main.RemoveDeadNpcs();

            Transform original = panel.ZhuiZongObj.transform;
            Transform parent = original.parent;
            if (parent == null) return;

            var watchedIds = Main.GetWatchedNpcs();
            if (watchedIds.Count == 0)
            {
                var blockTemp = parent.Find(BlockName);
                if (blockTemp != null) blockTemp.gameObject.SetActive(false);
                return;
            }

            var displayList = watchedIds.Count > MaxDisplay ? watchedIds.GetRange(0, MaxDisplay) : watchedIds;
            int extra = watchedIds.Count - displayList.Count;

            Transform block = parent.Find(BlockName);
            if (block == null)
            {
                GameObject go = UnityEngine.Object.Instantiate(original.gameObject, parent);
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

            // 标题
            var title = block.Find("Title");
            if (title != null)
            {
                var titleText = title.GetComponent<Text>();
                if (titleText != null) titleText.text = "关注人物";
            }

            // 内容文本
            var taskText = block.Find("TaskText");
            var text = taskText?.GetComponent<Text>();
            if (text == null)
            {
                block.gameObject.SetActive(false);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (int npcId in displayList)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Main.GetNpcBriefInfo(npcId));
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

            // 调整位置：放在寻物志块下方，或原生任务块下方
            RectTransform blockRect = block as RectTransform;
            if (blockRect != null)
            {
                Canvas.ForceUpdateCanvases();

                float lowestY = 0f;
                Vector3[] corners = new Vector3[4];
                bool found = false;

                // 1. 优先查找寻物志块（XWZ_Track）
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

                // 2. 如果没有寻物志，使用原生任务块
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

            if (!parent.gameObject.activeSelf)
                parent.gameObject.SetActive(true);
        }
    }
}