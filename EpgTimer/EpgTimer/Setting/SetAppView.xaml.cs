using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Diagnostics;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetAppView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetAppView : UserControl
    {
        private MenuUtil mutil = CommonManager.Instance.MUtil;

        private List<String> ngProcessList = new List<String>();
        private String ngMin = "10";
        public bool ngUsePC = false;
        public String ngUsePCMin = "3";
        public bool ngFileStreaming = false;
        public bool ngShareFile = false;

        private EpgSearchKeyInfo defSearchKey;

        private List<String> extList = new List<string>();
        private List<String> delChkFolderList = new List<string>();

        private List<string> buttonItem;
        private List<string> taskItem;
        
        private Dictionary<UInt64, ServiceViewItem> serviceList = new Dictionary<UInt64, ServiceViewItem>();
        private List<IEPGStationInfo> stationList = new List<IEPGStationInfo>();

        public bool ServiceStop = false;

        private SerializableSecureString password;

        public SetAppView()
        {
            InitializeComponent();

            try
            {
                SetAppView_tabItem1();
                SetAppView_tabItem2();
                SetAppView_tabItem3();
                SetAppView_tabItem4();
                SetAppView_tabItem5();
                SetAppView_tabItem6();
                SetAppView_tabItem7();

                tabControl1.SelectedItem = IniFileHandler.CanUpdateInifile ? tabItem1 : tabItem2;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void SetAppView_tabItem1()
        {
            // 保存できない項目は IsEnabled = false にする
            if (IniFileHandler.CanUpdateInifile == false)
            {
                CommonManager.Instance.VUtil.DisableControlChildren(tabItem1);
            }

            // 読める設定のみ項目に反映させる
            if (IniFileHandler.CanReadInifile)
            {
                int recEndMode = IniFileHandler.GetPrivateProfileInt("SET", "RecEndMode", 2, SettingPath.TimerSrvIniPath);
                switch (recEndMode)
                {
                case 0:
                    radioButton_none.IsChecked = true;
                    break;
                case 1:
                    radioButton_standby.IsChecked = true;
                    break;
                case 2:
                    radioButton_suspend.IsChecked = true;
                    break;
                case 3:
                    radioButton_shutdown.IsChecked = true;
                    break;
                default:
                    break;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "Reboot", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_reboot.IsChecked = true;
                }
                else
                {
                    checkBox_reboot.IsChecked = false;
                }
                textBox_pcWakeTime.Text = IniFileHandler.GetPrivateProfileInt("SET", "WakeTime", 5, SettingPath.TimerSrvIniPath).ToString();

                textBox_megine_start.Text = IniFileHandler.GetPrivateProfileInt("SET", "StartMargin", 5, SettingPath.TimerSrvIniPath).ToString();
                textBox_margine_end.Text = IniFileHandler.GetPrivateProfileInt("SET", "EndMargin", 2, SettingPath.TimerSrvIniPath).ToString();
                textBox_appWakeTime.Text = IniFileHandler.GetPrivateProfileInt("SET", "RecAppWakeTime", 2, SettingPath.TimerSrvIniPath).ToString();

                if (IniFileHandler.GetPrivateProfileInt("SET", "RecMinWake", 1, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appMin.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "RecView", 1, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appView.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "DropLog", 1, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appDrop.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "PgInfoLog", 1, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_addPgInfo.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "RecNW", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appNW.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "KeepDisk", 1, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appKeepDisk.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "RecOverWrite", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appOverWrite.IsChecked = true;
                }

                // button_standbyCtrl
                int ngCount = IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "Count", 0, SettingPath.TimerSrvIniPath);
                if (ngCount == 0)
                {
                    ngProcessList.Add("EpgDataCap_Bon.exe");
                }
                else
                {
                    for (int i = 0; i < ngCount; i++)
                    {
                        ngProcessList.Add(IniFileHandler.GetPrivateProfileString("NO_SUSPEND", i.ToString(), "", SettingPath.TimerSrvIniPath));
                    }
                }
                ngMin = IniFileHandler.GetPrivateProfileString("NO_SUSPEND", "NoStandbyTime", "10", SettingPath.TimerSrvIniPath);
                if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePC", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    ngUsePC = true;
                }
                ngUsePCMin = IniFileHandler.GetPrivateProfileString("NO_SUSPEND", "NoUsePCTime", "3", SettingPath.TimerSrvIniPath);
                if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoFileStreaming", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    ngFileStreaming = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoShareFile", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    ngShareFile = true;
                }
                comboBox_process.Items.Add("リアルタイム");
                comboBox_process.Items.Add("高");
                comboBox_process.Items.Add("通常以上");
                comboBox_process.Items.Add("通常");
                comboBox_process.Items.Add("通常以下");
                comboBox_process.Items.Add("低");
                comboBox_process.SelectedIndex = IniFileHandler.GetPrivateProfileInt("SET", "ProcessPriority", 3, SettingPath.TimerSrvIniPath);
            }
        }

        private void SetAppView_tabItem2()
        {
            // 保存できない項目は IsEnabled = false にする
            checkBox_back_priority.IsEnabled = IniFileHandler.CanUpdateInifile; // 優先度が同じで時間が重なった場合、後の予約を優先する
            checkBox_autoDel.IsEnabled = IniFileHandler.CanUpdateInifile; // HDDの空きが少ない場合、古い録画ファイルを削除する(追加設定で対象フォルダを設定)
            button_autoDel.IsEnabled = IniFileHandler.CanReadInifile; // 削除設定

            checkBox_recname.IsEnabled = IniFileHandler.CanUpdateInifile; // 録画時のファイル名にPlugInを使用する
            comboBox_recname.IsEnabled = IniFileHandler.CanUpdateInifile;
            button_recname.IsEnabled = !Settings.Instance.NWMode; // 設定

            checkBox_cautionOnRecChange.IsChecked = Settings.Instance.CautionOnRecChange;
            textBox_cautionOnRecMarginMin.Text = Settings.Instance.CautionOnRecMarginMin.ToString();
            checkBox_SyncResAutoAddChange.IsChecked = Settings.Instance.SyncResAutoAddChange;
            checkBox_SyncResAutoAddChgNewRes.IsChecked = Settings.Instance.SyncResAutoAddChgNewRes;
            checkBox_SyncResAutoAddDelete.IsChecked = Settings.Instance.SyncResAutoAddDelete;

            // 読める設定のみ項目に反映させる
            if (IniFileHandler.CanReadInifile)
            {
                if (IniFileHandler.GetPrivateProfileInt("SET", "BackPriority", 1, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_back_priority.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "AutoDel", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_autoDel.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "RecNamePlugIn", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_recname.IsChecked = true;
                }

                if (IniFileHandler.GetPrivateProfileInt("SET", "NoChkYen", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_noChkYen.IsChecked = true;
                }

                // button_autoDel
                int count;
                count = IniFileHandler.GetPrivateProfileInt("DEL_EXT", "Count", 0, SettingPath.TimerSrvIniPath);
                if (count == 0)
                {
                    extList.Add(".ts.err");
                    extList.Add(".ts.program.txt");
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        extList.Add(IniFileHandler.GetPrivateProfileString("DEL_EXT", i.ToString(), "", SettingPath.TimerSrvIniPath));
                    }
                }

                count = IniFileHandler.GetPrivateProfileInt("DEL_CHK", "Count", 0, SettingPath.TimerSrvIniPath);
                for (int i = 0; i < count; i++)
                {
                    delChkFolderList.Add(IniFileHandler.GetPrivateProfileString("DEL_CHK", i.ToString(), "", SettingPath.TimerSrvIniPath));
                }

                if (CommonManager.Instance.IsConnected == true)
                {
                    CommonManager.Instance.DB.ReloadPlugInFile();
                }
                comboBox_recname.ItemsSource = CommonManager.Instance.DB.RecNamePlugInList.Values;
                String plugInFile = IniFileHandler.GetPrivateProfileString("SET", "RecNamePlugInFile", "RecName_Macro.dll", SettingPath.TimerSrvIniPath);
                comboBox_recname.SelectedItem = plugInFile;
            }
        }

        private void SetAppView_tabItem3()
        {
            checkBox_showAsTab.IsChecked = Settings.Instance.ViewButtonShowAsTab;
            checkBox_suspendChk.IsChecked = (Settings.Instance.SuspendChk == 1);
            textBox_suspendChkTime.Text = Settings.Instance.SuspendChkTime.ToString();
            if (CommonManager.Instance.NWMode == true)
            {
                textblockTimer.Text = "EpgTimerNW側の設定です。";
            }
            else
            {
                textblockTimer.Text = "録画終了時にスタンバイ、休止する場合は必ず表示されます(ただし、サービス未使用時はこの設定は使用されず15秒固定)。";
            }

            listBox_Button_Set();
        }

        private void SetAppView_tabItem4()
        {
            if (CommonManager.Instance.NWMode == true)
            {
                checkBox_tcpServer.IsEnabled = false; // ネットワーク接続を許可する

                checkBox_timeSync.IsEnabled = false; // EPG取得時に放送波時間でPC時計を同期する
                checkBox_srvResident.IsEnabled = false; // EpgTimerSrvを常駐させる
                checkBox_srvShowTray.IsEnabled = false; // タスクトレイアイコンを表示する
                checkBox_srvNoBalloonTip.IsEnabled = false; // バルーンチップでの動作通知を抑制する

                checkBox_wakeReconnect.IsEnabled = true; // 起動時に前回接続サーバーに接続する
                group_WoLWait.IsEnabled = true; // WoL設定
                checkBox_suspendClose.IsEnabled = true; // 休止／スタンバイ移行時にEpgTimerNWを終了する
                checkBox_ngAutoEpgLoad.IsEnabled = true; // EPGデータを自動的に読み込まない
                checkBox_keepTCPConnect.IsEnabled = true; // EpgTimerSrvとの接続維持を試みる
            }
            if (ServiceCtrlClass.IsStarted("EpgTimer Service") == true)
            {
                checkBox_srvResident.IsEnabled = false;
            }

            checkBox_autoDelRecInfo.IsEnabled = IniFileHandler.CanUpdateInifile; // 録画結果を自動的に削除する
            checkBox_srvSaveNotifyLog.IsEnabled = IniFileHandler.CanUpdateInifile; // 情報通知ログをファイルに保存する
            checkBox_srvSaveDebugLog.IsEnabled = IniFileHandler.CanUpdateInifile; // デバッグ出力をファイルに保存する
            if (IniFileHandler.CanUpdateInifile == false)
            {
                button_recDef.Content = "録画プリセットを確認";
            }

            // 読める設定のみ項目に反映させる
            if (IniFileHandler.CanReadInifile)
            {
                if (IniFileHandler.GetPrivateProfileInt("SET", "AutoDelRecInfo", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_autoDelRecInfo.IsChecked = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("SET", "RecInfoDelFile", 0, SettingPath.CommonIniPath) == 1)
                {
                    checkBox_autoDelRecFile.IsChecked = true;
                }
                textBox_autoDelRecInfo.Text = IniFileHandler.GetPrivateProfileInt("SET", "AutoDelRecInfoNum", 100, SettingPath.TimerSrvIniPath).ToString();

                if (IniFileHandler.GetPrivateProfileInt("SET", "TimeSync", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_timeSync.IsChecked = true;
                }

                int residentMode = IniFileHandler.GetPrivateProfileInt("SET", "ResidentMode", 0, SettingPath.TimerSrvIniPath);
                checkBox_srvResident.IsChecked = residentMode >= 1;
                checkBox_srvShowTray.IsChecked = residentMode >= 2;
                checkBox_srvNoBalloonTip.IsChecked = IniFileHandler.GetPrivateProfileInt("SET", "NoBalloonTip", 0, SettingPath.TimerSrvIniPath) == 1;

                if (IniFileHandler.GetPrivateProfileInt("SET", "EnableTCPSrv", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_tcpServer.IsChecked = true;
                }
                textBox_tcpPort.Text = IniFileHandler.GetPrivateProfileInt("SET", "TCPPort", 4510, SettingPath.TimerSrvIniPath).ToString();
                textBox_tcpAcl.Text = IniFileHandler.GetPrivateProfileString("SET", "TCPAccessControlList", "+127.0.0.1,+192.168.0.0/16", SettingPath.TimerSrvIniPath);
                string base64string = IniFileHandler.GetPrivateProfileString("SET", "TCPAccessPassword", "", SettingPath.TimerSrvIniPath);
                password = new SerializableSecureString(base64string);
                if (password.Length > 0 && password.Length == base64string.Length)
                {
                    // decrypt 出来なかったので disable にする
                    passwordBox_tcpPassword.IsEnabled = false;
                }
                else
                {
                    // セキュアなコピーではないが PasswordBox.SecurePassword の setter がないため...
                    passwordBox_tcpPassword.Password = new System.Net.NetworkCredential(string.Empty, password.SecureString).Password;
                }

                textBox_tcpResTo.Text = IniFileHandler.GetPrivateProfileInt("SET", "TCPResponseTimeoutSec", 120, SettingPath.TimerSrvIniPath).ToString();

                checkBox_srvSaveNotifyLog.IsChecked = IniFileHandler.GetPrivateProfileInt("SET", "SaveNotifyLog", 0, SettingPath.TimerSrvIniPath) == 1;
                checkBox_srvSaveDebugLog.IsChecked = IniFileHandler.GetPrivateProfileInt("SET", "SaveDebugLog", 0, SettingPath.TimerSrvIniPath) == 1;
            }

            checkBox_closeMin.IsChecked = Settings.Instance.CloseMin;
            checkBox_minWake.IsChecked = Settings.Instance.WakeMin;
            checkBox_applyMulti.IsChecked = Settings.Instance.ApplyMultiInstance;
            checkBox_noToolTips.IsChecked = Settings.Instance.NoToolTip;
            checkBox_noBallonTips.IsChecked = Settings.Instance.NoBallonTips;
            textBox_ForceHideBalloonTipSec.Text = Settings.Instance.ForceHideBalloonTipSec.ToString();
            checkBox_AutoSaveNotifyLog.IsChecked = Settings.Instance.AutoSaveNotifyLog == 1;
            checkBox_showTray.IsChecked = Settings.Instance.ShowTray;
            checkBox_minHide.IsChecked = Settings.Instance.MinHide;
            checkBox_cautionManyChange.IsChecked = Settings.Instance.CautionManyChange;
            textBox_cautionManyChange.Text = Settings.Instance.CautionManyNum.ToString();
            checkBox_saveSearchKeyword.IsChecked = Settings.Instance.SaveSearchKeyword;
            checkBox_keepTCPConnect.IsChecked = Settings.Instance.ChkSrvRegistTCP;
            checkBox_upDateTaskText.IsChecked = Settings.Instance.UpdateTaskText;
            textBox_chkTimerInterval.Text = Settings.Instance.ChkSrvRegistInterval.ToString();

            checkBox_wakeReconnect.IsChecked = Settings.Instance.WakeReconnectNW;
            checkBox_WoLWait.IsChecked = Settings.Instance.WoLWait;
            checkBox_WoLWaitRecconect.IsChecked = Settings.Instance.WoLWaitRecconect;
            textBox_WoLWaitSecond.Text = Settings.Instance.WoLWaitSecond.ToString();
            checkBox_suspendClose.IsChecked = Settings.Instance.SuspendCloseNW;
            checkBox_ngAutoEpgLoad.IsChecked = Settings.Instance.NgAutoEpgLoadNW;

            defSearchKey = Settings.Instance.DefSearchKey.Clone();
        }

        private void SetAppView_tabItem5()
        {
            textBox_name1.Text = Settings.Instance.Cust1BtnName;
            textBox_exe1.Text = Settings.Instance.Cust1BtnCmd;
            textBox_opt1.Text = Settings.Instance.Cust1BtnCmdOpt;

            textBox_name2.Text = Settings.Instance.Cust2BtnName;
            textBox_exe2.Text = Settings.Instance.Cust2BtnCmd;
            textBox_opt2.Text = Settings.Instance.Cust2BtnCmdOpt;

            textBox_name3.Text = Settings.Instance.Cust3BtnName;
            textBox_exe3.Text = Settings.Instance.Cust3BtnCmd;
            textBox_opt3.Text = Settings.Instance.Cust3BtnCmdOpt;
        }

        private void SetAppView_tabItem6()
        {
            foreach (ChSet5Item info in ChSet5.Instance.ChList.Values)
            {
                ServiceViewItem item = new ServiceViewItem(info);
                serviceList.Add(item.Key, item);
            }
            listBox_service.ItemsSource = serviceList.Values;

            stationList = Settings.Instance.IEpgStationList;
            ReLoadStation();
        }

        private void SetAppView_tabItem7()
        {
            if (CommonManager.Instance.NWMode == true)
            {
                CommonManager.Instance.VUtil.DisableControlChildren(tabItem7);
            }

            if (button_inst.IsEnabled || button_uninst.IsEnabled || button_stop.IsEnabled)
            {
                UpdateServiceBtn();
            }
        }

        public void SaveSetting()
        {
            try
            {
                SaveSetting_tabItem1();
                SaveSetting_tabItem2();
                SaveSetting_tabItem3();
                SaveSetting_tabItem4();
                SaveSetting_tabItem5();
                SaveSetting_tabItem6();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void SaveSetting_tabItem1()
        {
            if (radioButton_none.IsEnabled == true && radioButton_none.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "0", SettingPath.TimerSrvIniPath);
            }
            if (radioButton_standby.IsEnabled == true && radioButton_standby.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "1", SettingPath.TimerSrvIniPath);
            }
            if (radioButton_suspend.IsEnabled == true && radioButton_suspend.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "2", SettingPath.TimerSrvIniPath);
            }
            if (radioButton_shutdown.IsEnabled == true && radioButton_shutdown.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "3", SettingPath.TimerSrvIniPath);
            }

            string setValue;
            if (checkBox_reboot.IsEnabled)
            {
                setValue = (checkBox_reboot.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "Reboot", setValue, SettingPath.TimerSrvIniPath);
            }

            if (textBox_pcWakeTime.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "WakeTime", textBox_pcWakeTime.Text, SettingPath.TimerSrvIniPath);
            }
            if (textBox_megine_start.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "StartMargin", textBox_megine_start.Text, SettingPath.TimerSrvIniPath);
            }
            if (textBox_margine_end.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "EndMargin", textBox_margine_end.Text, SettingPath.TimerSrvIniPath);
            }
            if (textBox_appWakeTime.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecAppWakeTime", textBox_appWakeTime.Text, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_appMin.IsEnabled)
            {
                setValue = (checkBox_appMin.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "RecMinWake", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_appView.IsEnabled)
            {
                setValue = (checkBox_appView.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "RecView", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_appDrop.IsEnabled)
            {
                setValue = (checkBox_appDrop.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "DropLog", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_addPgInfo.IsEnabled)
            {
                setValue = (checkBox_addPgInfo.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "PgInfoLog", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_appNW.IsEnabled)
            {
                setValue = (checkBox_appNW.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "RecNW", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_appKeepDisk.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "KeepDisk", checkBox_appKeepDisk.IsChecked == true ? "1" : "0", SettingPath.TimerSrvIniPath);
            }

            if (checkBox_appOverWrite.IsEnabled)
            {
                setValue = (checkBox_appOverWrite.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "RecOverWrite", setValue, SettingPath.TimerSrvIniPath);
            }

            if (IniFileHandler.CanUpdateInifile)
            {
                // button_standbyCtrl
                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "Count", ngProcessList.Count.ToString(), SettingPath.TimerSrvIniPath);
                for (int i = 0; i < ngProcessList.Count; i++)
                {
                    IniFileHandler.WritePrivateProfileString("NO_SUSPEND", i.ToString(), ngProcessList[i], SettingPath.TimerSrvIniPath);
                }

                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoStandbyTime", ngMin, SettingPath.TimerSrvIniPath);

                setValue = (ngUsePC == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoUsePC", setValue, SettingPath.TimerSrvIniPath);

                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoUsePCTime", ngUsePCMin, SettingPath.TimerSrvIniPath);

                setValue = (ngFileStreaming == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoFileStreaming", setValue, SettingPath.TimerSrvIniPath);

                setValue = (ngShareFile == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoShareFile", setValue, SettingPath.TimerSrvIniPath);
            }

            if (comboBox_process.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "ProcessPriority", comboBox_process.SelectedIndex.ToString(), SettingPath.TimerSrvIniPath);
            }
        }

        private void SaveSetting_tabItem2()
        {
            string setValue;
            if (checkBox_back_priority.IsEnabled)
            {
                setValue = (checkBox_back_priority.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "BackPriority", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_autoDel.IsEnabled)
            {
                setValue = (checkBox_autoDel.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "AutoDel", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_recname.IsEnabled)
            {
                setValue = (checkBox_recname.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "RecNamePlugIn", setValue, SettingPath.TimerSrvIniPath);
            }

            if (comboBox_recname.IsEnabled)
            {
                setValue = (comboBox_recname.SelectedItem != null ? (string)comboBox_recname.SelectedItem : "");
                IniFileHandler.WritePrivateProfileString("SET", "RecNamePlugInFile", setValue, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_noChkYen.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "NoChkYen", checkBox_noChkYen.IsChecked == true ? "1" : "0", SettingPath.TimerSrvIniPath);
            }

            if (IniFileHandler.CanUpdateInifile)
            {
                // button_autoDel
                IniFileHandler.WritePrivateProfileString("DEL_EXT", "Count", extList.Count.ToString(), SettingPath.TimerSrvIniPath);
                for (int i = 0; i < extList.Count; i++)
                {
                    IniFileHandler.WritePrivateProfileString("DEL_EXT", i.ToString(), extList[i], SettingPath.TimerSrvIniPath);
                }

                IniFileHandler.WritePrivateProfileString("DEL_CHK", "Count", delChkFolderList.Count.ToString(), SettingPath.TimerSrvIniPath);
                for (int i = 0; i < delChkFolderList.Count; i++)
                {
                    IniFileHandler.WritePrivateProfileString("DEL_CHK", i.ToString(), delChkFolderList[i], SettingPath.TimerSrvIniPath);
                }
            }

            Settings.Instance.CautionOnRecChange = (checkBox_cautionOnRecChange.IsChecked != false);
            Settings.Instance.CautionOnRecMarginMin = mutil.MyToNumerical(textBox_cautionOnRecMarginMin, Convert.ToInt32, Settings.Instance.CautionOnRecMarginMin); 
            Settings.Instance.SyncResAutoAddChange = (checkBox_SyncResAutoAddChange.IsChecked != false);
            Settings.Instance.SyncResAutoAddDelete = (checkBox_SyncResAutoAddDelete.IsChecked != false);
            Settings.Instance.SyncResAutoAddChgNewRes = (checkBox_SyncResAutoAddChgNewRes.IsChecked != false);
        }

        private void SaveSetting_tabItem3()
        {
            Settings.Instance.ViewButtonShowAsTab = checkBox_showAsTab.IsChecked == true;
            Settings.Instance.SuspendChk = (uint)(checkBox_suspendChk.IsChecked == true ? 1 : 0);
            Settings.Instance.SuspendChkTime = mutil.MyToNumerical(textBox_suspendChkTime, Convert.ToUInt32, Settings.Instance.SuspendChkTime);

            Settings.Instance.ViewButtonList = listBox_viewBtn.Items.OfType<string>().ToList();

            // tabItem3 - groupBox32
            Settings.Instance.TaskMenuList = listBox_viewTask.Items.OfType<string>().ToList();
        }

        private void SaveSetting_tabItem4()
        {
            string setValue;
            if (checkBox_autoDelRecInfo.IsEnabled)
            {
                setValue = (checkBox_autoDelRecInfo.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "AutoDelRecInfo", setValue, SettingPath.TimerSrvIniPath);

                setValue = (checkBox_autoDelRecFile.IsChecked == true ? "1" : null);
                IniFileHandler.WritePrivateProfileString("SET", "RecInfoDelFile", setValue, SettingPath.CommonIniPath);

                IniFileHandler.WritePrivateProfileString("SET", "AutoDelRecInfoNum", textBox_autoDelRecInfo.Text.ToString(), SettingPath.TimerSrvIniPath);
            }

            if (checkBox_timeSync.IsEnabled)
            {
                setValue = (checkBox_timeSync.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "TimeSync", setValue, SettingPath.TimerSrvIniPath);
            }

            Settings.Instance.CloseMin = (bool)checkBox_closeMin.IsChecked;
            Settings.Instance.WakeMin = (bool)checkBox_minWake.IsChecked;
            Settings.Instance.ApplyMultiInstance = (bool)checkBox_applyMulti.IsChecked;
            Settings.Instance.ShowTray = (bool)checkBox_showTray.IsChecked;
            Settings.Instance.MinHide = (bool)checkBox_minHide.IsChecked;

            if (checkBox_srvResident.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "ResidentMode",
                                                         checkBox_srvResident.IsChecked == false ? "0" : checkBox_srvShowTray.IsChecked == false ? "1" : "2", SettingPath.TimerSrvIniPath);
                IniFileHandler.WritePrivateProfileString("SET", "NoBalloonTip", checkBox_srvNoBalloonTip.IsChecked == false ? "0" : "1", SettingPath.TimerSrvIniPath);
            }

            if (checkBox_tcpServer.IsEnabled)
            {
                setValue = (checkBox_tcpServer.IsChecked == true ? "1" : "0");
                IniFileHandler.WritePrivateProfileString("SET", "EnableTCPSrv", setValue, SettingPath.TimerSrvIniPath);

                IniFileHandler.WritePrivateProfileString("SET", "TCPPort", textBox_tcpPort.Text, SettingPath.TimerSrvIniPath);
            }

            if (textBox_tcpAcl.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "TCPAccessControlList", textBox_tcpAcl.Text, SettingPath.TimerSrvIniPath);
            }
            if (passwordBox_tcpPassword.IsEnabled)
            {
                SerializableSecureString newPassword = new SerializableSecureString(passwordBox_tcpPassword.SecurePassword, System.Security.Cryptography.DataProtectionScope.LocalMachine);
                if (password.Compare(newPassword) == false)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "TCPAccessPassword", newPassword.Base64String, SettingPath.TimerSrvIniPath);
                }
            }

            if (textBox_tcpResTo.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "TCPResponseTimeoutSec", textBox_tcpResTo.Text, SettingPath.TimerSrvIniPath);
            }

            if (checkBox_srvSaveNotifyLog.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "SaveNotifyLog", checkBox_srvSaveNotifyLog.IsChecked == false ? "0" : "1", SettingPath.TimerSrvIniPath);
            }
            if (checkBox_srvSaveDebugLog.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "SaveDebugLog", checkBox_srvSaveDebugLog.IsChecked == false ? "0" : "1", SettingPath.TimerSrvIniPath);
            }

            Settings.Instance.NoToolTip = (checkBox_noToolTips.IsChecked == true);
            Settings.Instance.NoBallonTips = (checkBox_noBallonTips.IsChecked == true);
            Settings.Instance.ForceHideBalloonTipSec = mutil.MyToNumerical(textBox_ForceHideBalloonTipSec, Convert.ToInt32, 255, 0, Settings.Instance.ForceHideBalloonTipSec);
            Settings.Instance.AutoSaveNotifyLog = (short)(checkBox_AutoSaveNotifyLog.IsChecked == true ? 1 : 0);
            Settings.Instance.CautionManyChange = (checkBox_cautionManyChange.IsChecked != false);
            Settings.Instance.CautionManyNum = mutil.MyToNumerical(textBox_cautionManyChange, Convert.ToInt32, Settings.Instance.CautionManyNum); 
            Settings.Instance.SaveSearchKeyword = (checkBox_saveSearchKeyword.IsChecked != false);
            Settings.Instance.WakeReconnectNW = (checkBox_wakeReconnect.IsChecked == true);
            Settings.Instance.WoLWait = (checkBox_WoLWait.IsChecked == true);
            Settings.Instance.WoLWaitRecconect = (checkBox_WoLWaitRecconect.IsChecked == true);
            Settings.Instance.WoLWaitSecond = mutil.MyToNumerical(textBox_WoLWaitSecond, Convert.ToDouble, 3600, 1, Settings.Instance.WoLWaitSecond);
            Settings.Instance.SuspendCloseNW = (checkBox_suspendClose.IsChecked == true);
            Settings.Instance.NgAutoEpgLoadNW = (checkBox_ngAutoEpgLoad.IsChecked == true);
            Settings.Instance.ChkSrvRegistTCP = (checkBox_keepTCPConnect.IsChecked != false);
            Settings.Instance.UpdateTaskText = (checkBox_upDateTaskText.IsChecked == true);
            Settings.Instance.ChkSrvRegistInterval = mutil.MyToNumerical(textBox_chkTimerInterval, Convert.ToDouble, 1440 * 7, 1, Settings.Instance.ChkSrvRegistInterval);

            Settings.Instance.DefSearchKey = defSearchKey.Clone();
        }

        private void SaveSetting_tabItem5()
        {
            Settings.Instance.Cust1BtnName = textBox_name1.Text;
            Settings.Instance.Cust1BtnCmd = textBox_exe1.Text;
            Settings.Instance.Cust1BtnCmdOpt = textBox_opt1.Text;

            Settings.Instance.Cust2BtnName = textBox_name2.Text;
            Settings.Instance.Cust2BtnCmd = textBox_exe2.Text;
            Settings.Instance.Cust2BtnCmdOpt = textBox_opt2.Text;

            Settings.Instance.Cust3BtnName = textBox_name3.Text;
            Settings.Instance.Cust3BtnCmd = textBox_exe3.Text;
            Settings.Instance.Cust3BtnCmdOpt = textBox_opt3.Text;
        }

        private void SaveSetting_tabItem6()
        {
            Settings.Instance.IEpgStationList = stationList;
        }

        private void button_standbyCtrl_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetAppCancelWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            dlg.processList = this.ngProcessList;
            dlg.ngMin = this.ngMin;
            dlg.ngUsePC = this.ngUsePC;
            dlg.ngUsePCMin = this.ngUsePCMin;
            dlg.ngFileStreaming = this.ngFileStreaming;
            dlg.ngShareFile = this.ngShareFile;

            if (dlg.ShowDialog() == true)
            {
                this.ngProcessList = dlg.processList;
                this.ngMin = dlg.ngMin;
                this.ngUsePC = dlg.ngUsePC;
                this.ngUsePCMin = dlg.ngUsePCMin;
                this.ngFileStreaming = dlg.ngFileStreaming;
                this.ngShareFile = dlg.ngShareFile;
            }
        }

        private void button_autoDel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetApp2DelWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            dlg.extList = this.extList;
            dlg.delChkFolderList = this.delChkFolderList;

            if (dlg.ShowDialog() == true)
            {
                this.extList = dlg.extList;
                this.delChkFolderList = dlg.delChkFolderList;
            }
        }

        private void button_recname_Click(object sender, RoutedEventArgs e)
        {
            if (comboBox_recname.SelectedItem != null)
            {
                string name = comboBox_recname.SelectedItem as string;
                string filePath = SettingPath.ModulePath + "\\RecName\\" + name;

                RecNamePluginClass plugin = new RecNamePluginClass();
                HwndSource hwnd = (HwndSource)HwndSource.FromVisual(this);

                plugin.Setting(filePath, hwnd.Handle);
            }
        }

        //ボタン表示画面の上下ボタンのみ他と同じものを使用する。
        private BoxExchangeEditor bxb = new BoxExchangeEditor();
        private BoxExchangeEditor bxt = new BoxExchangeEditor();
        private void listBox_Button_Set()
        {
            //上部表示ボタン関係
            bxb.TargetBox = this.listBox_viewBtn;
            bxb.SourceBox = this.listBox_itemBtn;
            bxb.DuplicationSpecific = new List<object> { "（空白）" };
            button_btnUp.Click += new RoutedEventHandler(bxb.button_up_Click);
            button_btnDown.Click += new RoutedEventHandler(bxb.button_down_Click);
            button_btnAdd.Click += new RoutedEventHandler((sender, e) => button_Add(bxb, buttonItem));
            button_btnIns.Click += new RoutedEventHandler((sender, e) => button_Add(bxb, buttonItem, true));
            button_btnDel.Click += new RoutedEventHandler((sender, e) => button_Del(bxb, bxt, buttonItem));
            bxb.sourceBoxKeyEnable(listBox_itemBtn, (sender, e) => button_btnAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.targetBoxKeyEnable(listBox_viewBtn, (sender, e) => button_btnDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.doubleClickSetter(listBox_itemBtn, (sender, e) => button_btnAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.doubleClickSetter(listBox_viewBtn, (sender, e) => button_btnDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

            //タスクアイコン関係
            bxt.TargetBox = this.listBox_viewTask;
            bxt.SourceBox = this.listBox_itemTask;
            bxt.DuplicationSpecific = new List<object> { "（セパレータ）" };
            button_taskUp.Click += new RoutedEventHandler(bxt.button_up_Click);
            button_taskDown.Click += new RoutedEventHandler(bxt.button_down_Click);
            button_taskAdd.Click += new RoutedEventHandler((sender, e) => button_Add(bxt, taskItem));
            button_taskIns.Click += new RoutedEventHandler((sender, e) => button_Add(bxt, taskItem, true));
            button_taskDel.Click += new RoutedEventHandler((sender, e) => button_Del(bxt, bxb, taskItem));
            bxt.sourceBoxKeyEnable(listBox_itemTask, (sender, e) => button_taskAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.targetBoxKeyEnable(listBox_viewTask, (sender, e) => button_taskDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.doubleClickSetter(listBox_itemTask, (sender, e) => button_taskAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.doubleClickSetter(listBox_viewTask, (sender, e) => button_taskDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

            Settings.Instance.ViewButtonList.ForEach(str => listBox_viewBtn.Items.Add(str));
            buttonItem = Settings.Instance.GetViewButtonAllItems();
            reLoadButtonItem(bxb, buttonItem);

            Settings.Instance.TaskMenuList.ForEach(str => listBox_viewTask.Items.Add(str));
            taskItem = Settings.Instance.GetTaskMenuAllItems();
            reLoadButtonItem(bxt, taskItem);
        }

        private void button_Add(BoxExchangeEditor bx, List<string> src, bool isInsert = false)
        {
            int pos = bx.SourceBox.SelectedIndex - bx.SourceBox.SelectedItems.Count;
            bx.addItems(bx.SourceBox, bx.TargetBox, isInsert);
            reLoadButtonItem(bx, src);
            if (bx.SourceBox.Items.Count != 0)
            {
                pos = pos <= 1 ? 1 : pos;
                pos = Math.Max(0, Math.Min(pos, bx.SourceBox.Items.Count - 1));
                bx.SourceBox.SelectedIndex = pos - 1;//順序がヘンだが、ENTERの場合はこの後に+1処理が入る模様
            }
        }
        private void button_Del(BoxExchangeEditor bx, BoxExchangeEditor bx_other, List<string> src)
        {
            if (bx.TargetBox.SelectedItem == null) return;
            //
            string item1 = bx.TargetBox.SelectedItems.OfType<string>().FirstOrDefault(item => item == "設定");
            string item2 = bx_other.TargetBox.Items.OfType<string>().FirstOrDefault(item => item == "設定");
            if (item1 != null && item2 == null)
            {
                MessageBox.Show("設定は上部表示ボタンか右クリック表示項目のどちらかに必要です");
                return;
            }

            bx.deleteItems(bx.TargetBox);
            reLoadButtonItem(bx, src);
        }
        private void reLoadButtonItem(BoxExchangeEditor bx, List<string> src)
        {
            var viewlist = bx.TargetBox.Items.OfType<string>().ToList();
            var diflist = src.Except(viewlist).ToList();
            diflist.Insert(0, bx.DuplicationSpecific.OfType<string>().ElementAt(0));
            var newlist = diflist.Distinct().ToList();

            bx.SourceBox.ItemsSource = newlist;
        }

        private void button_recDef_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetDefRecSettingWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            dlg.ShowDialog();
        }

        private void button_searchDef_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetDefSearchSettingWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            dlg.SetDefSetting(defSearchKey);

            if (dlg.ShowDialog() == true)
            {
                defSearchKey = dlg.GetSetting();
            }
        }

        private void button_exe_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.GetFileNameByDialog((sender as Button).DataContext as TextBox, false, "", ".exe");
        }

        private void ReLoadStation()
        {
            listBox_iEPG.Items.Clear();
            if (listBox_service.SelectedItem != null)
            {
                ServiceViewItem item = listBox_service.SelectedItem as ServiceViewItem;
                foreach (IEPGStationInfo info in stationList)
                {
                    if (info.Key == item.Key)
                    {
                        listBox_iEPG.Items.Add(info);
                    }
                }
            }
        }

        private void button_add_iepg_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_service.SelectedItem != null)
            {
                ServiceViewItem item = listBox_service.SelectedItem as ServiceViewItem;
                foreach (IEPGStationInfo info in stationList)
                {
                    if (String.Compare(info.StationName, textBox_station.Text) == 0)
                    {
                        MessageBox.Show("すでに登録済みです");
                        return;
                    }
                }
                IEPGStationInfo addItem = new IEPGStationInfo();
                addItem.StationName = textBox_station.Text;
                addItem.Key = item.Key;

                stationList.Add(addItem);

                ReLoadStation();
            }
        }

        private void button_del_iepg_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_iEPG.SelectedItem != null)
            {
                IEPGStationInfo item = listBox_iEPG.SelectedItem as IEPGStationInfo;
                stationList.Remove(item);
                ReLoadStation();
            }
        }

        private void listBox_service_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReLoadStation();
        }

        private void checkBox_WoLWaitRecconect_Checked(object sender, RoutedEventArgs e)
        {
            checkBox_WoLWait.IsChecked = false;
        }
        private void checkBox_WoLWait_Checked(object sender, RoutedEventArgs e)
        {
            checkBox_WoLWaitRecconect.IsChecked = false;
        }

        private void button_clearSerchKeywords(object sender, RoutedEventArgs e)
        {
            Settings.Instance.AndKeyList = new List<string>();
            Settings.Instance.NotKeyList = new List<string>();
        }

        private void UpdateServiceBtn()
        {
            if (ServiceCtrlClass.ServiceIsInstalled("EpgTimer Service") == false)
            {
                button_inst.IsEnabled = true;
                button_uninst.IsEnabled = false;
                button_start.IsEnabled = false;
                button_stop.IsEnabled = false;
            }
            else
            {
                button_inst.IsEnabled = false;
                button_uninst.IsEnabled = true;
                if (ServiceCtrlClass.IsStarted("EpgTimer Service") == true)
                {
                    button_start.IsEnabled = false;
                    button_stop.IsEnabled = true;
                }
                else
                {
                    button_start.IsEnabled = true;
                    button_stop.IsEnabled = false;
                }
            }
            if (ServiceStop)
            {
                labelServiceInfo.Content = "※ 設定を閉じ、接続しなおしてください。";
            }
        }

        private bool WaitToExit()
        {
            int count = 0;
            for (int i = 0; i < 5 && (count = Process.GetProcessesByName("EpgTimerSrv").Count()) > 0; i++)
            {
                System.Threading.Thread.Sleep(1000);
            }
            if (count > 0)
            {
                MessageBox.Show("EpgTimerSrv を終了できませんでした。");
            }
            return count == 0;
        }

        private void button_inst_Click(object sender, RoutedEventArgs e)
        {
            String exePath = SettingPath.ModulePath + "\\EpgTimerSrv.exe";
            if (ServiceCtrlClass.Install("EpgTimer Service", "EpgTimer Service", exePath) == false)
            {
                MessageBox.Show("インストールに失敗しました。");
            }
            UpdateServiceBtn();
        }

        private void button_uninst_Click(object sender, RoutedEventArgs e)
        {
            bool started = ServiceCtrlClass.IsStarted("EpgTimer Service");
            if (ServiceCtrlClass.Uninstall("EpgTimer Service") == false)
            {
                MessageBox.Show("アンインストールに失敗しました。");
            }
            else if (started && WaitToExit())
            {
                ServiceStop |= started;
            }
            UpdateServiceBtn();
        }

        private void button_start_Click(object sender, RoutedEventArgs e)
        {
            if (CommonManager.Instance.NWMode == false && CommonManager.Instance.IsConnected == true)
            {
                int count = Process.GetProcessesByName("EpgTimerSrv").Count();
                if (count > 0)
                {
                    int residentMode = IniFileHandler.GetPrivateProfileInt("SET", "ResidentMode", 0, SettingPath.TimerSrvIniPath);
                    if (residentMode > 0)
                    {
                        MessageBox.Show("[動作設定]-[その他]-[EpgTimerSrvを常駐させる] をオフにしてください。");
                    }
                    else if (MessageBox.Show("EpgTimerSrv を一度終了する必要があります。終了させますか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        if (CommonManager.Instance.CtrlCmd.SendClose() == ErrCode.CMD_SUCCESS)
                        {
                            count = WaitToExit() ? 0 : 1;
                        }
                    }
                }
                if (count == 0)
                {
                    if (ServiceCtrlClass.StartService("EpgTimer Service") == false)
                    {
                        MessageBox.Show("サービスの開始に失敗しました。");
                    }
                    ServiceStop = true; // 接続しなおす必要がある。
                }
            }
            UpdateServiceBtn();
        }

        private void button_stop_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceCtrlClass.StopService("EpgTimer Service") == false)
            {
                MessageBox.Show("サービスの停止に失敗しました。");
            }
            else
            {
                ServiceStop = true;
            }
            UpdateServiceBtn();
        }
    }
}
