using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace EpgTimer
{
    /// <summary>
    /// InfoWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class InfoWindow : RestorableWindow
    {
        private NotifyIcon notifyIcon = new NotifyIcon();
        private bool trueClosing = false;

        private ListViewController<ReserveItem> lstCtrl;

        public InfoWindow(InfoWindowViewModel dataContext)
        {
            InitializeComponent();

            notifyIcon.Icon = Properties.Resources.TaskIconInfo;
            notifyIcon.Text = "予約簡易表示 (EpgTimer)";
            notifyIcon.MouseClick += NotifyIcon_MouseClick;
            notifyIcon.Visible = true;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            ToolStripMenuItem closeMenu = new ToolStripMenuItem();
            closeMenu.Text = "消す";
            closeMenu.Click += (s, e) => TrueClose();
            menu.Items.Add(closeMenu);
            notifyIcon.ContextMenuStrip = menu;

            //リストビュー関連の設定
            var list_columns = Resources["ReserveItemViewColumns"] as GridViewColumnList;
            //list_columns.AddRange(Resources["RecSettingViewColumns"] as GridViewColumnList);

            lstCtrl = new ListViewController<ReserveItem>(this);
            lstCtrl.SetSavePath(CommonUtil.GetMemberName(() => Settings.Instance.InfoWindowListColumn));
            lstCtrl.SetViewSetting(listView_InfoWindow, girdView_InfoWindow, false, false, list_columns, null, false);

            var StartTime = list_columns.Find(item => (item.Header as GridViewColumnHeader).Uid == "StartTime");
            if (StartTime != null)
            {
                StartTime.DisplayMemberBinding = new System.Windows.Data.Binding("StartTimeShort");
            }

            MouseLeftButtonDown += (s, e) => { DragMove(); };
            UpdateWindowState();

            DataContext = dataContext;
        }

        private void UpdateWindowState()
        {
            MenuItem_Header.IsChecked = Settings.Instance.InfoWindowHeaderIsVisible;
            girdView_InfoWindow.ColumnHeaderContainerStyle = TryFindResource(Settings.Instance.InfoWindowHeaderIsVisible ? "VisibleHeader" : "HiddenHeader") as Style;

            WindowStyle = Settings.Instance.InfoWindowTitleIsVisible ? WindowStyle.ToolWindow : WindowStyle.None;
            ResizeMode = Settings.Instance.InfoWindowTitleIsVisible ? ResizeMode.CanResize : ResizeMode.NoResize;
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if((e.Button & MouseButtons.Left) != 0)
            {
                WindowActivate();
            }
        }

        public void TrueClose()
        {
            trueClosing = true;
            Close();
        }

        public void WindowActivate()
        {
            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }
            Activate();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            lstCtrl.SaveViewDataToSettings();
            if (!trueClosing)
            {
                e.Cancel = true;
                Visibility = Visibility.Hidden;
                return;
            }
            else
            {
                notifyIcon.Dispose();
                notifyIcon = null;
            }
        }

        private void MenuItem_HeaderClick(object sender, EventArgs e)
        {
            Settings.Instance.InfoWindowHeaderIsVisible = !Settings.Instance.InfoWindowHeaderIsVisible;
            UpdateWindowState();
        }

        private void MenuItem_CloseClick(object sender, EventArgs e)
        {
            TrueClose();
        }

        private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Settings.Instance.InfoWindowTitleIsVisible = !Settings.Instance.InfoWindowTitleIsVisible;
            UpdateWindowState();
        }
    }
}
