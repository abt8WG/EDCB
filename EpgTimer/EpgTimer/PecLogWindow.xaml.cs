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
    public partial class PecLogWindow : Window
    {

        MainWindow _mainWindow = Application.Current.MainWindow as MainWindow;
        EpgEventInfo _epgEventInfo;
        ReserveData _reserveData;
        RecFileInfo _recFileInfo;
        List<RecLogItem> _resultList = new List<RecLogItem>();

        #region - Constructor -
        #endregion

        public PecLogWindow(Window owner0)
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
            if (Settings.Instance.RecLog_Enabled)
            {
                MenuItem_Recorded.Header = "録画完了で登録 (_R)";
                _epgEventInfo = epgEventInfo0;
                search(epgEventInfo0.ShortInfo.event_name);
            }
            else {
                drawText(RecLogView.notEnabledMessage);
            }
            show();
        }

        public void showResult(ReserveData reserveData0)
        {
            resset();
            if (Settings.Instance.RecLog_Enabled)
            {
                MenuItem_Recorded.Header = "録画完了に変更 (_R)";
                _reserveData = reserveData0;
                search(reserveData0.Title);
            }
            else {
                drawText(RecLogView.notEnabledMessage);
            }
            show();
        }

        public void showResult(RecFileInfo recFileInfo0)
        {
            resset();
            if (Settings.Instance.RecLog_Enabled)
            {
                MenuItem_Recorded.Header = "録画完了で登録 (_R)";
                _recFileInfo = recFileInfo0;
                search(recFileInfo0.Title);
            }
            else {
                drawText(RecLogView.notEnabledMessage);
            }
            show();
        }

        void search(string searchWord0)
        {
            string searchWord1 = CommonManager.Instance.MUtil.TrimKeyword(searchWord0); // 前後の記号類
            _resultList = _mainWindow.recLogView.getRecLogList(searchWord1, Settings.Instance.RecLogWindow_SearchResultLimit);
            List<string> lines1 = new List<string>();
            foreach (var item in _resultList)
            {
                string line1 = "[" + item.recodeStatus_Abbr + "]" + "[’" + item.epgEventInfoR.start_time.ToString("yy/MM/dd") + "] " +
                    item.epgEventInfoR.ShortInfo.event_name;
                lines1.Add(line1);
            }
            textBox.Text = searchWord1;
            drawText(lines1);
        }

        void drawText(List<string> texts0)
        {
            richTextBox.Document.Blocks.Clear();
            foreach (var item in texts0)
            {
                Paragraph paragraph1 =
                    new Paragraph(
                        new Run(item))
                    {
                        Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
                    };
                richTextBox.Document.Blocks.Add(paragraph1);
            }
        }

        void drawText(string text0)
        {
            drawText(new List<string>() { text0 });
        }

        void show()
        {
            Point pnt_Client1 = Mouse.GetPosition(Owner);
            Point pnt_Screen1 = Owner.PointToScreen(pnt_Client1);
            Left = pnt_Screen1.X;
            if (SystemParameters.WorkArea.Width < (Left + Width))
            {
                Left -= Width;
            }
            Top = pnt_Screen1.Y;
            if (SystemParameters.WorkArea.Height < (Top + Height))
            {
                Top -= Height;
            }
            //
            Show();
        }

        void resset()
        {
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
                Hide();
            }
        }

        void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    search(textBox.Text);
                    break;
            }
        }

        void MenuItem_Recorded_Click(object sender, RoutedEventArgs e)
        {
            DateTime lastUpdate1 = DateTime.Now;
            if (_reserveData != null)
            {
                RecLogItem recLogItem1 = db_RecLog.exists_Reserved(
                    _reserveData.OriginalNetworkID, _reserveData.TransportStreamID, _reserveData.ServiceID, _reserveData.EventID, _reserveData.StartTime);
                if (recLogItem1 != null)
                {
                    recLogItem1.recodeStatus = RecLogItem.RecodeStatuses.録画完了;
                    db_RecLog.update(recLogItem1);
                    showResult(_reserveData);
                }
            }
            else if (_epgEventInfo != null)
            {
                RecLogItem recLogItem2 = new RecLogItem()
                {
                    lastUpdate = lastUpdate1,
                    recodeStatus = RecLogItem.RecodeStatuses.録画完了,
                    epgEventInfoR = new EpgEventInfoR(_epgEventInfo, lastUpdate1)
                };
                db_RecLog.insert(recLogItem2);
                showResult(_epgEventInfo);
            }
            else if (_recFileInfo != null)
            {
                bool exist1 = db_RecLog.exists_RecInfo(_recFileInfo);
                if (!exist1)
                {
                    db_RecLog.insert(_recFileInfo, lastUpdate1);
                }
                showResult(_recFileInfo);
            }
        }

        private void richTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_recFileInfo != null && 0 < _resultList.Count)
            {
                e.Handled = true;
            }
        }

    }
}
