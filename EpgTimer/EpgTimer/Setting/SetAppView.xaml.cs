﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.IO;

using CtrlCmdCLI.Def;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetAppView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetAppView : UserControl
    {
        private List<String> ngProcessList = new List<String>();
        private String ngMin = "10";
        public bool ngUsePC = false;
        public String ngUsePCMin = "3";
        public bool ngFileStreaming = false;
        public bool ngShareFile = false;

        MenuSettingData ctxmSetInfo;

        private List<String> extList = new List<string>();
        private List<String> delChkFolderList = new List<string>();
        private EpgSearchKeyInfo defSearchKey = new EpgSearchKeyInfo();

        private List<ViewMenuItem> buttonItem = new List<ViewMenuItem>();
        private List<ViewMenuItem> taskItem = new List<ViewMenuItem>();

        private Dictionary<UInt64, ServiceViewItem> serviceList = new Dictionary<UInt64, ServiceViewItem>();
        private List<IEPGStationInfo> stationList = new List<IEPGStationInfo>();

        public bool ServiceStop = false;

        public SetAppView()
        {
            InitializeComponent();

            if (CommonManager.Instance.NWMode == true)
            {
                tabItem1.IsEnabled = false;
                tabItem2.IsEnabled = false;
                tabItem7.IsEnabled = false;
                tabControl1.SelectedItem = tabItem3;
                checkBox_tcpServer.IsEnabled = false;
                label41.IsEnabled = false;
                textBox_tcpPort.IsEnabled = false;
                checkBox_autoDelRecInfo.IsEnabled = false;
                label42.IsEnabled = false;
                textBox_autoDelRecInfo.IsEnabled = false;
                checkBox_timeSync.IsEnabled = false;
                checkBox_wakeReconnect.IsEnabled = true;
                checkBox_suspendClose.IsEnabled = true;
                checkBox_ngAutoEpgLoad.IsEnabled = true;
                checkBox_srvResident.IsEnabled = false;
            }

            try
            {
                if (Settings.Instance.NoStyle == 1)
                {
                    button_standbyCtrl.Style = null;
                    button_autoDel.Style = null;
                    button_recname.Style = null;
                    button_btnUp.Style = null;
                    button_btnDown.Style = null;
                    button_btnDel.Style = null;
                    button_btnAdd.Style = null;
                    button_taskUp.Style = null;
                    button_taskDown.Style = null;
                    button_taskDel.Style = null;
                    button_taskAdd.Style = null;
                    button_searchDef.Style = null;
                    button_recDef.Style = null;
                    button_set_cm.Style = null;
                    button_exe1.Style = null;
                    button_exe2.Style = null;
                    button_add.Style = null;
                    button_del.Style = null;
                    button_inst.Style = null;
                    button_uninst.Style = null;
                    button_stop.Style = null;

                }

                StringBuilder buff = new StringBuilder(512);
                buff.Clear();
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
                if (IniFileHandler.GetPrivateProfileInt("SET", "RecOverWrite", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_appOverWrite.IsChecked = true;
                }

                int ngCount = IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "Count", 0, SettingPath.TimerSrvIniPath);
                if (ngCount == 0)
                {
                    ngProcessList.Add("EpgDataCap_Bon.exe");
                }
                else
                {
                    for (int i = 0; i < ngCount; i++)
                    {
                        buff.Clear();
                        IniFileHandler.GetPrivateProfileString("NO_SUSPEND", i.ToString(), "", buff, 512, SettingPath.TimerSrvIniPath);
                        ngProcessList.Add(buff.ToString());
                    }
                }
                buff.Clear();
                IniFileHandler.GetPrivateProfileString("NO_SUSPEND", "NoStandbyTime", "10", buff, 512, SettingPath.TimerSrvIniPath);
                ngMin = buff.ToString();
                if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePC", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    ngUsePC = true;
                }
                buff.Clear();
                IniFileHandler.GetPrivateProfileString("NO_SUSPEND", "NoUsePCTime", "3", buff, 512, SettingPath.TimerSrvIniPath);
                ngUsePCMin = buff.ToString();
                if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoFileStreaming", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    ngFileStreaming = true;
                }
                if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoShareFile", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    ngShareFile = true;
                }

                this.ctxmSetInfo = Settings.Instance.MenuSet.Clone();

                comboBox_process.Items.Add("リアルタイム");
                comboBox_process.Items.Add("高");
                comboBox_process.Items.Add("通常以上");
                comboBox_process.Items.Add("通常");
                comboBox_process.Items.Add("通常以下");
                comboBox_process.Items.Add("低");
                comboBox_process.SelectedIndex = IniFileHandler.GetPrivateProfileInt("SET", "ProcessPriority", 3, SettingPath.TimerSrvIniPath);

                //2
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
                if (IniFileHandler.GetPrivateProfileInt("SET", "AutoDelRecInfo", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_autoDelRecInfo.IsChecked = true;
                }
                textBox_autoDelRecInfo.Text = IniFileHandler.GetPrivateProfileInt("SET", "AutoDelRecInfoNum", 100, SettingPath.TimerSrvIniPath).ToString();

                if (IniFileHandler.GetPrivateProfileInt("SET", "TimeSync", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_timeSync.IsChecked = true;
                }

                buff.Clear();
                IniFileHandler.GetPrivateProfileString("SET", "RecNamePlugInFile", "RecName_Macro.dll", buff, 512, SettingPath.TimerSrvIniPath);
                String plugInFile = buff.ToString();

                try
                {
                    checkBox_closeMin.IsChecked = Settings.Instance.CloseMin;
                    checkBox_minWake.IsChecked = Settings.Instance.WakeMin;
                    checkBox_noToolTips.IsChecked = Settings.Instance.NoToolTip;
                    checkBox_noBallonTips.IsChecked = Settings.Instance.NoBallonTips;
                    checkBox_playDClick.IsChecked = Settings.Instance.PlayDClick;
                    checkBox_showTray.IsChecked = Settings.Instance.ShowTray;
                    checkBox_minHide.IsChecked = Settings.Instance.MinHide;
                    checkBox_cautionManyChange.IsChecked = Settings.Instance.CautionManyChange;
                    textBox_cautionManyChange.Text = Settings.Instance.CautionManyNum.ToString(); 

                    checkBox_wakeReconnect.IsChecked = Settings.Instance.WakeReconnectNW;
                    checkBox_suspendClose.IsChecked = Settings.Instance.SuspendCloseNW;
                    checkBox_ngAutoEpgLoad.IsChecked = Settings.Instance.NgAutoEpgLoadNW;
                }
                catch
                {
                }

                if (checkBox_srvResident.IsEnabled)
                {
                    int residentMode = IniFileHandler.GetPrivateProfileInt("SET", "ResidentMode", 0, SettingPath.TimerSrvIniPath);
                    checkBox_srvResident.IsChecked = residentMode >= 1;
                    checkBox_srvShowTray.IsChecked = residentMode >= 2;
                    checkBox_srvNoBalloonTip.IsChecked = IniFileHandler.GetPrivateProfileInt("SET", "NoBalloonTip", 0, SettingPath.TimerSrvIniPath) != 0;
                }

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
                        buff.Clear();
                        IniFileHandler.GetPrivateProfileString("DEL_EXT", i.ToString(), "", buff, 512, SettingPath.TimerSrvIniPath);
                        extList.Add(buff.ToString());
                    }
                }

                count = IniFileHandler.GetPrivateProfileInt("DEL_CHK", "Count", 0, SettingPath.TimerSrvIniPath);
                for (int i = 0; i < count; i++)
                {
                    buff.Clear();
                    IniFileHandler.GetPrivateProfileString("DEL_CHK", i.ToString(), "", buff, 512, SettingPath.TimerSrvIniPath);
                    delChkFolderList.Add(buff.ToString());
                }


                try
                {
                    string[] files = Directory.GetFiles(SettingPath.ModulePath + "\\RecName", "RecName*.dll");
                    int select = 0;
                    foreach (string info in files)
                    {
                        int index = comboBox_recname.Items.Add(System.IO.Path.GetFileName(info));
                        if (String.Compare(System.IO.Path.GetFileName(info), plugInFile, true) == 0)
                        {
                            select = index;
                        }
                    }
                    if (comboBox_recname.Items.Count != 0)
                    {
                        comboBox_recname.SelectedIndex = select;
                    }
                }
                catch
                {
                }

                if (IniFileHandler.GetPrivateProfileInt("SET", "EnableTCPSrv", 0, SettingPath.TimerSrvIniPath) == 1)
                {
                    checkBox_tcpServer.IsChecked = true;
                }
                textBox_tcpPort.Text = IniFileHandler.GetPrivateProfileInt("SET", "TCPPort", 4510, SettingPath.TimerSrvIniPath).ToString();

                Settings.GetDefSearchSetting(ref defSearchKey);

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

                buttonItem.Add(new ViewMenuItem("（空白）", false));
                buttonItem.Add(new ViewMenuItem("設定", false));
                buttonItem.Add(new ViewMenuItem("検索", false));
                buttonItem.Add(new ViewMenuItem("スタンバイ", false));
                buttonItem.Add(new ViewMenuItem("休止", false));
                buttonItem.Add(new ViewMenuItem("EPG取得", false));
                buttonItem.Add(new ViewMenuItem("EPG再読み込み", false));
                buttonItem.Add(new ViewMenuItem("終了", false));
                buttonItem.Add(new ViewMenuItem("カスタム１", false));
                buttonItem.Add(new ViewMenuItem("カスタム２", false));
                buttonItem.Add(new ViewMenuItem("NetworkTV終了", false));
                buttonItem.Add(new ViewMenuItem("情報通知ログ", false));

                taskItem.Add(new ViewMenuItem("（セパレータ）", false));
                taskItem.Add(new ViewMenuItem("設定", false));
                taskItem.Add(new ViewMenuItem("スタンバイ", false));
                taskItem.Add(new ViewMenuItem("休止", false));
                taskItem.Add(new ViewMenuItem("EPG取得", false));
                taskItem.Add(new ViewMenuItem("終了", false));

                foreach (String info in Settings.Instance.ViewButtonList)
                {
                    //リストが空であることを示す特殊なアイテムを無視
                    if (String.Compare(info, "（なし）") == 0)
                    {
                        continue;
                    }
                    //.NET的に同一文字列のStringを入れると選択動作がおかしくなるみたいなので毎回作成しておく
                    listBox_viewBtn.Items.Add(new ViewMenuItem(info, true));
                    if (String.Compare(info, "（空白）") != 0)
                    {
                        foreach (ViewMenuItem item in buttonItem)
                        {
                            if (String.Compare(info, item.MenuName) == 0)
                            {
                                item.IsSelected = true;
                                break;
                            }
                        }
                    }
                }
                foreach (String info in Settings.Instance.TaskMenuList)
                {
                    //.NET的に同一文字列のStringを入れると選択動作がおかしくなるみたいなので毎回作成しておく
                    listBox_viewTask.Items.Add(new ViewMenuItem(info, true));
                    if (String.Compare(info, "（セパレータ）") != 0)
                    {
                        foreach (ViewMenuItem item in taskItem)
                        {
                            if (String.Compare(info, item.MenuName) == 0)
                            {
                                item.IsSelected = true;
                                break;
                            }
                        }
                    }
                }

                ReLoadButtonItem();
                ReLoadTaskItem();

                textBox_name1.Text = Settings.Instance.Cust1BtnName;
                textBox_exe1.Text = Settings.Instance.Cust1BtnCmd;
                textBox_opt1.Text = Settings.Instance.Cust1BtnCmdOpt;

                textBox_name2.Text = Settings.Instance.Cust2BtnName;
                textBox_exe2.Text = Settings.Instance.Cust2BtnCmd;
                textBox_opt2.Text = Settings.Instance.Cust2BtnCmdOpt;

                foreach (ChSet5Item info in ChSet5.Instance.ChList.Values)
                {
                    ServiceViewItem item = new ServiceViewItem(info);
                    serviceList.Add(item.Key, item);
                }
                listBox_service.ItemsSource = serviceList.Values;

                stationList = Settings.Instance.IEpgStationList;
                ReLoadStation();

                UpdateServiceBtn();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void ReLoadButtonItem()
        {
            listBox_itemBtn.Items.Clear();
            foreach (ViewMenuItem info in buttonItem)
            {
                if (info.IsSelected == false)
                {
                    listBox_itemBtn.Items.Add(info);
                }
            }
        }

        private void ReLoadTaskItem()
        {
            listBox_itemTask.Items.Clear();
            foreach (ViewMenuItem info in taskItem)
            {
                if (info.IsSelected == false)
                {
                    listBox_itemTask.Items.Add(info);
                }
            }
        }

        public void SaveSetting()
        {
            if (radioButton_none.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "0", SettingPath.TimerSrvIniPath);
            }
            if (radioButton_standby.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "1", SettingPath.TimerSrvIniPath);
            }
            if (radioButton_suspend.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "2", SettingPath.TimerSrvIniPath);
            }
            if (radioButton_shutdown.IsChecked == true)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", "3", SettingPath.TimerSrvIniPath);
            }

            string setValue = (checkBox_reboot.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "Reboot", setValue, SettingPath.TimerSrvIniPath);

            IniFileHandler.WritePrivateProfileString("SET", "WakeTime", textBox_pcWakeTime.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "StartMargin", textBox_megine_start.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "EndMargin", textBox_margine_end.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecAppWakeTime", textBox_appWakeTime.Text, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_appMin.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "RecMinWake", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_appView.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "RecView", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_appDrop.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "DropLog", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_addPgInfo.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "PgInfoLog", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_appNW.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "RecNW", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_appOverWrite.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "RecOverWrite", setValue, SettingPath.TimerSrvIniPath);

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

            IniFileHandler.WritePrivateProfileString("SET", "ProcessPriority", comboBox_process.SelectedIndex.ToString(), SettingPath.TimerSrvIniPath);

            Settings.Instance.MenuSet = this.ctxmSetInfo.Clone();

            setValue = (checkBox_back_priority.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "BackPriority", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_autoDel.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "AutoDel", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_recname.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "RecNamePlugIn", setValue, SettingPath.TimerSrvIniPath);

            setValue = (comboBox_recname.SelectedItem != null ? (string)comboBox_recname.SelectedItem : "");
            IniFileHandler.WritePrivateProfileString("SET", "RecNamePlugInFile", setValue, SettingPath.TimerSrvIniPath);

            setValue = (checkBox_autoDelRecInfo.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "AutoDelRecInfo", setValue, SettingPath.TimerSrvIniPath);

            IniFileHandler.WritePrivateProfileString("SET", "AutoDelRecInfoNum", textBox_autoDelRecInfo.Text.ToString(), SettingPath.TimerSrvIniPath);

            setValue = (checkBox_timeSync.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "TimeSync", setValue, SettingPath.TimerSrvIniPath);

            Settings.Instance.CloseMin = (bool)checkBox_closeMin.IsChecked;
            Settings.Instance.WakeMin = (bool)checkBox_minWake.IsChecked;
            Settings.Instance.ShowTray = (bool)checkBox_showTray.IsChecked;
            Settings.Instance.MinHide = (bool)checkBox_minHide.IsChecked;

            IniFileHandler.WritePrivateProfileString("SET", "ResidentMode",
                checkBox_srvResident.IsChecked == false ? "0" : checkBox_srvShowTray.IsChecked == false ? "1" : "2", SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "NoBalloonTip", checkBox_srvNoBalloonTip.IsChecked == false ? "0" : "1", SettingPath.TimerSrvIniPath);

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

            setValue = (checkBox_tcpServer.IsChecked == true ? "1" : "0");
            IniFileHandler.WritePrivateProfileString("SET", "EnableTCPSrv", setValue, SettingPath.TimerSrvIniPath);

            IniFileHandler.WritePrivateProfileString("SET", "TCPPort", textBox_tcpPort.Text, SettingPath.TimerSrvIniPath);

            Settings.Instance.NoToolTip = (checkBox_noToolTips.IsChecked == true);
            Settings.Instance.NoBallonTips = (checkBox_noBallonTips.IsChecked == true);
            Settings.Instance.PlayDClick = (checkBox_playDClick.IsChecked == true);
            Settings.Instance.CautionManyChange = (checkBox_cautionManyChange.IsChecked != false);
            try
            {
                Settings.Instance.CautionManyNum = Convert.ToInt32(textBox_cautionManyChange.Text);
            }
            catch { }

            Settings.Instance.WakeReconnectNW = (checkBox_wakeReconnect.IsChecked == true);
            Settings.Instance.SuspendCloseNW = (checkBox_suspendClose.IsChecked == true);
            Settings.Instance.NgAutoEpgLoadNW = (checkBox_ngAutoEpgLoad.IsChecked == true);

            Settings.Instance.SearchKeyRegExp = (defSearchKey.regExpFlag != 0);
            Settings.Instance.SearchKeyAimaiFlag = (defSearchKey.aimaiFlag != 0);
            Settings.Instance.SearchKeyTitleOnly = (defSearchKey.titleOnlyFlag != 0);

            Settings.Instance.SearchKeyContentList.Clear();
            foreach (EpgContentData info in defSearchKey.contentList)
            {
                ContentKindInfo item = new ContentKindInfo();
                item.Nibble1 = info.content_nibble_level_1;
                item.Nibble2 = info.content_nibble_level_2;
                Settings.Instance.SearchKeyContentList.Add(item);
            }
            Settings.Instance.SearchKeyDateItemList.Clear();
            foreach (EpgSearchDateInfo info in defSearchKey.dateList)
            {
                DateItem item = new DateItem();
                item.DateInfo = info;
                Settings.Instance.SearchKeyDateItemList.Add(item);
            }
            Settings.Instance.SearchKeyServiceList.Clear();
            foreach (Int64 info in defSearchKey.serviceList)
            {
                Settings.Instance.SearchKeyServiceList.Add(info);
            }

            Settings.Instance.SearchKeyNotContent = (defSearchKey.notContetFlag != 0);
            Settings.Instance.SearchKeyNotDate = (defSearchKey.notDateFlag != 0);
            Settings.Instance.SearchKeyFreeCA = defSearchKey.freeCAFlag;
            Settings.Instance.SearchKeyChkRecEnd = defSearchKey.chkRecEnd;
            Settings.Instance.SearchKeyChkRecDay = defSearchKey.chkRecDay;
            Settings.Instance.SearchKeyChkRecNoService = defSearchKey.chkRecNoService;
            Settings.Instance.SearchKeyChkDurationMin = defSearchKey.chkDurationMin;
            Settings.Instance.SearchKeyChkDurationMax = defSearchKey.chkDurationMax;

            Settings.Instance.ViewButtonShowAsTab = checkBox_showAsTab.IsChecked == true;
            Settings.Instance.SuspendChk = (uint)(checkBox_suspendChk.IsChecked == true ? 1 : 0);
            try
            {
                Settings.Instance.SuspendChkTime = Convert.ToUInt16(textBox_suspendChkTime.Text.ToString());
            }
            catch { }

            Settings.Instance.ViewButtonList.Clear();
            foreach (ViewMenuItem info in listBox_viewBtn.Items)
            {
                Settings.Instance.ViewButtonList.Add(info.MenuName);
            }
            if (Settings.Instance.ViewButtonList.Count == 0)
            {
                //リストが空であることを示す特殊なアイテムを追加
                Settings.Instance.ViewButtonList.Add("（なし）");
            }

            Settings.Instance.TaskMenuList.Clear();
            foreach (ViewMenuItem info in listBox_viewTask.Items)
            {
                Settings.Instance.TaskMenuList.Add(info.MenuName);
            }

            Settings.Instance.Cust1BtnName = textBox_name1.Text;
            Settings.Instance.Cust1BtnCmd = textBox_exe1.Text;
            Settings.Instance.Cust1BtnCmdOpt = textBox_opt1.Text;

            Settings.Instance.Cust2BtnName = textBox_name2.Text;
            Settings.Instance.Cust2BtnCmd = textBox_exe2.Text;
            Settings.Instance.Cust2BtnCmdOpt = textBox_opt2.Text;

            Settings.Instance.IEpgStationList = stationList;
        }

        private void button_standbyCtrl_Click(object sender, RoutedEventArgs e)
        {
            SetAppCancelWindow dlg = new SetAppCancelWindow();
            dlg.processList = this.ngProcessList;
            dlg.ngMin = this.ngMin;
            dlg.ngUsePC = this.ngUsePC;
            dlg.ngUsePCMin = this.ngUsePCMin;
            dlg.ngFileStreaming = this.ngFileStreaming;
            dlg.ngShareFile = this.ngShareFile;
            dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;

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
            SetApp2DelWindow dlg = new SetApp2DelWindow();
            dlg.extList = this.extList;
            dlg.delChkFolderList = this.delChkFolderList;
            dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;

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

        private void button_btnUp_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_viewBtn.SelectedItem != null)
            {
                if (listBox_viewBtn.SelectedIndex >= 1)
                {
                    object temp = listBox_viewBtn.SelectedItem;
                    int index = listBox_viewBtn.SelectedIndex;
                    listBox_viewBtn.Items.RemoveAt(listBox_viewBtn.SelectedIndex);
                    listBox_viewBtn.Items.Insert(index - 1, temp);
                    listBox_viewBtn.SelectedIndex = index - 1;
                }
            }
        }

        private void button_btnDel_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_viewBtn.SelectedItem != null)
            {
                ViewMenuItem info = listBox_viewBtn.SelectedItem as ViewMenuItem;
                if (String.Compare(info.MenuName, "設定") == 0)
                {
                    bool found = false;
                    foreach (ViewMenuItem item in listBox_viewTask.Items)
                    {
                        if ((found = item.MenuName == "設定") != false)
                        {
                            break;
                        }
                    }
                    if (!found)
                    {
                        MessageBox.Show("設定は上部表示ボタンか右クリック表示項目のどちらかに必要です");
                        return;
                    }
                }
                if (String.Compare(info.MenuName, "（空白）") != 0)
                {
                    foreach (ViewMenuItem item in buttonItem)
                    {
                        if (String.Compare(info.MenuName, item.MenuName) == 0)
                        {
                            item.IsSelected = false;
                            break;
                        }
                    }
                }
                listBox_viewBtn.Items.Remove(info);
                ReLoadButtonItem();
            }
        }

        private void button_btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_itemBtn.SelectedItem != null)
            {
                ViewMenuItem info = listBox_itemBtn.SelectedItem as ViewMenuItem;
                if (String.Compare(info.MenuName, "（空白）") != 0)
                {
                    info.IsSelected = true;
                }
                listBox_viewBtn.Items.Add(new ViewMenuItem(info.MenuName, true));
                ReLoadButtonItem();
            }
        }

        private void button_btnDown_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_viewBtn.SelectedItem != null)
            {
                if (listBox_viewBtn.SelectedIndex < listBox_viewBtn.Items.Count - 1)
                {
                    object temp = listBox_viewBtn.SelectedItem;
                    int index = listBox_viewBtn.SelectedIndex;
                    listBox_viewBtn.Items.RemoveAt(listBox_viewBtn.SelectedIndex);
                    listBox_viewBtn.Items.Insert(index + 1, temp);
                    listBox_viewBtn.SelectedIndex = index + 1;
                }
            }
        }

        private void button_taskUp_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_viewTask.SelectedItem != null)
            {
                if (listBox_viewTask.SelectedIndex >= 1)
                {
                    object temp = listBox_viewTask.SelectedItem;
                    int index = listBox_viewTask.SelectedIndex;
                    listBox_viewTask.Items.RemoveAt(listBox_viewTask.SelectedIndex);
                    listBox_viewTask.Items.Insert(index - 1, temp);
                    listBox_viewTask.SelectedIndex = index - 1;
                }
            }
        }

        private void button_taskDel_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_viewTask.SelectedItem != null)
            {
                ViewMenuItem info = listBox_viewTask.SelectedItem as ViewMenuItem;
                if (String.Compare(info.MenuName, "設定") == 0)
                {
                    bool found = false;
                    foreach (ViewMenuItem item in listBox_viewBtn.Items)
                    {
                        if ((found = item.MenuName == "設定") != false)
                        {
                            break;
                        }
                    }
                    if (!found)
                    {
                        MessageBox.Show("設定は上部表示ボタンか右クリック表示項目のどちらかに必要です");
                        return;
                    }
                }
                if (String.Compare(info.MenuName, "（セパレータ）") != 0)
                {
                    foreach (ViewMenuItem item in taskItem)
                    {
                        if (String.Compare(info.MenuName, item.MenuName) == 0)
                        {
                            item.IsSelected = false;
                            break;
                        }
                    }
                }
                listBox_viewTask.Items.Remove(info);
                ReLoadTaskItem();
            }
        }

        private void button_taskAdd_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_itemTask.SelectedItem != null)
            {
                ViewMenuItem info = listBox_itemTask.SelectedItem as ViewMenuItem;
                if (String.Compare(info.MenuName, "（セパレータ）") != 0)
                {
                    info.IsSelected = true;
                }
                listBox_viewTask.Items.Add(new ViewMenuItem(info.MenuName, true));
                ReLoadTaskItem();
            }
        }

        private void button_taskDown_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_viewTask.SelectedItem != null)
            {
                if (listBox_viewTask.SelectedIndex < listBox_viewTask.Items.Count - 1)
                {
                    object temp = listBox_viewTask.SelectedItem;
                    int index = listBox_viewTask.SelectedIndex;
                    listBox_viewTask.Items.RemoveAt(listBox_viewTask.SelectedIndex);
                    listBox_viewTask.Items.Insert(index + 1, temp);
                    listBox_viewTask.SelectedIndex = index + 1;
                }
            }
        }

        private void button_recDef_Click(object sender, RoutedEventArgs e)
        {
            SetDefRecSettingWindow dlg = new SetDefRecSettingWindow();
            dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
            dlg.ShowDialog();
        }

        private void button_searchDef_Click(object sender, RoutedEventArgs e)
        {
            SetDefSearchSettingWindow dlg = new SetDefSearchSettingWindow();
            dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
            dlg.SetDefSetting(defSearchKey);

            if (dlg.ShowDialog() == true)
            {
                dlg.GetSetting(ref defSearchKey);
            }
        }

        private void button_set_cm_Click(object sender, RoutedEventArgs e)
        {
            SetContextMenuWindow dlg = new SetContextMenuWindow();
            dlg.info = this.ctxmSetInfo.Clone();
            dlg.Owner = (Window)PresentationSource.FromVisual(this).RootVisual;
            
            if (dlg.ShowDialog() == true)
            {
                this.ctxmSetInfo = dlg.info.Clone();
            }
        }

        private void button_exe1_Click(object sender, RoutedEventArgs e)
        {
            string path = CommonManager.Instance.GetFileNameByDialog(textBox_exe1.Text,"",".exe");
            if (path != null)
            {
                textBox_exe1.Text = path;
            }
        }

        private void button_exe2_Click(object sender, RoutedEventArgs e)
        {
            string path = CommonManager.Instance.GetFileNameByDialog(textBox_exe2.Text, "", ".exe");
            if (path != null)
            {
                textBox_exe2.Text = path;
            }
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

        private void UpdateServiceBtn()
        {
            if (ServiceCtrlClass.ServiceIsInstalled("EpgTimer Service") == false)
            {
                button_inst.IsEnabled = true;
                button_uninst.IsEnabled = false;
                button_stop.IsEnabled = false;
            }
            else
            {
                button_inst.IsEnabled = false;
                button_uninst.IsEnabled = true;
                if (ServiceCtrlClass.IsStarted("EpgTimer Service") == true)
                {
                    button_stop.IsEnabled = true;
                }
                else
                {
                    button_stop.IsEnabled = false;
                }
            }
        }
        
        private void button_inst_Click(object sender, RoutedEventArgs e)
        {
            String exePath = SettingPath.ModulePath + "\\EpgTimerSrv.exe";
            if (ServiceCtrlClass.Install("EpgTimer Service", "EpgTimer Service", exePath) == false)
            {
                MessageBox.Show("インストールに失敗しました。\r\nVista以降のOSでは、管理者権限で起動されている必要があります。");
            }
            UpdateServiceBtn();
        }

        private void button_uninst_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceCtrlClass.Uninstall("EpgTimer Service") == false)
            {
                MessageBox.Show("アンインストールに失敗しました。\r\nVista以降のOSでは、管理者権限で起動されている必要があります。");
            }
            else
            {
                ServiceStop = true;
            }
            UpdateServiceBtn();
        }

        private void button_stop_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceCtrlClass.StopService("EpgTimer Service") == false)
            {
                MessageBox.Show("サービスの停止に失敗しました。\r\nVista以降のOSでは、管理者権限で起動されている必要があります。");
            }
            else
            {
                ServiceStop = true;
            }
            UpdateServiceBtn();
        }
    }
}
