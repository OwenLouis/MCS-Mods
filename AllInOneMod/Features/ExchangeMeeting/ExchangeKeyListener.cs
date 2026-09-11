using BepInEx.Configuration;
using script.ExchangeMeeting.UI.Interface;
using System;
using UnityEngine;

namespace Yueyehuiling.MCS.AllInOneMod.Features.ExchangeMeeting
{
    public class ExchangeKeyListener : MonoBehaviour
    {
        private ConfigEntry<KeyboardShortcut> _key;

        public void Init(ConfigEntry<KeyboardShortcut> key)
        {
            _key = key;
        }

        public void Update()
        {
            try
            {
                if (_key == null) return;
                if (_key.Value.IsDown())
                {
                    var avatar = Tools.instance.getPlayer();
                    if (avatar == null) return;
                    int level = avatar.level;
                    if (level >= 10)
                    {
                        // 检查交易会 UI 是否已初始化
                        if (IExchangeUIMag.Inst != null)
                        {
                            if (IExchangeUIMag.Inst.ExchangeCtr != null)
                            {
                                // 获取交易浏览界面的 GameObject
                                Transform exchangeTransform = IExchangeUIMag.Inst.ExchangeCtr.UI.GetTransform();
                                Transform publishTransform = IExchangeUIMag.Inst.PublishCtr.UI.GetTransform();
                                if (exchangeTransform != null && exchangeTransform.gameObject.activeSelf
                                    || publishTransform != null && publishTransform.gameObject.activeSelf)
                                {
                                    Main.LogInfo("交易会窗口已打开，忽略重复按键");
                                }
                                else
                                {
                                    IExchangeUIMag.Inst.OpenPublish();
                                }
                            }
                            else
                            {
                                IExchangeUIMag.Open();
                                IExchangeUIMag.Inst.OpenPublish();
                            }
                        }
                        else
                        {
                            IExchangeUIMag.Open();
                            IExchangeUIMag.Inst.OpenPublish();
                        }
                    }
                    else
                    {
                        UIPopTip.Inst.Pop("元婴期才能使用交易会！", 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LogError($"[ExchangeMeeting] 执行异常: {ex}");
            }
        }
    }
}