using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace Yueyehuiling.MCS.FindNpcMod
{
    public static class CustomMessageBox
    {
        private static GameObject currentDialog;
        private static MessageBoxWindow currentWindow;

        // 条目数据类
        public class DisplayItem
        {
            public string Text;
            public int NpcId;
            public UnityAction OnClick;
            public object Tag;
        }
        internal static Font GetGameFont()
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont("SimHei", 24);
                if (font == null)
                {
                    Main.LogInfo("[CustomMessageBox] 获取字体失败");
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return font;
            }
            catch
            {
                Main.LogInfo("[CustomMessageBox] 获取字体失败");
                return null;
            }
        }

        // 显示列表弹窗
        public static void ShowList(string title, List<DisplayItem> items)
        {
            // 关闭已有弹窗
            if (currentDialog != null)
            {
                UnityEngine.Object.Destroy(currentDialog);
                currentDialog = null;
                currentWindow = null;
                RestoreWorldMouseEvents();
            }

            if (items == null || items.Count == 0)
            {
                Main.LogInfo("[CustomMessageBox] 列表为空，不显示弹窗。");
                return;
            }

            // 创建挂载窗口的 GameObject
            GameObject go = new GameObject("CustomMessageBox_Window");
            currentDialog = go;
            currentWindow = go.AddComponent<MessageBoxWindow>();
            currentWindow.Initialize(title, items);
            // 保留世界事件禁用（由窗口自己管理）
        }

        // 世界鼠标事件禁用/恢复（保留，但窗口会自行管理）
        private static Dictionary<Camera, int> _savedEventMasks = new Dictionary<Camera, int>();
        private static void DisableWorldMouseEvents()
        {
            _savedEventMasks.Clear();
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam != null)
                {
                    _savedEventMasks[cam] = cam.eventMask;
                    cam.eventMask = 0;
                }
            }
        }
        private static void RestoreWorldMouseEvents()
        {
            foreach (var kv in _savedEventMasks)
            {
                if (kv.Key != null)
                    kv.Key.eventMask = kv.Value;
            }
            _savedEventMasks.Clear();
        }

        // 窗口内部类
        private class MessageBoxWindow : MonoBehaviour
        {
            private string title;
            private List<DisplayItem> items;
            private Vector2 scrollPosition = Vector2.zero;
            private Rect windowRect;
            private bool isDragging = false;

            // UI 样式
            private GUIStyle windowStyle;
            private GUIStyle titleStyle;
            private GUIStyle itemButtonStyle;
            private GUIStyle itemLabelStyle;
            private GUIStyle confirmButtonStyle;
            private GUIStyle backgroundStyle;
            private Texture2D backgroundTexture;
            private Texture2D buttonNormalTex;
            private Texture2D buttonHoverTex;
            private Texture2D buttonActiveTex;
            private Texture2D windowBackgroundTexture;

            private bool stylesInitialized = false;
            private float uiScale = 1f;

            private float windowWidth = 560f;
            private const float windowMinHeight = 300f;
            private float windowMaxHeight = 600f;
            private const float titleFontSize = 30f;
            private const float textFontSize = 20f;
            private const float buttonFontSize = 20f;
            private float titleHeight = 30f;
            private float textHeight = 20f;
            private float buttonHeight = 36f;

            public void Initialize(string title, List<DisplayItem> items)
            {
                this.windowWidth = Screen.width * 0.55f;
                this.windowMaxHeight = Screen.height * 0.6f;
                titleHeight = titleFontSize + 16f;
                textHeight = textFontSize + 16f;
                buttonHeight = buttonFontSize + 18f;

                this.title = title;
                this.items = items;
                // 计算窗口尺寸
                float itemHeight = textHeight;
                float totalItemHeight = items.Count * itemHeight + 40f;
                float baseHeight = titleHeight + buttonHeight + 60f;
                float totalHeight = totalItemHeight + baseHeight;
                float height = Mathf.Clamp(totalHeight, windowMinHeight, windowMaxHeight);
                // 位置居中
                float x = (Screen.width - windowWidth) / 2f;
                float y = (Screen.height - height) / 2f;
                windowRect = new Rect(x, y, windowWidth, height);

                // 禁用世界鼠标事件
                DisableWorldMouseEvents();

                // 初始样式延迟到 OnGUI 第一帧
            }

            private void OnDestroy()
            {
                // 恢复世界鼠标事件
                RestoreWorldMouseEvents();
                if (currentDialog == gameObject)
                {
                    currentDialog = null;
                    currentWindow = null;
                }
            }

            private void OnGUI()
            {
                if (!stylesInitialized)
                {
                    InitStyles();
                    stylesInitialized = true;
                }

                // 1. 绘制全屏遮罩（点击关闭）
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", backgroundStyle);

                // 2. 绘制窗口
                windowRect = GUI.Window(198712, windowRect, DrawWindow, "", windowStyle);
                // 如果窗口位置移动，限制在屏幕内
                ClampWindowToScreen();
            }

            private void DrawWindow(int id)
            {
                // 绘制纯色背景，覆盖整个窗口，防止白色透出
                if (windowBackgroundTexture != null)
                    GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), windowBackgroundTexture);

                // 标题
                GUILayout.Space(6);
                GUILayout.Label(title, titleStyle, GUILayout.Height(titleHeight));

                // 计算可用滚动区域高度
                float reservedHeight = titleHeight + buttonHeight + 50f; // 标题 + 按钮 + 边距
                float scrollAreaHeight = windowRect.height - reservedHeight;
                if (scrollAreaHeight < 50f) scrollAreaHeight = 50f;

                // 计算所有条目总高度（含间距）
                float itemHeightWithMargin = textHeight + 4f; // 条目高度 + 下边距
                float totalItemsHeight = items.Count * itemHeightWithMargin + 10f;

                // 判断是否需要滚动
                bool needScroll = totalItemsHeight > scrollAreaHeight;

                if (needScroll)
                {
                    // 需要滚动：使用 ScrollView
                    scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, GUILayout.Height(scrollAreaHeight));
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        GUILayout.BeginHorizontal();
                        // 主按钮（显示文本，点击打开NPC信息）
                        if (GUILayout.Button(item.Text, itemButtonStyle, GUILayout.Height(textHeight)))
                        {
                            if (item.OnClick != null)
                                item.OnClick();
                            else if (item.NpcId > 0)
                                OpenNPCInfo(item.NpcId);
                        }
                        // 关注按钮
                        bool isWatched = Main.IsNpcWatched(item.NpcId);
                        string watchLabel = isWatched ? "取消关注" : "关注";
                        if (GUILayout.Button(watchLabel, GUILayout.Width(70f), GUILayout.Height(textHeight)))
                        {
                            Main.ToggleWatchNpc(item.NpcId);
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                }
                else
                {
                    // 不需要滚动：直接显示所有条目（无滚动条）
                    GUILayout.BeginVertical(GUILayout.Height(scrollAreaHeight));
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(item.Text, itemButtonStyle, GUILayout.Height(textHeight)))
                        {
                            if (item.OnClick != null)
                                item.OnClick();
                            else if (item.NpcId > 0)
                                OpenNPCInfo(item.NpcId);
                        }
                        // 关注按钮
                        bool isWatched = Main.IsNpcWatched(item.NpcId);
                        string watchLabel = isWatched ? "取消关注" : "关注";
                        if (GUILayout.Button(watchLabel, GUILayout.Width(70f), GUILayout.Height(textHeight)))
                        {
                            Main.ToggleWatchNpc(item.NpcId);
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }

                // 确认按钮
                GUILayout.Space(8);
                if (GUILayout.Button("确 认", confirmButtonStyle, GUILayout.Height(buttonHeight)))
                {
                    CloseWindow();
                }
                GUILayout.Space(8);

                // 可拖动
                GUI.DragWindow(new Rect(0, 0, windowRect.width, 30f));
            }

            private void ClampWindowToScreen()
            {
                float padding = 20f;
                windowRect.x = Mathf.Clamp(windowRect.x, padding, Screen.width - windowRect.width - padding);
                windowRect.y = Mathf.Clamp(windowRect.y, padding, Screen.height - windowRect.height - padding);
            }

            private void CloseWindow()
            {
                if (currentDialog != null)
                    UnityEngine.Object.Destroy(currentDialog);
            }

            private void InitStyles()
            {
                // 计算缩放比例
                float refWidth = 1920f;
                float refHeight = 1080f;
                uiScale = Mathf.Clamp(Mathf.Sqrt((Screen.width * Screen.height) / (refWidth * refHeight)), 0.7f, 1.3f);

                // 获取游戏字体
                Font gameFont = CustomMessageBox.GetGameFont();
                if (gameFont == null)
                {
                    gameFont = GUI.skin.font;
                }

                // 生成纹理
                Color bgColor = new Color(0.02f, 0.1f, 0.13f, 0.95f);
                Color borderColor = new Color(0.66f, 0.54f, 0.31f, 0.95f);
                Color fillColor = new Color(0.06f, 0.31f, 0.3f, 0.9f);
                Color hoverColor = new Color(0.11f, 0.44f, 0.4f, 0.95f);
                Color activeColor = new Color(0.42f, 0.3f, 0.14f, 0.95f);
                Color textColor = new Color(0.94f, 0.86f, 0.62f, 1f);

                Texture2D windowTex = MakeRoundedTexture(128, 128, 12, bgColor, borderColor, 2);
                Texture2D buttonTex = MakeRoundedTexture(64, 32, 10, fillColor, borderColor, 1);
                Texture2D buttonHoverTex = MakeRoundedTexture(64, 32, 10, hoverColor, borderColor, 1);
                Texture2D buttonActiveTex = MakeRoundedTexture(64, 32, 10, activeColor, borderColor, 1);
                Texture2D bgTex = MakeSolidTexture(new Color(0, 0, 0, 0.5f));

                // 窗口样式
                windowStyle = new GUIStyle(GUI.skin.window);
                windowStyle.normal.background = null;
                windowStyle.border = new RectOffset(16, 16, 16, 16);
                windowStyle.padding = new RectOffset(10, 10, 10, 10);
                windowStyle.margin = new RectOffset(0, 0, 0, 0);

                // 标题样式
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.font = gameFont;  // 设置字体
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontSize = Mathf.RoundToInt(titleFontSize * uiScale);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.normal.textColor = textColor;

                // 条目按钮样式
                itemButtonStyle = new GUIStyle(GUI.skin.button);
                itemButtonStyle.font = gameFont;  // 设置字体
                itemButtonStyle.normal.background = buttonTex;
                itemButtonStyle.hover.background = buttonHoverTex;
                itemButtonStyle.active.background = buttonActiveTex;
                itemButtonStyle.focused.background = buttonHoverTex;
                itemButtonStyle.onNormal.background = buttonActiveTex;
                itemButtonStyle.onHover.background = buttonHoverTex;
                itemButtonStyle.border = new RectOffset(10, 10, 8, 8);
                itemButtonStyle.fontSize = Mathf.RoundToInt(textFontSize * uiScale);
                itemButtonStyle.alignment = TextAnchor.MiddleLeft;
                itemButtonStyle.padding = new RectOffset(12, 12, 4, 4);
                itemButtonStyle.normal.textColor = textColor;
                itemButtonStyle.hover.textColor = Color.white;
                itemButtonStyle.active.textColor = Color.white;
                itemButtonStyle.margin = new RectOffset(0, 0, 4, 4); // 上下间距

                // 确认按钮样式（稍大，居中）
                confirmButtonStyle = new GUIStyle(itemButtonStyle);
                confirmButtonStyle.font = gameFont;  // 设置字体
                confirmButtonStyle.alignment = TextAnchor.MiddleCenter;
                confirmButtonStyle.fontSize = Mathf.RoundToInt(buttonFontSize * uiScale);
                confirmButtonStyle.padding = new RectOffset(8, 8, 4, 4);

                // 背景遮罩样式
                backgroundStyle = new GUIStyle();
                backgroundStyle.normal.background = bgTex;

                windowBackgroundTexture = MakeSolidTexture(bgColor); // bgColor 就是原来的背景色
            }

            #region 纹理生成辅助方法（从 UniversalNpcSearch 移植）
            private static Texture2D MakeSolidTexture(Color color)
            {
                Texture2D tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.SetPixel(0, 0, color);
                tex.Apply();
                return tex;
            }

            private static Texture2D MakeRoundedTexture(int width, int height, int radius, Color fill, Color border, int borderWidth)
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                Color transparent = new Color(0, 0, 0, 0);
                int r2 = radius * radius;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int dx = 0, dy = 0;
                        if (x < radius) dx = radius - x;
                        else if (x >= width - radius) dx = x - (width - radius - 1);
                        if (y < radius) dy = radius - y;
                        else if (y >= height - radius) dy = y - (height - radius - 1);
                        if (dx * dx + dy * dy > r2)
                        {
                            tex.SetPixel(x, y, transparent);
                        }
                        else
                        {
                            bool isBorder = (x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth);
                            if (!isBorder && (dx > 0 || dy > 0))
                            {
                                int innerRadius = Mathf.Max(0, radius - borderWidth);
                                int dx2 = Mathf.Max(0, dx - borderWidth);
                                int dy2 = Mathf.Max(0, dy - borderWidth);
                                isBorder = (dx2 * dx2 + dy2 * dy2 > innerRadius * innerRadius);
                            }
                            tex.SetPixel(x, y, isBorder ? border : fill);
                        }
                    }
                }
                tex.Apply();
                return tex;
            }
            #endregion
        }

        // 打开 NPC 信息（原 private static，改为 internal 以便窗口调用）
        internal static void OpenNPCInfo(int npcId)
        {
            try
            {
                if (NpcJieSuanManager.inst.IsDeath(npcId) || NpcJieSuanManager.inst.IsFly(npcId))
                {
                    UIPopTip.Inst.Pop("该NPC已不在人世或已离开", 0);
                    return;
                }

                int newId = NPCEx.NPCIDToNew(npcId);
                UINPCData npcData = new UINPCData(newId, false);

                if (newId < 20000)
                {
                    npcData.RefreshOldNpcData();
                    npcData.IsFight = true;
                    UINPCJiaoHu.Inst.NowJiaoHuEnemy = npcData;
                    UINPCJiaoHu.Inst.InfoPanel.npc = npcData;
                    UINPCJiaoHu.Inst.ShowNPCInfoPanel(npcData);
                    UINPCJiaoHu.Inst.InfoPanel.TabGroup.HideTab();
                }
                else
                {
                    npcData.RefreshData();
                    npcData.IsFight = false;
                    UINPCJiaoHu.Inst.NowJiaoHuNPC = npcData;
                    UINPCJiaoHu.Inst.ShowNPCInfoPanel(npcData);
                    UINPCJiaoHu.Inst.InfoPanel.TabGroup.UnHideTab();
                }

                // 关闭当前弹窗
                if (currentDialog != null)
                {
                    UnityEngine.Object.Destroy(currentDialog);
                    currentDialog = null;
                    currentWindow = null;
                }

                Main.LogInfo($"打开NPC信息: {npcId}");
            }
            catch (Exception ex)
            {
                Main.LogInfo($"打开NPC信息失败: {ex}");
                UIPopTip.Inst.Pop("打开NPC信息失败", 0);
            }
        }
    }
}