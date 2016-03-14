using EpgTimer.Common;
using EpgTimer.DefineClass;
using EpgTimer.UserCtrlView;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Threading;

namespace EpgTimer
{
    /// <summary>
    /// 
    /// </summary>
    public partial class RecLogView
    {

        public enum searchMethods { LIKE, Contrains, Freetext };
        public static readonly string notEnabledMessage = "RecLogが無効に設定されています。";

        BackgroundWorker _bgw_ReserveInfo = new BackgroundWorker();
        BackgroundWorker _bgw_RecInfo = new BackgroundWorker();
        BackgroundWorker _bgw_EpgData = new BackgroundWorker();
        List<RecLogItem> _recLogItemList = new List<RecLogItem>();
        readonly string _timestampFormat = "MM/dd HH:mm";
        RecLogItem _recLogItem_Edit;

        #region - Constructor -
        #endregion

        public RecLogView()
        {
            InitializeComponent();
            //
            listView_RecLog.DataContext = _recLogItemList;
            comboBox_Edit_Status.DataContext = new object[] {
                RecLogItem.RecodeStatuses.予約済み, RecLogItem.RecodeStatuses.録画完了, RecLogItem.RecodeStatuses.録画異常,  RecLogItem.RecodeStatuses.視聴済み,
                RecLogItem.RecodeStatuses.無効登録
            };
            //
            db_RecLog = new DB_RecLog(Settings.Instance.RecLog_DB_MachineName, Settings.Instance.RecLog_DB_InstanceName);
            checkBox_RecLogEnabled.IsChecked = Settings.Instance.RecLog_IsEnabled;
            if (!string.IsNullOrWhiteSpace(Settings.Instance.RecLog_DB_MachineName))
            {
                textBox_MachineName.Text = Settings.Instance.RecLog_DB_MachineName;
            }
            else
            {
                textBox_MachineName.Text = Environment.MachineName;
            }
            textBox_InstanceName.Text = Settings.Instance.RecLog_DB_InstanceName;
            searchMethod = Settings.Instance.RecLog_SearchMethod;
            searchColumn = (DB_RecLog.searchColumns)Settings.Instance.RecLog_SearchColumn;
            recodeStatus = (RecLogItem.RecodeStatuses)Settings.Instance.RecLog_RecodeStatus;
            searchResultLimit = Settings.Instance.RecLog_SearchResultLimit;
            textBox_ResultLimit_RecLogWindow.Text = Settings.Instance.RecLogWindow_SearchResultLimit.ToString();
            //
            _bgw_ReserveInfo.DoWork += _bgw_ReserveInfo_DoWork;
            _bgw_RecInfo.DoWork += _bgw_RecInfo_DoWork;
            _bgw_EpgData.DoWork += _bgw_EpgData_DoWork;
            //
            grid_Edit.Visibility = Visibility.Collapsed;
            border_Button_DB_ConnectTest.BorderThickness = new Thickness(0);
            if (Settings.Instance.RecLog_IsEnabled)
            {
                border_CheckBox_RecLogEnabled.BorderThickness = new Thickness(0);
                grid_Setting.Visibility = Visibility.Collapsed;
                toggleButton_Setting.IsChecked = false;
                border_ToggleButton_Setting.BorderThickness = new Thickness(0);
            }
            richTextBox_HowTo.Document =
                new FlowDocument(
                    new Paragraph(
                        new Run(this._howto)));
            clearEditor();
            isSearchOptionChanged = false;
        }

        #region - Method -
        #endregion

        void search(string searchWord0)
        {
            if (!Settings.Instance.RecLog_IsEnabled)
            {
                MessageBox.Show(notEnabledMessage);
                return;
            }
            //
            _recLogItemList.Clear();
            clearEditor();
            //
            if (searchColumn == DB_RecLog.searchColumns.NONE)
            {
                MessageBox.Show("エラー：検索対象を1つ以上選択してください。");
            }
            else if (recodeStatus == RecLogItem.RecodeStatuses.無し)
            {
                MessageBox.Show("エラー：録画ステータスを1つ以上選択してください。");
            }
            else
            {
                List<RecLogItem> recLogItemList1 = getRecLogList(searchWord0, searchResultLimit, recodeStatus, searchColumn);
                foreach (var item in recLogItemList1)
                {
                    _recLogItemList.Add(item);
                }
            }
            listView_RecLog.Items.Refresh();
        }

        public List<RecLogItem> getRecLogList(string searchWord0, int resultLimit0, RecLogItem.RecodeStatuses recodeStatuse0 = RecLogItem.RecodeStatuses.ALL,
            DB_RecLog.searchColumns searchColumns0 = DB_RecLog.searchColumns.title)
        {
            List<RecLogItem> recLogItemList1;
            switch (searchMethod)
            {
                case searchMethods.LIKE:
                    recLogItemList1 = db_RecLog.search_Like(searchWord0, recodeStatuse0, searchColumns0, resultLimit0);
                    break;
                case searchMethods.Contrains:
                    recLogItemList1 = db_RecLog.search_Fulltext(searchWord0, recodeStatuse0, searchColumns0, resultLimit0);
                    break;
                case searchMethods.Freetext:
                    recLogItemList1 = db_RecLog.search_Fulltext(searchWord0, recodeStatuse0, searchColumns0, resultLimit0, true);
                    break;
                default:
                    throw new NotSupportedException();
            }

            return recLogItemList1;
        }

        public void update(UpdateNotifyItem notifyItem0)
        {
            if (CommonManager.Instance.NWMode == true) { return; }

            switch (notifyItem0)
            {
                case UpdateNotifyItem.EpgData:
                    if (_bgw_EpgData.IsBusy)
                    {
                        System.Diagnostics.Trace.WriteLine("RecLogView._bgw_EpgData.IsBusy");
                    }
                    else
                    {
                        _bgw_EpgData.RunWorkerAsync();
                    }
                    break;
                case UpdateNotifyItem.ReserveInfo:
                    if (_bgw_ReserveInfo.IsBusy)
                    {
                        System.Diagnostics.Trace.WriteLine("RecLogView._bgw_ReserveInfo.IsBusy");
                    }
                    else
                    {
                        _bgw_ReserveInfo.RunWorkerAsync();
                    }
                    break;
                case UpdateNotifyItem.RecInfo:
                    if (_bgw_RecInfo.IsBusy)
                    {
                        System.Diagnostics.Trace.WriteLine("RecLogView._bgw_RecInfo.IsBusy");
                    }
                    else
                    {
                        _bgw_RecInfo.RunWorkerAsync();
                    }
                    break;
            }
        }

        EpgEventInfo getEpgEventInfo(ushort original_network_id0, ushort transport_stream_id0, ushort service_id0, ushort event_id0)
        {
            UInt64 key1 = CommonManager.Create64Key(original_network_id0, transport_stream_id0, service_id0);
            if (CommonManager.Instance.DB.ServiceEventList.ContainsKey(key1) == true)
            {
                foreach (EpgEventInfo epgEventInfo1 in CommonManager.Instance.DB.ServiceEventList[key1].eventList)
                {
                    if (epgEventInfo1.event_id == event_id0)
                    {
                        return epgEventInfo1;
                    }
                }
            }

            return null;
        }

        void delete(RecLogItem item0)
        {
            delete(new RecLogItem[] { item0 });
        }

        void delete(System.Collections.IList items0)
        {
            StringBuilder sb1 = new StringBuilder();
            sb1.AppendLine("削除しますか？");
            foreach (RecLogItem item in items0)
            {
                sb1.AppendLine("・" + item.tvProgramTitle);
            }
            if (MessageBox.Show(sb1.ToString(), "削除確認", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation, MessageBoxResult.Cancel) == MessageBoxResult.OK)
            {
                foreach (RecLogItem item in items0)
                {
                    db_RecLog.delete(item);
                    _recLogItemList.Remove(item);
                }
                listView_RecLog.Items.Refresh();
            }
        }

        void clearEditor()
        {
            _recLogItem_Edit = null;
            //
            textBox_Edit_ProgramTitle.Clear();
            label_Editor_Date.Content = null;
            label_Editor_ServiceName.Content = null;
            comboBox_Edit_Status.SelectedItem = null;
            checkBox_AllowOverWrite.IsChecked = false;
            textBox_RecFilePath.Clear();
            richTextBox_Comment.Document.Blocks.Clear();
            richTextBox_ShortInfo_text_char.Document.Blocks.Clear();
            richTextBox_ExtInfo_text_char.Document.Blocks.Clear();
            //
            textBox_Edit_ProgramTitle.BorderThickness = new Thickness(0);
            textBox_RecFilePath.BorderThickness = new Thickness(0);
            border_RecStatus.BorderThickness = new Thickness(0);
            border_AllowOverWrite.BorderThickness = new Thickness(0);
            richTextBox_Comment.BorderThickness = new Thickness(0);
            richTextBox_ShortInfo_text_char.BorderThickness = new Thickness(0);
            richTextBox_ExtInfo_text_char.BorderThickness = new Thickness(0);
        }

        void setRecLogItem2editor()
        {
            RecLogItem recLogItem_Edit1 = listView_RecLog.SelectedItem as RecLogItem;
            if (recLogItem_Edit1 == null) { return; }
            //
            clearEditor();
            grid_Edit.Visibility = Visibility.Visible;
            //
            textBox_Edit_ProgramTitle.Text = recLogItem_Edit1.epgEventInfoR.ShortInfo.event_name;
            label_Editor_Date.Content = CommonManager.ConvertTimeText(
                recLogItem_Edit1.epgEventInfoR.start_time, recLogItem_Edit1.epgEventInfoR.durationSec, false, false, true);
            label_Editor_ServiceName.Content = recLogItem_Edit1.tvStationName +
                " (" + CommonManager.ConvertNetworkNameText(recLogItem_Edit1.epgEventInfoR.original_network_id) + ")";
            comboBox_Edit_Status.SelectedItem = recLogItem_Edit1.recodeStatus;
            checkBox_AllowOverWrite.IsChecked = recLogItem_Edit1.epgAlllowOverWrite;
            textBox_RecFilePath.Text = recLogItem_Edit1.recFilePath;
            setText(richTextBox_Comment, recLogItem_Edit1.comment);
            setText(richTextBox_ShortInfo_text_char, recLogItem_Edit1.epgEventInfoR.ShortInfo.text_char);
            setText(richTextBox_ExtInfo_text_char, recLogItem_Edit1.epgEventInfoR.ExtInfo.text_char);
            //
            _recLogItem_Edit = recLogItem_Edit1;
        }

        string getText(RichTextBox richTextBox0)
        {
            return new TextRange(richTextBox0.Document.ContentStart, richTextBox0.Document.ContentEnd).Text;
        }

        void setText(RichTextBox richTextBox0, string text0)
        {
            richTextBox0.Document.Blocks.Add(
                new Paragraph(
                    new Run(text0)));
        }

        void addDBLog(string msg0)
        {
            listBox_DBLog.Dispatcher.BeginInvoke(
                 new Action(
                     () =>
                     {
                         listBox_DBLog.Items.Insert(0, msg0);
                         if (30 < listBox_DBLog.Items.Count)
                         {
                             listBox_DBLog.Items.RemoveAt(listBox_DBLog.Items.Count - 1);
                         }
                     }));
        }

        bool dbConnectTest()
        {
            addDBLog("DB接続テスト");
            bool isTestOnly1 = Settings.Instance.NWMode;
            DB.connectTestResults connectTestResult1 = db_RecLog.connectionTest(isTestOnly1);
            switch (connectTestResult1)
            {
                case DB.connectTestResults.success:
                    addDBLog("成功");
                    return true;
                case DB.connectTestResults.createDB:
                    if (isTestOnly1)
                    {
                        addDBLog("データベースEDCBが見つかりません");
                    }
                    else
                    {
                        addDBLog("データベースを新規作成");
                        System.Threading.Thread.Sleep(5000);    // データベース作成完了を待機
                        db_RecLog.createTable_RecLog_EpgEventInfo();
                    }
                    return true;
                case DB.connectTestResults.serverNotFound:
                    addDBLog("SQLServerが見つかりません");
                    return false;
                case DB.connectTestResults.unKnownError:
                    addDBLog("Unknown Error");
                    return false;
                default:
                    return false;
            }
        }

        void updateReserveInfo()
        {
            DateTime lastUpdate1 = DateTime.Now;
            //
            int reservedCount_New1 = 0;
            int reservedCount_Update1 = 0;
            foreach (ReserveData rd1 in CommonManager.Instance.DB.ReserveList.Values)
            {
                EpgEventInfo epgEventInfo1 = getEpgEventInfo(rd1.OriginalNetworkID, rd1.TransportStreamID, rd1.ServiceID, rd1.EventID);
                if (epgEventInfo1 == null)
                {
                    //addMessage("*** EPGデータが見つからない?");
                    continue;
                    //epgEventInfo1 = new EpgEventInfo();
                }
                EpgEventInfoR epgEventInfoR1 = new EpgEventInfoR(epgEventInfo1, lastUpdate1);
                RecLogItem recLogItem1 = db_RecLog.exists(rd1);
                if (recLogItem1 == null)
                {
                    // 新規登録
                    reservedCount_New1++;
                    RecLogItem recLogItem2 = new RecLogItem()
                    {
                        lastUpdate = lastUpdate1,
                        epgEventInfoR = epgEventInfoR1
                    };
                    if (rd1.RecSetting.RecMode == 0x05)   // 録画モード：無効
                    {
                        recLogItem2.recodeStatus = RecLogItem.RecodeStatuses.無効登録;
                    }
                    else
                    {
                        recLogItem2.recodeStatus = RecLogItem.RecodeStatuses.予約済み;
                    }
                    db_RecLog.insert(recLogItem2);
                }
                else
                {
                    //更新
                    reservedCount_Update1++;
                    recLogItem1.lastUpdate = lastUpdate1;
                    db_RecLog.update(recLogItem1, false);
                }
            }
            //
            // 予約削除
            //
            List<RecLogItem> list_NotUpdated1 = db_RecLog.select_Reserved_NotUpdated(lastUpdate1);
            List<RecLogItem> list_Deleted1 = new List<RecLogItem>();
            List<RecLogItem> list_RecstatusUpdateErr1 = new List<RecLogItem>();
            foreach (var item1 in list_NotUpdated1)
            {
                if (item1.epgEventInfoR != null && lastUpdate1 < item1.epgEventInfoR.start_time)
                {   // 未来に放送
                    list_Deleted1.Add(item1);
                }
                else
                {
                    // 録画完了？
                    list_RecstatusUpdateErr1.Add(item1);
                }
            }
            //
            if (0 < list_RecstatusUpdateErr1.Count)
            {
                addDBLog("ステータス更新失敗：" + list_RecstatusUpdateErr1.Count);
                foreach (var item1 in list_RecstatusUpdateErr1)
                {
                    item1.recodeStatus = RecLogItem.RecodeStatuses.録画完了;
                    db_RecLog.update(item1);
                }
            }
            //
            int deleted1 = db_RecLog.delete(list_Deleted1.ToArray());
            //
            addDBLog("予約更新(+" + reservedCount_New1 + ",-" + deleted1 + ") " + lastUpdate1.ToString(_timestampFormat));
        }

        void hideEditor()
        {
            clearEditor();
            grid_Edit.Visibility = Visibility.Collapsed;
        }

        #region - Property -
        #endregion

        public DB_RecLog db_RecLog { get; private set; }

        DB_RecLog.searchColumns searchColumn
        {
            get { return _searchColumn; }
            set
            {
                _searchColumn = value;
                checkBox_Search_Title.IsChecked = value.HasFlag(DB_RecLog.searchColumns.title);
                checkBox_Search_Content.IsChecked = value.HasFlag(DB_RecLog.searchColumns.content);
                checkBox_Search_Comment.IsChecked = value.HasFlag(DB_RecLog.searchColumns.comment);
                checkBox_Search_RecFileName.IsChecked = value.HasFlag(DB_RecLog.searchColumns.recFilePath);
            }
        }
        DB_RecLog.searchColumns _searchColumn = DB_RecLog.searchColumns.NONE;

        RecLogItem.RecodeStatuses recodeStatus
        {
            get { return _recodeStatus; }
            set
            {
                _recodeStatus = value;
                checkBox_RecStatus_Reserved.IsChecked = value.HasFlag(RecLogItem.RecodeStatuses.予約済み);
                checkBox_RecStatus_Recoded.IsChecked = value.HasFlag(RecLogItem.RecodeStatuses.録画完了);
                checkBox_RecStatus_Recoded_Abnormal.IsChecked = value.HasFlag(RecLogItem.RecodeStatuses.録画異常);
                checkBox_RecStatus_Viewed.IsChecked = value.HasFlag(RecLogItem.RecodeStatuses.視聴済み);
                checkBox_RecStatus_Reserved_Null.IsChecked = value.HasFlag(RecLogItem.RecodeStatuses.無効登録);
            }
        }
        RecLogItem.RecodeStatuses _recodeStatus = RecLogItem.RecodeStatuses.無し;

        bool isSearchOptionChanged
        {
            get { return _isSearchOptionChanged; }
            set
            {
                _isSearchOptionChanged = value;
                if (value)
                {
                    border_Button_SaveSearchOption.Visibility = Visibility.Visible;
                }
                else
                {
                    border_Button_SaveSearchOption.Visibility = Visibility.Collapsed;
                }
            }
        }
        bool _isSearchOptionChanged = false;

        int searchResultLimit
        {
            get
            {
                int searchResultLimit1;
                if (int.TryParse(textBox_ResultLimit.Text, out searchResultLimit1))
                {
                    return searchResultLimit1;
                }
                else
                {
                    return Settings.Instance.RecLog_SearchResultLimit;
                }
            }
            set { textBox_ResultLimit.Text = value.ToString(); }
        }

        searchMethods searchMethod
        {
            get
            {
                if (radioButton_SearchMethod_Like.IsChecked == true)
                {
                    return searchMethods.LIKE;
                }
                else if (radioButton_SearchMethod_Contains.IsChecked == true)
                {
                    return searchMethods.Contrains;
                }
                else if (radioButton_SearchMethod_Freetext.IsChecked == true)
                {
                    return searchMethods.Freetext;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            set
            {
                switch (value)
                {
                    case searchMethods.LIKE:
                        radioButton_SearchMethod_Like.IsChecked = true;
                        break;
                    case searchMethods.Contrains:
                        radioButton_SearchMethod_Contains.IsChecked = true;
                        break;
                    case searchMethods.Freetext:
                        radioButton_SearchMethod_Freetext.IsChecked = true;
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        #region - Event Handler -
        #endregion

        /// <summary>
        /// EPGデータ更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _bgw_EpgData_DoWork(object sender, DoWorkEventArgs e)
        {
            DateTime lastUpdate1 = DateTime.Now;
            //
            int epgUpdatedCount1 = 0;
            foreach (RecLogItem rli1 in db_RecLog.select_Reserved())
            {
                if (rli1.epgEventInfoR != null)
                {
                    EpgEventInfo epgEventInfo1 = getEpgEventInfo(
                        rli1.epgEventInfoR.original_network_id, rli1.epgEventInfoR.transport_stream_id, rli1.epgEventInfoR.service_id, rli1.epgEventInfoR.event_id);
                    EpgEventInfoR epgEventInfoR1 = new EpgEventInfoR(epgEventInfo1, lastUpdate1);
                    epgUpdatedCount1++;
                    epgEventInfoR1.ID = rli1.epgEventInfoID;
                    epgEventInfoR1.lastUpdate = lastUpdate1;
                    db_RecLog.updateEpg(epgEventInfoR1);
                }
            }
            //
            addDBLog("EPG更新(" + epgUpdatedCount1 + ") " + lastUpdate1.ToString(_timestampFormat));
        }

        /// <summary>
        /// 録画済更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _bgw_RecInfo_DoWork(object sender, DoWorkEventArgs e)
        {
            DateTime lastUpdate1 = DateTime.Now;
            //
            CommonManager.Instance.DB.ReloadrecFileInfo();
            int recordedCount_New1 = 0;
            foreach (RecFileInfo rfi1 in CommonManager.Instance.DB.RecFileInfo.Values)
            {
                RecLogItem recLogItem1 = db_RecLog.exists(RecLogItem.RecodeStatuses.予約済み, rfi1.OriginalNetworkID, rfi1.TransportStreamID, rfi1.ServiceID, rfi1.EventID, rfi1.StartTime);
                if (recLogItem1 != null)
                {
                    //  録画済みに変更
                    recordedCount_New1++;
                    if ((RecEndStatus)rfi1.RecStatus == RecEndStatus.NORMAL)
                    {
                        recLogItem1.recodeStatus = RecLogItem.RecodeStatuses.録画完了;
                    }
                    else
                    {
                        recLogItem1.recodeStatus = RecLogItem.RecodeStatuses.録画異常;
                    }
                    recLogItem1.recFilePath = rfi1.RecFilePath;
                    recLogItem1.lastUpdate = lastUpdate1;
                    db_RecLog.update(recLogItem1);
                }
                else
                {
                    //addMessage("*** RecLogItemが見つからない?");
                }
            }
            //
            addDBLog("録画完了(" + recordedCount_New1 + ") " + lastUpdate1.ToString(_timestampFormat));
        }

        /// <summary>
        ///  予約済み更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _bgw_ReserveInfo_DoWork(object sender, DoWorkEventArgs e)
        {
            updateReserveInfo();
        }

        private void button_Search_Click(object sender, RoutedEventArgs e)
        {
            search(textBox_Search.Text);
        }

        private void cmdMenu_Del_Click(object sender, RoutedEventArgs e)
        {
            RecLogItem recLogItem_Selected1 = listView_RecLog.SelectedItem as RecLogItem;
            delete(recLogItem_Selected1);
        }

        private void listView_RecLog_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    setRecLogItem2editor();
                    break;
                case Key.Delete:
                    delete(listView_RecLog.SelectedItems);
                    break;
                case Key.Escape:
                    listView_RecLog.SelectedItem = null;
                    break;
            }
        }

        private void textBox_Search_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    search(textBox_Search.Text);
                    break;
            }
        }

        private void button_DB_ConnectTest_Click(object sender, RoutedEventArgs e)
        {
            db_RecLog.setSqlServerMachineName(textBox_MachineName.Text);
            db_RecLog.setSqlServerInstanceName(textBox_InstanceName.Text);
            bool isSuccess1 = dbConnectTest();
            if (isSuccess1)
            {
                Settings.Instance.RecLog_DB_MachineName = textBox_MachineName.Text;
                Settings.Instance.RecLog_DB_InstanceName = textBox_InstanceName.Text;
            }
            border_Button_DB_ConnectTest.BorderThickness = new Thickness();
        }

        private void checkBox_RecLogEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (checkBox_RecLogEnabled.IsChecked == true)
            {
                if (dbConnectTest())
                {
                    border_CheckBox_RecLogEnabled.BorderThickness = new Thickness();
                    //
                    if (CommonManager.Instance.NWMode == false)
                    {
                        int recFileInfoCount1 = 0;
                        BackgroundWorker bgw1 = new BackgroundWorker();
                        bgw1.RunWorkerCompleted += delegate
                        {
                            addDBLog("準備完了");
                        };
                        bgw1.DoWork += delegate
                        {
                            CommonManager.Instance.DB.ReloadrecFileInfo();
                            DateTime lastUpdate1 = DateTime.Now;
                            foreach (RecFileInfo rfi1 in CommonManager.Instance.DB.RecFileInfo.Values)
                            {
                                RecLogItem recLogItem1 = db_RecLog.exists(rfi1);
                                if (recLogItem1 == null)
                                {
                                    db_RecLog.insert(rfi1, lastUpdate1);
                                    recFileInfoCount1++;
                                }
                            }
                            StringBuilder sb1 = new StringBuilder();
                            sb1.AppendLine("録画完了リストを登録");
                            sb1.Append("　登録数：" + recFileInfoCount1);
                            addDBLog(sb1.ToString());
                            //
                            updateReserveInfo();
                        };
                        bgw1.RunWorkerAsync();
                    }
                }
                else
                {
                    checkBox_RecLogEnabled.IsChecked = false;
                }
                if (checkBox_RecLogEnabled.IsChecked != true)
                {
                    border_CheckBox_RecLogEnabled.BorderThickness = new Thickness(2);
                }
            }
            //
            Settings.Instance.RecLog_IsEnabled = (bool)checkBox_RecLogEnabled.IsChecked;
        }

        private void richTextBox_Comment_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_recLogItem_Edit == null) { return; }

            richTextBox_Comment.BorderThickness = new Thickness(1);
        }

        private void richTextBox_ShortInfo_text_char_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_recLogItem_Edit == null) { return; }

            richTextBox_ShortInfo_text_char.BorderThickness = new Thickness(1);
        }

        private void comboBox_Edit_Status_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_recLogItem_Edit == null) { return; }

            border_RecStatus.BorderThickness = new Thickness(1);
        }

        private void checkBox_AllowOverWrite_Click(object sender, RoutedEventArgs e)
        {
            if (_recLogItem_Edit == null) { return; }

            border_AllowOverWrite.BorderThickness = new Thickness(1);
        }

        private void richTextBox_ExtInfo_text_char_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_recLogItem_Edit == null) { return; }

            richTextBox_ExtInfo_text_char.BorderThickness = new Thickness(1);
        }

        private void button_Edit_Update_Click(object sender, RoutedEventArgs e)
        {
            if (_recLogItem_Edit == null) { return; }
            //
            _recLogItem_Edit.epgEventInfoR.ShortInfo.event_name = textBox_Edit_ProgramTitle.Text;
            _recLogItem_Edit.recodeStatus = (RecLogItem.RecodeStatuses)comboBox_Edit_Status.SelectedItem;
            _recLogItem_Edit.epgAlllowOverWrite = (checkBox_AllowOverWrite.IsChecked == true);
            _recLogItem_Edit.recFilePath = textBox_RecFilePath.Text;
            _recLogItem_Edit.comment = getText(richTextBox_Comment);
            _recLogItem_Edit.epgEventInfoR.ShortInfo.text_char = getText(richTextBox_ShortInfo_text_char);
            _recLogItem_Edit.epgEventInfoR.ExtInfo.text_char = getText(richTextBox_ExtInfo_text_char);
            //
            db_RecLog.update(_recLogItem_Edit);
            //
            var mainWindow = Application.Current.MainWindow as MainWindow;
            new BlackoutWindow(mainWindow).showWindow("データ更新完了");
            //
            hideEditor();
        }

        private void button_Edit_Cancel_Click(object sender, RoutedEventArgs e)
        {
            hideEditor();
        }

        private void listView_RecLog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView_RecLog.SelectedItems.Count == 0)
            {
                hideEditor();
            }
            else
            {
                setRecLogItem2editor();
            }
        }

        private void listView_RecLog_KeyDown_1(object sender, KeyEventArgs e)
        {

        }

        private void toggleButton_Setting_Click(object sender, RoutedEventArgs e)
        {
            if (toggleButton_Setting.IsChecked == true)
            {
                grid_Setting.Visibility = Visibility.Visible;
                border_ToggleButton_Setting.BorderThickness = new Thickness(2);
                toggleButton_Setting.Content = "設定を閉じる";
            }
            else
            {
                grid_Setting.Visibility = Visibility.Collapsed;
                border_ToggleButton_Setting.BorderThickness = new Thickness(0);
                toggleButton_Setting.Content = "設定";
                //
                switch (searchMethod)
                {
                    case searchMethods.LIKE:
                        Settings.Instance.RecLog_SearchMethod = searchMethods.LIKE;
                        break;
                    case searchMethods.Contrains:
                        Settings.Instance.RecLog_SearchMethod = searchMethods.Contrains;
                        break;
                    case searchMethods.Freetext:
                        Settings.Instance.RecLog_SearchMethod = searchMethods.Freetext;
                        break;
                    default:
                        throw new NotSupportedException(); ;
                }
                int RecLogWindow_SearchResultLimit1;
                if (int.TryParse(textBox_ResultLimit_RecLogWindow.Text, out RecLogWindow_SearchResultLimit1))
                {
                    Settings.Instance.RecLogWindow_SearchResultLimit = RecLogWindow_SearchResultLimit1;
                }
                Settings.SaveToXmlFile();
                addDBLog("設定を保存しました");
            }
        }

        private void textBox_MachineName_KeyDown(object sender, KeyEventArgs e)
        {
            border_Button_DB_ConnectTest.BorderThickness = new Thickness(2);
        }

        private void textBox_InstanceName_KeyDown(object sender, KeyEventArgs e)
        {
            border_Button_DB_ConnectTest.BorderThickness = new Thickness(2);
        }

        private void textBox_ResultLimit_KeyDown(object sender, KeyEventArgs e)
        {
            isSearchOptionChanged = true;
        }

        private void button_SaveSearchOption_Click(object sender, RoutedEventArgs e)
        {
            Settings.Instance.RecLog_SearchColumn = (int)searchColumn;
            Settings.Instance.RecLog_RecodeStatus = (int)recodeStatus;
            Settings.Instance.RecLog_SearchResultLimit = searchResultLimit;
            //
            Settings.SaveToXmlFile();
            isSearchOptionChanged = false;
            addDBLog("検索オプションを保存しました");

            border_Button_SaveSearchOption.Visibility = Visibility.Collapsed;
        }

        private void checkBox_Search_Title_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                searchColumn |= DB_RecLog.searchColumns.title;
            }
            else
            {
                searchColumn &= ~DB_RecLog.searchColumns.title;
            }
        }

        private void checkBox_Search_Content_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                searchColumn |= DB_RecLog.searchColumns.content;
            }
            else
            {
                searchColumn &= ~DB_RecLog.searchColumns.content;
            }
        }

        private void checkBox_Search_Comment_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                searchColumn |= DB_RecLog.searchColumns.comment;
            }
            else
            {
                searchColumn &= ~DB_RecLog.searchColumns.comment;
            }
        }

        private void checkBox_Search_RecFileName_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                searchColumn |= DB_RecLog.searchColumns.recFilePath;
            }
            else
            {
                searchColumn &= ~DB_RecLog.searchColumns.recFilePath;
            }
        }

        private void checkBox_RecStatus_Recoded_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                recodeStatus |= RecLogItem.RecodeStatuses.録画完了;
            }
            else
            {
                recodeStatus &= ~RecLogItem.RecodeStatuses.録画完了;
            }
        }

        private void checkBox_RecStatus_Recoded_Abnormal_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                recodeStatus |= RecLogItem.RecodeStatuses.録画異常;
            }
            else
            {
                recodeStatus &= ~RecLogItem.RecodeStatuses.録画異常;
            }
        }

        private void checkBox_RecStatus_Viewed_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                recodeStatus |= RecLogItem.RecodeStatuses.視聴済み;
            }
            else
            {
                recodeStatus &= ~RecLogItem.RecodeStatuses.視聴済み;
            }
        }

        private void checkBox_RecStatus_Reserved_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                recodeStatus |= RecLogItem.RecodeStatuses.予約済み;
            }
            else
            {
                recodeStatus &= ~RecLogItem.RecodeStatuses.予約済み;
            }
        }

        private void checkBox_RecStatus_Reserved_Null_Click(object sender, RoutedEventArgs e)
        {
            isSearchOptionChanged = true;
            if (((CheckBox)sender).IsChecked == true)
            {
                recodeStatus |= RecLogItem.RecodeStatuses.無効登録;
            }
            else
            {
                recodeStatus &= ~RecLogItem.RecodeStatuses.無効登録;
            }
        }

        private void button_Reset_Click(object sender, RoutedEventArgs e)
        {
            this.textBox_Search.Clear();
            this._recLogItemList.Clear();
            this.listView_RecLog.Items.Refresh();
        }

        private void button_CreateIndex_Click(object sender, RoutedEventArgs e)
        {
            if (CommonManager.Instance.NWMode != true)
            {
                if (checkBox_RecLogEnabled.IsChecked == true)
                {
                    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        if (dbConnectTest())
                        {
                            db_RecLog.createIndex();
                            addDBLog("Indexを作成しました。");
                        }
                    });
                }
            }
        }

        string _howto = @"
[HOWTO]

１．できること

　・「録画ログ」タブでキーワード検索、検索結果の編集。
　・「予約一覧」、「番組表」、「検索ウインドウ」などの右クリック・メニューから録画ログを検索

２．準備

　この機能を使用するにはSQLServerが必要です。
　フルテキスト検索付きのものをインストールして下さい
 
　ダウンロード：
　　Microsoft SQL Server 2014 Express（https://www.microsoft.com/ja-jp/download/details.aspx?id=42299）
　　ExpressAdv 64BIT\SQLEXPRADV_x64_JPN.exe（６４ビット）　または　ExpressAdv 32BIT\SQLEXPRADV_x86_JPN.exe　（３２ビット）　

　インストール中の「機能」選択で、「インスタンス機能」-「データベースエンジンサービス」-「検索のためのフルテキスト抽出とセマンティック抽出」にチェックをします。
   
　SQLServerをインストール後「RecLogを有効にする」をチェックし、「準備完了」と表示されるまで待ちます。

３．リモート接続

　SQLServerをリモート接続できるように設定（ファイアウォールなど）すれば、ネットワークモードのEpgTimerでも検索や録画ログの編集をすることができます。

　SQL Server Expressのリモート接続設定
　　・SQL Server構成マネージャ「ネットワーク構成」「TCP/IP」を「有効」
　　　SQL Server 2014 構成マネージャの起動コマンド：「SQLServerManager12.msc」
　　・ServerBrowerを起動
　　・ファイアウォールで許可
　　　TCP 1433 (SQLServer)
　　　UDP 1434 (Server Browser)

";

    }
}