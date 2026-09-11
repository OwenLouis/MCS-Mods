using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GUIPackage;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yueyehuiling.MCS.CustomNPCSpawn
{
    [BepInPlugin("Yueyehuiling.MCS.CustomNPCSpawn", "CustomNPCSpawn", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        internal static ManualLogSource ModLogger;
        private static ConfigEntry<string> spawnConfig;
        private static ConfigEntry<int> spawnIntervalYears;
        private static ConfigEntry<int> sexMode;
        private static ConfigEntry<int> maxNpcCount;
        internal static int lastSpawnYear = -1;
        internal static bool statisticsPrinted = false;

        // 默认配置：境界范围 1-3（炼气初期~后期），数量取自原 NpcCreateData
        private const string DefaultSpawnRules =
            "1,1-3,4;2,1-3,4;3,1-3,4;4,1-3,4;5,1-3,4;6,1-3,2;7,1-3,2;8,1-3,2;9,1-3,2;10,1-3,8;11,1-3,2;12,1-3,2;13,1-3,3;14,1-3,3;23,1-3,2";

        private void Awake()
        {
            ModLogger = base.Logger;
            spawnConfig = Config.Bind("General", "SpawnRules", DefaultSpawnRules,
                "格式: Type,Level,Count 或 Type,LevelMin-LevelMax,Count 或 Type,LevelMin-LevelMax,min-max\n" +
                "例如: 1,1-3,3-5;2,2,2 表示生成3~5个Type1（境界1~3随机）和2个Type2（境界2）\n" +
                "Type 与门派对应: 1竹山宗 2金虹剑派 3星河剑派 4离火门 5化尘教 6-14为散修等");
            spawnIntervalYears = Config.Bind("General", "SpawnIntervalYears", 20,
                "生成NPC的间隔年数（>=20）。游戏内每20年调用一次，建议设为20的倍数。");
            sexMode = Config.Bind("General", "SexMode", 0,
                "性别模式：0=正常（随机），1=仅男性，2=仅女性。");
            maxNpcCount = Config.Bind("General", "MaxNpcCount", 1000,
                "最大NPC总数（0表示无限制）。生成前检查一次，过程中不再检查。");

            Harmony.CreateAndPatchAll(typeof(CustomSpawnPatch));
            ModLogger.LogInfo("自定义NPC追加生成 Mod 加载完成。");
        }

        public static class CustomSpawnPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(YSNewSaveSystem), "LoadSave")]
            public static void LoadSavePostfix()
            {
                ModLogger.LogInfo("读取存档重置配置");
                Main.lastSpawnYear = -1;
                Main.statisticsPrinted = false;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(UI_Manager), "Update")]
            public static void UpdatePostfix()
            {
                if (!statisticsPrinted)
                {
                    PrintNpcStatistics();
                    statisticsPrinted = true;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(NPCFactory), "AuToCreateNpcs")]

            public static void AuToCreateNpcsPostfix(NPCFactory __instance)
            {
                try
                {
                    // 间隔检查
                    int interval = spawnIntervalYears.Value;
                    if (interval < 10) interval = 10;
                    int currentYear = GetCurrentYear();
                    if (lastSpawnYear == -1)
                    {
                        // 首次运行：直接生成，并记录年份
                    }
                    else if (currentYear - lastSpawnYear < interval)
                    {
                        ModLogger.LogInfo($"距离上次生成已过 {currentYear - lastSpawnYear} 年，未达到间隔 {interval} 年，跳过。");
                        return;
                    }

                    // 执行生成（生成前检查最大数量）
                    int max = maxNpcCount.Value;
                    int currentCount = GetCurrentNpcCount();
                    if (max > 0)
                    {
                        if (currentCount >= max)
                        {
                            ModLogger.LogInfo($"当前NPC数量 {currentCount} 已达到上限 {max}，跳过生成。");
                            return;
                        }
                        ModLogger.LogInfo($"当前NPC数量 {currentCount}，上限 {max}，将尝试生成。");
                    }

                    ModLogger.LogInfo($"当前年份 {currentYear} ，开始生成追加NPC。");
                    lastSpawnYear = currentYear;
                    int generatedCount = SpawnCustomNPCs(__instance);

                    // 显示提示
                    if (generatedCount > 0)
                    {
                        UIPopTip.Inst.Pop($"本次额外生成了 {generatedCount} 个 NPC，当前总数为 {currentCount}", 0);
                    }

                    // 生成后输出统计
                    PrintNpcStatistics();
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"自定义NPC追加生成出错：{ex}");
                }
            }
            private static int GetCurrentYear()
            {
                var player = Tools.instance.getPlayer();
                if (player != null && player.worldTimeMag != null)
                {
                    return player.worldTimeMag.getNowTime().Year;
                }
                return 0;
            }
            private static int GetCurrentNpcCount()
            {
                var avatarData = jsonData.instance.AvatarJsonData;
                if (avatarData == null) return 0;
                int count = 0;
                foreach (string key in avatarData.keys)
                {
                    if (int.TryParse(key, out int id) && id >= 20000)
                        count++;
                }
                return count;
            }
            private static void PrintNpcStatistics()
            {
                try
                {
                    var avatarData = jsonData.instance.AvatarJsonData;
                    if (avatarData == null || avatarData.keys == null)
                    {
                        ModLogger.LogInfo("AvatarJsonData 未加载，无法统计 NPC。");
                        return;
                    }

                    List<string> keys = new List<string>(avatarData.keys);
                    ModLogger.LogInfo($"开始统计 NPC，共 {keys.Count} 个数据条目。");

                    // 门派名称映射
                    var shiLiData = jsonData.instance.CyShiLiNameData;
                    Dictionary<int, string> shiLiNameMap = new Dictionary<int, string>();
                    if (shiLiData != null)
                    {
                        foreach (string key in shiLiData.keys)
                        {
                            int id = int.Parse(key);
                            string name = ToolsEx.ToCN(shiLiData[key]["name"].str);
                            if (!string.IsNullOrEmpty(name))
                                shiLiNameMap[id] = name;
                        }
                    }

                    // 大境界名称
                    string[] majorRealmNames = new string[] { "炼气期", "筑基期", "金丹期", "元婴期", "化神期" };

                    // 统计: 门派ID -> (大境界ID -> 数量)
                    Dictionary<int, Dictionary<int, int>> stats = new Dictionary<int, Dictionary<int, int>>();
                    // 全局境界统计: 大境界ID -> 数量
                    Dictionary<int, int> globalRealmStats = new Dictionary<int, int>();
                    // 性别统计
                    int maleCount = 0, femaleCount = 0, unknownSexCount = 0;

                    foreach (string key in keys)
                    {
                        if (!int.TryParse(key, out int npcId))
                            continue;
                        if (npcId < 20000) continue;

                        var entry = avatarData[key];
                        if (entry == null) continue;

                        if (!entry.HasField("MenPai") || !entry.HasField("Level"))
                            continue;

                        int menPai = 0;
                        var menPaiField = entry["MenPai"];
                        if (menPaiField.IsNumber)
                            menPai = menPaiField.I;
                        else if (menPaiField.IsString && !string.IsNullOrEmpty(menPaiField.str))
                            int.TryParse(menPaiField.str, out menPai);

                        int level = entry["Level"].I;
                        int majorRealmId = (level - 1) / 3 + 1;
                        if (majorRealmId < 1 || majorRealmId > 5) majorRealmId = 0;

                        // 性别
                        int sex = entry.HasField("SexType") ? entry["SexType"].I : 0;
                        if (sex == 1) maleCount++;
                        else if (sex == 2) femaleCount++;
                        else unknownSexCount++;

                        // 门派统计
                        if (!stats.ContainsKey(menPai))
                            stats[menPai] = new Dictionary<int, int>();
                        var levelDict = stats[menPai];
                        if (levelDict.ContainsKey(majorRealmId))
                            levelDict[majorRealmId]++;
                        else
                            levelDict[majorRealmId] = 1;

                        // 全局境界统计
                        if (globalRealmStats.ContainsKey(majorRealmId))
                            globalRealmStats[majorRealmId]++;
                        else
                            globalRealmStats[majorRealmId] = 1;
                    }

                    // 构建输出
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("\n========== NPC 数量统计 ==========");

                    // 1. 性别统计
                    sb.AppendLine("【性别】");
                    sb.AppendLine($"  男性: {maleCount} 人");
                    sb.AppendLine($"  女性: {femaleCount} 人");
                    if (unknownSexCount > 0)
                        sb.AppendLine($"  未知: {unknownSexCount} 人");
                    sb.AppendLine();

                    // 2. 不分门派的境界统计
                    int globalTotal = globalRealmStats.Values.Sum();
                    sb.AppendLine($"【全局统计】 共 {globalTotal} 人");
                    int totalRealmNpc = 0;
                    var sortedRealms = globalRealmStats.Keys.ToList();
                    sortedRealms.Sort();
                    foreach (int majorId in sortedRealms)
                    {
                        int count = globalRealmStats[majorId];
                        string levelName = (majorId >= 1 && majorId <= 5) ? majorRealmNames[majorId - 1] : "未知";
                        sb.AppendLine($"  {levelName}: {count} 人");
                        totalRealmNpc += count;
                    }
                    sb.AppendLine();

                    // 3. 门派分布
                    int totalNpc = 0;
                    var sortedMenPai = stats.Keys.ToList();
                    sortedMenPai.Sort();

                    foreach (int menPaiId in sortedMenPai)
                    {
                        var levelDict = stats[menPaiId];
                        string menPaiName = shiLiNameMap.TryGetValue(menPaiId, out string name) ? name : $"门派{menPaiId}";
                        int menPaiTotal = levelDict.Values.Sum();

                        sb.AppendLine($"【{menPaiName}】 共 {menPaiTotal} 人");
                        var sortedLevels = levelDict.Keys.ToList();
                        sortedLevels.Sort();
                        foreach (int majorId in sortedLevels)
                        {
                            int count = levelDict[majorId];
                            string levelName = (majorId >= 1 && majorId <= 5) ? majorRealmNames[majorId - 1] : "未知";
                            sb.AppendLine($"  {levelName}: {count} 人");
                        }
                        sb.AppendLine();
                    }

                    // 总计
                    foreach (var levelDict in stats.Values)
                        totalNpc += levelDict.Values.Sum();

                    sb.AppendLine($"总计: {totalNpc} 个 NPC");
                    sb.AppendLine("=============================================");

                    ModLogger.LogInfo(sb.ToString());
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"统计 NPC 失败：{ex}");
                }
            }
            private static int SpawnCustomNPCs(NPCFactory factory)
            {
                string configStr = spawnConfig.Value?.Trim();
                if (string.IsNullOrEmpty(configStr))
                {
                    ModLogger.LogInfo("未配置追加生成规则。");
                    return 0;
                }

                var rules = ParseRules(configStr);
                if (rules == null || rules.Count == 0) return 0;

                int sex = sexMode.Value;
                int max = maxNpcCount.Value;
                int currentCount = GetCurrentNpcCount();

                // 生成前检查一次最大数量
                if (max > 0 && currentCount >= max)
                {
                    ModLogger.LogInfo($"当前NPC数量 {currentCount} 已达到上限 {max}，跳过本次生成。");
                    return 0;
                }

                Random rand = new Random();
                int totalGenerated = 0;

                foreach (var rule in rules)
                {
                    // 确定实际生成数量（若范围为随机）
                    int targetCount = rule.Count;
                    if (rule.IsCountRange)
                    {
                        targetCount = rand.Next(rule.MinCount, rule.MaxCount + 1);
                    }

                    int successCount = 0;
                    for (int i = 0; i < targetCount; i++)
                    {
                        // 确定实际境界
                        int actualLevel = rule.Level;
                        if (rule.IsLevelRange)
                        {
                            actualLevel = rand.Next(rule.MinLevel, rule.MaxLevel + 1);
                        }

                        JSONObject template = FindTemplate(rule.Type, actualLevel);
                        if (template == null)
                        {
                            ModLogger.LogInfo($"未找到 Type={rule.Type}, Level={actualLevel} 的模板，跳过该 NPC。");
                            continue;
                        }

                        try
                        {
                            int npcId = factory.AfterCreateNpc(template, false, 0, false, null, sex);
                            if (npcId > 0)
                            {
                                successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogError($"生成 NPC Type={rule.Type}, Level={actualLevel} 失败: {ex.Message}");
                        }
                    }
                    ModLogger.LogInfo($"成功生成 {successCount}/{targetCount} 个 Type={rule.Type}（境界范围 {rule.LevelDisplay}）的 NPC。");
                    totalGenerated += successCount;
                }
                return totalGenerated;
            }
            private static JSONObject FindTemplate(int type, int level)
            {
                var leiXingData = jsonData.instance.NPCLeiXingDate;
                if (leiXingData == null) return null;

                foreach (string key in leiXingData.keys)
                {
                    var entry = leiXingData[key];
                    if (entry == null) continue;
                    if (entry["Type"].I == type && entry["Level"].I == level)
                    {
                        return entry;
                    }
                }
                return null;
            }
            private class SpawnRule
            {
                public int Type;
                public int Level;           // 当 IsLevelRange=false 时使用
                public int MinLevel;
                public int MaxLevel;
                public bool IsLevelRange;
                public int Count;           // 当 IsCountRange=false 时使用
                public int MinCount;
                public int MaxCount;
                public bool IsCountRange;
                public string LevelDisplay => IsLevelRange ? $"{MinLevel}-{MaxLevel}" : Level.ToString();
            }
            private static List<SpawnRule> ParseRules(string configStr)
            {
                var list = new List<SpawnRule>();
                string[] entries = configStr.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string entry in entries)
                {
                    string[] parts = entry.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3) continue;
                    if (!int.TryParse(parts[0].Trim(), out int type)) continue;

                    string levelPart = parts[1].Trim();
                    string countPart = parts[2].Trim();

                    // 解析 Level
                    int level = 0, minLevel = 0, maxLevel = 0;
                    bool isLevelRange = false;
                    if (levelPart.Contains('-'))
                    {
                        string[] lr = levelPart.Split('-');
                        if (lr.Length == 2 && int.TryParse(lr[0].Trim(), out minLevel) && int.TryParse(lr[1].Trim(), out maxLevel) && minLevel >= 0 && maxLevel >= minLevel)
                        {
                            isLevelRange = true;
                        }
                        else
                        {
                            ModLogger.LogInfo($"无效的境界范围: {levelPart}，跳过。");
                            continue;
                        }
                    }
                    else
                    {
                        if (!int.TryParse(levelPart, out level)) continue;
                    }

                    // 解析 Count
                    int count = 0, minCount = 0, maxCount = 0;
                    bool isCountRange = false;
                    if (countPart.Contains('-'))
                    {
                        string[] cr = countPart.Split('-');
                        if (cr.Length == 2 && int.TryParse(cr[0].Trim(), out minCount) && int.TryParse(cr[1].Trim(), out maxCount) && minCount >= 0 && maxCount >= minCount)
                        {
                            isCountRange = true;
                        }
                        else
                        {
                            ModLogger.LogInfo($"无效的数量范围: {countPart}，跳过。");
                            continue;
                        }
                    }
                    else
                    {
                        if (!int.TryParse(countPart, out count)) continue;
                    }

                    var rule = new SpawnRule
                    {
                        Type = type,
                        IsLevelRange = isLevelRange,
                        IsCountRange = isCountRange
                    };
                    if (isLevelRange)
                    {
                        rule.MinLevel = minLevel;
                        rule.MaxLevel = maxLevel;
                    }
                    else
                    {
                        rule.Level = level;
                    }
                    if (isCountRange)
                    {
                        rule.MinCount = minCount;
                        rule.MaxCount = maxCount;
                    }
                    else
                    {
                        rule.Count = count;
                    }
                    list.Add(rule);
                }
                return list;
            }
        }
    }
}