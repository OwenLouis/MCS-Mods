using HarmonyLib;
using JSONClass;
using KBEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using YSGame;
using YSGame.Fight;

namespace Yueyehuiling.MCS.AllInOneMod.Features.SkillDamagePreview
{
    public static class SkillDamagePreviewPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFightSkillTip), "SetSkill")]
        public static void Postfix(UIFightSkillTip __instance, GUIPackage.Skill skill)
        {
            try
            {
                if (!_skillJsonData.DataDict.TryGetValue(skill.skill_ID, out var skillData))
                    return;
                if (skillData.script != "SkillAttack")
                    return;

                Avatar player = Tools.instance.getPlayer();
                if (player == null) return;

                Avatar receiver = player.OtherAvatar;
                if (receiver == null) return;

                // --- 备份技能状态 ---
                float oldCurCD = skill.CurCD;
                var oldNowSkillIsChuFa = new Dictionary<int, bool>(skill.nowSkillIsChuFa);

                // --- 备份玩家/敌人状态 ---
                var backup = new GameStateBackup(player, receiver);

                bool wasVirtual = RoundManager.instance.IsVirtual;
                RoundManager.instance.IsVirtual = true;

                // 临时修改受击者血量，使损失血量比例不变，但绝对数值极大，避免死亡判定
                int originalHP = receiver.HP;
                int originalHP_Max = receiver._HP_Max;
                float percent = originalHP / (float)receiver.HP_Max;
                // 将受击者最大血量设置为 int.MaxValue / 200，防止溢出
                receiver._HP_Max = int.MaxValue / 200;
                // 按比例缩放当前血量，防止溢出（用 long 计算）
                long scaledHP = (long)((int.MaxValue / (float)200) * percent);
                receiver.HP = (int)scaledHP;

                // 临时提高 buff 9200 的层数，防止触发剧情
                if (receiver.buffmag.HasBuff(9200))
                {
                    foreach (var buff in receiver.bufflist)
                    {
                        if (buff[2] == 9200)
                        {
                            buff[1] = int.MaxValue / 200; // 设为一个足够大的数
                            break;
                        }
                    }
                }

                List<int> result = skill.VirtualPutingSkill(player, receiver, 0);
                int predictedDamage = (result != null && result.Count > 0) ? result[0] : 0;

                // 恢复受击者血量
                receiver._HP_Max = originalHP_Max;
                receiver.HP = originalHP;

                // --- 恢复技能状态 ---
                skill.CurCD = oldCurCD;
                skill.nowSkillIsChuFa.Clear();
                foreach (var kv in oldNowSkillIsChuFa)
                    skill.nowSkillIsChuFa[kv.Key] = kv.Value;

                // --- 恢复玩家/敌人状态 ---
                backup.Restore();

                // --- 清理动画/动作队列 ---
                YSFuncList.Ints.Clear();

                RoundManager.instance.IsVirtual = wasVirtual;

                // --- 强制刷新技能图标，消除短暂变暗 ---
                if (UIFightPanel.Inst != null)
                    UIFightPanel.Inst.RefreshCD();

                // --- 显示预测伤害 ---
                string damageText;
                if (predictedDamage > 0)
                    damageText = $"<color=#FF6B6B>预测伤害：{predictedDamage}</color>";
                else if (predictedDamage < 0)
                    damageText = $"<color=#6BCB6B>预测恢复：{-predictedDamage}</color>";
                else
                    damageText = $"<color=#FFD93D>预测伤害：0</color>";

                if (!__instance.SkillDescriptionText.text.Contains("预测伤害") &&
                    !__instance.SkillDescriptionText.text.Contains("预测恢复"))
                {
                    __instance.SkillDescriptionText.text += "\n\n" + damageText;
                }
            }
            catch (System.Exception ex)
            {
                Main.LogError($"[SkillDamagePreview] 执行异常: {ex.Message}");
            }
        }

        private class GameStateBackup
        {
            private Avatar player, receiver;
            private int playerHP, receiverHP;
            private string playerBuffJson, receiverBuffJson;
            private List<card> playerCards, receiverCards;
            private List<int> playerUsedSkills, receiverUsedSkills;
            private Dictionary<int, Dictionary<int, int>> playerSkillSeidFlag, receiverSkillSeidFlag;
            private Dictionary<int, Dictionary<int, int>> playerBuffSeidFlag, receiverBuffSeidFlag;

            // 备份死亡标志
            private bool savedSomeOneDie;
            private Avatar savedDieAvatar;

            public GameStateBackup(Avatar p, Avatar r)
            {
                player = p;
                receiver = r;
                playerHP = p.HP;
                receiverHP = r.HP;
                playerBuffJson = JsonConvert.SerializeObject(p.bufflist);
                receiverBuffJson = JsonConvert.SerializeObject(r.bufflist);
                playerCards = new List<card>(p.cardMag._cardlist);
                receiverCards = new List<card>(r.cardMag._cardlist);
                playerUsedSkills = new List<int>(p.UsedSkills);
                receiverUsedSkills = new List<int>(r.UsedSkills);
                playerSkillSeidFlag = DeepCopyDict(p.SkillSeidFlag);
                receiverSkillSeidFlag = DeepCopyDict(r.SkillSeidFlag);
                playerBuffSeidFlag = DeepCopyDict(p.BuffSeidFlag);
                receiverBuffSeidFlag = DeepCopyDict(r.BuffSeidFlag);

                if (RoundManager.instance != null)
                {
                    savedSomeOneDie = RoundManager.instance.SomeOneDie;
                    savedDieAvatar = RoundManager.instance.DieAvatar;
                }
            }

            public void Restore()
            {
                player.HP = playerHP;
                receiver.HP = receiverHP;
                player.bufflist = JsonConvert.DeserializeObject<List<List<int>>>(playerBuffJson);
                receiver.bufflist = JsonConvert.DeserializeObject<List<List<int>>>(receiverBuffJson);
                player.cardMag._cardlist = playerCards;
                receiver.cardMag._cardlist = receiverCards;
                player.UsedSkills = playerUsedSkills;
                receiver.UsedSkills = receiverUsedSkills;
                player.SkillSeidFlag = DeepCopyDict(playerSkillSeidFlag);
                receiver.SkillSeidFlag = DeepCopyDict(receiverSkillSeidFlag);
                player.BuffSeidFlag = DeepCopyDict(playerBuffSeidFlag);
                receiver.BuffSeidFlag = DeepCopyDict(receiverBuffSeidFlag);

                if (RoundManager.instance != null)
                {
                    RoundManager.instance.SomeOneDie = savedSomeOneDie;
                    RoundManager.instance.DieAvatar = savedDieAvatar;
                }
            }

            private Dictionary<int, Dictionary<int, int>> DeepCopyDict(
                Dictionary<int, Dictionary<int, int>> src)
            {
                var dst = new Dictionary<int, Dictionary<int, int>>();
                foreach (var kv in src)
                {
                    dst[kv.Key] = new Dictionary<int, int>(kv.Value);
                }
                return dst;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Avatar), "setHP")]
        public static bool setHPPrefix()
        {
            if (RoundManager.instance != null && RoundManager.instance.IsVirtual)
            {
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Avatar), "die")]
        public static bool diePrefix()
        {
            if (RoundManager.instance != null && RoundManager.instance.IsVirtual)
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Avatar), "recvDamage", new System.Type[] { typeof(Entity), typeof(Entity), typeof(int), typeof(int), typeof(int) })]
        public static void RecvDamagePostfix(Entity _attaker, Entity _receiver, int skillId, int __result)
        {
            if (skillId >= 10000 && __result > 0 && !RoundManager.instance.IsVirtual)
            {
                FightJiLuAddText(_attaker, _receiver, skillId, __result);
            }
        }

        public static void FightJiLuAddText(Entity _attaker, Entity _receiver, int skillId, int damage)
        {
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)_attaker.renderObj;
            UnityEngine.GameObject gameObject2 = (UnityEngine.GameObject)_receiver.renderObj;
            string text = "<color=#aeee7f>" + gameObject.GetComponent<GameEntity>().entity_name + "</color>";
            string text2 = "<color=#f27a2b>" + gameObject2.GetComponent<GameEntity>().entity_name + "</color>";
            string text3 = ToolsEx.ToCN(jsonData.instance.skillJsonData[string.Concat(skillId)]["name"].str);
            string text4 = "<color=#f6c73c>" + text3 + "</color>";
            string text5 = "";
            text5 = ((damage > 0) ? (",造成<color=#ffec96>" + damage + "</color>点伤害") : ((damage >= 0) ? "。" : (",回复<color=#ffec96>" + damage + "</color>点生命")));
            string text6 = "";
            text6 = ((_attaker != _receiver) ? (text + "对" + text2 + "释放了" + text4 + text5) : (text + "释放了" + text4 + text5));
            UIFightPanel.Inst.FightJiLu.AddText(text6);
        }
    }
}
