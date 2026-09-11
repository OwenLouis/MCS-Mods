using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using script.NewLianDan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Yueyehuiling.MCS.DanFangShortcut
{
    [BepInPlugin("Yueyehuiling.MCS.DanFangShortcut", "DanFangShortcut", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        private static Main Inst;
        internal static ManualLogSource ModLogger;
        private static List<string> MessageList = null;
        private static string configPath;
        // 存储9个丹方，每个丹方为 槽位索引 -> (物品ID, 数量)
        private static Dictionary<int, DanFangData> savedDanFangs = new Dictionary<int, DanFangData>();
        private static ConfigEntry<string> ConfigRareItemIds;
        private static HashSet<int> rareItemIds = new HashSet<int>();

        // 新增丹方数据类
        public class DanFangData
        {
            public string Name;
            public Dictionary<int, SlotData> Slots;
        }

        // 辅助类：存储单个槽位数据
        public class SlotData
        {
            public int itemId;
            public int count;
            public string itemName;
        }

        private void Awake()
        {
            ModLogger = base.Logger;
            ModLogger.LogInfo("DanFangShortcut 加载并注册补丁完成！");
            Harmony.CreateAndPatchAll(typeof(Main), null);
            Main.Inst = this;
            ConfigRareItemIds = Config.Bind("炼丹设置", "稀有药材ID列表", "8592,6213,6303", "输入稀有药材的ID，用英文逗号分隔，例如：1001,1002,1003");
            ParseRareItemIds(ConfigRareItemIds.Value);
            configPath = Path.Combine(Paths.GetSavePath(), "DanFangShortcut.json");
            LoadData();
        }

        public void Update()
        {
            try
            {
                // 检查炼丹界面是否打开
                if (LianDanUIMag.Instance == null || !LianDanUIMag.Instance.gameObject.activeSelf)
                    return;

                // Ctrl+数字 保存
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    for (int i = 1; i <= 9; i++)
                    {
                        if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                        {
                            SaveDanFang(i);
                            break;
                        }
                        // 按 Ctrl+0 显示消息列表
                        if (Input.GetKeyDown(KeyCode.Alpha0))
                        {
                            ShowMessageBox();
                            break;
                        }
                    }
                }
                else // 数字键加载
                {
                    for (int i = 1; i <= 9; i++)
                    {
                        if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                        {
                            LoadDanFang(i);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Update 异常: {ex}");
            }
        }

        // 保存当前面板数据到槽位 slot (1~9)
        private void SaveDanFang(int slot)
        {
            // 尝试获取当前面板匹配的丹方ID
            int danFangId = GetCurrentDanFangId();
            string defaultName = "";

            if (danFangId > 0)
            {
                // 从物品数据中获取丹方名称（纯文本）
                JSONObject itemObj = jsonData.instance.ItemJsonData[danFangId.ToString()];
                if (itemObj != null)
                {
                    defaultName = ToolsEx.ToCN(itemObj["name"].str);
                }
            }

            // 弹出输入框，默认填充识别到的名称
            string prompt = string.IsNullOrEmpty(defaultName)
                ? "请输入此丹方的名称"
                : $"匹配丹方“{defaultName}”";

            UInputBox.Show(prompt, delegate (string inputName)
            {
                // 如果用户未输入，则使用默认名称
                if (string.IsNullOrWhiteSpace(inputName))
                {
                    inputName = string.IsNullOrEmpty(defaultName) ? $"丹方 {slot}" : defaultName;
                }
                PerformSave(slot, inputName);
            }, null, "danfang_save");
        }

        // 实际保存逻辑提取为独立方法
        private void PerformSave(int slot, string name)
        {
            try
            {
                var caoYaoList = LianDanUIMag.Instance.LianDanPanel.CaoYaoList;
                var slotDataDict = new Dictionary<int, SlotData>();
                for (int i = 0; i < caoYaoList.Count; i++)
                {
                    var slotObj = caoYaoList[i];
                    if (slotObj == null) continue;
                    var baseItem = slotObj.Item;
                    if (baseItem != null && baseItem.Count > 0)
                    {
                        // 获取物品名称
                        string itemName = "";
                        JSONObject itemObj = jsonData.instance.ItemJsonData[baseItem.Id.ToString()];
                        if (itemObj != null)
                        {
                            itemName = ToolsEx.ToCN(itemObj["name"].str);
                        }
                        slotDataDict[i] = new SlotData { itemId = baseItem.Id, count = baseItem.Count, itemName = itemName };
                    }
                }

                var danFangData = new DanFangData
                {
                    Name = name,
                    Slots = slotDataDict
                };
                savedDanFangs[slot] = danFangData;
                SaveData();
                PopTip($"丹方“{name}”已保存到槽位 {slot}", 0);
                ModLogger.LogInfo($"丹方“{name}”保存到槽位 {slot}，共 {slotDataDict.Count} 种草药");
            }
            catch (Exception e)
            {
                ModLogger.LogError($"保存丹方失败: {e}");
                PopTip("保存丹方失败，请查看日志", 0);
            }
        }

        // 加载指定槽位的丹方到面板
        private void LoadDanFang(int slot)
        {
            try
            {
                if (!savedDanFangs.ContainsKey(slot))
                {
                    PopTip($"槽位 {slot} 无保存的丹方", 0);
                    return;
                }

                var panel = LianDanUIMag.Instance.LianDanPanel;
                var danFangData = savedDanFangs[slot];
                var slotDataDict = danFangData.Slots;
                string danFangName = danFangData.Name;

                // ========== 计算可炼制次数及补全提示（稀有药材优先） ==========
                var avatar = Tools.instance.getPlayer();
                if (avatar != null)
                {
                    // 1. 合并丹方需求
                    Dictionary<int, int> totalNeed = new Dictionary<int, int>();
                    foreach (var kv in slotDataDict)
                    {
                        var data = kv.Value;
                        if (totalNeed.ContainsKey(data.itemId))
                            totalNeed[data.itemId] += data.count;
                        else
                            totalNeed[data.itemId] = data.count;
                    }

                    // 2. 计算每种药材的拥有量
                    Dictionary<int, int> haveCount = new Dictionary<int, int>();
                    foreach (var id in totalNeed.Keys)
                        haveCount[id] = avatar.getItemNum(id);

                    // 3. 计算每种药材的 canMake = 拥有量 / 需求量
                    Dictionary<int, int> canMakePerMaterial = new Dictionary<int, int>();
                    foreach (var kv in totalNeed)
                        canMakePerMaterial[kv.Key] = haveCount[kv.Key] / kv.Value;

                    // 4. 区分稀有药材和非稀有药材
                    bool hasRare = false;
                    Dictionary<int, int> rareCan = new Dictionary<int, int>();
                    Dictionary<int, int> nonRareCan = new Dictionary<int, int>();
                    foreach (var kv in canMakePerMaterial)
                    {
                        if (rareItemIds.Contains(kv.Key))
                        {
                            hasRare = true;
                            rareCan[kv.Key] = kv.Value;
                        }
                        else
                        {
                            nonRareCan[kv.Key] = kv.Value;
                        }
                    }

                    // 5. 根据是否有稀有药材分别处理
                    if (hasRare)
                    {
                        // 稀有药材的最小 canMake
                        int rareMin = int.MaxValue;
                        foreach (var v in rareCan.Values)
                            if (v < rareMin) rareMin = v;

                        // 如果稀有药材不足，直接提示无法炼制
                        if (rareMin == 0)
                        {
                            string rareName = "";
                            int need = 0, have = 0;
                            foreach (var pair in totalNeed)
                            {
                                if (rareItemIds.Contains(pair.Key))
                                {
                                    int h = avatar.getItemNum(pair.Key);
                                    if (h < pair.Value)
                                    {
                                        rareName = GetItemName(pair.Key);
                                        need = pair.Value;
                                        have = h;
                                        break;
                                    }
                                }
                            }
                            PopTip($"稀有药材 {rareName} 不足（需{need}，有{have}），无法炼制", 0);
                            // 直接返回，不执行后续加载
                            return;
                        }

                        // 非稀有药材的最小 canMake（若无非稀有药材，则设为 int.MaxValue）
                        int nonRareMin = int.MaxValue;
                        foreach (var v in nonRareCan.Values)
                            if (v < nonRareMin) nonRareMin = v;

                        if (nonRareMin == int.MaxValue) nonRareMin = rareMin; // 无普通药材时，不限

                        if (rareMin <= nonRareMin)
                        {
                            // 瓶颈是稀有药材，无需补充普通药材
                            string bottleneckName = "";
                            foreach (var kv in rareCan)
                                if (kv.Value == rareMin) { bottleneckName = GetItemName(kv.Key); break; }
                            string msg = BuildDanFangMessage(
                                danFangName,
                                totalNeed,
                                rareMin,
                                rareMin,
                                $"稀有药材 {bottleneckName}",
                                haveCount
                            );
                            CustomMessageBox.Show(msg);
                            //PopTip($"受限于稀有药材 {bottleneckName}，当前可炼 {rareMin} 次", 0);
                        }
                        else
                        {
                            // 瓶颈是普通药材，需要补充普通药材以达到 rareMin 次
                            // 找出瓶颈普通药材ID（canMake最小且非稀有）
                            int bottleneckId = -1;
                            int minNonRare = int.MaxValue;
                            foreach (var kv in nonRareCan)
                            {
                                if (kv.Value < minNonRare) { minNonRare = kv.Value; bottleneckId = kv.Key; }
                            }
                            string bottleneck = $"普通药材 {GetItemName(bottleneckId)}";
                            string msg = BuildDanFangMessage(
                                danFangName,
                                totalNeed,
                                minNonRare,
                                rareMin,
                                bottleneck,
                                haveCount
                            );
                            CustomMessageBox.Show(msg);
                            //PopTip($"补足部分药材后可达 {rareMin} 次", 0);
                        }
                    }
                    else
                    {
                        // 无稀有药材，按常规逻辑：以最大 canMake 为目标，补全其他药材
                        int currentMax = int.MaxValue;
                        int potentialMax = 0;
                        foreach (var v in canMakePerMaterial.Values)
                        {
                            if (v < currentMax) currentMax = v;
                            if (v > potentialMax) potentialMax = v;
                        }

                        if (currentMax == 0)
                        {
                            // 有药材不足，无法炼制
                            string missing = "";
                            foreach (var pair in totalNeed)
                            {
                                int have = avatar.getItemNum(pair.Key);
                                if (have < pair.Value)
                                {
                                    missing += $"{GetItemName(pair.Key)} 缺 {pair.Value - have}；";
                                }
                            }
                            PopTip($"材料不足，无法炼制。缺少：{missing}", 0);
                        }
                        else if (currentMax == potentialMax)
                        {
                            string msg = BuildDanFangMessage(danFangName, totalNeed, currentMax, currentMax, null, haveCount);
                            CustomMessageBox.Show(msg);
                            //PopTip($"材料充足，可炼 {currentMax} 次", 0);
                        }
                        else
                        {
                            // 补全到 potentialMax 次
                            string msg = BuildDanFangMessage(
                                danFangName,
                                totalNeed,
                                currentMax,
                                potentialMax,
                                null,
                                haveCount
                            );
                            CustomMessageBox.Show(msg);
                            //PopTip($"当前可炼 {currentMax} 次，补全可至 {potentialMax} 次", 0);
                        }
                    }
                }

                // ========== 加载丹方到面板 ==========
                panel.BackAllCaoYao();
                foreach (var kv in slotDataDict)
                {
                    panel.PutCaoYao(kv.Key, kv.Value.itemId, kv.Value.count);
                }
                panel.CheckCanMade();
                PopTip($"已加载丹方“{danFangName}”", 0);
                ModLogger.LogInfo($"加载丹方“{danFangName}”（槽位 {slot}），共 {slotDataDict.Count} 种草药");
            }
            catch (Exception e)
            {
                ModLogger.LogError($"加载丹方失败: {e}");
                PopTip("加载丹方失败，请查看日志", 0);
            }
        }

        // ----- 数据持久化 -----
        private void LoadData()
        {
            if (!File.Exists(configPath)) return;
            try
            {
                string json = File.ReadAllText(configPath);
                // 尝试反序列化为新格式
                savedDanFangs = JsonConvert.DeserializeObject<Dictionary<int, DanFangData>>(json);
                // 兼容旧数据：如果某个条目没有 Name 字段，则默认为 "丹方 X"
                foreach (var kv in savedDanFangs)
                {
                    if (string.IsNullOrEmpty(kv.Value.Name))
                    {
                        kv.Value.Name = $"丹方 {kv.Key}";
                    }
                }
                ModLogger.LogInfo($"丹方数据已加载，共 {savedDanFangs.Count} 个保存槽位");
            }
            catch (Exception e)
            {
                ModLogger.LogError($"加载数据失败，可能是旧格式: {e.Message}");
                // 如果反序列化失败，可能是旧格式（直接是 Dictionary<int, Dictionary<int, SlotData>>）
                try
                {
                    string json = File.ReadAllText(configPath);
                    // 尝试以旧格式反序列化
                    var oldData = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<int, SlotData>>>(json);
                    savedDanFangs = new Dictionary<int, DanFangData>();
                    foreach (var kv in oldData)
                    {
                        savedDanFangs[kv.Key] = new DanFangData
                        {
                            Name = $"丹方 {kv.Key}",
                            Slots = kv.Value
                        };
                    }
                    ModLogger.LogInfo($"兼容旧数据：已转换 {savedDanFangs.Count} 个丹方");
                    // 重新保存为新格式
                    SaveData();
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"加载数据失败: {ex.Message}");
                }
            }
        }

        private void SaveData()
        {
            try
            {
                string json = JsonConvert.SerializeObject(savedDanFangs, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, json);
                ModLogger.LogInfo("丹方数据已保存");
            }
            catch (System.Exception e)
            {
                ModLogger.LogError($"保存数据失败: {e.Message}");
            }
        }

        // 构建统一的弹窗文本
        private string BuildDanFangMessage(
            string danFangName,
            Dictionary<int, int> totalNeed,
            int currentCanMake,
            int potentialCanMake,
            string bottleneck = null,
            Dictionary<int, int> haveCount = null
        )
        {
            if (haveCount == null)
            {
                var avatar = Tools.instance.getPlayer();
                haveCount = new Dictionary<int, int>();
                foreach (var id in totalNeed.Keys)
                    haveCount[id] = avatar.getItemNum(id);
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"丹方：{danFangName}");
            sb.AppendLine($"当前可炼制：{currentCanMake} 次");
            if (potentialCanMake > currentCanMake)
                sb.AppendLine($"补足后可炼制：{potentialCanMake} 次");
            if (!string.IsNullOrEmpty(bottleneck))
                sb.AppendLine($"瓶颈：{bottleneck}");
            sb.AppendLine();
            sb.AppendLine("药材需求：");

            var sortedKeys = totalNeed.Keys.OrderBy(id => GetItemName(id));
            foreach (var id in sortedKeys)
            {
                int needTotal = totalNeed[id] * potentialCanMake;
                int have = haveCount[id];
                int missing = Math.Max(0, needTotal - have);
                string name = GetItemName(id);
                sb.AppendLine($"  • {name}：需 {needTotal}，有 {have}，缺 {missing}");
            }
            return sb.ToString();
        }

        // 获取当前炼丹面板匹配的丹方ID（如果匹配已知丹方），否则返回0
        private int GetCurrentDanFangId()
        {
            var panel = LianDanUIMag.Instance.LianDanPanel;
            var player = Tools.instance.getPlayer();
            List<JSONObject> danFangList = player.DanFang.list;
            for (int i = 0; i < danFangList.Count; i++)
            {
                int matchCount = 0;
                for (int j = 0; j < 5; j++)
                {
                    int itemId = 0;
                    int count = 0;
                    if (!panel.CaoYaoList[j].IsNull())
                    {
                        itemId = panel.CaoYaoList[j].Item.Id;
                        count = panel.CaoYaoList[j].Item.Count;
                    }
                    if (danFangList[i]["Type"][j].I == itemId && danFangList[i]["Num"][j].I == count)
                    {
                        matchCount++;
                    }
                }
                if (matchCount == 5)
                {
                    return danFangList[i]["ID"].I;
                }
            }
            return 0;
        }
        private void ParseRareItemIds(string idsStr)
        {
            rareItemIds.Clear();
            if (string.IsNullOrWhiteSpace(idsStr)) return;
            foreach (string idStr in idsStr.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                    rareItemIds.Add(id);
            }
        }
        private string GetItemName(int itemId)
        {
            if (jsonData.instance.ItemJsonData.TryGetValue(itemId.ToString(), out JSONObject obj))
            {
                return ToolsEx.ToCN(obj["name"].str);
            }
            return $"未知物品({itemId})";
        }

        public static int i = 0;

        // 统一的提示方法：显示弹窗 + 记录日志 + 加入消息列表
        public void PopTip(string tip, PopTipIconType iconType = 0)
        {
            UIPopTip.Inst.Pop(tip, iconType);
            ModLogger.LogInfo(tip);
            if (MessageList == null) MessageList = new List<string>();
            MessageList.Add(tip);
            // 限制列表长度，防止内存溢出
            if (MessageList.Count > 50) MessageList.RemoveAt(0);
        }

        // 弹出消息列表窗口
        public void ShowMessageBox()
        {
            if (MessageList == null || MessageList.Count == 0)
            {
                PopTip("暂无历史消息", 0);
                return;
            }
            string temp = "";
            foreach (string str in MessageList)
            {
                if (temp.Length > 500) break;
                if (!string.IsNullOrEmpty(temp)) temp += "\n";
                temp += str;
            }
            CustomMessageBox.Show(temp);
        }
    }
}