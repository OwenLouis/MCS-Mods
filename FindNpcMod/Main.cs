using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Yueyehuiling.MCS.FindNpcMod
{
    [BepInPlugin("Yueyehuiling.MCS.FindNpcMod", "FindNpcMod", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        private static Main Inst;
        private static ManualLogSource ModLogger;
        private ConfigEntry<KeyboardShortcut> ShowCounter { get; set; }
        // 关注列表配置
        private static ConfigEntry<string> watchedNpcIdsConfig;
        private static ConfigEntry<bool> showZiZhiAgeConfig;
        private static List<int> watchedNpcIds = new List<int>();
        private static string actionInfo = "1,无所事事\r\n2,闭关\r\n3,采药\r\n4,采矿\r\n5,练丹\r\n6,练器\r\n7,修炼神通\r\n8,买秘籍\r\n9,买法宝\r\n10,论道\r\n11,买丹药\r\n30,追杀宁州妖兽\r\n31,做主城任务\r\n33,游历修行\r\n34,劫杀\r\n35,做门派任务\r\n36,收集突破物品\r\n37,收集续命丹药\r\n41,杀海上妖兽\r\n42,游历修行\r\n43,着手碎星岛进货\r\n44,准备出海\r\n45,炼制阵旗\r\n50,境界大突破\r\n51,东石谷私坊处理货物\r\n52,天星私坊处理货物\r\n53,海上私坊处理货物\r\n51,交易私坊处理货物\r\n52,交易私坊处理货物\r\n53,交易私坊处理货物\r\n54,东石谷拍卖\r\n55,天机阁拍卖\r\n56,海上拍卖\r\n57,南涯拍卖\r\n100,神游太虚当中\r\n101,去广场路上\r\n102,长老大殿\r\n103,掌门大殿\r\n111,天机阁跑商\r\n112,天机阁进货\r\n113,到访朋友或者你的洞府\r\n114,到访朋友或者你洞府的路上\r\n115,处理碎星商会事务\r\n116,着手碎星岛发展\r\n117,在为你护法\r\n121,灵核采集一\r\n122,灵核采集二\r\n123,灵核采集三\r\n124,灵核采集四\r\n125,灵核采集五\r\n126,灵核采集六\r\n201,被长辈看住了\r\n202,被长辈管在家了\r\n203,港口等着一起出海\r\n210,宗门广场主持事务\r\n211,被长辈看管于府内闭关\r\n212,处理家族委托的要事\r\n221,家里闭关打熬身体\r\n222,蓬莎岛办事\r\n231,禾山办事\r\n232,沂山办事\r\n233,南崖城商会处理商会事宜\r\n234,青石灵脉遇到麻烦了\r\n131,准备杀手任务";


        private void Awake()
        {
            ModLogger = base.Logger;
            ModLogger.LogInfo("FindNpcMod 加载并注册补丁完成！");
            Harmony.CreateAndPatchAll(typeof(Main), null);
            Harmony.CreateAndPatchAll(typeof(WatchedNpcUIPatch), null);
            this.ShowCounter = base.Config.Bind<KeyboardShortcut>("快捷键", "Key", new KeyboardShortcut(KeyCode.F9, Array.Empty<KeyCode>()), ConfigDescription.Empty);
            Main.Inst = this;
            watchedNpcIdsConfig = Config.Bind("General", "WatchedNpcIds", "", "关注的NPC ID列表，用逗号分隔");
            showZiZhiAgeConfig = Config.Bind("General", "ShowZiZhiAge", true, "是否显示资质和年龄信息，并根据资质排序");
            LoadWatchedNpcs();
        }

        // 加载关注列表
        private void LoadWatchedNpcs()
        {
            watchedNpcIds.Clear();
            string val = watchedNpcIdsConfig.Value;
            if (!string.IsNullOrEmpty(val))
            {
                foreach (string idStr in val.Split(','))
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                        watchedNpcIds.Add(id);
                }
            }
        }

        // 保存关注列表
        private void SaveWatchedNpcs()
        {
            watchedNpcIdsConfig.Value = string.Join(",", watchedNpcIds);
        }

        // 移除已死亡的NPC
        public static void RemoveDeadNpcs()
        {
            bool removed = false;
            for (int i = watchedNpcIds.Count - 1; i >= 0; i--)
            {
                int id = watchedNpcIds[i];
                if (NPCEx.IsDeath(id))
                {
                    string name = GetNpcName(id);
                    UIPopTip.Inst.Pop($"关注NPC {name} 已死亡，已移除", 0);
                    watchedNpcIds.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed)
            {
                Inst.SaveWatchedNpcs();
            }
        }

        // 切换关注状态
        public static void ToggleWatchNpc(int npcId)
        {
            if (watchedNpcIds.Contains(npcId))
                watchedNpcIds.Remove(npcId);
            else
                watchedNpcIds.Add(npcId);
            // 保存配置
            Inst.SaveWatchedNpcs();
            // 刷新任务追踪面板
            if (UIMiniTaskPanel.Inst != null)
                WatchedNpcUIPatch.Apply(UIMiniTaskPanel.Inst);
        }

        // 是否已关注
        public static bool IsNpcWatched(int npcId)
        {
            return watchedNpcIds.Contains(npcId);
        }

        // 获取关注的NPC列表
        public static List<int> GetWatchedNpcs()
        {
            return new List<int>(watchedNpcIds);
        }

        // 获取NPC名称（用于显示）
        public static string GetNpcName(int npcId)
        {
            try
            {
                // 1. 检查是否为死亡 NPC，从 npcDeathJson 获取名称
                if (NpcJieSuanManager.inst.IsDeath(npcId))
                {
                    JSONObject deathData = NpcJieSuanManager.inst.npcDeath.npcDeathJson;
                    if (deathData != null && deathData.HasField(npcId.ToString()))
                    {
                        JSONObject deathInfo = deathData[npcId.ToString()];
                        string name = deathInfo["deathName"].str;
                        if (!string.IsNullOrEmpty(name))
                        {
                            return Regex.Unescape(name);
                        }
                    }
                    // 如果 npcDeathJson 中没有，则使用 ID 显示
                    return "已死亡 NPC " + npcId;
                }

                // 2. 正常 NPC：优先从 AvatarJsonData 获取
                JSONObject avatarData = jsonData.instance.AvatarJsonData[npcId.ToString()];
                if (avatarData != null)
                {
                    string name = avatarData["Name"].str;
                    if (!string.IsNullOrEmpty(name))
                        return Regex.Unescape(name);
                }

                // 3. 回退到 UINPCData
                UINPCData npc = new UINPCData(npcId, false);
                npc.SetID(npcId);
                return npc.Name;
            }
            catch
            {
                return "NPC " + npcId;
            }
        }

        public void Update()
        {
            try
            {
                if (this.ShowCounter.Value.IsDown())
                {
                    UInputBox.Show("你想找哪个NPC？", delegate (string s)
                    {
                        if (string.IsNullOrWhiteSpace(s))
                            return;
                        // ---------- 构建映射（同原代码） ----------
                        JSONObject shiLiData = jsonData.instance.CyShiLiNameData;
                        Dictionary<string, int> shiLiExact = new Dictionary<string, int>();
                        foreach (string key in shiLiData.keys)
                        {
                            string name = shiLiData[key]["name"].str;
                            if (!string.IsNullOrEmpty(name))
                            {
                                name = Regex.Unescape(name);
                                shiLiExact[name] = int.Parse(key);
                            }
                        }

                        JSONObject levelData = jsonData.instance.LevelUpDataJsonData;
                        Dictionary<string, int> levelExact = new Dictionary<string, int>();
                        foreach (string key in levelData.keys)
                        {
                            string name = levelData[key]["Name"].str;
                            if (!string.IsNullOrEmpty(name))
                            {
                                name = Regex.Unescape(name);
                                levelExact[name] = int.Parse(key);
                            }
                        }

                        // 技能映射（同原代码）
                        Dictionary<string, HashSet<int>> skillIdToNameMap = new Dictionary<string, HashSet<int>>();
                        if (jsonData.instance._skillJsonData != null)
                        {
                            foreach (string key in jsonData.instance._skillJsonData.keys)
                            {
                                JSONObject skillData = jsonData.instance._skillJsonData[key];
                                if (skillData == null) continue;
                                string name = ToolsEx.ToCN(skillData["name"].str);
                                int skillId = skillData.HasField("Skill_ID") ? skillData["Skill_ID"].I : int.Parse(key);
                                if (!skillIdToNameMap.ContainsKey(name))
                                    skillIdToNameMap[name] = new HashSet<int>();
                                skillIdToNameMap[name].Add(skillId);
                            }
                        }

                        Dictionary<string, HashSet<int>> staticSkillNameToIdMap = new Dictionary<string, HashSet<int>>();
                        if (jsonData.instance.StaticSkillJsonData != null)
                        {
                            foreach (string key in jsonData.instance.StaticSkillJsonData.keys)
                            {
                                JSONObject skillData = jsonData.instance.StaticSkillJsonData[key];
                                if (skillData == null) continue;
                                string name = ToolsEx.ToCN(skillData["name"].str);
                                int id = int.Parse(key);
                                if (!staticSkillNameToIdMap.ContainsKey(name))
                                    staticSkillNameToIdMap[name] = new HashSet<int>();
                                staticSkillNameToIdMap[name].Add(id);
                            }
                        }

                        ModLogger.LogInfo("开始解析");

                        // ---------- 解析输入 ----------
                        string[] parts = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        List<NPCInfo> matchedNpcs = new List<NPCInfo>();

                        if (parts.Length == 1 || s.StartsWith(":") || s.StartsWith("!"))
                        {
                            // 单条件处理
                            string input = s;
                            bool searchByName = true;
                            HashSet<int> matchedShiLiIds = null;
                            HashSet<int> matchedLevelIds = null;
                            bool searchItem = false;
                            int? searchQuality = null;
                            string searchItemName = null;
                            bool searchSkill = false;
                            HashSet<int> matchedSkillIds = null;
                            HashSet<int> matchedStaticSkillIds = null;

                            // 解析门派（模糊匹配）
                            if (shiLiExact.Any(kv => kv.Key.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                matchedShiLiIds = new HashSet<int>();
                                foreach (var kv in shiLiExact)
                                {
                                    if (kv.Key.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                                        matchedShiLiIds.Add(kv.Value);
                                }
                                searchByName = false;
                            }
                            // 解析境界（模糊匹配）
                            else if (levelExact.Any(kv => kv.Key.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                matchedLevelIds = new HashSet<int>();
                                foreach (var kv in levelExact)
                                {
                                    if (kv.Key.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                                        matchedLevelIds.Add(kv.Value);
                                }
                                searchByName = false;
                            }
                            // 解析物品
                            else if (input.StartsWith(":"))
                            {
                                string rest = input.Substring(1).Trim();
                                searchByName = false;
                                searchItem = true;
                                string[] parts2 = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts2.Length >= 2 && int.TryParse(parts2[0], out int quality))
                                {
                                    searchQuality = quality;
                                    searchItemName = string.Join(" ", parts2, 1, parts2.Length - 1);
                                }
                                else
                                {
                                    searchQuality = null;
                                    searchItemName = rest;
                                }
                            }
                            // 解析技能
                            else if (input.StartsWith("!"))
                            {
                                string skillName = input.Substring(1).Trim();
                                if (string.IsNullOrWhiteSpace(skillName))
                                {
                                    UIPopTip.Inst.Pop("请输入技能名称", 0);
                                    return;
                                }
                                searchByName = false;
                                searchSkill = true;
                                matchedSkillIds = new HashSet<int>();
                                foreach (var kv in skillIdToNameMap)
                                {
                                    if (kv.Key.IndexOf(skillName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        foreach (int id in kv.Value)
                                            matchedSkillIds.Add(id);
                                    }
                                }
                                matchedStaticSkillIds = new HashSet<int>();
                                foreach (var kv in staticSkillNameToIdMap)
                                {
                                    if (kv.Key.IndexOf(skillName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        foreach (int id in kv.Value)
                                            matchedStaticSkillIds.Add(id);
                                    }
                                }
                                if (matchedSkillIds.Count == 0 && matchedStaticSkillIds.Count == 0)
                                {
                                    UIPopTip.Inst.Pop($"未找到名称包含“{skillName}”的技能", 0);
                                    return;
                                }
                            }

                            // 遍历NPC
                            foreach (string key in jsonData.instance.AvatarRandomJsonData.keys)
                            {
                                int npcId = int.Parse(key);
                                if (npcId <= 20000) continue;

                                var randomData = jsonData.instance.AvatarRandomJsonData[key];
                                var avatarData = jsonData.instance.AvatarJsonData[key];
                                if (randomData == null || avatarData == null) continue;

                                bool match = false;
                                if (searchByName)
                                {
                                    string name = ToolsEx.ToCN(randomData["Name"].str);
                                    string title = avatarData["Title"].str;
                                    // 姓名匹配或标题匹配（模糊）
                                    if (name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        title.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                                        match = true;
                                }
                                else if (matchedShiLiIds != null)
                                {
                                    int menPai = avatarData["MenPai"].I;
                                    string title = avatarData["Title"].str;
                                    // 门派ID匹配 或 标题包含输入（模糊）
                                    if (matchedShiLiIds.Contains(menPai) || title.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                                        match = true;
                                }
                                else if (matchedLevelIds != null)
                                {
                                    int level = avatarData["Level"].I;
                                    if (matchedLevelIds.Contains(level)) match = true;
                                }
                                else if (searchItem)
                                {
                                    JSONObject backpack = jsonData.instance.AvatarBackpackJsonData[key]["Backpack"];
                                    if (backpack != null && backpack.list != null)
                                    {
                                        foreach (JSONObject itemJson in backpack.list)
                                        {
                                            int itemId = itemJson["ItemID"].I;
                                            JSONObject itemData = jsonData.instance._ItemJsonData[itemId.ToString()];
                                            if (itemData == null) continue;
                                            string itemName = ToolsEx.ToCN(itemData["name"].str);
                                            int quality = itemData.HasField("quality") ? itemData["quality"].I : 0;
                                            if (itemName.IndexOf(searchItemName, StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                if (!searchQuality.HasValue || quality == searchQuality.Value)
                                                {
                                                    match = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (searchSkill)
                                {
                                    // 获取 NPC 的流派（LiuPai）和境界（Level）
                                    int liuPai = avatarData["LiuPai"].I;
                                    int level = avatarData["Level"].I;

                                    // 构建可请教技能 ID 集合（神通和功法分开）
                                    HashSet<int> qingJiaoSkillIds = new HashSet<int>();
                                    HashSet<int> qingJiaoStaticSkillIds = new HashSet<int>();

                                    // 1. 从 NPCLeiXingDate 中匹配流派和境界
                                    foreach (JSONObject entry in jsonData.instance.NPCLeiXingDate.list)
                                    {
                                        if (entry["LiuPai"].I == liuPai && entry["Level"].I <= level)
                                        {
                                            // 神通
                                            JSONObject skills = entry["skills"];
                                            if (skills != null && skills.list != null)
                                            {
                                                foreach (JSONObject sidObj in skills.list)
                                                {
                                                    qingJiaoSkillIds.Add(sidObj.I);
                                                }
                                            }
                                            // 功法
                                            JSONObject staticSkills = entry["staticSkills"];
                                            if (staticSkills != null && staticSkills.list != null)
                                            {
                                                foreach (JSONObject sidObj in staticSkills.list)
                                                {
                                                    qingJiaoStaticSkillIds.Add(sidObj.I);
                                                }
                                            }
                                        }
                                    }

                                    bool hasSkill = false;
                                    foreach (int sid in matchedSkillIds)
                                    {
                                        if (qingJiaoSkillIds.Contains(sid))
                                        {
                                            hasSkill = true;
                                            break;
                                        }
                                    }

                                    if (!hasSkill)
                                    {
                                        foreach (int sid in matchedStaticSkillIds)
                                        {
                                            if (qingJiaoStaticSkillIds.Contains(sid))
                                            {
                                                hasSkill = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (hasSkill) match = true;
                                }

                                if (match)
                                {
                                    NPCInfo info = BuildNPCInfo(key, randomData, avatarData, shiLiData, levelData, actionInfo);
                                    matchedNpcs.Add(info);
                                }
                            }
                        }
                        else if (parts.Length >= 2)
                        {
                            // 双条件：门派 + 境界（模糊匹配）
                            string shiLiPart = parts[0];
                            string levelPart = parts[1];
                            HashSet<int> matchedShiLiIds = new HashSet<int>();
                            foreach (var kv in shiLiExact)
                            {
                                if (kv.Key.IndexOf(shiLiPart, StringComparison.OrdinalIgnoreCase) >= 0)
                                    matchedShiLiIds.Add(kv.Value);
                            }
                            HashSet<int> matchedLevelIds = new HashSet<int>();
                            foreach (var kv in levelExact)
                            {
                                if (kv.Key.IndexOf(levelPart, StringComparison.OrdinalIgnoreCase) >= 0)
                                    matchedLevelIds.Add(kv.Value);
                            }
                            if (matchedShiLiIds.Count == 0 || matchedLevelIds.Count == 0)
                            {
                                UIPopTip.Inst.Pop("没有匹配的门派或境界，请检查输入。", 0);
                                return;
                            }

                            foreach (string key in jsonData.instance.AvatarRandomJsonData.keys)
                            {
                                try
                                {
                                    int npcId = int.Parse(key);
                                    if (npcId <= 20000) continue;
                                    var randomData = jsonData.instance.AvatarRandomJsonData[key];
                                    var avatarData = jsonData.instance.AvatarJsonData[key];
                                    if (randomData == null || avatarData == null) continue;
                                    if (!avatarData.HasField("MenPai") || !avatarData.HasField("Level")) continue;
                                    int menPai = avatarData["MenPai"].I;
                                    int level = avatarData["Level"].I;
                                    string title = avatarData["Title"].str;

                                    // 门派匹配：MenPai 在集合中 或 Title 包含门派关键词
                                    bool shiLiMatch = matchedShiLiIds.Contains(menPai) ||
                                                      title.IndexOf(shiLiPart, StringComparison.OrdinalIgnoreCase) >= 0;
                                    bool levelMatch = matchedLevelIds.Contains(level);

                                    if (shiLiMatch && levelMatch)
                                    {
                                        NPCInfo info = BuildNPCInfo(key, randomData, avatarData, shiLiData, levelData, actionInfo);
                                        matchedNpcs.Add(info);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ModLogger.LogError($"处理 NPC key {key} 时出错: {ex}");
                                }
                            }
                        }

                        if (matchedNpcs.Count == 0)
                        {
                            UIPopTip.Inst.Pop($"没有找到符合条件的 NPC。", 0);
                            return;
                        }

                        // 1. 获取每个NPC的位置
                        foreach (var info in matchedNpcs)
                        {
                            info.Location = GetNpcLocation(info.NpcId);
                        }

                        // 2. 排序：位置非空在前，空在后；性别女>男；境界降序
                        bool showZiZhiAge = showZiZhiAgeConfig.Value;
                        List<NPCInfo> ordered;
                        if (showZiZhiAge)
                        {
                            ordered = matchedNpcs.OrderBy(n => string.IsNullOrEmpty(n.Location) ? 1 : 0)
                                                 .ThenByDescending(n => n.Gender == "[女]" ? 2 : 1)
                                                 .ThenByDescending(n => int.Parse(n.ZiZhi) >= 90 ? 1 : 0)
                                                 .ThenByDescending(n => GetLevelOrder(n.Level))
                                                 .ThenByDescending(n => int.Parse(n.ZiZhi))
                                                 .ToList();
                        }
                        else
                        {
                            ordered = matchedNpcs.OrderBy(n => string.IsNullOrEmpty(n.Location) ? 1 : 0)
                                                 .ThenByDescending(n => n.Gender == "[女]" ? 2 : 1)
                                                 .ThenByDescending(n => GetLevelOrder(n.Level))
                                                 .ToList();
                        }

                        // 3. 生成显示行
                        List<string> displayLines = new List<string>();
                        List<CustomMessageBox.DisplayItem> displayItems = new List<CustomMessageBox.DisplayItem>();
                        foreach (var info in ordered)
                        {
                            string daoLvMark = info.IsDaoLv ? "*" : "";
                            string paddedName = PadNameToWidth(info.Name + daoLvMark);
                            string displayName;
                            if (showZiZhiAge)
                            {
                                displayName = $"{info.Gender} {paddedName} {info.ZiZhi} {info.Age} {info.MenPai} {info.Level}({info.ExpPercent}%) {info.Title} ";
                            }
                            else
                            {
                                displayName = $"{info.Gender} {paddedName} {info.MenPai} {info.Level}({info.ExpPercent}%) {info.Title} ";
                            }
                            string locationInfo = string.IsNullOrEmpty(info.Location)
                                ? $"没空,目前正在{info.ActionDesc}"
                                : $"{info.Location}{info.ActionDesc}";
                            string fullText = $"{displayName} {locationInfo}";

                            int npcId = info.NpcId;
                            displayItems.Add(new CustomMessageBox.DisplayItem
                            {
                                Text = fullText,
                                NpcId = npcId,
                            });
                        }

                        string titleStr = $"查询结果 (共{ordered.Count}人)";
                        CustomMessageBox.ShowList(titleStr, displayItems);
                    }, null, "dongfu");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Update 异常: {ex}");
            }
        }

        // 辅助方法：获取NPC位置信息（合并原三个地图查找逻辑）
        public static string GetNpcLocation(int npcId)
        {
            // 1. 大地图
            foreach (int mapKey in NpcJieSuanManager.inst.npcMap.bigMapNPCDictionary.Keys)
            {
                if (NpcJieSuanManager.inst.npcMap.bigMapNPCDictionary[mapKey].Contains(npcId))
                {
                    string mapName = "大地图上";
                    foreach (string key2 in jsonData.instance.AllMapLuDainType.keys)
                    {
                        if (key2 == mapKey.ToString())
                        {
                            mapName = ToolsEx.ToCN(jsonData.instance.AllMapLuDainType[key2]["LuDianName"].str);
                            break;
                        }
                    }
                    return $"在{mapName}";
                }
            }
            // 2. 场景
            foreach (string sceneKey in NpcJieSuanManager.inst.npcMap.threeSenceNPCDictionary.Keys)
            {
                if (NpcJieSuanManager.inst.npcMap.threeSenceNPCDictionary[sceneKey].Contains(npcId))
                {
                    string sceneName = ToolsEx.ToCN(jsonData.instance.SceneNameJsonData[sceneKey]["MapName"].str);
                    return $"在{sceneName}";
                }
            }
            // 3. 副本
            foreach (string sceneKey in NpcJieSuanManager.inst.npcMap.fuBenNPCDictionary.Keys)
            {
                var dict = NpcJieSuanManager.inst.npcMap.fuBenNPCDictionary[sceneKey];
                foreach (int floor in dict.Keys)
                {
                    if (dict[floor].Contains(npcId))
                    {
                        string sceneName = ToolsEx.ToCN(jsonData.instance.SceneNameJsonData[sceneKey]["MapName"].str);
                        return $"在{sceneName}的第{floor}位置";
                    }
                }
            }
            // 没找到，返回空字符串
            return "";
        }

        // 新增：获取境界名称
        private static string GetLevelName(int levelId)
        {
            JSONObject levelData = jsonData.instance.LevelUpDataJsonData;
            if (levelData != null && levelData.HasField(levelId.ToString()))
            {
                return ToolsEx.ToCN(levelData[levelId.ToString()]["Name"].str);
            }
            return "境界" + levelId;
        }

        public static string GetNpcLocationInfo(int npcId)
        {
            try
            {
                JSONObject avatarData = jsonData.instance.AvatarJsonData[npcId.ToString()];
                if (avatarData == null) return "位置未知";

                string location = GetNpcLocation(npcId);
                int actionId = avatarData["ActionId"].I;
                string actionDesc = "不知道在干什么";
                foreach (string line in actionInfo.Split('\n'))
                {
                    if (line.StartsWith(actionId.ToString() + ","))
                    {
                        actionDesc = line.Replace(actionId.ToString() + ",", "");
                        break;
                    }
                }
                if (string.IsNullOrEmpty(location))
                    return $"没空,目前正在{actionDesc}";
                else
                    return $"{location}{actionDesc}";
            }
            catch
            {
                return "位置未知";
            }
        }
        // 新增：获取NPC简要信息（姓名、境界、位置）

        public static string GetNpcBriefInfo(int npcId)
        {
            try
            {
                JSONObject avatarData = jsonData.instance.AvatarJsonData[npcId.ToString()];
                if (avatarData == null) return "NPC " + npcId;

                string name = GetNpcName(npcId);
                int level = avatarData["Level"].I;
                string levelName = GetLevelName(level);
                // 计算修为百分比
                int exp = avatarData["exp"].I;
                int nextExp = avatarData["NextExp"].I;
                int percent = 0;
                if (nextExp > 0)
                {
                    percent = (int)Math.Round((double)exp / nextExp * 100);
                    if (percent > 100) percent = 100;
                }
                string locationInfo = GetNpcLocationInfo(npcId);
                return $"{name} {levelName}({percent}%) {locationInfo}";
            }
            catch
            {
                return "NPC " + npcId;
            }
        }

        // 辅助方法：构建 NPCInfo 对象
        private NPCInfo BuildNPCInfo(string key, JSONObject randomData, JSONObject avatarData, JSONObject shiLiData, JSONObject levelData, string action_info)
        {
            NPCInfo info = new NPCInfo();
            int npcId = int.Parse(key);
            info.NpcId = npcId;

            // 性别
            int sex = randomData["Sex"].I;
            info.Gender = sex == 1 ? "[男]" : "[女]";

            // 姓名
            string name = randomData["Name"].str;
            info.Name = string.IsNullOrEmpty(name) ? "" : Regex.Unescape(name);

            // 门派
            int menPaiId = avatarData["MenPai"].I;
            if (shiLiData.keys.Contains(menPaiId.ToString()))
            {
                string menpai = shiLiData[menPaiId.ToString()]["name"].str;
                info.MenPai = string.IsNullOrEmpty(menpai) ? "" : Regex.Unescape(menpai);
            }
            else
            {
                info.MenPai = "";
            }

            // 境界
            int levelId = avatarData["Level"].I;
            if (levelData.keys.Contains(levelId.ToString()))
            {
                string levelstr = levelData[levelId.ToString()]["Name"].str;
                info.Level = string.IsNullOrEmpty(levelstr) ? "" : Regex.Unescape(levelstr);
            }
            else
            {
                info.Level = "";
            }

            // 计算修为百分比
            int exp = avatarData["exp"].I;
            int nextExp = avatarData["NextExp"].I;
            int percent = 0;
            if (nextExp > 0)
            {
                percent = (int)Math.Round((double)exp / nextExp * 100);
                if (percent > 100) percent = 100;
            }
            info.ExpPercent = percent;

            info.Title = avatarData["Title"].str ?? "";

            // 行为描述
            int actionId = avatarData["ActionId"].I;
            string actionDesc = "不知道在干什么";
            foreach (string line in action_info.Split('\n'))
            {
                if (line.StartsWith(actionId.ToString() + ","))
                {
                    actionDesc = line.Replace(actionId.ToString() + ",", "");
                    break;
                }
            }
            info.ActionDesc = actionDesc;

            // ====== 新增：年龄/寿元 ======
            int ageMonths = avatarData.HasField("age") ? avatarData["age"].I : 0;
            int shouYuan = avatarData.HasField("shouYuan") ? avatarData["shouYuan"].I : 0;
            int ageYears = ageMonths / 12;
            //info.Age = $"{ageYears}/{shouYuan}";
            info.Age = $"{shouYuan - ageYears}";

            // ====== 新增：资质 ======
            //info.ZiZhi = avatarData.HasField("ziZhi") ? (avatarData["ziZhi"].I + (shouYuan - ageYears)).ToString() : "?";
            info.ZiZhi = avatarData.HasField("ziZhi") ? (avatarData["ziZhi"].I).ToString() : "?";

            // 位置信息延迟填充，不在这里处理
            info.Location = "";

            // 判断是否道侣
            info.IsDaoLv = PlayerEx.IsDaoLv(npcId);

            return info;
        }

        // 辅助方法：获取境界排序权重（境界越高权重越大，用于降序）
        private int GetLevelOrder(string levelName)
        {
            // 按境界从高到低定义权重，可根据实际需要扩展
            Dictionary<string, int> levelWeights = new Dictionary<string, int>
            {
                {"炼气初期", 1},
                {"炼气中期", 2},
                {"炼气后期", 3},
                {"筑基初期", 4},
                {"筑基中期", 5},
                {"筑基后期", 6},
                {"金丹初期", 7},
                {"金丹中期", 8},
                {"金丹后期", 9},
                {"元婴初期", 10},
                {"元婴中期", 11},
                {"元婴后期", 12},
                {"化神初期", 13},
                {"化神中期", 14},
                {"化神后期", 15}
            };
            if (levelWeights.TryGetValue(levelName, out int weight))
                return weight;
            return 0; // 未知境界放最后
        }

        // 计算字符串显示宽度（中文=2，其他=1）
        private int GetDisplayWidth(string text)
        {
            int width = 0;
            foreach (char c in text)
            {
                if (c >= 0x4e00 && c <= 0x9fa5) // 基本汉字
                    width += 2;
                else
                    width += 1;
            }
            return width;
        }

        // 填充姓名到目标宽度（目标宽度为8，即4个汉字宽度）
        private string PadNameToWidth(string name, int targetWidth = 9)
        {
            int currentWidth = GetDisplayWidth(name);
            if (currentWidth >= targetWidth)
                return name;
            int spaces = targetWidth - currentWidth;
            return name + new string(' ', spaces);
        }

        public static int logCounter = 0;
        public static void LogInfo(string msg)
        {
            ModLogger.LogInfo($"[{logCounter++}] {msg}");
        }
        public static void LogError(string msg)
        {
            ModLogger.LogError($"[{logCounter++}] {msg}");
        }

        // 新增：NPC信息类
        public class NPCInfo
        {
            public int NpcId { get; set; }
            public string Gender { get; set; }      // "[男]" 或 "[女]"
            public string Name { get; set; }
            public string MenPai { get; set; }
            public string Level { get; set; }
            public string Title { get; set; }
            public string ActionDesc { get; set; }
            public string Location { get; set; }    // 位置描述（可选，可在显示时生成）
            public string ZiZhi { get; set; }
            public string Age { get; set; }
            public bool IsDaoLv { get; set; }  // 新增
            public int ExpPercent { get; set; }
        }
    }
}