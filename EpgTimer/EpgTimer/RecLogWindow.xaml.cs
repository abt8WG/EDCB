using EpgTimer.DefineClass;
using System;
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
using System.Windows.Shapes;
using System.ComponentModel;
using EpgTimer.Common;
using System.Text.RegularExpressions;

namespace EpgTimer
{
    /// <summary>
    /// PopupWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class RecLogWindow : Window
    {

        MainWindow _mainWindow = Application.Current.MainWindow as MainWindow;
        EpgEventInfo _epgEventInfo;
        ReserveData _reserveData;
        RecFileInfo _recFileInfo;
        List<RecLogItem> _resultList = new List<RecLogItem>();
        MenuItem _menuItem = new MenuItem() { };
        SolidColorBrush _background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
        SolidColorBrush _background_Selected = new SolidColorBrush(Colors.LightYellow);
        bool _isVisible = false;
        bool _isClosed = false;

        #region - Constructor -
        #endregion

        public RecLogWindow(Window owner0)
        {
            InitializeComponent();
            //
            Owner = owner0;
        }

        #region - Method -
        #endregion

        public void showResult(EpgEventInfo epgEventInfo0)
        {
            resset();
            if (Settings.Instance.RecLog_IsEnabled)
            {
                _epgEventInfo = epgEventInfo0;
                RecLogItem selectedRecLogItem1 = _mainWindow.recLogView.db_RecLog.exists(RecLogItem.RecodeStatuses.ALL,
                    epgEventInfo0.original_network_id, epgEventInfo0.transport_stream_id, epgEventInfo0.service_id, epgEventInfo0.event_id, epgEventInfo0.start_time);
                menuItem_ChangeStatus.Header = "録画完了で登録 (_R)";
                menuItem_ChangeStatus.IsEnabled = (selectedRecLogItem1 == null);
                search(epgEventInfo0.ShortInfo.event_name, selectedRecLogItem1);
            }
            else
            {
                drawText(RecLogView.notEnabledMessage);
            }
            show();
        }

        public void showResult(ReserveData reserveData0)
        {
            resset();
            if (Settings.Instance.RecLog_IsEnabled)
            {
                _reserveData = reserveData0;
                RecLogItem selectedRecLogItem1 = _mainWindow.recLogView.db_RecLog.exists(reserveData0);
                menuItem_ChangeStatus.Header = "録画完了に変更 (_R)";
                menuItem_ChangeStatus.IsEnabled = (selectedRecLogItem1 == null || selectedRecLogItem1.recodeStatus != RecLogItem.RecodeStatuses.録画完了);
                search(reserveData0.Title, selectedRecLogItem1);
            }
            else
            {
                drawText(RecLogView.notEnabledMessage);
            }
            show();
        }

        public void showResult(RecFileInfo recFileInfo0)
        {
            resset();
            if (Settings.Instance.RecLog_IsEnabled)
            {
                _recFileInfo = recFileInfo0;
                RecLogItem selectedRecLogItem1 = _mainWindow.recLogView.db_RecLog.exists(recFileInfo0);
                menuItem_ChangeStatus.Header = "録画完了で登録 (_R)";
                menuItem_ChangeStatus.IsEnabled = (selectedRecLogItem1 == null);
                search(recFileInfo0.Title, selectedRecLogItem1);
            }
            else
            {
                drawText(RecLogView.notEnabledMessage);
            }
            show();
        }

        void search(string searchWord0, RecLogItem selectedRecLogItem = null)
        {
            string selectedItem1 = null;
            string searchWord1 = trimKeyword(searchWord0);
            _resultList = _mainWindow.recLogView.getRecLogList(searchWord1, Settings.Instance.RecLogWindow_SearchResultLimit);
            List<string> lines1 = new List<string>();
            if (0 < _resultList.Count)
            {
                foreach (RecLogItem item in _resultList)
                {
                    string line1 = "[" + item.recodeStatus_Abbr + "]" + "[’" + item.epgEventInfoR.start_time.ToString("yy/MM/dd") + "] " +
                        item.epgEventInfoR.ShortInfo.event_name;
                    if (selectedRecLogItem != null && selectedRecLogItem.ID == item.ID)
                    {
                        selectedItem1 = line1;
                    }
                    else
                    {
                        lines1.Add(line1);
                    }
                }
            }
            else
            {
                lines1.Add("(NOT FOUND)");
            }
            //
            if (string.IsNullOrEmpty(selectedItem1))
            {
                richTextBox_SelectedItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                richTextBox_SelectedItem.Visibility = Visibility.Visible;
                drawText(richTextBox_SelectedItem, new List<string>() { selectedItem1 }, _background_Selected);
            }
            textBox.Text = searchWord1;
            drawText(lines1);
        }

        /// <summary>
        /// 前後の記号を取り除く
        /// </summary>
        /// <param name="txtKey"></param>
        /// <returns></returns>
        public static string trimKeyword(string txtKey)
        {
            string markExp1 =
                "(" +
                    "(\\[[^\\]]+\\])+" +
                    "|" +
                    "(【[^】]+】)+" +
                    "|" +
                    "(［[^］]+］)+" +
                    "|" +
                     "^(\\(５\\．１\\)|\\(5\\.1\\))" +
                     "|" +
                     "(◆|▼).+$" +
                     "|" +
                     "＜[^＞]+＞" +
                      "|" +
                     "（[^）]+）" +
                      "|" +
                     "\\([^\\)]+\\)" +
                      "|" +
                     "出演：.+$" +
                ")";
            return Regex.Replace(txtKey, markExp1, string.Empty).Trim();
        }

        void drawText(string text0)
        {
            drawText(new List<string>() { text0 });
        }

        void drawText(List<string> texts0)
        {
            drawText(richTextBox, texts0, _background);
        }

        void drawText(RichTextBox rtBox0, List<string> texts0, SolidColorBrush background0)
        {
            rtBox0.Document.Blocks.Clear();
            foreach (var text1 in texts0)
            {
                Paragraph paragraph1 =
                    new Paragraph(
                        new Run(text1))
                    {
                        Background = background0
                    };
                rtBox0.Document.Blocks.Add(paragraph1);
            }
        }

        void show()
        {
            _isVisible = true;

            Point pnt_Client1 = Mouse.GetPosition(Owner);
            Point pnt_Screen1 = Owner.PointToScreen(pnt_Client1);
            Left = pnt_Screen1.X;
            if (SystemParameters.WorkArea.Width < Right)
            {
                Left -= Width;
            }
            Top = pnt_Screen1.Y;
            if (SystemParameters.WorkArea.Height < Bottom)
            {
                Top -= Height;
            }
            //
            base.Show();
        }

        void hide()
        {
            _isVisible = false;
            base.Hide();
        }

        void resset()
        {
            richTextBox_SelectedItem.Visibility = Visibility.Visible;
            _resultList.Clear();
            _epgEventInfo = null;
            _reserveData = null;
            _recFileInfo = null;
            richTextBox.Document.Blocks.Clear();
        }

        #region - Property -
        #endregion

        double Right
        {
            get { return Left + Width; }
        }

        double Bottom
        {
            get { return Top + Height; }
        }

        DB_RecLog db_RecLog
        {
            get { return _mainWindow.recLogView.db_RecLog; }
        }

        #region - Event Handler -
        #endregion

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            //
            switch (e.Key)
            {
                case Key.Escape:
                    Hide();
                    break;
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            //
            Point pnt_Client1 = Mouse.GetPosition(this);
            Point pnt_Screen1 = PointToScreen(pnt_Client1);

            double left1 = Left + border.Margin.Left;
            double right1 = Right - border.Margin.Right;
            double top1 = Top + border.Margin.Top;
            double bottom1 = Bottom - border.Margin.Bottom;

            if (pnt_Screen1.X < left1 || right1 < pnt_Screen1.X || pnt_Screen1.Y < top1 || bottom1 < pnt_Screen1.Y)
            {
                hide();
            }
        }

        void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    richTextBox_SelectedItem.Visibility = Visibility.Collapsed;
                    menuItem_ChangeStatus.IsEnabled = false;
                    search(textBox.Text);
                    break;
            }
        }

        void menuItem_ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            DateTime lastUpdate1 = DateTime.Now;
            RecLogItem recLogItem1 = null;
            if (_reserveData != null)
            {
                recLogItem1 = db_RecLog.exists(_reserveData);
                if (recLogItem1 != null)
                {
                    recLogItem1.recodeStatus = RecLogItem.RecodeStatuses.録画完了;
                    db_RecLog.update(recLogItem1);
                    //showResult(_reserveData);
                }
            }
            else if (_epgEventInfo != null)
            {
                recLogItem1 = new RecLogItem()
                {
                    lastUpdate = lastUpdate1,
                    recodeStatus = RecLogItem.RecodeStatuses.録画完了,
                    epgEventInfoR = new EpgEventInfoR(_epgEventInfo, lastUpdate1)
                };
                db_RecLog.insert(recLogItem1);
                //showResult(_epgEventInfo);
            }
            else if (_recFileInfo != null)
            {
                recLogItem1 = db_RecLog.exists(_recFileInfo);
                if (recLogItem1 == null)
                {
                    recLogItem1 = db_RecLog.insert(_recFileInfo, lastUpdate1);
                }
                //showResult(_recFileInfo);
            }
            if (recLogItem1 != null)
            {
                string line1 = "[録]" + "[’" + recLogItem1.epgEventInfoR.start_time.ToString("yy/MM/dd") + "] " + recLogItem1.epgEventInfoR.ShortInfo.event_name;
                drawText(richTextBox_SelectedItem, new List<string>() { line1 }, _background_Selected);
            }
        }

        private void richTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_recFileInfo != null && 0 < _resultList.Count)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// HideしたWindowsがOwnerとともに表示されてしまうのを防ぐ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_isClosed) { return; }
            //
            if (_isVisible)
            {
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Hidden;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _isClosed = true;
        }

    }
}
