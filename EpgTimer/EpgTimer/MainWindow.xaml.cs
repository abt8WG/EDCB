using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Threading; //紅
using System.Runtime.InteropServices; //紅
using System.Security.AccessControl;

namespace EpgTimer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private Semaphore semaphore;

        private TaskTrayClass taskTray = null;
        private bool serviceMode = false;
        private Dictionary<string, Button> buttonList = new Dictionary<string, Button>();
        private CtrlCmdUtil cmd = CommonManager.Instance.CtrlCmd;

        private MenuBinds mBinds = new MenuBinds();

        private PipeServer pipeServer = null;
        private string pipeName = "\\\\.\\pipe\\EpgTimerGUI_Ctrl_BonPipe_" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
        private string pipeEventName = "Global\\EpgTimerGUI_Ctrl_BonConnect_" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString();

        private bool firstInstance = false;
        private bool closeFlag = false;
        private bool initExe = false;
        private bool? minimizedStarting = false;

        private DispatcherTimer chkTimer = null;

        private bool idleShowBalloon = false;

        private string appName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);

        private InfoWindowViewModel infoWindowViewModel = null;
        private InfoWindow infoWindow = null;

        private bool CheckCmdLineCompleted = false;

        public MainWindow()
        {
            Settings.LoadFromXmlFile();
            CommonManager.Instance.NWMode = Settings.Instance.NWMode;

            CommonManager.Instance.MM.ReloadWorkData();
            CommonManager.Instance.ReloadCustContentColorList();
            Settings.Instance.ReloadOtherOptions();

            CommonUtil.ApplyStyle(Settings.Instance.NoStyle == 0 ? Settings.Instance.StyleXamlPath : null);

            SemaphoreSecurity ss = new SemaphoreSecurity();
            ss.AddAccessRule(new SemaphoreAccessRule("Everyone", SemaphoreRights.FullControl, AccessControlType.Allow));
            semaphore = new Semaphore(int.MaxValue, int.MaxValue, "Global\\EpgTimer_Bon3", out firstInstance, ss);
            semaphore.WaitOne(0);
            if (!firstInstance && Settings.Instance.ApplyMultiInstance == false)
            {
                ConnectSrv();
                DisconnectServer();

                semaphore.Release();
                semaphore.Close();
                semaphore = null;

                CloseCmd();
                return;
            }

            InitializeComponent();

#if DEBUG
            appName += "(debug)";
#endif

            initExe = true;

            try
            {
                infoWindowViewModel = new InfoWindowViewModel();

                // 多重起動時は最小化しない
                if (firstInstance && Settings.Instance.WakeMin == true)
                {
                    // Icon化起動すると Windows_Loaded イベントが来ないので
                    // InitializeComponent 後に ConnectCmd しておく。
                    ConnectCmd(Settings.Instance.NWMode && Settings.Instance.WakeReconnectNW == false);

                    if (Settings.Instance.ShowTray && Settings.Instance.MinHide)
                    {
                        this.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            this.WindowState = System.Windows.WindowState.Minimized;
                            minimizedStarting = true;
                        }));
                    }
                }

                //ウインドウ位置の復元
                if (Settings.Instance.MainWndTop != -100)
                {
                    this.Top = Settings.Instance.MainWndTop;
                }
                if (Settings.Instance.MainWndLeft != -100)
                {
                    this.Left = Settings.Instance.MainWndLeft;
                }
                if (Settings.Instance.MainWndWidth != -100)
                {
                    this.Width = Settings.Instance.MainWndWidth;
                }
                if (Settings.Instance.MainWndHeight != -100)
                {
                    this.Height = Settings.Instance.MainWndHeight;
                }
                this.WindowState = Settings.Instance.LastWindowState;


                //上のボタン
                Action<string, Action> ButtonGen = (key, handler) =>
                {
                    Button btn = new Button();
                    btn.MinWidth = 75;
                    btn.Margin = new Thickness(2, 2, 2, 5);
                    //btn.Height = 23;
                    btn.Click += (sender, e) => handler();
                    btn.Content = key;
                    buttonList.Add(key, btn);
                };
                ButtonGen("設定", OpenSettingDialog);
                ButtonGen("再接続", OpenConnectDialog);
                ButtonGen("再接続(前回)", () => ConnectCmd());
                ButtonGen("検索", OpenSearchDialog);
                ButtonGen("スタンバイ", () => SuspendCmd(1));
                ButtonGen("休止", () => SuspendCmd(2));
                ButtonGen("終了", CloseCmd);
                ButtonGen("EPG取得", EpgCapCmd);
                ButtonGen("EPG再読み込み", EpgReloadCmd);
                ButtonGen("NetworkTV終了", NwTVEndCmd);
                ButtonGen("情報通知ログ", OpenNotifyLogDialog);
                ButtonGen("予約簡易表示", () => ShowInfoWindow());
                ButtonGen("カスタム１", () => CustumCmd(1));
                ButtonGen("カスタム２", () => CustumCmd(2));
                ButtonGen("カスタム３", () => CustumCmd(3));

                //検索ボタンは他と共通でショートカット割り振られているので、その部分はコマンド側で処理する。
                this.CommandBindings.Add(new CommandBinding(EpgCmds.Search, (sender, e) => CommonButtons_Click("検索")));
                mBinds.AddInputCommand(EpgCmds.Search);
                SetSearchButtonTooltip(buttonList["検索"]);
                RefreshButton();

                ResetButtonView();

                StatusbarReset();//ステータスバーリセット

                if(Settings.Instance.InfoWindowEnabled)
                {
                    ShowInfoWindow();
                }

                //タスクトレイの表示
                taskTray = new TaskTrayClass(this);
                if (CommonManager.Instance.NWMode == true && Settings.Instance.ChkSrvRegistTCP == true)
                {
                    taskTray.Icon = TaskIconSpec.TaskIconGray;
                }
                else
                {
                    taskTray.Icon = TaskIconSpec.TaskIconBlue;
                }
                taskTray.Visible = Settings.Instance.ShowTray;
                taskTray.ContextMenuClick += (sender, e) => CommonButtons_Click(sender as string);
                taskTray.Text = GetTaskTrayReserveInfoText();
                ResetTaskMenu();
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex);
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void CheckCmdLine()
        {
            if (CheckCmdLineCompleted == true || CommonManager.Instance.IsConnected == false)
                return;

            foreach (string arg in Environment.GetCommandLineArgs())
            {
                String ext = System.IO.Path.GetExtension(arg);
                if (string.Compare(ext, ".exe", true) == 0)
                {
                    //何もしない
                }
                else if (string.Compare(ext, ".eaa", true) == 0)
                {
                    //自動予約登録条件追加
                    EAAFileClass eaaFile = new EAAFileClass();
                    if (eaaFile.LoadEAAFile(arg) == true)
                    {
                        List<EpgAutoAddData> val = new List<EpgAutoAddData>();
                        val.Add(eaaFile.AddKey);
                        cmd.SendAddEpgAutoAdd(val);
                    }
                    else
                    {
                        MessageBox.Show("解析に失敗しました。");
                    }
                }
                else if (string.Compare(ext, ".tvpid", true) == 0 || string.Compare(ext, ".tvpio", true) == 0)
                {
                    //iEPG追加
                    IEPGFileClass iepgFile = new IEPGFileClass();
                    if (iepgFile.LoadTVPIDFile(arg) == true)
                    {
                        List<ReserveData> val = new List<ReserveData>();
                        val.Add(iepgFile.AddInfo);
                        cmd.SendAddReserve(val);
                    }
                    else
                    {
                        MessageBox.Show("解析に失敗しました。デジタル用Version 2のiEPGの必要があります。");
                    }
                }
                else if (string.Compare(ext, ".tvpi", true) == 0)
                {
                    //iEPG追加
                    IEPGFileClass iepgFile = new IEPGFileClass();
                    if (iepgFile.LoadTVPIFile(arg) == true)
                    {
                        List<ReserveData> val = new List<ReserveData>();
                        val.Add(iepgFile.AddInfo);
                        cmd.SendAddReserve(val);
                    }
                    else
                    {
                        MessageBox.Show("解析に失敗しました。放送局名がサービスに関連づけされていない可能性があります。");
                    }
                }
            }
            CheckCmdLineCompleted = true;
        }

        private void ResetTaskMenu()
        {
            taskTray.SetContextMenu(Settings.Instance.TaskMenuList
                .Select(s1 => s1.Replace("（セパレータ）", ""))
                .Where(s2 => s2 == "" || buttonList.ContainsKey(s2) == true)
                .Select(id => new Tuple<string, string>(id, id == "" ? "" : buttonList[id].Content as string)));
        }

        const string specific = "PushLike";
        private void ResetButtonView()
        {
            //カスタムボタンの更新
            buttonList["カスタム１"].Content = Settings.Instance.Cust1BtnName;
            buttonList["カスタム２"].Content = Settings.Instance.Cust2BtnName;
            buttonList["カスタム３"].Content = Settings.Instance.Cust3BtnName;

            var delTabs = tabControl_main.Items.OfType<TabItem>().Where(ti => (string)ti.Tag == specific).ToList();
            delTabs.ForEach(ti => tabControl_main.Items.Remove(ti));
            stackPanel_button.Children.Clear();

            if (Settings.Instance.ViewButtonShowAsTab == true)
            {
                Settings.Instance.ViewButtonList.ForEach(id => TabButtonAdd(id));
            }
            else
            {
                foreach (string info in Settings.Instance.ViewButtonList)
                {
                    if (String.Compare(info, "（空白）") == 0)
                    {
                        stackPanel_button.Children.Add(new Label { Width = 15 });
                    }
                    else
                    {
                        Button btn;
                        if (buttonList.TryGetValue(info, out btn) == true)
                        {
                            stackPanel_button.Children.Add(btn);
                        }
                    }
                }
            }
            EmphasizeSearchButton(SearchWindow.HasHideSearchWindow);
        }

        TabItem TabButtonAdd(string id)
        {
            Button btn;
            if (buttonList.TryGetValue(id, out btn) == false) return null;

            //ボタン風のタブを追加する
            var ti = new TabItem();
            ti.Header = btn.Content;
            ti.ToolTip = btn.ToolTip;
            ti.Tag = specific;
            ti.Uid = id;
            ti.Background = null;
            ti.BorderBrush = null;

            //タブ移動をキャンセルしつつ擬似的に対応するボタンを押す
            ti.PreviewMouseDown += (sender, e) => e.Handled = true;
            ti.MouseLeftButtonUp += (sender, e) => CommonButtons_Click(((TabItem)sender).Uid);

            //検索ボタン用のツールチップ設定。
            if (id == "検索") SetSearchButtonTooltip(ti);

            tabControl_main.Items.Add(ti);
            return ti;
        }
        void SetSearchButtonTooltip(FrameworkElement fe)
        {
            fe.ToolTip = "";
            fe.ToolTipOpening += (sender, e) =>
            {
                var keytip = MenuBinds.GetInputGestureText(EpgCmds.Search);
                var addtip = SearchWindow.HasHideSearchWindow == false ? "" : "最後に番組表などへジャンプした検索/キーワードダイアログを復帰します。";
                fe.ToolTip = ((string.IsNullOrEmpty(keytip) == true ? "" : keytip + "\r\n") + addtip).TrimEnd();
            };
        }

        void CommonButtons_Click(string tag)
        {
            Button btn;
            if (string.IsNullOrEmpty(tag) == true || buttonList.TryGetValue(tag, out btn) == false) return;
            btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        void DisconnectServer()
        {
            if (pipeServer != null)
            {
                cmd.SetConnectTimeOut(3000);
                cmd.SendUnRegistGUI((uint)System.Diagnostics.Process.GetCurrentProcess().Id);
                pipeServer.StopServer();
                pipeServer = null;
            }

            CommonManager.Instance.NW.DisconnectServer();

            CommonManager.Instance.NWMode = true;
            cmd.SetSendMode(CommonManager.Instance.NWMode);
            cmd.SetNWSetting("", 0);
            CommonManager.Instance.IsConnected = false;
        }

        DispatcherTimer connectTimer = null;
        void OpenConnectDialog()
        {
            if (connectTimer != null) return;

            var dlg = new ConnectWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            if (dlg.ShowDialog() == true)
            {
                ConnectCmd(true);
            }
            else if (CommonManager.Instance.IsConnected == false)
            {
                InitializeClient();
            }
        }
        void ConnectCmd(bool showDialog = false)
        {
            var interval = TimeSpan.FromSeconds(Settings.Instance.WoLWaitSecond + 60);
            var CheckIsConnected = new Action(() =>
            {
                if (connectTimer != null)
                {
                    connectTimer.Stop();
                    connectTimer = null;
                }
                if (CommonManager.Instance.IsConnected == true)
                {
                    CommonManager.Instance.StatusNotifySet("EpgTimerSrvへ接続完了");
                }
                else
                {
                    if (showDialog == true)
                    {
                        MessageBox.Show("サーバーへの接続に失敗しました");
                    }
                    CommonManager.Instance.StatusNotifyAppend("接続に失敗 < ");
                }
            });

            if (Settings.Instance.WoLWait == true || Settings.Instance.WoLWaitRecconect == true)
            {
                try { NWConnect.SendMagicPacket(ConnectWindow.ConvertTextMacAddress(Settings.Instance.NWMacAdd)); }
                catch { }

                connectTimer = new DispatcherTimer();
                connectTimer.Interval = TimeSpan.FromSeconds(Math.Max(Settings.Instance.WoLWaitSecond, 1));
                connectTimer.Tick += (sender, e) =>
                {
                    CommonManager.Instance.StatusNotifyAppend("EpgTimerSrvへ接続中... < ", interval);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { ConnectSrv(); }
                        catch { }
                        CheckIsConnected();
                    }), DispatcherPriority.Background);
                };
                connectTimer.Start();
            }

            if (Settings.Instance.WoLWait != true)
            {
                CommonManager.Instance.StatusNotifySet("EpgTimerSrvへ接続中...", interval);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (Settings.Instance.WoLWait == true ||
                        ConnectSrv() == false && Settings.Instance.WoLWaitRecconect == true)
                    {
                        string msg1 = showDialog == true ? "" : "起動時自動";
                        string msg2 = Settings.Instance.WoLWaitRecconect == true ? "再" : "";
                        string msg = string.Format(msg1 + msg2 + "接続待機中({0}秒間)...", Settings.Instance.WoLWaitSecond);
                        CommonManager.Instance.StatusNotifySet(msg, interval);
                        return;
                    }
                }
                catch { }
                CheckIsConnected();
            }), DispatcherPriority.Background);
        }

        bool ConnectSrv()
        {
            IniSetting.Instance.Clear();

            serviceMode = ServiceCtrlClass.ServiceIsInstalled("EpgTimer Service");

            Title = appName;

            DisconnectServer();

            CommonManager.Instance.DB.ClearRecFileAppend(true);
            CommonManager.Instance.DB.SetNoAutoReloadEPG(Settings.Instance.NgAutoEpgLoadNW);
            if (initExe == true)
            {
                ChkTimerWork();
            }

            if (Settings.Instance.NWMode == false)
            {
                bool startExe = false;
                try
                {
                    if (System.Diagnostics.Process.GetProcessesByName("EpgTimerSrv").Count() == 0)
                    {
                        if (serviceMode == true)
                        {
                            if (ServiceCtrlClass.IsStarted("EpgTimer Service") == false)
                            {
                                ServiceCtrlClass.StartService("EpgTimer Service");
                                int count = 5;
                                do
                                {
                                    System.Threading.Thread.Sleep(1000);
                                }
                                while (ServiceCtrlClass.IsStarted("EpgTimer Service") == false && --count > 0);
                                if (count == 0)
                                {
                                    MessageBox.Show("サービスの開始に失敗しました。\r\nVista以降のOSでは、管理者権限で起動されている必要があります。");
                                }
                                else
                                {
                                    startExe = true;
                                }
                            }
                            else
                            {
                                startExe = true;
                            }
                        }
                        else
                        {
                            String moduleFolder = SettingPath.ModulePath.TrimEnd('\\');
                            String exePath = moduleFolder + "\\EpgTimerSrv.exe";
                            if (System.IO.File.Exists(exePath))
                            {
                                System.Diagnostics.Process process = System.Diagnostics.Process.Start(exePath);
                                startExe = true;
                            }
                            else //if (showDialog == true)
                            {
                                MessageBox.Show("EpgTimerSrv.exeの起動ができませんでした");
                            }
                        }
                    }
                    else
                    {
                        startExe = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                    serviceMode = false;
                    startExe = false;
                }

                if (startExe == true)
                {
                    CommonManager.Instance.DB.SetNoAutoReloadEPG(false);
                    CommonManager.Instance.NWMode = false;
                    cmd.SetSendMode(false);

                    pipeServer = new PipeServer();
                    pipeServer.StartServer(pipeEventName, pipeName, (c, r) => OutsideCmdCallback(c, r, false));

                    ErrCode err = ErrCode.CMD_SUCCESS;
                    for (int i = 0; i < 150 && (err = cmd.SendRegistGUI((uint)System.Diagnostics.Process.GetCurrentProcess().Id)) != ErrCode.CMD_SUCCESS; i++)
                    {
                        Thread.Sleep(100);
                    }
                    CommonManager.Instance.IsConnected = (err == ErrCode.CMD_SUCCESS);
                }
                else
                {
                    CommonManager.Instance.NWMode = true; // ローカル接続できないのでネットワークモードとして動作させる
                    cmd.SetSendMode(true);
                    CommonManager.Instance.IsConnected = false;
                }
            }
            else
            {
                try
                {
                    foreach (var address in System.Net.Dns.GetHostAddresses(Settings.Instance.NWServerIP))
                    {
                        if (address.IsIPv6LinkLocal == false &&
                            CommonManager.Instance.NW.ConnectServer(address, Settings.Instance.NWServerPort, Settings.Instance.NWWaitPort, (c, r) => OutsideCmdCallback(c, r, true)) == true)
                        {
                            CommonManager.Instance.IsConnected = CommonManager.Instance.NW.IsConnected;
                            break;
                        }
                    }
                }
                catch { }

                if (CommonManager.Instance.IsConnected == false)
                {
                    if (Settings.Instance.ChkSrvRegistTCP == true && taskTray != null)
                    {
                        taskTray.Icon = TaskIconSpec.TaskIconGray;
                    }
                    cmd.SetNWSetting("", 0);
                }
            }

            return InitializeClient();
        }

        private bool InitializeClient()
        {
            if (initExe == true)
            {
                IniFileHandler.UpdateSrvProfileIniNW();

                CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.ReserveInfo);
                CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.RecInfo);
                CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddEpgInfo);
                CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddManualInfo);
                CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.EpgData);
                CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.PlugInFile);
                CommonManager.Instance.DB.ReloadReserveInfo();
                CommonManager.Instance.DB.ClearRecFileAppend(true);
                CommonManager.Instance.DB.ReloadEpgAutoAddInfo();
                CommonManager.Instance.DB.ReloadManualAutoAddInfo();
                CommonManager.Instance.DB.ReloadEpgData();
                reserveView.UpdateInfo();
                infoWindowViewModel.UpdateInfo();
                tunerReserveView.UpdateInfo();
                autoAddView.UpdateInfo();
                recInfoView.UpdateInfo();
                epgView.UpdateInfo();
                SearchWindow.UpdatesInfo();
            }

            if (CommonManager.Instance.IsConnected)
            {
                ChSet5.Clear();

                if (CommonManager.Instance.NWMode)
                {
                    Title = appName + " - " + Settings.Instance.NWServerIP + ":" + Settings.Instance.NWServerPort.ToString();
                }
                else
                {
                    Title = appName + " - ローカル接続";
                }

                CheckCmdLine();
            }
            else
            {
                Title = appName + " - 未接続";
            }
            return CommonManager.Instance.IsConnected;
        }

        public void ChkTimerWork()
        {
            //オプション状態などが変っている場合もあるので、いったん破棄する。
            if (chkTimer != null)
            {
                chkTimer.Stop();
                chkTimer = null;
            }

            bool chkSrvRegistTCP = CommonManager.Instance.NWMode == true && Settings.Instance.ChkSrvRegistTCP == true;
            bool updateTaskText = Settings.Instance.UpdateTaskText == true;

            if (chkSrvRegistTCP == true || updateTaskText == true)
            {
                chkTimer = new DispatcherTimer();
                chkTimer.Interval = TimeSpan.FromMinutes(Math.Max(Settings.Instance.ChkSrvRegistInterval, 1));
                if (chkSrvRegistTCP == true)
                {
                    chkTimer.Tick += (sender, e) =>
                    {
                        if (CommonManager.Instance.NW.IsConnected == true)
                        {
                            var status = new NotifySrvInfo();
                            var waitPort = Settings.Instance.NWWaitPort;
                            bool registered = true;
                            if (waitPort == 0 && cmd.SendGetNotifySrvStatus(ref status) == ErrCode.CMD_SUCCESS ||
                                waitPort != 0 && cmd.SendIsRegistTCP(waitPort, ref registered)  == ErrCode.CMD_SUCCESS)
                            {
                                if (waitPort == 0 && CommonManager.Instance.NW.OnPolling == false ||
                                    waitPort != 0 && registered == false ||
                                    taskTray.Icon == TaskIconSpec.TaskIconGray)//EpgTimerNW側の休止復帰も含む
                                {
                                    if (ConnectSrv() == true)
                                    {
                                        CommonManager.Instance.StatusNotifyAppend("自動再接続 - ");
                                    }
                                    else
                                    {
                                        CommonManager.Instance.StatusNotifySet("自動再接続 - EpgTimerSrvへの再接続に失敗");
                                    }
                                }
                                return;
                            }
                        }
                        taskTray.Icon = TaskIconSpec.TaskIconGray;
                    };
                }
                if (updateTaskText == true)
                {
                    chkTimer.Tick += (sender, e) => taskTray.Text = GetTaskTrayReserveInfoText();
                }
                chkTimer.Start();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (CommonManager.Instance.IsConnected == false)
            {
                if ((Settings.Instance.NWMode == true && Settings.Instance.WakeReconnectNW == false) || ConnectSrv() == false)
                {
                    OpenConnectDialog();
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Settings.Instance.CloseMin == true && closeFlag == false)
            {
                e.Cancel = true;
                WindowState = System.Windows.WindowState.Minimized;
            }
            else
            {
                // 予約簡易表示ウィンドウを閉じる
                Settings.Instance.InfoWindowEnabled = (infoWindow != null);
                if (infoWindowViewModel != null)
                {
                    Settings.Instance.InfoWindowTopMost = infoWindowViewModel.IsTopMost;
                    Settings.Instance.InfoWindowBottomMost = infoWindowViewModel.IsBottomMost;
                    Settings.Instance.InfoWindowDisabledReserveItemVisible = infoWindowViewModel.IsDisabledReserveItemVisible;
                }
                if (infoWindow != null)
                {
                    infoWindow.TrueClose();
                }

                SearchWindow.CloseWindows();

                if (initExe == true)
                {
                    SaveData();
                }

                bool exitServer = CommonManager.Instance.NWMode == false;
                DisconnectServer();

                if (semaphore!= null)
                {
                    int n = semaphore.Release();
                    if (n == int.MaxValue - 1)
                    {
                        if (serviceMode == false && initExe == true)
                        {
                            if (exitServer == false && System.Diagnostics.Process.GetProcessesByName("EpgTimerSrv").Count() > 0)
                            {
                                IniSetting.Instance.Clear();
                                int residentMode = IniFileHandler.GetPrivateProfileInt("SET", "ResidentMode", 0, SettingPath.TimerSrvIniPath);
                                if (residentMode == 0 && MessageBox.Show("このコンピューター上で起動中の EpgTimerSrv.exe を終了させますか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                                {
                                    exitServer = true;
                                }
                            }
                            if (exitServer == true)
                            {
                                CtrlCmdUtil cmd = new CtrlCmdUtil();
                                cmd.SetSendMode(false);
                                cmd.SendClose();
                            }
                        }
                    }
                    semaphore.Close();
                }
                if (taskTray != null)
                {
                    taskTray.Dispose();
                }
            }
        }

        private void SaveData()
        {
            SaveViewData();
            Settings.SaveToXmlFile();
        }

        private void SaveViewData()
        {
            reserveView.SaveViewData();
            recInfoView.SaveViewData();
            autoAddView.SaveViewData();
            epgView.SaveViewData();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                if (this.Visibility == Visibility.Visible && this.Width > 0 && this.Height > 0)
                {
                    Settings.Instance.MainWndWidth = this.Width;
                    Settings.Instance.MainWndHeight = this.Height;
                }
            }
            if (this.WindowState == WindowState.Normal || this.WindowState == WindowState.Maximized)
            {
                SearchWindow.UpdatesParentStatus();
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                if (this.Visibility == Visibility.Visible && this.Top > 0 && this.Left > 0)
                {
                    Settings.Instance.MainWndTop = this.Top;
                    Settings.Instance.MainWndLeft = this.Left;
                }
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.minimizedStarting == null) return;
            if (this.WindowState == WindowState.Minimized)
            {
                if (Settings.Instance.ShowTray && Settings.Instance.MinHide)
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        // InfoWindow は消さない
                        if (win is InfoWindow)
                            continue;

                        win.Visibility = Visibility.Hidden;
                    }
                }
            }
            if (this.WindowState == WindowState.Normal || this.WindowState == WindowState.Maximized)
            {
                if (this.minimizedStarting == true)
                {
                    minimizedStarting = null;
                    if (Settings.Instance.LastWindowState == WindowState.Normal || Settings.Instance.LastWindowState == WindowState.Maximized)
                    {
                        this.WindowState = Settings.Instance.LastWindowState;
                    }
                    minimizedStarting = false;
                    if (CommonManager.Instance.NWMode == true && Settings.Instance.WakeReconnectNW == false && CommonManager.Instance.NW.IsConnected == false)
                    {
                        Dispatcher.BeginInvoke(new Action(() => OpenConnectDialog()), DispatcherPriority.Render);
                    }
                }
                foreach (Window win in Application.Current.Windows)
                {
                    // InfoWindow は触らない
                    if (win is InfoWindow)
                        continue;

                    win.Visibility = Visibility.Visible;
                }
                SearchWindow.UpdatesParentStatus();

                taskTray.LastViewState = this.WindowState;
                Settings.Instance.LastWindowState = this.WindowState;
            }
            taskTray.Visible = Settings.Instance.ShowTray;
        }

        private void Window_PreviewDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            string[] filePath = e.Data.GetData(DataFormats.FileDrop, true) as string[];
            foreach (string path in filePath)
            {
                String ext = System.IO.Path.GetExtension(path);
                if (string.Compare(ext, ".eaa", true) == 0)
                {
                    //自動予約登録条件追加
                    EAAFileClass eaaFile = new EAAFileClass();
                    if (eaaFile.LoadEAAFile(path) == true)
                    {
                        List<EpgAutoAddData> val = new List<EpgAutoAddData>();
                        val.Add(eaaFile.AddKey);
                        cmd.SendAddEpgAutoAdd(val);
                    }
                    else
                    {
                        MessageBox.Show("解析に失敗しました。");
                    }
                }
                else if (string.Compare(ext, ".tvpid", true) == 0 || string.Compare(ext, ".tvpio", true) == 0)
                {
                    //iEPG追加
                    IEPGFileClass iepgFile = new IEPGFileClass();
                    if (iepgFile.LoadTVPIDFile(path) == true)
                    {
                        List<ReserveData> val = new List<ReserveData>();
                        val.Add(iepgFile.AddInfo);
                        cmd.SendAddReserve(val);
                    }
                    else
                    {
                        MessageBox.Show("解析に失敗しました。デジタル用Version 2のiEPGの必要があります。");
                    }
                }
                else if (string.Compare(ext, ".tvpi", true) == 0)
                {
                    //iEPG追加
                    IEPGFileClass iepgFile = new IEPGFileClass();
                    if (iepgFile.LoadTVPIFile(path) == true)
                    {
                        List<ReserveData> val = new List<ReserveData>();
                        val.Add(iepgFile.AddInfo);
                        cmd.SendAddReserve(val);
                    }
                    else
                    {
                        MessageBox.Show("解析に失敗しました。放送局名がサービスに関連づけされていない可能性があります。");
                    }
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.D1:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_reserve.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D2:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_tunerReserve.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D3:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_recinfo.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D4:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_epgAutoAdd.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                    case Key.D5:
                        if (e.IsRepeat == false)
                        {
                            this.tabItem_epg.IsSelected = true;
                        }
                        e.Handled = true;
                        break;
                }
            }
        }

        void OpenSettingDialog()
        {
            SaveViewData();

            var setting = new SettingWindow();
            setting.Owner = CommonUtil.GetTopWindow(this);
            if (setting.ShowDialog() == true)
            {
                if (setting.ServiceStop == false)
                {
                    if (CommonManager.Instance.NWMode == true)
                    {
                        if (setting.setBasicView.IsChangeSettingPath == true)
                        {
                            IniFileHandler.UpdateSrvProfileIniNW();
                        }
                        CommonManager.Instance.DB.SetNoAutoReloadEPG(Settings.Instance.NgAutoEpgLoadNW);
                    }
                    else
                    {
                        cmd.SendReloadSetting();
                        cmd.SendNotifyProfileUpdate();
                    }

                    infoWindowViewModel.IsDisabledReserveItemVisible = Settings.Instance.InfoWindowDisabledReserveItemVisible;

                    StatusbarReset();//ステータスバーリセット
                    ChkTimerWork();//タイマーリセット

                    reserveView.UpdateInfo();
                    infoWindowViewModel.UpdateInfo();
                    tunerReserveView.UpdateInfo();
                    recInfoView.UpdateInfo();
                    autoAddView.UpdateInfo();
                    epgView.UpdateSetting();
                    SearchWindow.UpdatesInfo(false);
                    ResetTaskMenu();
                    taskTray.Text = GetTaskTrayReserveInfoText();
                    taskTray.Visible = Settings.Instance.ShowTray;
                    RefreshMenu(false);
                    ResetButtonView();

                    CommonManager.Instance.StatusNotifySet("設定変更に伴う画面再構築を実行");
                }
            }
            if (setting.ServiceStop == true)
            {
                DisconnectServer();
                OpenConnectDialog();
            }
        }

        public void RefreshMenu(bool MenuOnly = false)
        {
            CommonManager.Instance.MM.ReloadWorkData();
            reserveView.RefreshMenu();
            tunerReserveView.RefreshMenu();
            recInfoView.RefreshMenu();
            autoAddView.RefreshMenu();
            //epgViewでは設定全体の更新の際に、EPG再描画に合わせてメニューも更新されるため。
            if (MenuOnly == true)
            {
                epgView.RefreshMenu();
            }
            SearchWindow.RefreshMenu(this);

            RefreshButton();
        }
        public void RefreshButton()
        {
            //検索ボタン用。
            mBinds.ResetInputBindings(this);
        }
        public enum UpdateViewMode { ReserveInfo, ReserveInfoNoTuner, ReserveInfoNoAutoAdd }
        public void RefreshAllViewsReserveInfo(UpdateViewMode mode = UpdateViewMode.ReserveInfo)
        {
            reserveView.UpdateInfo();
            infoWindowViewModel.UpdateInfo();
            if (mode != UpdateViewMode.ReserveInfoNoTuner) tunerReserveView.UpdateInfo();
            if (mode != UpdateViewMode.ReserveInfoNoAutoAdd) autoAddView.UpdateInfo();
            epgView.UpdateReserveInfo();
            SearchWindow.UpdatesInfo(false);
        }
        /*
        public enum UpdateViewMode : uint
        {
            None =                  0x00000000,
            All =                   0x11134002,
          //SettingData =           0x11134001,//1回しか出てこないので未使用
          //Connected =             0x11132002,//1回しか出てこないので未使用
          //EpgData =               0x11012002,//1回しか出てこないので未使用
            ReserveInfo =           0x11031001,
            ReserveInfoNoTuner =    0x10031001,
            ReserveInfoNoAutoAdd =  0x11001001
        }
        public void RefreshAllViewsReserveInfo(UpdateViewMode mode = UpdateViewMode.ReserveInfo) { UpdateViews(mode); }
        public void UpdateViews(UpdateViewMode mode = UpdateViewMode.All)
        {
            if (((uint)mode & 0x10000000) != 0) reserveView.UpdateInfo();                       //ViewsResInfo
            if (((uint)mode & 0x01000000) != 0) tunerReserveView.UpdateInfo();                  //ViewsResInfo
            if (((uint)mode & 0x00100000) != 0) recInfoView.UpdateInfo();
            if (((uint)mode & 0x00010000) != 0) autoAddView.epgAutoAddView.UpdateInfo();        //ViewsResInfo
            if (((uint)mode & 0x00020000) != 0) autoAddView.manualAutoAddView.UpdateInfo();     //ViewsResInfo
            if (((uint)mode & 0x00001000) != 0) epgView.UpdateReserveData();                    //ViewsResInfo
            if (((uint)mode & 0x00002000) != 0) epgView.UpdateEpgData();
            if (((uint)mode & 0x00004000) != 0) epgView.UpdateSetting();
            if (((uint)mode & 0x00000001) != 0) SearchWindow.UpdatesInfo(true);                 //ViewsResInfo
            if (((uint)mode & 0x00000002) != 0) SearchWindow.UpdatesInfo();
        }
        */
        void StatusbarReset()
        {
            statusBar.ClearText();//一応
            statusBar.Visibility = Settings.Instance.DisplayStatus == true ? Visibility.Visible : Visibility.Collapsed;
        }

        void OpenSearchDialog()
        {
            // 最小化したSearchWindowを復帰
            if (SearchWindow.HasHideSearchWindow == true)
            {
                SearchWindow.RestoreHideSearchWindow();
            }
            else
            {
                CommonManager.Instance.MUtil.OpenSearchEpgDialog();
            }
        }

        public void RestoreMinimizedWindow()
        {
            this.Visibility = Visibility.Visible;
            this.WindowState = Settings.Instance.LastWindowState;
        }

        void CloseCmd()
        {
            closeFlag = true;
            Close();
        }

        void EpgCapCmd()
        {
            if (cmd.SendEpgCapNow() != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show("EPG取得を行える状態ではありません。\r\n（もうすぐ予約が始まる。EPGデータ読み込み中。など）");
            }
        }

        void EpgReloadCmd()
        {
            if (CommonManager.Instance.NWMode == true)
            {
                CommonManager.Instance.DB.SetOneTimeReloadEpg();
            }
            if (cmd.SendReloadEpg() != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show("EPG再読み込みを行える状態ではありません。\r\n（EPGデータ読み込み中。など）");
                return;
            }
            CommonManager.Instance.StatusNotifySet("EPG再読み込みを実行");
        }

        void SuspendCmd(byte suspendMode)
        {
            suspendMode = suspendMode == 1 ? suspendMode : (byte)2;
            ErrCode err = cmd.SendChkSuspend();
            if (err != ErrCode.CMD_SUCCESS)
            {
                if (err == ErrCode.CMD_ERR_CONNECT)
                {
                    MessageBox.Show("サーバーに接続できませんでした");
                }
                else
                {
                    MessageBox.Show((suspendMode == 1 ? "スタンバイ" : "休止") +
                        "に移行できる状態ではありません。\r\n（もうすぐ予約が始まる。または抑制条件のexeが起動している。など）");
                }
                return;
            }

            if (Settings.Instance.SuspendChk == 1)
            {
                SuspendCheckWindow dlg = new SuspendCheckWindow();
                dlg.SetMode(0, suspendMode);
                if (dlg.ShowDialog() == true)
                {
                    return;
                }
            }

            ushort cmdVal = suspendMode;
            if (IniFileHandler.GetPrivateProfileInt("SET", "Reboot", 0, SettingPath.TimerSrvIniPath) == 1)
            {
                cmdVal |= 0x0100;
            }
            if (CommonManager.Instance.NWMode == true)
            {
                cmdVal |= 0xFF00;//今はサーバ側の設定を読めてるので無くても大丈夫なはずだけど、一応そのまま

                if (Settings.Instance.SuspendCloseNW == true)
                {
                    if (CommonManager.Instance.NW.IsConnected == true)
                    {
                        if (cmd.SendUnRegistTCP(Settings.Instance.NWWaitPort) == ErrCode.CMD_ERR_CONNECT)
                        { }

                        cmd.SendSuspend(cmdVal);
                        CloseCmd();
                        return;
                    }
                }
            }
            SaveData();
            cmd.SendSuspend(cmdVal);
        }

        void CustumCmd(int id)
        {
            try
            {
                switch (id)
                {
                    case 1:
                        System.Diagnostics.Process.Start(Settings.Instance.Cust1BtnCmd, Settings.Instance.Cust1BtnCmdOpt);
                        break;
                    case 2:
                        System.Diagnostics.Process.Start(Settings.Instance.Cust2BtnCmd, Settings.Instance.Cust2BtnCmdOpt);
                        break;
                    case 3:
                        System.Diagnostics.Process.Start(Settings.Instance.Cust3BtnCmd, Settings.Instance.Cust3BtnCmdOpt);
                        break;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        void NwTVEndCmd()
        {
            CommonManager.Instance.TVTestCtrl.CloseTVTest();
        }

        void OpenNotifyLogDialog()
        {
            var dlg = new NotifyLogWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            dlg.ShowDialog();
        }

        private void OutsideCmdCallback(CMD_STREAM pCmdParam, CMD_STREAM pResParam, bool networkFlag)
        {
            System.Diagnostics.Trace.WriteLine((CtrlCmd)pCmdParam.uiParam);

            switch ((CtrlCmd)pCmdParam.uiParam)
            {
                case CtrlCmd.CMD_TIMER_GUI_SHOW_DLG:
                    if (networkFlag)
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_NON_SUPPORT;
                    }
                    else
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_SUCCESS;
                        this.Visibility = System.Windows.Visibility.Visible;
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_VIEW_EXECUTE:
                    if (networkFlag)
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_NON_SUPPORT;
                    }
                    else
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_SUCCESS;
                        String exeCmd = "";
                        (new CtrlCmdReader(new System.IO.MemoryStream(pCmdParam.bData, false))).Read(ref exeCmd);
                        try
                        {
                            string[] cmd = exeCmd.Split('\"');
                            System.Diagnostics.Process process;
                            System.Diagnostics.ProcessStartInfo startInfo;
                            if (cmd.Length >= 3)
                            {
                                startInfo = new System.Diagnostics.ProcessStartInfo(cmd[1], cmd[2]);
                            }
                            else if (cmd.Length >= 2)
                            {
                                startInfo = new System.Diagnostics.ProcessStartInfo(cmd[1]);
                            }
                            else
                            {
                                startInfo = new System.Diagnostics.ProcessStartInfo(cmd[0]);
                            }
                            if (cmd.Length >= 2)
                            {
                                if (cmd[1].IndexOf(".bat") >= 0)
                                {
                                    startInfo.CreateNoWindow = true;
                                    if (Settings.Instance.ExecBat == 0)
                                    {
                                        startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
                                    }
                                    else if (Settings.Instance.ExecBat == 1)
                                    {
                                        startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                                    }
                                }
                            }
                            process = System.Diagnostics.Process.Start(startInfo);
                            var w = new CtrlCmdWriter(new System.IO.MemoryStream());
                            w.Write(process.Id);
                            w.Stream.Close();
                            pResParam.bData = w.Stream.ToArray();
                            pResParam.uiSize = (uint)pResParam.bData.Length;
                        }
                        catch { }
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_QUERY_SUSPEND:
                    if (networkFlag)
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_NON_SUPPORT;
                    }
                    else
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_SUCCESS;

                        UInt16 param = 0;
                        (new CtrlCmdReader(new System.IO.MemoryStream(pCmdParam.bData, false))).Read(ref param);

                        Dispatcher.BeginInvoke(new Action(() => { if (closeFlag == false) ShowSleepDialog(param); }));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_QUERY_REBOOT:
                    if (networkFlag)
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_NON_SUPPORT;
                    }
                    else
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_SUCCESS;

                        UInt16 param = 0;
                        (new CtrlCmdReader(new System.IO.MemoryStream(pCmdParam.bData, false))).Read(ref param);

                        Byte reboot = (Byte)((param & 0xFF00) >> 8);
                        Byte suspendMode = (Byte)(param & 0x00FF);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (closeFlag == true) return;
                            SuspendCheckWindow dlg = new SuspendCheckWindow();
                            dlg.SetMode(reboot, suspendMode);
                            if (dlg.ShowDialog() != true)
                            {
                                SaveData();
                                cmd.SendReboot();
                            }
                        }));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_SRV_STATUS_NOTIFY2:
                    {
                        pResParam.uiParam = (uint)ErrCode.CMD_SUCCESS;

                        NotifySrvInfo status = new NotifySrvInfo();
                        var r = new CtrlCmdReader(new System.IO.MemoryStream(pCmdParam.bData, false));
                        ushort version = 0;
                        r.Read(ref version);
                        r.Version = version;
                        r.Read(ref status);
                        //通知の巡回カウンタをuiSizeを利用して返す(やや汚い)
                        pCmdParam.uiSize = status.param3;
                        Dispatcher.BeginInvoke(new Action(() => { if (closeFlag == false) NotifyStatus(status); }));
                    }
                    break;
                default:
                    pResParam.uiParam = (uint)ErrCode.CMD_NON_SUPPORT;
                    break;
            }
        }

        private TaskIconSpec GetTaskTrayIcon(uint status)
        {
            //statusは0,1,2しか取らないはずだが、コード上は任意になっているので、一応変換をかませておく。
            switch(status)
            {
                case 1: return TaskIconSpec.TaskIconRed;
                case 2: return TaskIconSpec.TaskIconGreen;
                default: return TaskIconSpec.TaskIconBlue;
            }
        }

        private string GetTaskTrayReserveInfoText()
        {
            if (Settings.Instance.ShowTray == false) return "";

            var sortList = CommonManager.Instance.DB.ReserveList.Values
                .Where(info => info.IsEnabled == true && info.EndTimeWithMargin() > DateTime.Now)
                .OrderBy(info => info.StartTimeWithMargin()).ToList();

            string infoText = Settings.Instance.UpdateTaskText == true && taskTray.Icon == TaskIconSpec.TaskIconGray ? "[未接続]\r\n(?)" : "";

            if (sortList.Count == 0) return infoText + "次の予約なし";

            int infoCount = 0;
            if (sortList[0].IsOnRec() == true)
            {
                infoText += "録画中:";
                infoCount = sortList.Count(info => info.IsOnRec()) - 1;
            }
            else if (Settings.Instance.UpdateTaskText == true && sortList[0].IsOnRec(60) == true) //1時間以内に開始されるもの
            {
                infoText += "まもなく録画:";
                infoCount = sortList.Count(info => info.IsOnRec(60)) - 1;
            }
            else
            {
                infoText += "次の予約:";
            }

            infoText += sortList[0].StationName + " " + new ReserveItem(sortList[0]).StartTimeShort + " " + sortList[0].Title;
            string endText = (infoCount == 0 ? "" : "\r\n他" + infoCount.ToString());

            if (infoText.Length + endText.Length > 63)
            {
                infoText = infoText.Substring(0, 60 - endText.Length) + "...";
            }
            return infoText + endText;
        }

        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("Kernel32.dll")]
        public static extern UInt32 GetTickCount();

        private void ShowSleepDialog(UInt16 param)
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
            GetLastInputInfo(ref info);

            // 現在時刻取得
            UInt64 dwNow = GetTickCount();

            // GetTickCount()は49.7日周期でリセットされるので桁上りさせる
            if (info.dwTime > dwNow)
            {
                dwNow += 0x100000000;
            }

            if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePC", 0, SettingPath.TimerSrvIniPath) == 1)
            {
                UInt32 ngUsePCTime = (UInt32)IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePCTime", 3, SettingPath.TimerSrvIniPath);
                UInt32 threshold = ngUsePCTime * 60 * 1000;

                if (ngUsePCTime == 0 || dwNow - info.dwTime < threshold)
                {
                    return;
                }
            }

            Byte suspendMode = (Byte)(param & 0x00FF);

            {
                SuspendCheckWindow dlg = new SuspendCheckWindow();
                dlg.SetMode(0, suspendMode);
                if (dlg.ShowDialog() != true)
                {
                    SaveData();
                    cmd.SendSuspend(param);
                }
            }
        }

        void NotifyStatus(NotifySrvInfo status)
        {
            int IdleTimeSec = 10 * 60;
            int TimeOutMSec = 10 * 1000;
            var TaskTrayBaloonWork = new Action<string, string>((title, tips) =>
            {
                if (CommonUtil.GetIdleTimeSec() < IdleTimeSec || idleShowBalloon == false)
                {
                    taskTray.ShowBalloonTip(title, tips, TimeOutMSec);
                    if (CommonUtil.GetIdleTimeSec() > IdleTimeSec)
                    {
                        idleShowBalloon = true;
                    }
                }
                CommonManager.Instance.NotifyLogList.Add(status);
                CommonManager.Instance.AddNotifySave(status);
            });

            System.Diagnostics.Trace.WriteLine((UpdateNotifyItem)status.notifyID);

            switch ((UpdateNotifyItem)status.notifyID)
            {
                case UpdateNotifyItem.SrvStatus:
                    taskTray.Icon = GetTaskTrayIcon(status.param1);
                    break;
                case UpdateNotifyItem.PreRecStart:
                    TaskTrayBaloonWork("予約録画開始準備", status.param4);
                    break;
                case UpdateNotifyItem.RecStart:
                    TaskTrayBaloonWork("録画開始", status.param4);
                    RefreshAllViewsReserveInfo();
                    break;
                case UpdateNotifyItem.RecEnd:
                    TaskTrayBaloonWork("録画終了", status.param4);
                    break;
                case UpdateNotifyItem.RecTuijyu:
                    TaskTrayBaloonWork("追従発生", status.param4);
                    break;
                case UpdateNotifyItem.ChgTuijyu:
                    TaskTrayBaloonWork("番組変更", status.param4);
                    break;
                case UpdateNotifyItem.PreEpgCapStart:
                    TaskTrayBaloonWork("EPG取得", status.param4);
                    break;
                case UpdateNotifyItem.EpgCapStart:
                    TaskTrayBaloonWork("取得", "開始");
                    break;
                case UpdateNotifyItem.EpgCapEnd:
                    TaskTrayBaloonWork("取得", "終了");
                    break;
                case UpdateNotifyItem.EpgData:
                    {
                        //NWでは重いが、使用している箇所多いので即取得する。
                        //自動取得falseのときはReloadEpgData()ではじかれているので元々読込まれない。
                        CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.EpgData);
                        CommonManager.Instance.DB.ReloadEpgData();
                        reserveView.UpdateInfo();//ジャンルや番組内容などが更新される
                        infoWindowViewModel.UpdateInfo();
                        if (Settings.Instance.DisplayReserveAutoAddMissing == true)
                        {
                            tunerReserveView.UpdateInfo();
                        }
                        autoAddView.epgAutoAddView.UpdateInfo();//検索数の更新
                        epgView.UpdateInfo();
                        SearchWindow.UpdatesInfo();

                        CommonManager.Instance.StatusNotifyAppend("EPGデータ更新 < ");
                        GC.Collect();
                    }
                    break;
                case UpdateNotifyItem.ReserveInfo:
                    {
                        //使用している箇所多いので即取得する。
                        //というより後ろでタスクトレイのルーチンが取得をかけるので遅延の効果がない。
                        CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.ReserveInfo);
                        CommonManager.Instance.DB.ReloadReserveInfo();
                        RefreshAllViewsReserveInfo();
                        CommonManager.Instance.StatusNotifyAppend("予約データ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.RecInfo:
                    {
                        CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.RecInfo);
                        if (CommonManager.Instance.NWMode == false)
                        {
                            CommonManager.Instance.DB.ReloadrecFileInfo();
                        }
                        recInfoView.UpdateInfo();
                        CommonManager.Instance.StatusNotifyAppend("録画済みデータ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.AutoAddEpgInfo:
                    {
                        //使用箇所多いので即取得する。
                        CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddEpgInfo);
                        CommonManager.Instance.DB.ReloadEpgAutoAddInfo();
                        autoAddView.epgAutoAddView.UpdateInfo();

                        if (Settings.Instance.DisplayReserveAutoAddMissing == true)
                        {
                            RefreshAllViewsReserveInfo(UpdateViewMode.ReserveInfoNoAutoAdd);
                        }
                        CommonManager.Instance.StatusNotifyAppend("キーワード予約データ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.AutoAddManualInfo:
                    {
                        //使用箇所多いので即取得する。
                        CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddManualInfo);
                        CommonManager.Instance.DB.ReloadManualAutoAddInfo();
                        autoAddView.manualAutoAddView.UpdateInfo();

                        if (Settings.Instance.DisplayReserveAutoAddMissing == true)
                        {
                            RefreshAllViewsReserveInfo(UpdateViewMode.ReserveInfoNoAutoAdd);
                        }
                        CommonManager.Instance.StatusNotifyAppend("プログラム予約登録データ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.IniFile:
                    {
                        IniSetting.Instance.Clear();
                        if (CommonManager.Instance.NWMode == true)
                        {
                            IniFileHandler.UpdateSrvProfileIniNW();
                            RefreshAllViewsReserveInfo();
                            CommonManager.Instance.StatusNotifyAppend("設定ファイル転送 < ");
                        }
                    }
                    break;
                default:
                    break;
            }

            if (CommonUtil.GetIdleTimeSec() < IdleTimeSec)
            {
                idleShowBalloon = false;
            }

            taskTray.Text = GetTaskTrayReserveInfoText();
        }

        void RefreshReserveInfo()
        {
            try
            {
                new BlackoutWindow(this).showWindow("情報の強制更新");
                DBManager DB = CommonManager.Instance.DB;

                //誤って変更しないよう、一度Srv側のリストを読み直す
                DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddEpgInfo);
                if (DB.ReloadEpgAutoAddInfo() == ErrCode.CMD_SUCCESS)
                {
                    if (DB.EpgAutoAddList.Count != 0)
                    {
                        cmd.SendChgEpgAutoAdd(DB.EpgAutoAddList.Values.ToList());
                    }
                }
                //追加データもクリアしておく。
                DB.ClearEpgAutoAddDataAppend();

                //EPG自動登録とは独立
                DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddManualInfo);
                if (DB.ReloadManualAutoAddInfo() == ErrCode.CMD_SUCCESS)
                {
                    if (DB.ManualAutoAddList.Count != 0)
                    {
                        cmd.SendChgManualAdd(DB.ManualAutoAddList.Values.ToList());
                    }
                }

                //上の二つが空リストでなくても、予約情報の更新がされない場合もある
                DB.SetUpdateNotify((UInt32)UpdateNotifyItem.ReserveInfo);
                if (DB.ReloadReserveInfo() == ErrCode.CMD_SUCCESS)
                {
                    if (DB.ReserveList.Count != 0)
                    {
                        //予約一覧は一つでも更新をかければ、再構築される。
                        cmd.SendChgReserve(new List<ReserveData> { DB.ReserveList.Values.ToList()[0] });
                    }
                    else
                    {
                        //更新しない場合でも、再描画だけはかけておく
                        RefreshAllViewsReserveInfo();
                    }
                }
                CommonManager.Instance.StatusNotifySet("情報の強制更新を実行(F5)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }

        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.F5:
                        RefreshReserveInfo();
                        break;
                }
            }
            base.OnKeyDown(e);
        }

        public void moveTo_tabItem(CtxmCode code)
        {
            TabItem tab;
            switch (code)
            {
                case CtxmCode.ReserveView:
                    tab = this.tabItem_reserve;
                    break;
                case CtxmCode.TunerReserveView:
                    tab = this.tabItem_tunerReserve;
                    break;
                default://CtxmCode.EpgView
                    tab = this.tabItem_epg;
                    break;
            }
            BlackoutWindow.NowJumpTable = true;
            new BlackoutWindow(this).showWindow(tab.Header.ToString());
            this.Focus();//チューナ画面やEPG画面でのフォーカス対策。とりあえずこれで解決する。
            tab.IsSelected = false;//必ずOnVisibleChanged()を発生させるため。
            tab.IsSelected = true;
        }

        public void EmphasizeSearchButton(bool emphasize)
        {
            Button button1 = buttonList["検索"];

            //検索ボタンを点滅させる
            if (emphasize && Settings.Instance.ViewButtonShowAsTab == false)
            {
                if (stackPanel_button.Children.Contains(button1) == false)
                {
                    stackPanel_button.Children.Add(button1);
                }
                button1.Effect = new DropShadowEffect();
                var animation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.7,
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true
                };
                button1.BeginAnimation(Button.OpacityProperty, animation);
            }
            else
            {
                //ストックのボタンは削除されないので、一応このコードは毎回実行させることにする。
                button1.BeginAnimation(Button.OpacityProperty, null);
                button1.Opacity = 1;
                button1.Effect = null;
                if (Settings.Instance.ViewButtonList.Contains("検索") == false)
                {
                    stackPanel_button.Children.Remove(button1);
                }
            }

            //もしあればタブとして表示のタブも点滅させる
            if (Settings.Instance.ViewButtonShowAsTab == true)
            {
                var ti = tabControl_main.Items.OfType<TabItem>().FirstOrDefault(item => item.Uid == "検索");
                if (emphasize)
                {
                    if (ti == null) ti = TabButtonAdd("検索");
                    var animation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.1,
                        RepeatBehavior = RepeatBehavior.Forever,
                        AutoReverse = true
                    };
                    ti.BeginAnimation(TabItem.OpacityProperty, animation);
                }
                else if (ti != null)
                {
                    if (Settings.Instance.ViewButtonList.Contains("検索") == false)
                    {
                        tabControl_main.Items.Remove(ti);
                    }
                    else
                    {
                        ti.BeginAnimation(TabItem.OpacityProperty, null);
                        ti.Opacity = 1;
                    }
                }
            }
        }

        public void ListFoucsOnVisibleChanged()
        {
            if (this.reserveView.listView_reserve.IsVisible == true)
            {
                this.reserveView.listView_reserve.Focus();
            }
            else if (this.recInfoView.listView_recinfo.IsVisible == true)
            {
                this.recInfoView.listView_recinfo.Focus();
            }
            else if (this.autoAddView.epgAutoAddView.listView_key.IsVisible == true)
            {
                this.autoAddView.epgAutoAddView.listView_key.Focus();
            }
            else if (this.autoAddView.manualAutoAddView.listView_key.IsVisible == true)
            {
                this.autoAddView.manualAutoAddView.listView_key.Focus();
            }
        }

        private void ShowInfoWindow()
        {
            if (infoWindow == null)
            {
                infoWindowViewModel.UpdateInfo();
                infoWindow = new InfoWindow(infoWindowViewModel);
                infoWindow.IsVisibleChanged += InfoWindow_IsVisibleChanged;
                infoWindow.Closed += InfoWindow_Closed;
                infoWindow.Show();
            }
            else
            {
                infoWindow.WindowActivate();
            }
        }

        private void InfoWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            bool isVisible = (bool)e.NewValue;

            infoWindowViewModel.IsAutoRefreshEnabled = isVisible;
        }

        private void InfoWindow_Closed(object sender, EventArgs e)
        {
            infoWindow.IsVisibleChanged -= InfoWindow_IsVisibleChanged;
            infoWindow.Closed -= InfoWindow_Closed;
            infoWindow = null;
        }

        private void showInfoWindowButton_Click(object sender, RoutedEventArgs e)
        {
            ShowInfoWindow();
        }

    }
}
