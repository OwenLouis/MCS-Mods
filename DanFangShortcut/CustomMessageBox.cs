using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;
using System;

namespace Yueyehuiling.MCS.DanFangShortcut
{
    public static class CustomMessageBox
    {
        private static GameObject currentDialog;

        private static Font GetGameFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) return font;
            Text[] allTexts = UnityEngine.Object.FindObjectsOfType<Text>();
            foreach (Text t in allTexts)
            {
                if (t.font != null)
                    return t.font;
            }
            try { return Font.CreateDynamicFontFromOSFont("Arial", 14); }
            catch { return null; }
        }

        public static void Show(string message, string title = "提示")
        {
            try
            {
                int fontSize = 24;
                if (currentDialog != null)
                {
                    UnityEngine.Object.Destroy(currentDialog);
                    currentDialog = null;
                }

                Font gameFont = GetGameFont();
                if (gameFont == null)
                {
                    Debug.LogError("[CustomMessageBox] 无法获取字体");
                    return;
                }

                // 1. 先创建一个临时Text来计算所需尺寸
                GameObject tempTextObj = new GameObject("TempText");
                Text tempText = tempTextObj.AddComponent<Text>();
                tempText.text = message;
                tempText.font = gameFont;
                tempText.fontSize = fontSize;
                tempText.horizontalOverflow = HorizontalWrapMode.Wrap;
                tempText.verticalOverflow = VerticalWrapMode.Overflow;
                // 给一个初始宽度（例如屏幕宽度的60%），然后计算高度
                float maxWidth = Screen.width * 0.6f;
                tempText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth);
                Canvas.ForceUpdateCanvases();
                float preferredHeight = tempText.preferredHeight;
                float preferredWidth = tempText.preferredWidth;
                // 销毁临时对象
                UnityEngine.Object.Destroy(tempTextObj);

                // 加上边距和标题、按钮的空间
                float paddingX = 60f;
                float paddingY = 100f; // 标题 + 按钮 + 边距
                float panelWidth = Mathf.Min(preferredWidth + paddingX, Screen.width * 0.85f);
                float panelHeight = Mathf.Min(preferredHeight + paddingY, Screen.height * 0.85f);

                // 如果内容高度过大，缩小字号
                if (preferredHeight + paddingY > Screen.height * 0.85f)
                {
                    // 缩小字号，重新计算
                    while (fontSize > 12)
                    {
                        fontSize--;
                        tempTextObj = new GameObject("TempText");
                        tempText = tempTextObj.AddComponent<Text>();
                        tempText.text = message;
                        tempText.font = gameFont;
                        tempText.fontSize = fontSize;
                        tempText.horizontalOverflow = HorizontalWrapMode.Wrap;
                        tempText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth);
                        Canvas.ForceUpdateCanvases();
                        preferredHeight = tempText.preferredHeight;
                        preferredWidth = tempText.preferredWidth;
                        UnityEngine.Object.Destroy(tempTextObj);
                        if (preferredHeight + paddingY <= Screen.height * 0.85f)
                            break;
                    }
                    panelWidth = Mathf.Min(preferredWidth + paddingX, Screen.width * 0.85f);
                    panelHeight = Mathf.Min(preferredHeight + paddingY, Screen.height * 0.85f);
                }

                // 2. 创建实际UI
                GameObject canvasObj = new GameObject("CustomMessageBoxCanvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                canvasObj.AddComponent<GraphicRaycaster>();

                // 3. Panel
                GameObject panelObj = new GameObject("Panel");
                panelObj.transform.SetParent(canvasObj.transform, false);
                Image panelImg = panelObj.AddComponent<Image>();
                panelImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
                RectTransform panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(panelWidth, panelHeight + 40);
                panelRect.anchoredPosition = Vector2.zero;

                // 4. 标题
                GameObject titleObj = new GameObject("Title");
                titleObj.transform.SetParent(panelObj.transform, false);
                Text titleText = titleObj.AddComponent<Text>();
                titleText.text = title;
                titleText.font = gameFont;
                titleText.fontSize = 28;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = Color.white;
                RectTransform titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0, 1);
                titleRect.anchorMax = new Vector2(1, 1);
                titleRect.pivot = new Vector2(0.5f, 1);
                titleRect.sizeDelta = new Vector2(0, 50);
                titleRect.anchoredPosition = new Vector2(0, -10);

                // 5. 消息文本
                GameObject msgObj = new GameObject("Message");
                msgObj.transform.SetParent(panelObj.transform, false);
                Text msgText = msgObj.AddComponent<Text>();
                msgText.text = message;
                msgText.font = gameFont;
                // 使用调整后的字号
                msgText.fontSize = tempText != null ? tempText.fontSize : 22;
                msgText.alignment = TextAnchor.UpperLeft;
                msgText.color = Color.white;
                msgText.horizontalOverflow = HorizontalWrapMode.Wrap;
                msgText.verticalOverflow = VerticalWrapMode.Overflow;

                RectTransform msgRect = msgObj.GetComponent<RectTransform>();
                msgRect.anchorMin = Vector2.zero;
                msgRect.anchorMax = Vector2.one;
                msgRect.offsetMin = new Vector2(20, 60);   // 下边距给按钮
                msgRect.offsetMax = new Vector2(-20, -60); // 上边距给标题

                // 6. 确认按钮
                GameObject btnObj = new GameObject("OKButton");
                btnObj.transform.SetParent(panelObj.transform, false);
                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.3f, 0.6f, 0.9f);
                btnImg.raycastTarget = true;
                Button btn = btnObj.AddComponent<Button>();

                GameObject btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(btnObj.transform, false);
                Text btnText = btnTextObj.AddComponent<Text>();
                btnText.text = "确 定";
                btnText.font = gameFont;
                btnText.fontSize = 24;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
                RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;

                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f, 0);
                btnRect.anchorMax = new Vector2(0.5f, 0);
                btnRect.pivot = new Vector2(0.5f, 0);
                btnRect.sizeDelta = new Vector2(120, 40);
                btnRect.anchoredPosition = new Vector2(0, 15);

                btn.onClick.AddListener(() =>
                {
                    if (currentDialog != null)
                        UnityEngine.Object.Destroy(currentDialog);
                    currentDialog = null;
                });

                currentDialog = canvasObj;
                Debug.Log("[CustomMessageBox] 弹窗创建成功");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CustomMessageBox] 创建失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
