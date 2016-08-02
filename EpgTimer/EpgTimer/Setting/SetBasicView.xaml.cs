using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetBasicView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetBasicView : UserControl
    {
        private ObservableCollection<EpgCaptime> timeList = new ObservableCollection<EpgCaptime>();
        private List<ServiceViewItem> serviceList;

        public bool IsChangeSettingPath { get; private set; }

        public SetBasicView()
        {
            InitializeComponent();

            IsChangeSettingPath = false;

            try
            {
                // tabItem1 - 保存フォルダ
                // 保存できない項目は IsEnabled = false にする
                if (CommonManager.Instance.NWMode == true)
                {
                    group_setPath.IsEnabled = false; // 設定関係保存フォルダ
                    group_exePath.IsEnabled = false; // 録画用アプリのexe
                    group_recInfoFolder.IsEnabled = false; // 録画情報保存フォルダ
                    label4.IsEnabled = false; //※ 録画中やEPG取得中に設定を変更すると正常動作しなくなる可能性があります。
                    button_shortCutSrv.IsEnabled = false;
                }
                group_recFolder.IsEnabled = IniFileHandler.CanUpdateInifile; // 録画保存フォルダ

                if (textBox_recFolder.IsEnabled)
                {
                    textBox_recFolder.KeyDown += ViewUtil.KeyDown_Enter(button_rec_add);
                }

                // 読める設定のみ項目に反映させる
                if (IniFileHandler.CanReadInifile)
                {
                    textBox_setPath.Text = SettingPath.SettingFolderPath;
                    textBox_exe.Text = SettingPath.EdcbExePath;

                    textBox_recInfoFolder.Text = IniFileHandler.GetPrivateProfileString("SET", "RecInfoFolder", "", SettingPath.CommonIniPath);

                    Settings.Instance.DefRecFolders.ForEach(folder => listBox_recFolder.Items.Add(new UserCtrlView.BGBarListBoxItem(folder)));
                }
                if (CommonManager.Instance.NWMode == false)
                {
                    string viewAppIniPath = SettingPath.ModulePath.TrimEnd('\\') + "\\ViewApp.ini";
                    textBox_cmdBon.Text = IniFileHandler.GetPrivateProfileString("APP_CMD_OPT", "Bon", "-d", viewAppIniPath);
                    textBox_cmdMin.Text = IniFileHandler.GetPrivateProfileString("APP_CMD_OPT", "Min", "-min", viewAppIniPath);
                    textBox_cmdViewOff.Text = IniFileHandler.GetPrivateProfileString("APP_CMD_OPT", "ViewOff", "-noview", viewAppIniPath);
                }

                // tabItem2 - チューナー
                // 保存できない項目は IsEnabled = false にする
                if (IniFileHandler.CanUpdateInifile == false)
                {
                    ViewUtil.DisableControlChildren(tabItem2);
                    grid_tuner.IsEnabled = true;
                    ViewUtil.ChangeChildren(grid_tuner, false);
                }
                listBox_bon.IsEnabled = IniFileHandler.CanUpdateInifile;

                // 読める設定のみ項目に反映させる
                if (IniFileHandler.CanReadInifile)
                {
                    SortedList<Int32, TunerInfo> tunerInfo = new SortedList<Int32, TunerInfo>();
                    foreach (string fileName in CommonManager.Instance.GetBonFileList())
                    {
                        try
                        {
                            TunerInfo item = new TunerInfo(fileName);
                            item.TunerNum = IniFileHandler.GetPrivateProfileInt(item.BonDriver, "Count", 0, SettingPath.TimerSrvIniPath).ToString();
                            item.IsEpgCap = (IniFileHandler.GetPrivateProfileInt(item.BonDriver, "GetEpg", 1, SettingPath.TimerSrvIniPath) != 0);
                            item.EPGNum = IniFileHandler.GetPrivateProfileInt(item.BonDriver, "EPGCount", 0, SettingPath.TimerSrvIniPath).ToString();
                            int priority = IniFileHandler.GetPrivateProfileInt(item.BonDriver, "Priority", 0xFFFF, SettingPath.TimerSrvIniPath);
                            while (true)
                            {
                                if (tunerInfo.ContainsKey(priority) == true)
                                {
                                    priority++;
                                }
                                else
                                {
                                    tunerInfo.Add(priority, item);
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                        }
                    }
                    foreach (TunerInfo info in tunerInfo.Values)
                    {
                        listBox_bon.Items.Add(info);
                    }
                    if (listBox_bon.Items.Count > 0)
                    {
                        listBox_bon.SelectedIndex = 0;
                    }
                }
                button_shortCut.Content = SettingPath.ModuleName + ".exe" + (File.Exists(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), SettingPath.ModuleName + ".lnk")) ? "を解除" : "");
                button_shortCutSrv.Content = (string)button_shortCutSrv.Content + (File.Exists(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "EpgTimerSrv.lnk")) ? "を解除" : "");

                // tabItem3 - EPG取得
                // 保存できない項目は IsEnabled = false にする
                if (IniFileHandler.CanUpdateInifile == false)
                {
                    ViewUtil.DisableControlChildren(tabItem3);
                }
                listView_service.IsEnabled = IniFileHandler.CanUpdateInifile;

                // 読める設定のみ項目に反映させる
                serviceList = new List<ServiceViewItem>();
                if (IniFileHandler.CanReadInifile)
                {
                    comboBox_HH.DataContext = CommonManager.Instance.HourDictionary.Values;
                    comboBox_HH.SelectedIndex = 0;
                    comboBox_MM.DataContext = CommonManager.Instance.MinDictionary.Values;
                    comboBox_MM.SelectedIndex = 0;

                    try
                    {
                        foreach (ChSet5Item info in ChSet5.ChList.Values)
                        {
                            ServiceViewItem item = new ServiceViewItem(info);
                            if (info.EpgCapFlag == 1)
                            {
                                item.IsSelected = true;
                            }
                            else
                            {
                                item.IsSelected = false;
                            }
                            serviceList.Add(item);
                        }
                    }
                    catch
                    {
                    }
                    listView_service.DataContext = serviceList;

                    if (IniFileHandler.GetPrivateProfileInt("SET", "BSBasicOnly", 1, SettingPath.CommonIniPath) == 1)
                    {
                        checkBox_bs.IsChecked = true;
                    }
                    else
                    {
                        checkBox_bs.IsChecked = false;
                    }
                    if (IniFileHandler.GetPrivateProfileInt("SET", "CS1BasicOnly", 1, SettingPath.CommonIniPath) == 1)
                    {
                        checkBox_cs1.IsChecked = true;
                    }
                    else
                    {
                        checkBox_cs1.IsChecked = false;
                    }
                    if (IniFileHandler.GetPrivateProfileInt("SET", "CS2BasicOnly", 1, SettingPath.CommonIniPath) == 1)
                    {
                        checkBox_cs2.IsChecked = true;
                    }
                    else
                    {
                        checkBox_cs2.IsChecked = false;
                    }
                    if (IniFileHandler.GetPrivateProfileInt("SET", "CS3BasicOnly", 0, SettingPath.CommonIniPath) == 1)
                    {
                        checkBox_cs3.IsChecked = true;
                    }
                    else
                    {
                        checkBox_cs3.IsChecked = false;
                    }

                    int capCount = IniFileHandler.GetPrivateProfileInt("EPG_CAP", "Count", 0, SettingPath.TimerSrvIniPath);
                    if (capCount == 0)
                    {
                        EpgCaptime item = new EpgCaptime();
                        item.IsSelected = true;
                        item.Time = "23:00";
                        item.BSBasicOnly = checkBox_bs.IsChecked == true;
                        item.CS1BasicOnly = checkBox_cs1.IsChecked == true;
                        item.CS2BasicOnly = checkBox_cs2.IsChecked == true;
                        item.CS3BasicOnly = checkBox_cs3.IsChecked == true;
                        timeList.Add(item);
                    }
                    else
                    {
                        for (int i = 0; i < capCount; i++)
                        {
                            EpgCaptime item = new EpgCaptime();
                            item.Time = IniFileHandler.GetPrivateProfileString("EPG_CAP", i.ToString(), "", SettingPath.TimerSrvIniPath);
                            if (IniFileHandler.GetPrivateProfileInt("EPG_CAP", i.ToString() + "Select", 0, SettingPath.TimerSrvIniPath) == 1)
                            {
                                item.IsSelected = true;
                            }
                            else
                            {
                                item.IsSelected = false;
                            }
                            // 取得種別(bit0(LSB)=BS,bit1=CS1,bit2=CS2,bit3=CS3)。負値のときは共通設定に従う
                            int flags = IniFileHandler.GetPrivateProfileInt("EPG_CAP", i.ToString() + "BasicOnlyFlags", -1, SettingPath.TimerSrvIniPath);
                            if (flags >= 0)
                            {
                                item.BSBasicOnly = (flags & 1) != 0;
                                item.CS1BasicOnly = (flags & 2) != 0;
                                item.CS2BasicOnly = (flags & 4) != 0;
                                item.CS3BasicOnly = (flags & 8) != 0;
                            }
                            else
                            {
                                item.BSBasicOnly = checkBox_bs.IsChecked == true;
                                item.CS1BasicOnly = checkBox_cs1.IsChecked == true;
                                item.CS2BasicOnly = checkBox_cs2.IsChecked == true;
                                item.CS3BasicOnly = checkBox_cs3.IsChecked == true;
                            }
                            timeList.Add(item);
                        }
                    }
                    ListView_time.DataContext = timeList;

                    textBox_ngCapMin.Text = IniFileHandler.GetPrivateProfileInt("SET", "NGEpgCapTime", 20, SettingPath.TimerSrvIniPath).ToString();
                    textBox_ngTunerMin.Text = IniFileHandler.GetPrivateProfileInt("SET", "NGEpgCapTunerTime", 20, SettingPath.TimerSrvIniPath).ToString();
                }

                SetBasicView_tabItem4();

                listBox_Button_Set();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        private string TimerSrvFolder { get { return System.IO.Path.GetDirectoryName(SettingPath.TimerSrvIniPath); } }

        private void SetBasicView_tabItem4()
        {
            // tabItem4 - HTTPサーバー

            // 保存できない項目は IsEnabled = false にする
            if (CommonManager.Instance.NWMode == true)
            {
                ViewUtil.DisableControlChildren(tabItem4);
            }
            else
            {
                string httppublicIniPath = SettingPath.SettingFolderPath + "\\HttpPublic.ini";
                textBox_ffmpegPath.Text = IniFileHandler.GetPrivateProfileString("SET", "ffmpeg", "", httppublicIniPath);
                textBox_readexPath.Text = IniFileHandler.GetPrivateProfileString("SET", "readex", "", httppublicIniPath);
            }

            // 読める設定のみ項目に反映させる
            if (IniFileHandler.CanReadInifile)
            {
                int enableHttpSrv = IniFileHandler.GetPrivateProfileInt("SET", "EnableHttpSrv", 0, SettingPath.TimerSrvIniPath);
                checkBox_httpServer.IsChecked = enableHttpSrv > 0;
                checkBox_httpLog.IsChecked = enableHttpSrv == 2;

                textBox_httpPort.Text = IniFileHandler.GetPrivateProfileString("SET", "HttpPort", "5510", SettingPath.TimerSrvIniPath);

                string gitdir = Environment.GetEnvironmentVariable("git_install_root");
                string opensslExe = string.IsNullOrEmpty(gitdir) ? "" : gitdir + "\\usr\\bin\\openssl.exe";
                button_generatePem.IsEnabled = false;
                textBox_opensslPath.Text = IniFileHandler.GetPrivateProfileString("SET", "OpensslExeFile", opensslExe, SettingPath.TimerSrvIniPath);

                textBox_httpAcl.Text = IniFileHandler.GetPrivateProfileString("SET", "HttpAccessControlList", "+127.0.0.1", SettingPath.TimerSrvIniPath);
                textBox_httpThreads.Text = IniFileHandler.GetPrivateProfileInt("SET", "HttpNumThreads", 5, SettingPath.TimerSrvIniPath).ToString();
                textBox_httpTimeout.Text = IniFileHandler.GetPrivateProfileInt("SET", "HttpRequestTimeoutSec", 120, SettingPath.TimerSrvIniPath).ToString();
                textBox_httpSSLVersion.Text = IniFileHandler.GetPrivateProfileInt("SET", "HttpSslProtocolVersion", 2, SettingPath.TimerSrvIniPath).ToString();

				// TODO: 未実装パラメータ
				//IniFileHandler.GetPrivateProfileString("SET", "HttpSslCipherList", "HIGH:!aNULL:!MD5", SettingPath.TimerSrvIniPath).ToString();
				//IniFileHandler.GetPrivateProfileInt("SET", "HttpKeepAlive", 0, SettingPath.TimerSrvIniPath).ToString();

				string document_root = CommonManager.Instance.NWMode ? "" : TimerSrvFolder + "\\HttpPublic";
                textBox_docrootPath.Text = IniFileHandler.GetPrivateProfileString("SET", "HttpPublicFolder", document_root, SettingPath.TimerSrvIniPath);

                checkBox_httpAuth.IsChecked = File.Exists(TimerSrvFolder + "\\glpasswd");
                textBox_httpAuthDom.Text = IniFileHandler.GetPrivateProfileString("SET", "HttpAuthenticationDomain", "mydomain.com", SettingPath.TimerSrvIniPath);

                checkBox_dlnaServer.IsChecked = IniFileHandler.GetPrivateProfileInt("SET", "EnableDMS", 0, SettingPath.TimerSrvIniPath) == 1;
            }
            if (CommonManager.Instance.NWMode == false)
            {
                // ローカル接続時はファイルやフォルダの存在確認をしておく
                CheckHttpFiles();
                CheckHttpsFiles();
                CheckDlnaFiles();
            }
            else
            {
                // ネットワーク接続時は警告を出さない
                warn_http.Visibility = Visibility.Collapsed;
                warn_ssldll.Visibility = Visibility.Collapsed;
                warn_sslcertpem.Visibility = Visibility.Collapsed;
                warn_dlna.Visibility = Visibility.Collapsed;
            }
        }

        public void SaveSetting()
        {
            try
            {
                // tabItem1
                if (textBox_setPath.IsEnabled)
                {
                    string setPath = textBox_setPath.Text.Trim();
                    setPath = setPath == "" ? SettingPath.DefSettingFolderPath : setPath;
                    System.IO.Directory.CreateDirectory(setPath);

                    IsChangeSettingPath = SettingPath.SettingFolderPath.TrimEnd('\\') != setPath.TrimEnd('\\');
                    SettingPath.SettingFolderPath = setPath;
                }
                if (textBox_recInfoFolder.IsEnabled)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "RecInfoFolder",
                                                             textBox_recInfoFolder.Text.Trim() == "" ? null : textBox_recInfoFolder.Text, SettingPath.CommonIniPath);
                }
                if (textBox_exe.IsEnabled)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "RecExePath", textBox_exe.Text, SettingPath.CommonIniPath);
                }

                string viewAppIniPath = SettingPath.ModulePath.TrimEnd('\\') + "\\ViewApp.ini";
                if (textBox_cmdBon.IsEnabled)
                {
                    if (IniFileHandler.GetPrivateProfileString("APP_CMD_OPT", "Bon", "-d", viewAppIniPath) != textBox_cmdBon.Text)
                    {
                        IniFileHandler.WritePrivateProfileString("APP_CMD_OPT", "Bon", textBox_cmdBon.Text, viewAppIniPath);
                    }
                }
                if (textBox_cmdMin.IsEnabled)
                {
                    if (IniFileHandler.GetPrivateProfileString("APP_CMD_OPT", "Min", "-min", viewAppIniPath) != textBox_cmdMin.Text)
                    {
                        IniFileHandler.WritePrivateProfileString("APP_CMD_OPT", "Min", textBox_cmdMin.Text, viewAppIniPath);
                    }
                }
                if (textBox_cmdViewOff.IsEnabled)
                {
                    if (IniFileHandler.GetPrivateProfileString("APP_CMD_OPT", "ViewOff", "-noview", viewAppIniPath) != textBox_cmdViewOff.Text)
                    {
                        IniFileHandler.WritePrivateProfileString("APP_CMD_OPT", "ViewOff", textBox_cmdViewOff.Text, viewAppIniPath);
                    }
                }

                if (listBox_recFolder.IsEnabled)
                {
                    int recFolderCount = listBox_recFolder.Items.Count;
                    IniFileHandler.WritePrivateProfileString("SET", "RecFolderNum", recFolderCount.ToString(), SettingPath.CommonIniPath);
                    for (int i = 0; i < recFolderCount; i++)
                    {
                        string key = "RecFolderPath" + i.ToString();
                        string val = listBox_recFolder.Items[i].ToString();
                        IniFileHandler.WritePrivateProfileString("SET", key, val, SettingPath.CommonIniPath);
                    }
                }

                // tabItem2
                if (listBox_bon.IsEnabled)
                {
                    for (int i = 0; i < listBox_bon.Items.Count; i++)
                    {
                        TunerInfo info = listBox_bon.Items[i] as TunerInfo;

                        IniFileHandler.WritePrivateProfileString(info.BonDriver, "Count", info.TunerNum, SettingPath.TimerSrvIniPath);
                        if (info.IsEpgCap == true)
                        {
                            IniFileHandler.WritePrivateProfileString(info.BonDriver, "GetEpg", "1", SettingPath.TimerSrvIniPath);
                        }
                        else
                        {
                            IniFileHandler.WritePrivateProfileString(info.BonDriver, "GetEpg", "0", SettingPath.TimerSrvIniPath);
                        }
                        IniFileHandler.WritePrivateProfileString(info.BonDriver, "EPGCount", info.EPGNum, SettingPath.TimerSrvIniPath);
                        IniFileHandler.WritePrivateProfileString(info.BonDriver, "Priority", i.ToString(), SettingPath.TimerSrvIniPath);
                    }
                }

                // tabItem3
                if (checkBox_bs.IsEnabled)
                {
                    if (checkBox_bs.IsChecked == true)
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "BSBasicOnly", "1", SettingPath.CommonIniPath);
                    }
                    else
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "BSBasicOnly", "0", SettingPath.CommonIniPath);
                    }
                }
                if (checkBox_cs1.IsEnabled)
                {
                    if (checkBox_cs1.IsChecked == true)
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "CS1BasicOnly", "1", SettingPath.CommonIniPath);
                    }
                    else
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "CS1BasicOnly", "0", SettingPath.CommonIniPath);
                    }
                }
                if (checkBox_cs2.IsEnabled)
                {
                    if (checkBox_cs2.IsChecked == true)
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "CS2BasicOnly", "1", SettingPath.CommonIniPath);
                    }
                    else
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "CS2BasicOnly", "0", SettingPath.CommonIniPath);
                    }
                }
                if (checkBox_cs3.IsEnabled)
                {
                    if (checkBox_cs3.IsChecked == true)
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "CS3BasicOnly", "1", SettingPath.CommonIniPath);
                    }
                    else
                    {
                        IniFileHandler.WritePrivateProfileString("SET", "CS3BasicOnly", "0", SettingPath.CommonIniPath);
                    }
                }

                foreach (ServiceViewItem info in serviceList)
                {
                    UInt64 key = info.ServiceInfo.Key;
                    try
                    {
                        if (info.IsSelected == true)
                        {
                            ChSet5.ChList[key].EpgCapFlag = 1;
                        }
                        else
                        {
                            ChSet5.ChList[key].EpgCapFlag = 0;
                        }
                    }
                    catch
                    {
                    }
                }

                if (ListView_time.IsEnabled)
                {
                    IniFileHandler.WritePrivateProfileString("EPG_CAP", "Count", timeList.Count.ToString(), SettingPath.TimerSrvIniPath);
                    for (int i = 0; i < timeList.Count; i++)
                    {
                        EpgCaptime item = timeList[i] as EpgCaptime;
                        IniFileHandler.WritePrivateProfileString("EPG_CAP", i.ToString(), item.Time, SettingPath.TimerSrvIniPath);
                        if (item.IsSelected == true)
                        {
                            IniFileHandler.WritePrivateProfileString("EPG_CAP", i.ToString() + "Select", "1", SettingPath.TimerSrvIniPath);
                        }
                        else
                        {
                            IniFileHandler.WritePrivateProfileString("EPG_CAP", i.ToString() + "Select", "0", SettingPath.TimerSrvIniPath);
                        }
                        int flags = (item.BSBasicOnly ? 1 : 0) | (item.CS1BasicOnly ? 2 : 0) | (item.CS2BasicOnly ? 4 : 0) | (item.CS3BasicOnly ? 8 : 0);
                        IniFileHandler.WritePrivateProfileString("EPG_CAP", i.ToString() + "BasicOnlyFlags", flags.ToString(), SettingPath.TimerSrvIniPath);
                    }
                }

                if (textBox_ngCapMin.IsEnabled)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "NGEpgCapTime", textBox_ngCapMin.Text, SettingPath.TimerSrvIniPath);
                }
                if (textBox_ngTunerMin.IsEnabled)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "NGEpgCapTunerTime", textBox_ngTunerMin.Text, SettingPath.TimerSrvIniPath);
                }

                // tabItem4
                SaveSetting_tabItem4();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void SaveSetting_tabItem4()
        {
            // tabItem4
            if (checkBox_httpServer.IsEnabled)
            {
                if (checkBox_httpServer.IsChecked == false)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "EnableHttpSrv", "0", SettingPath.TimerSrvIniPath);
                }
                else if (checkBox_httpLog.IsChecked == false)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "EnableHttpSrv", "1", SettingPath.TimerSrvIniPath);
                }
                else
                {
                    IniFileHandler.WritePrivateProfileString("SET", "EnableHttpSrv", "2", SettingPath.TimerSrvIniPath);
                }
            }

            if (textBox_httpPort.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpPort", textBox_httpPort.Text, SettingPath.TimerSrvIniPath);
            }

            if (textBox_httpAcl.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpAccessControlList", textBox_httpAcl.Text, SettingPath.TimerSrvIniPath);
            }
            if (textBox_httpThreads.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpNumThreads", textBox_httpThreads.Text, SettingPath.TimerSrvIniPath).ToString();
            }
            if (textBox_httpTimeout.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpRequestTimeoutSec", textBox_httpTimeout.Text, SettingPath.TimerSrvIniPath).ToString();
            }
            if (textBox_httpSSLVersion.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpSslProtocolVersion", textBox_httpSSLVersion.Text, SettingPath.TimerSrvIniPath).ToString();
            }
            if (textBox_httpAuthDom.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpAuthenticationDomain", textBox_httpAuthDom.Text, SettingPath.TimerSrvIniPath);
            }

            if (textBox_docrootPath.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "HttpPublicFolder", textBox_docrootPath.Text, SettingPath.TimerSrvIniPath);
            }
            string httppublicIniPath = SettingPath.SettingFolderPath + "\\HttpPublic.ini";
            if (textBox_ffmpegPath.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "ffmpeg", textBox_ffmpegPath.Text, httppublicIniPath);
            }
            if (textBox_readexPath.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "readex", textBox_readexPath.Text, httppublicIniPath);
            }

            if (checkBox_dlnaServer.IsEnabled)
            {
                IniFileHandler.WritePrivateProfileString("SET", "EnableDMS", checkBox_dlnaServer.IsChecked == false ? "0" : "1", SettingPath.TimerSrvIniPath);
            }
        }

        private void button_setPath_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.GetFolderNameByDialog(textBox_setPath, "設定関係保存フォルダの選択");
        }
        private void button_exe_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.GetFileNameByDialog(textBox_exe, false, "", ".exe");
        }
        private void button_recInfoFolder_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.GetFolderNameByDialog(textBox_recInfoFolder, "録画情報保存フォルダの選択");
        }

        private void listBox_Button_Set()
        {
            //エスケープキャンセルだけは常に有効にする。
            var bxr = new BoxExchangeEdit.BoxExchangeEditor(null, this.listBox_recFolder, true);
            var bxb = new BoxExchangeEdit.BoxExchangeEditor(null, this.listBox_bon, true);
            var bxt = new BoxExchangeEdit.BoxExchangeEditor(null, this.ListView_time, true);

            listBox_recFolder.SelectionChanged += ViewUtil.ListBox_TextBoxSyncSelectionChanged(listBox_recFolder, textBox_recFolder);

            if (listBox_recFolder.IsEnabled == true)
            {
                //録画設定関係
                bxr.AllowDragDrop();
                bxr.AllowKeyAction();
                bxr.targetBoxAllowDoubleClick(bxr.TargetBox, (sender, e) => button_rec_open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                button_rec_up.Click += new RoutedEventHandler(bxr.button_Up_Click);
                button_rec_down.Click += new RoutedEventHandler(bxr.button_Down_Click);
                button_rec_del.Click += new RoutedEventHandler(bxr.button_Delete_Click);
                button_rec_add.Click += button_rec_add_Click;
            }
            if (listBox_bon.IsEnabled == true)
            {
                //チューナ関係関係
                bxb.AllowDragDrop();
                button_bon_up.Click += new RoutedEventHandler(bxb.button_Up_Click);
                button_bon_down.Click += new RoutedEventHandler(bxb.button_Down_Click);
            }
            if (ListView_time.IsEnabled == true)
            {
                //EPG取得関係
                bxt.TargetItemsSource = timeList;
                bxt.AllowDragDrop();
                bxt.AllowKeyAction();
                button_delTime.Click += new RoutedEventHandler(bxt.button_Delete_Click);

                new BoxExchangeEdit.BoxExchangeEditor(null, this.listView_service, true);
            }
        }

        private void button_rec_del_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 誤って削除ボタンを押したときにすぐに追加できるようにするため
                // 該当アイテムを追加するパスの候補に戻しておく。
                if (listBox_recFolder.SelectedItem != null)
                {
                    textBox_recFolder.Text = listBox_recFolder.SelectedItem.ToString();
                    listBox_recFolder.Items.RemoveAt(listBox_recFolder.SelectedIndex);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_rec_open_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.GetFolderNameByDialog(textBox_recFolder, "録画フォルダの選択");
        }

        private void button_shortCut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string shortcutPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), SettingPath.ModuleName + ".lnk");
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
                else
                {
                    CreateShortCut(shortcutPath, Assembly.GetEntryAssembly().Location, "");
                }
                button_shortCut.Content = ((string)button_shortCut.Content).Replace("を解除", "") + (File.Exists(shortcutPath) ? "を解除" : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

		private void button_rec_add_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (String.IsNullOrEmpty(textBox_recFolder.Text) == false)
				{
					foreach (var info in listBox_recFolder.Items)
					{
						if (String.Compare(textBox_recFolder.Text, info.ToString(), true) == 0)
						{
							MessageBox.Show("すでに追加されています");
							return;
						}
					}

					// 追加対象のフォルダーの空き容量をサーバーに問い合わせてみる
					// SendEnumRecFolders にフォルダー名を指定した場合、そのフォルダーの空き容量を返してくる
					var folders = new List<RecFolderInfo>();
					if (CommonManager.Instance.CtrlCmd.SendEnumRecFolders(textBox_recFolder.Text, ref folders) != ErrCode.CMD_SUCCESS)
					{
						if (CommonManager.Instance.NW.IsConnected == false)
						{
							if (System.IO.Directory.Exists(textBox_recFolder.Text))
							{
								//サーバーが問い合わせに対応していないようなので、フォルダー名だけ登録する
								folders.Add(new RecFolderInfo(textBox_recFolder.Text));
							}
							else
							{
								MessageBox.Show("フォルダーが存在するか確認してください。");
								return;
							}
						}
						else
						{
							MessageBox.Show("EpgTimerNW ではフォルダーの追加は出来ません。");
							return;
						}
					}
					if (folders.Count == 1)
					{
						listBox_recFolder.Items.Add(new UserCtrlView.BGBarListBoxItem(folders[0]));
					}
					else
					{
						// SendEnumRecFolders でフォルダーの空き容量が取得できなかった場合
						MessageBox.Show("サーバーからアクセスできるフォルダーか確認してください。");
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
			}
		}

        private void button_shortCutSrv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string shortcutPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "EpgTimerSrv.lnk");
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
                else
                {
                    CreateShortCut(shortcutPath, System.IO.Path.Combine(SettingPath.ModulePath, "EpgTimerSrv.exe"), "");
                }
                button_shortCutSrv.Content = ((string)button_shortCutSrv.Content).Replace("を解除", "") + (File.Exists(shortcutPath) ? "を解除" : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// ショートカットの作成
        /// </summary>
        /// <remarks>WSHを使用して、ショートカット(lnkファイル)を作成します。(遅延バインディング)</remarks>
        /// <param name="path">出力先のファイル名(*.lnk)</param>
        /// <param name="targetPath">対象のアセンブリ(*.exe)</param>
        /// <param name="description">説明</param>
        private void CreateShortCut(String path, String targetPath, String description)
        {
            //using System.Reflection;

            // WSHオブジェクトを作成し、CreateShortcutメソッドを実行する
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortCut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });

            Type shortcutType = shell.GetType();
            // TargetPathプロパティをセットする
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortCut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortCut, new object[] { System.IO.Path.GetDirectoryName(targetPath) });
            // Descriptionプロパティをセットする
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortCut, new object[] { description });
            // Saveメソッドを実行する
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortCut, null);
        }

        private void listBox_bon_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (listBox_bon.SelectedItem != null)
                {
                    TunerInfo info = listBox_bon.SelectedItem as TunerInfo;
                    textBox_bon_num.DataContext = info;
                    checkBox_bon_epg.DataContext = info;
                    textBox_bon_epgnum.DataContext = info;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_allChk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (ServiceViewItem info in this.serviceList)
                {
                    info.IsSelected = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_videoChk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (ServiceViewItem info in this.serviceList)
                {
                    info.IsSelected = (ChSet5.IsVideo(info.ServiceInfo.ServiceType) == true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_allClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (ServiceViewItem info in this.serviceList)
                {
                    info.IsSelected = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_addTime_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (comboBox_HH.SelectedItem != null && comboBox_MM.SelectedItem != null)
                {
                    UInt16 hh = (UInt16)comboBox_HH.SelectedItem;
                    UInt16 mm = (UInt16)comboBox_MM.SelectedItem;
                    String time = hh.ToString("D2") + ":" + mm.ToString("D2");
                    int wday = comboBox_wday.SelectedIndex;
                    if (1 <= wday && wday <= 7)
                    {
                        // 曜日指定接尾辞(w1=Mon,...,w7=Sun)
                        time += "w" + ((wday + 5) % 7 + 1);
                    }

                    foreach (EpgCaptime info in timeList)
                    {
                        if (String.Compare(info.Time, time, true) == 0)
                        {
                            MessageBox.Show("すでに登録済みです");
                            return;
                        }
                    }
                    EpgCaptime item = new EpgCaptime();
                    item.IsSelected = true;
                    item.Time = time;
                    item.BSBasicOnly = checkBox_bs.IsChecked == true;
                    item.CS1BasicOnly = checkBox_cs1.IsChecked == true;
                    item.CS2BasicOnly = checkBox_cs2.IsChecked == true;
                    item.CS3BasicOnly = checkBox_cs3.IsChecked == true;
                    timeList.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private bool CheckHttpFiles()
        {
            bool bRet = checkBox_httpServer.IsChecked == false || Directory.Exists(textBox_docrootPath.Text) && File.Exists(TimerSrvFolder + "\\lua52.dll");
            warn_http.IsEnabled = bRet == false;
            warn_http.Visibility = bRet ? Visibility.Collapsed : Visibility.Visible;
            return bRet;
        }

        private bool CheckHttpsFiles()
        {
            bool bFile = checkBox_httpServer.IsChecked == false || textBox_httpPort.Text.IndexOf("s") < 0;
            bool bPem = true;
            label_httpSSLVersion.IsEnabled = !bFile;
            if (!bFile)
            {
                bFile = File.Exists(TimerSrvFolder + "\\ssleay32.dll") && File.Exists(TimerSrvFolder + "\\libeay32.dll");
                bPem = File.Exists(TimerSrvFolder + "\\ssl_cert.pem");
            }
            warn_ssl.Visibility = bFile && bPem ? Visibility.Collapsed : Visibility.Visible;
            warn_ssldll.IsEnabled = bFile == false;
            warn_ssldll.Visibility = bFile? Visibility.Collapsed : Visibility.Visible;
            warn_sslcertpem.IsEnabled = bPem == false;
            warn_sslcertpem.Visibility = bPem ? Visibility.Collapsed : Visibility.Visible;
            return bFile && bPem;
        }

        private bool CheckDlnaFiles()
        {
            bool bRet = checkBox_httpServer.IsChecked == false || checkBox_dlnaServer.IsChecked == false;
            if (!bRet)
            {
                bRet = Directory.Exists(textBox_docrootPath.Text + "\\dlna");
            }
            warn_dlna.IsEnabled = bRet == false;
            warn_dlna.Visibility = bRet ? Visibility.Collapsed : Visibility.Visible;
            return bRet;
        }

        private void checkBox_httpServer_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckHttpFiles())
            {
                MessageBox.Show("lua52.dll ファイルが見つかりません。", "確認");
                checkBox_httpServer.IsChecked = false;
            }
            else
            {
                if (!CheckHttpsFiles())
                {
                    textBox_httpPort.Focus();
                }
                CheckDlnaFiles();
            }
        }

        private void textBox_httpPort_TextChanged(object sender, RoutedEventArgs e)
        {
            CheckHttpsFiles();
        }

        private void textBox_opensslPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            button_generatePem.IsEnabled = File.Exists(textBox_opensslPath.Text);
        }

        private void checkBox_httpAuth_Click(object sender, RoutedEventArgs e)
        {
            string glpasswdFile = TimerSrvFolder + "\\glpasswd";
            if (checkBox_httpAuth.IsChecked == false && File.Exists(glpasswdFile))
            {

                string msg = "認証を止めるにはすべてのユーザーを削除する必要があります。\r\nユーザーの確認をせずに削除しますか？ 削除すると戻せません。";
                if (MessageBox.Show(msg, "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    File.Delete(glpasswdFile);
                }
                else
                {
                    checkBox_httpAuth.IsChecked = true;
                }
            }
        }

        private void checkBox_dlnaServer_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckDlnaFiles())
            {
                MessageBox.Show("dlna フォルダが見つかりません。", "確認");
                checkBox_dlnaServer.IsChecked = false;
            }
        }

        private void button_opensslPath_Click(object sender, RoutedEventArgs e)
        {
            string path = CommonManager.GetFileNameByDialog(textBox_opensslPath.Text, "openssl.exe の場所", ".exe");
            if (path != null)
            {
                textBox_opensslPath.Text = path;

                // クローズ時 TextBox が IsEnabled=false の場合があるためここで保存しておく
                IniFileHandler.WritePrivateProfileString("SET", "OpensslExeFile", textBox_opensslPath.Text, SettingPath.TimerSrvIniPath);
            }
        }

        private void button_generatePem_Click(object sender, RoutedEventArgs e)
        {
            string cnfFile = "";
            string keyFile = "";
            string csrFile = "";
            string pemFile = TimerSrvFolder + "\\ssl_cert.pem";
            try
            {
                cnfFile = System.IO.Path.GetTempFileName();
                keyFile = System.IO.Path.GetTempFileName();
                csrFile = System.IO.Path.GetTempFileName();

                // openssl configuation file を用意する
                StreamWriter cnf = File.CreateText(cnfFile);
                cnf.WriteLine("[req]");
                cnf.WriteLine("distinguished_name=a");
                cnf.WriteLine("prompt=no");
                cnf.WriteLine("[a]");
                cnf.WriteLine("C=JP");
                cnf.WriteLine("CN=" + Environment.MachineName);
                cnf.Close();

                // openssl.exe を使って ssl_cert.pem を生成する
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo(textBox_opensslPath.Text);
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

                // 秘密鍵(private key)を生成する
                startInfo.Arguments = "genrsa -out " + keyFile + " 2048";
                System.Diagnostics.Process.Start(startInfo).WaitForExit();
                // 証明書署名要求(certificate signing request)を生成する
                startInfo.Arguments = "req -config " + cnfFile + " -utf8 -new -key " + keyFile + " -out " + csrFile;
                System.Diagnostics.Process.Start(startInfo).WaitForExit();
                // 自己署名証明書(self signed root certificate)を生成する
                startInfo.Arguments = "x509 -req -days 3650 -sha256 -in " + csrFile + " -signkey " + keyFile + " -out " + pemFile;
                System.Diagnostics.Process.Start(startInfo).WaitForExit();

                // 自己署名証明書に秘密鍵を追加する
                StreamReader key = File.OpenText(keyFile);
                StreamWriter pem = File.AppendText(pemFile);
                pem.Write(key.ReadToEnd());
                key.Close();
                pem.Close();
            }
            catch { }
            finally
            {
                File.Delete(cnfFile);
                File.Delete(keyFile);
                File.Delete(csrFile);
            }
            CheckHttpsFiles();
        }

        private void button_docrootPath_Click(object sender, RoutedEventArgs e)
        {
            string path = CommonManager.GetFolderNameByDialog(textBox_docrootPath.Text, "document rootフォルダの選択");
            if (path != null)
            {
                textBox_docrootPath.Text = path;
            }
        }

        private void button_ffmpegPath_Click(object sender, RoutedEventArgs e)
        {
            string path = CommonManager.GetFileNameByDialog(textBox_ffmpegPath.Text, "ffmpeg.exe の場所", ".exe");
            if (path != null)
            {
                textBox_ffmpegPath.Text = path;
            }
        }

        private void button_readexPath_Click(object sender, RoutedEventArgs e)
        {
            string path = CommonManager.GetFileNameByDialog(textBox_readexPath.Text, "readex.exe の場所", ".exe");
            if (path != null)
            {
                textBox_readexPath.Text = path;
            }
        }

        private void hyperLink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = sender as Hyperlink;
            if (link == null)
                return;

            string uri = null;
            if (link.NavigateUri != null)
            {
                uri = link.NavigateUri.AbsoluteUri;
            }
            if (string.IsNullOrEmpty(uri))
            {
                Run r = link.Inlines.FirstInline as Run;
                if (r != null)
                {
                    uri = r.Text;
                }
            }
            if (!string.IsNullOrEmpty(uri))
            {
                System.Diagnostics.Process.Start(uri);
            }
        }

        private void button_register_Click(object sender, RoutedEventArgs e)
        {
            RegisterWebUserWindow dlg = new RegisterWebUserWindow(textBox_httpAuthDom.Text);
            PresentationSource topWindow = PresentationSource.FromVisual(this);
            if (topWindow != null)
            {
                dlg.Owner = (Window)topWindow.RootVisual;
            }
            dlg.ShowDialog();
            checkBox_httpAuth.IsChecked = File.Exists(TimerSrvFolder + "\\glpasswd");
        }
    }

    //BonDriver一覧の表示・設定用クラス
    public class TunerInfo
    {
        public TunerInfo(string bon) { BonDriver = bon; }
        public String BonDriver { get; set; }
        public String TunerNum { get; set; }
        public String EPGNum { get; set; }
        public bool IsEpgCap { get; set; }
        public override string ToString() { return BonDriver; }
    }
}
