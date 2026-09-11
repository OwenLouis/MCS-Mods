using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Yueyehuiling.MCS.AllInOneMod.Features.ExchangeMeeting;

namespace Yueyehuiling.MCS.AllInOneMod
{
    [BepInPlugin("Yueyehuiling.MCS.AllInOneMod", "AllInOneMod", "1.0.0")]
    public class Main : BaseUnityPlugin
    {
        private static ManualLogSource ModLogger;

        // ====== 功能开关 ======
        public static ConfigEntry<bool> EnableSortNpcPack;
        public static ConfigEntry<bool> EnableSortBiGuanTuPo;
        public static ConfigEntry<bool> EnableCopyMenpaiTask;
        public static ConfigEntry<bool> EnableOptimizeExchange;
        public static ConfigEntry<bool> EnableScheduledEvent;
        public static ConfigEntry<bool> EnableBiGuanExpectedDate;
        public static ConfigEntry<bool> EnableFixSteamChengJiu;
        public static ConfigEntry<bool> EnablePopTipMerger;
        public static ConfigEntry<bool> EnableSkillDamagePreview;
        public static ConfigEntry<bool> EnablePermanentRoundCount;
        public static ConfigEntry<bool> EnableRemoveSeaFog;

        // ====== 快捷键配置 ======
        public static ConfigEntry<KeyboardShortcut> ExchangeShortcutKey;
        // ====== 其他配置 ======
        public static ConfigEntry<float> MergeInterval;

        // ====== Harmony 实例 ======
        private Harmony _harmony;

        private void Awake()
        {
            ModLogger = base.Logger;
            ModLogger.LogInfo("AllInOneMod 加载中...");

            // 初始化所有功能开关
            EnableSortNpcPack = Config.Bind("功能开关", "NPC背包排序", true, "打开NPC背包时自动排序");
            EnableSortBiGuanTuPo = Config.Bind("功能开关", "闭关突破排序", true, "突破界面功法按突破层数/品级排序");
            EnableCopyMenpaiTask = Config.Bind("功能开关", "宗门任务复制", true, "宗门长老派发任务列表添加复制按钮");
            EnableOptimizeExchange = Config.Bind("功能开关", "交易会快捷键和复制", true, "快捷键打开元婴交易会，元婴交易会列表添加复制按钮");
            EnableScheduledEvent = Config.Bind("功能开关", "天衍阁传音预告", true, "任务追踪显示天衍阁传音预告");
            EnableBiGuanExpectedDate = Config.Bind("功能开关", "显示闭关预计日期", true, "闭关修炼时显示预计日期");
            EnableFixSteamChengJiu = Config.Bind("功能开关", "修复Steam成就问题", true, "修复吸血技能无法完成总计造成伤害成就的问题");
            EnablePopTipMerger = Config.Bind("功能开关", "合并重复消息提示", true, "合并短时间内重复的右侧消息提示");
            EnableSkillDamagePreview = Config.Bind("功能开关", "技能伤害预测", true, "显示技能造成的预测伤害，增加部分伤害记录");
            EnablePermanentRoundCount = Config.Bind("功能开关", "常驻回合数显示", true, "战斗中常驻显示当前回合数");
            EnableRemoveSeaFog = Config.Bind("功能开关", "去除无尽海迷雾", false, "去除无尽海地图上的迷雾遮罩");

            // 初始化快捷键
            ExchangeShortcutKey = Config.Bind("快捷键", "打开交易会", new KeyboardShortcut(KeyCode.H), "打开交易会窗口");

            // 初始化其他配置
            MergeInterval = Config.Bind("详细配置", "消息合并间隔秒数", 0.5f, "相同消息合并输出的时间窗口（秒）");

            // 初始化 Harmony
            _harmony = new Harmony("Yueyehuiling.MCS.AllInOneMod");

            // 根据开关注册补丁
            ApplyPatches();

            ModLogger.LogInfo("AllInOneMod 加载完成！");
        }

        private void ApplyPatches()
        {
            // 1. NPC背包排序
            if (EnableSortNpcPack.Value)
            {
                _harmony.PatchAll(typeof(Features.SortNpcPack.SortNpcPackPatch));
                ModLogger.LogInfo("已启用: NPC背包排序");
            }

            // 2. 闭关突破排序
            if (EnableSortBiGuanTuPo.Value)
            {
                _harmony.PatchAll(typeof(Features.SortBiGuanTuPo.SortBiGuanTuPoPatch));
                ModLogger.LogInfo("已启用: 闭关突破排序");
            }

            // 3. 宗门任务复制按钮
            if (EnableCopyMenpaiTask.Value)
            {
                _harmony.PatchAll(typeof(Features.CopyMenpaiTask.CopyMenpaiTaskPatch));
                ModLogger.LogInfo("已启用: 宗门任务复制");
            }

            // 4. 交易会快捷键和复制
            if (EnableOptimizeExchange.Value)
            {
                _harmony.PatchAll(typeof(Features.ExchangeMeeting.CopyExchangePatch));
                var listener = gameObject.AddComponent<ExchangeKeyListener>();
                listener.Init(ExchangeShortcutKey);
                ModLogger.LogInfo("已启用: 交易会快捷键和复制");
            }

            // 5. 天衍阁传音预告
            if (EnableScheduledEvent.Value)
            {
                _harmony.PatchAll(typeof(Features.ScheduledEvent.ScheduledEventUIPatch));
                ModLogger.LogInfo("已启用: 天衍阁传音预告");
            }

            // 6. 显示闭关预计日期
            if (EnableBiGuanExpectedDate.Value)
            {
                _harmony.PatchAll(typeof(Features.BiGuanExpectedDate.BiGuanExpectedDatePatch));
                ModLogger.LogInfo("已启用: 显示闭关预计日期");
            }

            // 7. 修复Steam成就问题
            if (EnableFixSteamChengJiu.Value)
            {
                _harmony.PatchAll(typeof(Features.FixSteamChengJiu.SteamChengJiuPatch));
                ModLogger.LogInfo("已启用: 修复Steam成就问题");
            }

            // 8. 合并重复消息提示
            if (EnablePopTipMerger.Value)
            {
                _harmony.PatchAll(typeof(Features.PopTipMerger.PopTipMergerPatch));
                ModLogger.LogInfo("已启用: 合并重复消息提示");
            }

            // 9. 技能伤害预测
            if (EnableSkillDamagePreview.Value)
            {
                _harmony.PatchAll(typeof(Features.SkillDamagePreview.SkillDamagePreviewPatch));
                ModLogger.LogInfo("已启用: 技能伤害预测");
            }

            // 10. 常驻回合数显示
            if (EnablePermanentRoundCount.Value)
            {
                _harmony.PatchAll(typeof(Features.PermanentRoundCount.PermanentRoundCountPatch));
                ModLogger.LogInfo("已启用: 常驻回合数显示");
            }

            // 11. 去除无尽海迷雾
            if (EnableRemoveSeaFog.Value)
            {
                _harmony.PatchAll(typeof(Features.RemoveSeaFog.RemoveSeaFogPatch));
                ModLogger.LogInfo("已启用: 去除无尽海迷雾");
            }
        }

        public static void LogInfo(string message)
        {
            ModLogger.LogInfo(message);
        }

        public static void LogError(string message)
        {
            ModLogger.LogError(message);
        }
    }
}
