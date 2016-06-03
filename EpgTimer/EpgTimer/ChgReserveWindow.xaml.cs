﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace EpgTimer
{
    /// <summary>
    /// ChgReserveWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ChgReserveWindow : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged実装 (nekopanda版)

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
        }

        #endregion

        private ReserveData reserveInfo = null;
        private MenuUtil mutil = CommonManager.Instance.MUtil;
        private MenuBinds mBinds = new MenuBinds();

        protected enum AddMode { Add, Re_Add, Change }
        private AddMode addMode = AddMode.Change;   //予約モード、再予約モード、変更モード
        private byte openMode = 0;                  //EPGViewで番組表を表示するかどうか

        private bool initialized = false;
        private ReserveData resInfoDisplay = null;
        private EpgEventInfo eventInfoDisplay = null;
        private EpgEventInfo eventInfoNew = null;
        private EpgEventInfo eventInfoSelected = null;

        #region ReserveMode 変更通知プロパティ (nekopanda版)

        private UIReserveMode _ReserveMode = UIReserveMode.None;

        public UIReserveMode ReserveMode
        {
            get { return this._ReserveMode; }
            set
            {
                if (this._ReserveMode != value)
                {
                    this._ReserveMode = value;
                    this.RaisePropertyChanged();
                    this.ReserveModeChanged();
                }
            }
        }

        #endregion

        #region CanSelectAutoAdd 変更通知プロパティ (nekopanda版)

        private bool _CanSelectAutoAdd;

        public bool CanSelectAutoAdd
        {
            get { return this._CanSelectAutoAdd; }
            set
            {
                if (this._CanSelectAutoAdd != value)
                {
                    this._CanSelectAutoAdd = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        #endregion

        public ChgReserveWindow()
        {
            InitializeComponent();

            this.DataContext = this; // (nekopanda版)

            //コマンドの登録
            this.CommandBindings.Add(new CommandBinding(EpgCmds.Cancel, (sender, e) => DialogResult = false));
            this.CommandBindings.Add(new CommandBinding(EpgCmds.AddInDialog, button_chg_reserve_Click, (sender, e) => e.CanExecute = addMode != AddMode.Change));
            this.CommandBindings.Add(new CommandBinding(EpgCmds.ChangeInDialog, button_chg_reserve_Click, (sender, e) => e.CanExecute = addMode == AddMode.Change));
            this.CommandBindings.Add(new CommandBinding(EpgCmds.DeleteInDialog, button_del_reserve_Click, (sender, e) => e.CanExecute = addMode == AddMode.Change));

            //ボタンの設定
            mBinds.SetCommandToButton(button_cancel, EpgCmds.Cancel);
            mBinds.SetCommandToButton(button_chg_reserve, EpgCmds.ChangeInDialog);
            mBinds.SetCommandToButton(button_del_reserve, EpgCmds.DeleteInDialog);
            mBinds.AddInputCommand(EpgCmds.AddInDialog);//ボタンの切り替え用も登録しておく。
            mBinds.ResetInputBindings(this);

            //その他設定
            //深夜時間関係は、comboBoxの表示だけ変更する手もあるが、
            //オプション変更タイミングなどいろいろ面倒なので、実際の値で処理することにする。
            comboBox_service.ItemsSource = ChSet5.Instance.ChList.Values;
            comboBox_sh.ItemsSource = CommonManager.Instance.HourDictionarySelect.Values;
            comboBox_eh.ItemsSource = CommonManager.Instance.HourDictionarySelect.Values;
            comboBox_sm.ItemsSource = CommonManager.Instance.MinDictionary.Values;
            comboBox_em.ItemsSource = CommonManager.Instance.MinDictionary.Values;
            comboBox_ss.ItemsSource = CommonManager.Instance.MinDictionary.Values;
            comboBox_es.ItemsSource = CommonManager.Instance.MinDictionary.Values;
        }

        public void SetOpenMode(byte mode)
        {
            openMode = mode;
        }

        public void SetAddReserveMode()
        {
            addMode = AddMode.Add;
        }

        private void SetAddMode(AddMode mode)
        {
            addMode = mode;
            switch (mode)
            {
                case AddMode.Add:
                    this.Title = "予約登録";
                    button_chg_reserve.Content = "予約";
                    mBinds.SetCommandToButton(button_chg_reserve, EpgCmds.AddInDialog);
                    button_del_reserve.Visibility = Visibility.Hidden;
                    break;
                case AddMode.Re_Add:
                    button_chg_reserve.Content = "再予約";
                    mBinds.SetCommandToButton(button_chg_reserve, EpgCmds.AddInDialog);
                    //なお、削除ボタンはCanExeの判定でグレーアウトする。
                    break;
            }
        }

        // nekopanda版
        private UIReserveMode GetReserveModeFromInfo()
        {
            if (reserveInfo == null) return UIReserveMode.Program;
            if (reserveInfo.Comment.StartsWith("EPG自動予約")) return UIReserveMode.EPGAuto;
            if (reserveInfo.EventID != 0xFFFF) return UIReserveMode.EPGManual;
            return UIReserveMode.Program;
        }

        /// <summary>初期値の予約情報をセットする</summary>
        /// <param name="info">[IN] 初期値の予約情報</param>
        public void SetReserveInfo(ReserveData info)
        {
            reserveInfo = info.Clone();
            recSettingView.SetDefSetting(reserveInfo.RecSetting, reserveInfo.IsEpgReserve == false);
        }

        private void SetResModeProgram()
        {
            bool resModeProgram = (ReserveMode == UIReserveMode.Program);

            textBox_title.IsEnabled = resModeProgram;
            comboBox_service.IsEnabled = resModeProgram;
            datePicker_start.IsEnabled = resModeProgram;
            comboBox_sh.IsEnabled = resModeProgram;
            comboBox_sm.IsEnabled = resModeProgram;
            comboBox_ss.IsEnabled = resModeProgram;
            datePicker_end.IsEnabled = resModeProgram;
            comboBox_eh.IsEnabled = resModeProgram;
            comboBox_em.IsEnabled = resModeProgram;
            comboBox_es.IsEnabled = resModeProgram;
            recSettingView.SetViewMode(!resModeProgram);
        }

        //問題は起きないはずだが、ロード時に何度もSelectionChangedが呼び出されるので一応制御を入れておく
        //SelectedIndexChanged使えないい？
        int TabSelectedIndex = -1;

        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabControl.SelectedIndex == TabSelectedIndex) return;
            TabSelectedIndex = tabControl.SelectedIndex;

            if (tabItem_program.IsSelected)
            {
                if (ReserveMode == UIReserveMode.Program)
                {
                    var resInfo = new ReserveData();
                    GetReserveTimeInfo(ref resInfo);

                    //描画回数の削減を気にしないなら、この条件文は無くてもいい
                    if (CtrlCmdDefEx.EqualsPg(resInfoDisplay, resInfo, false, true) == false)
                    {
                        //EPGを自動で読み込んでない時でも、元がEPG予約ならその番組情報は表示させられるようにする
                        if (reserveInfo.IsEpgReserve == true && CtrlCmdDefEx.EqualsPg(reserveInfo, resInfo, false, true) == true)
                        {
                            SetProgramContent(reserveInfo.SearchEventInfo(true));
                        }
                        else
                        {
                            SetProgramContent(resInfo.SearchEventInfoLikeThat());
                        }

                        resInfoDisplay = resInfo;
                    }
                }
                else
                {
                    //EPG予約を変更していない場合引っかかる。
                    //最も表示される可能性が高いので、何度も探しにいかせないようにする。
                    if (eventInfoSelected == null)
                    {
                        eventInfoSelected = reserveInfo.SearchEventInfo(true);
                    }
                    SetProgramContent(eventInfoSelected);
                    resInfoDisplay = null;
                }
            }
        }

        private void SetProgramContent(EpgEventInfo info)
        {
            //放映時刻情報に対してEPGデータ無い場合もあるので、resInfoDisplayとは別にeventInfoDisplayを管理する
            if (CtrlCmdDefEx.EqualsPg(eventInfoDisplay, info) == false)
            {
                richTextBox_descInfo.Document = CommonManager.Instance.ConvertDisplayText(info);
            }
            eventInfoDisplay = info;
        }

        private void ResetProgramContent()
        {
            richTextBox_descInfo.Document = CommonManager.Instance.ConvertDisplayText(null);
            eventInfoDisplay = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (addMode == AddMode.Add || reserveInfo == null)
            {
                addMode = AddMode.Add;
                reserveInfo = null;
                ReserveMode = UIReserveMode.Program;
                CanSelectAutoAdd = false;

                if (comboBox_service.Items.Count > 0)
                {
                    comboBox_service.SelectedIndex = 0;
                }

                SetAddMode(AddMode.Add);
                SetReserveTime(DateTime.Now.AddMinutes(1), DateTime.Now.AddMinutes(31));
                reserveInfo = new ReserveData();
            }
            else
            {
                ReserveMode = GetReserveModeFromInfo();
                CanSelectAutoAdd = (ReserveMode == UIReserveMode.EPGAuto);

                SetReserveTimeInfo(reserveInfo);
            }

            ResetProgramContent();                  //番組詳細を初期表示
            tabControl.SelectedIndex = openMode;

            initialized = true;
        }

        private void SetReserveTime(DateTime startTime, DateTime endTime)
        {
            //深夜時間帯の処理
            bool use28 = Settings.Instance.LaterTimeUse == true && (endTime - startTime).TotalDays < 1;
            bool late_start = use28 && startTime.Hour + 24 < comboBox_sh.Items.Count && DateTime28.IsLateHour(startTime.Hour);
            bool late_end = use28 && endTime.Hour + 24 < comboBox_eh.Items.Count && DateTime28.JudgeLateHour(endTime, startTime);

            datePicker_start.SelectedDate = startTime.AddDays(late_start == true ? -1 : 0);
            comboBox_sh.SelectedIndex = startTime.Hour + (late_start == true ? 24 : 0);
            comboBox_sm.SelectedIndex = startTime.Minute;
            comboBox_ss.SelectedIndex = startTime.Second;

            datePicker_end.SelectedDate = endTime.AddDays(late_end == true ? -1 : 0);
            comboBox_eh.SelectedIndex = endTime.Hour + (late_end == true ? 24 : 0);
            comboBox_em.SelectedIndex = endTime.Minute;
            comboBox_es.SelectedIndex = endTime.Second;
        }

        private int GetReserveTimeInfo(ref ReserveData resInfo)
        {
            if (resInfo == null) return -1;

            try
            {
                resInfo.Title = textBox_title.Text;
                ChSet5Item ch = comboBox_service.SelectedItem as ChSet5Item;

                resInfo.StationName = ch.ServiceName;
                resInfo.OriginalNetworkID = ch.ONID;
                resInfo.TransportStreamID = ch.TSID;
                resInfo.ServiceID = ch.SID;
                //resInfo.EventID = 0xFFFF;　条件付の情報なのでここでは書き換えないことにする

                //深夜時間帯の処理
                DateTime date_start = datePicker_start.SelectedDate.Value.AddDays((int)(comboBox_sh.SelectedIndex / 24));
                DateTime date_end = datePicker_end.SelectedDate.Value.AddDays((int)(comboBox_eh.SelectedIndex / 24));

                resInfo.StartTime = new DateTime(date_start.Year, date_start.Month, date_start.Day,
                    comboBox_sh.SelectedIndex % 24,
                    comboBox_sm.SelectedIndex,
                    comboBox_ss.SelectedIndex,
                    0,
                    DateTimeKind.Utc
                    );

                DateTime endTime = new DateTime(date_start.Year, date_end.Month, date_end.Day,
                    comboBox_eh.SelectedIndex % 24,
                    comboBox_em.SelectedIndex,
                    comboBox_es.SelectedIndex,
                    0,
                    DateTimeKind.Utc
                    );
                if (resInfo.StartTime > endTime)
                {
                    resInfo.DurationSecond = 0;
                    return -2;
                }
                else
                {
                    TimeSpan duration = endTime - resInfo.StartTime;
                    resInfo.DurationSecond = (uint)duration.TotalSeconds;
                    return 0;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
            return -1;
        }

        private void SetReserveTimeInfo(ReserveData resInfo)
        {
            if (resInfo == null) return;

            try
            {
                textBox_title.Text = resInfo.Title;

                foreach (ChSet5Item ch in comboBox_service.Items)
                {
                    if (ch.Key == resInfo.Create64Key())
                    {
                        comboBox_service.SelectedItem = ch;
                        break;
                    }
                }

                SetReserveTime(resInfo.StartTime,resInfo.StartTime.AddSeconds(resInfo.DurationSecond));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        private bool CheckExistReserveItem()
        {
            bool retval = CommonManager.Instance.DB.ReserveList.ContainsKey(this.reserveInfo.ReserveID);
            if (retval == false)
            {
                MessageBox.Show("項目がありません。\r\n" + "既に削除されています。\r\n" + "(別のEpgtimerによる操作など)", "データエラー", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                //予約復旧を提示させる。これだけで大丈夫だったりする。
                SetAddMode(AddMode.Re_Add);
            }
            return retval;
        }

        private void button_chg_reserve_Click(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (CmdExeUtil.IsDisplayKgMessage(e) == true)
                {
                    bool change_proc = false;
                    switch (addMode)
                    {
                        case AddMode.Add:
                            change_proc = (MessageBox.Show("予約を追加します。\r\nよろしいですか？", "予約の確認", MessageBoxButton.OKCancel) == MessageBoxResult.OK);
                            break;
                        case AddMode.Re_Add:
                            change_proc = (MessageBox.Show("この内容で再予約します。\r\nよろしいですか？", "再予約の確認", MessageBoxButton.OKCancel) == MessageBoxResult.OK);
                            break;
                        case AddMode.Change:
                            change_proc = (MessageBox.Show("この予約を変更します。\r\nよろしいですか？", "変更の確認", MessageBoxButton.OKCancel) == MessageBoxResult.OK);
                            break;
                    }
                    if (change_proc == false) return;
                }

                if (addMode == AddMode.Change && CheckExistReserveItem() == false) return;

                //ダイアログを閉じないときはreserveInfoを変更しないよう注意する
                if (ReserveMode == UIReserveMode.Program)
                {
                    var resInfo = new ReserveData();
                    if (GetReserveTimeInfo(ref resInfo) == -2)
                    {
                        MessageBox.Show("終了日時が開始日時より前です");
                        return;
                    }

                    //reserveInfo取得前に保存する。サービスや時間が変わったら、個別予約扱いにする。タイトルのみ変更は見ない。
                    bool chgManualMode = !CtrlCmdDefEx.EqualsPg(resInfo, reserveInfo, false, true);

                    GetReserveTimeInfo(ref reserveInfo);
                    if (reserveInfo.EventID != 0xFFFF || chgManualMode == true)
                    {
                        reserveInfo.EventID = 0xFFFF;
                        reserveInfo.Comment = "";
                    }
                    reserveInfo.StartTimeEpg = reserveInfo.StartTime;
                }
                else if(ReserveMode == UIReserveMode.EPGManual)
                {
                    //EPG予約に変える場合、またはEPG予約で別の番組に変わる場合
                    if (eventInfoNew != null)
                    {
                        //基本的にAddReserveEpgWindowと同じ処理内容
                        if (mutil.IsEnableReserveAdd(eventInfoNew) == false) return;
                        eventInfoNew.ConvertToReserveData(ref reserveInfo);
                        reserveInfo.Comment = "";
                    }
                    // 自動予約から個別予約に変える場合
                    else if(GetReserveModeFromInfo() == UIReserveMode.EPGAuto)
                    {
                        reserveInfo.Comment = "";
                    }
                }

                reserveInfo.RecSetting = recSettingView.GetRecSetting();

                if (addMode == AddMode.Change)
                {
                    bool ret = mutil.ReserveChange(CommonUtil.ToList(reserveInfo));
                    CommonManager.Instance.StatusNotifySet(ret, "録画予約を変更");
                }
                else
                {
                    bool ret = mutil.ReserveAdd(CommonUtil.ToList(reserveInfo));
                    CommonManager.Instance.StatusNotifySet(ret, "録画予約を追加");
                }

                // EPG自動予約以外になったら戻せないようにしておく
                if(ReserveMode != UIReserveMode.EPGAuto)
                {
                    CanSelectAutoAdd = false;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }

            DialogResult = true;
        }
        private void button_del_reserve_Click(object sender, ExecutedRoutedEventArgs e)
        {
            if (CmdExeUtil.IsDisplayKgMessage(e) == true)
            {
                if (MessageBox.Show("この予約を削除します。\r\nよろしいですか？", "削除の確認", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                { return; }
            }

            if (CheckExistReserveItem() == false) return;

            bool ret = mutil.ReserveDelete(CommonUtil.ToList(reserveInfo));
            CommonManager.Instance.StatusNotifySet(ret, "録画予約を削除");

            DialogResult = true;
        }

        private void ReserveModeChanged()
        {
            // UIに反映させる
            SetResModeProgram();

            if (!initialized) return;

            if (ReserveMode == UIReserveMode.Program)
            {
                eventInfoNew = null;
            }
            else if (ReserveMode == UIReserveMode.EPGManual)
            {
                var resInfo = new ReserveData();
                GetReserveTimeInfo(ref resInfo);

                if (reserveInfo.IsEpgReserve == true && CtrlCmdDefEx.EqualsPg(reserveInfo, resInfo, false, true) == true)
                {
                    //EPG予約で、元の状態に戻る場合
                    textBox_title.Text = reserveInfo.Title;
                    eventInfoNew = null;
                }
                else
                {
                    eventInfoNew = resInfo.SearchEventInfoLikeThat();
                    if (eventInfoNew == null)
                    {
                        MessageBox.Show("変更可能な番組がありません。\r\n" +
                                        "EPGの期間外か、EPGデータが読み込まれていません。");
                        ReserveMode = UIReserveMode.Program;
                    }
                    else
                    {
                        SetReserveTimeInfo(CtrlCmdDefEx.ConvertEpgToReserveData(eventInfoNew));
                    }
                }
            }
            else if (ReserveMode == UIReserveMode.EPGAuto)
            {
                SetReserveTimeInfo(reserveInfo);
            }

            eventInfoSelected = eventInfoNew;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            mainWindow.ListFoucsOnVisibleChanged();
        }
    }

    // nekopanda版
    public enum UIReserveMode
    {
        None = 0,
        EPGAuto = 1,
        EPGManual = 2,
        Program = 3
    }

    // ReserveModeをラジオボタンのIsCheckedにバインドするときのコンバーター
    public class UIReserveModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return (int)((UIReserveMode)value) == int.Parse(parameter.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            if ((bool)value) return (UIReserveMode)int.Parse(parameter.ToString());
            return null;
        }
    }
}
