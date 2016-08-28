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

namespace EpgTimer
{
    /// <summary>
    /// SettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {
        public bool ServiceStop = false;

        public SettingWindow()
        {
            InitializeComponent();
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                setBasicView.SaveSetting();
                setAppView.SaveSetting();
                setEpgView.SaveSetting();
                setOtherAppView.SaveSetting();

                Settings.SaveToXmlFile();
                ChSet5.SaveFile();
                if (CommonManager.Instance.NWMode == false)
                {
                    Settings.Instance.ReloadOtherOptions();//NWでは別途iniの更新通知後に実行される。
                }
                CommonManager.Instance.ReloadCustContentColorList();

                // Common.ini や EpgTimerSrv.ini の更新分をサーバー側へ通知する
                IniSetting.Instance.UpToDate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                MessageBox.Show("不正な入力値によるエラーのため、一部設定のみ更新されました。");
            }

            ServiceStop |= setAppView.ServiceStop;
            this.DialogResult = true;
        }

        private void button_cancel_Click(object sender, RoutedEventArgs e)
        {
            IniSetting.Instance.Clear();
            setEpgView.UpdateStyle(Settings.Instance.NoStyle == 0 ? Settings.Instance.StyleXamlPath : "");
            ServiceStop |= setAppView.ServiceStop;
            this.DialogResult = false;
        }
    }
}
