using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EpgTimer
{
    /// <summary>
    /// ConnectWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ConnectWindow : Window
    {
        private const string DefPresetStr = "前回接続時";

        private bool IsAvailableServer;
        private bool IsTouchedPreset;
        ObservableCollection<NWPresetItem> ConnectionList;
        
        public ConnectWindow()
        {
            InitializeComponent();

            try
            {
                IsAvailableServer = ServiceCtrlClass.ServiceIsInstalled("EpgTimer Service") == true ||
                    System.IO.File.Exists(SettingPath.ModulePath.TrimEnd('\\') + "\\EpgTimerSrv.exe") == true;

                btn_edit.Visibility = Visibility.Collapsed;

                ConnectionList = new ObservableCollection<NWPresetItem>(Settings.Instance.NWPreset);
                var nowSet = new NWPresetItem(DefPresetStr, Settings.Instance.NWServerIP, Settings.Instance.NWServerPort, Settings.Instance.NWWaitPort, Settings.Instance.NWMacAdd, Settings.Instance.NWPassword);
                int pos = ConnectionList.ToList().FindIndex(item => item.EqualsTo(nowSet, true));
                if (pos == -1)
                {
                    ConnectionList.Add(nowSet);
                    pos = ConnectionList.Count - 1;
                }
                if (IsAvailableServer == true)
                {
                    ConnectionList.Insert(0, new NWPresetItem("ローカル接続", "", 0, 0, "", new SerializableSecureString()));
                    pos++;
                }
                ConnectionList.Add(new NWPresetItem("<新規登録>", "", 0, 0, "", new SerializableSecureString()));

                listView_List.ItemsSource = ConnectionList;
                if (IsAvailableServer == false)
                {
                    Settings.Instance.NWMode = true;
                }
                listView_List.SelectedIndex = Settings.Instance.NWMode == true ? pos : 0;

                this.KeyDown += ViewUtil.KeyDown_Escape_Close();
                listView_List.KeyDown += ViewUtil.KeyDown_Enter(button_connect);
            }
            catch { }
        }

        private void UpdateNWPreset()
        {
            int start = IsAvailableServer ? 1 : 0;
            int count = ConnectionList.Count - 1 - start;
            if (start >= 0 && count >= 0)
            {
                Settings.Instance.NWPreset = ConnectionList.Skip(start).Take(count).ToList();
                IsTouchedPreset = true;
            }
        }

        private void DoConnect()
        {
            try
            {
                Settings.Instance.NWMode = IsAvailableServer == false || listView_List.SelectedIndex > 0;
                if (Settings.Instance.NWMode == true)
                {
                    NWPresetItem data = GetSetting();
                    Settings.Instance.NWServerIP = data.NWServerIP;
                    Settings.Instance.NWServerPort = data.NWServerPort;
                    Settings.Instance.NWWaitPort = data.NWWaitPort;
                    Settings.Instance.NWMacAdd = data.NWMacAdd;
                    Settings.Instance.NWPassword = data.NWPassword;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
            return;
        }

        private void listView_List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv != null)
            {
                if (IsAvailableServer == true && lv.SelectedIndex == 0)
                {
                    // ローカル接続
                    SetSetting(null);
                    grid_Edit.IsEnabled = false;
                    btn_add.Visibility = Visibility.Visible;
                    btn_edit.Visibility = Visibility.Collapsed;
                }
                else
                {
                    grid_Edit.IsEnabled = true;

                    if (lv.SelectedIndex != lv.Items.Count - 1)
                    {
                        // 編集
                        SetSetting(lv.SelectedItem as NWPresetItem);
                        if (textBox_Name.Text.Length == 0)
                        {
                            // 未登録
                            btn_add.Visibility = Visibility.Visible;
                            btn_edit.Visibility = Visibility.Collapsed;
                            btn_delete.IsEnabled = false;
                        }
                        else
                        {
                            // 登録済み
                            btn_add.Visibility = Visibility.Collapsed;
                            btn_edit.Visibility = Visibility.Visible;
                            btn_delete.IsEnabled = true;
                        }
                    }
                    else
                    {
                        // 新規追加
                        SetSetting(new NWPresetItem("", "", 4510, 4520, "", new SerializableSecureString()));
                        btn_add.Visibility = Visibility.Visible;
                        btn_edit.Visibility = Visibility.Collapsed;
                        btn_delete.IsEnabled = false;
                    }
                }
            }
        }

        private void listView_List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv.SelectedIndex == lv.Items.Count - 1)
            {
                textBox_srvIP.Focus();
            }
            else
            {
                DoConnect();
            }
        }

        private void button_connect_Click(object sender, RoutedEventArgs e)
        {
            DoConnect();
        }

        private void button_wake_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NWConnect.SendMagicPacket(ConvertTextMacAddress(textBox_mac.Text));
                Settings.Instance.NWMacAdd = textBox_mac.Text;
            }
            catch
            {
                MessageBox.Show("書式が間違っているか、16進アドレスの数値が読み取れません。");
            }
        }

        //失敗するとエラー
        public static byte[] ConvertTextMacAddress(string txt)
        {
            byte[] macAddress = Enumerable.Repeat<byte>(0xFF, 6).ToArray();

            string[] mac = txt.Split('-');
            for (int i = 0; i < Math.Max(mac.Length, 6); i++)
            {
                macAddress[i] = Convert.ToByte(mac[i], 16);
            }

            return macAddress;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            button_connect.Focus();
        }

        private void SetSetting(NWPresetItem data)
        {
            if (data != null)
            {
                textBox_Name.Text = data.Name == DefPresetStr ? "" : data.Name;
                textBox_srvIP.Text = data.NWServerIP;
                textBox_srvPort.Text = data.NWServerPort.ToString();
                checkBox_clientPort.IsChecked = data.NWWaitPort != 0;
                textBox_clientPort.Text = data.NWWaitPort == 0 ? "4520" : data.NWWaitPort.ToString();
                textBox_mac.Text = data.NWMacAdd;
                textBox_Password.Password = new System.Net.NetworkCredential(string.Empty, data.NWPassword.SecureString).Password; // セキュアなコピーではない
            }
            else
            {
                textBox_Name.Text = "";
                textBox_srvIP.Text = "";
                textBox_srvPort.Text = "";
                checkBox_clientPort.IsChecked = false;
                textBox_clientPort.Text = "";
                textBox_mac.Text = "";
                textBox_Password.Password = "";
            }
        }
        private NWPresetItem GetSetting()
        {
            var preset = new NWPresetItem();
            preset.Name = textBox_Name.Text.Trim();
            preset.NWServerIP = textBox_srvIP.Text.Trim();
            preset.NWServerPort = MenuUtil.MyToNumerical(textBox_srvPort, Convert.ToUInt32, Settings.Instance.NWServerPort);
            preset.NWWaitPort = checkBox_clientPort.IsChecked == false ? 0 : MenuUtil.MyToNumerical(textBox_clientPort, Convert.ToUInt32, Settings.Instance.NWWaitPort);
            preset.NWMacAdd = textBox_mac.Text.Trim();
            preset.NWPassword = new SerializableSecureString(textBox_Password.SecurePassword);
            if (preset.Name.Length == 0)
            {
                preset.Name = preset.NWServerIP;
            }
            return preset;
        }

        private void btn_add_Click(object sender, RoutedEventArgs e)
        {
            NWPresetItem newitem = GetSetting();
            if (newitem.NWServerIP.Length == 0)
            {
                MessageBox.Show("接続サーバーが指定されていません。");
            }
            else
            {
                int pos = ConnectionList.ToList().FindIndex(item => item.Name == newitem.Name);
                if (pos >= 0)
                {
                    MessageBox.Show("登録名が既に登録されています。");
                }
                else
                {
                    var olditem = listView_List.SelectedItem as NWPresetItem;
                    if (olditem != null && olditem.Name == DefPresetStr)
                    {
                        ConnectionList.RemoveAt(listView_List.SelectedIndex);
                    }
                    ConnectionList.Insert(ConnectionList.Count - 1, newitem);
                    UpdateNWPreset();
                    listView_List.SelectedIndex = ConnectionList.Count - 2;
                }
            }
        }

        private void btn_edit_Click(object sender, RoutedEventArgs e)
        {
            NWPresetItem newitem = GetSetting();
            int pos = listView_List.SelectedIndex;
            if (pos >= 0)
            {
                ConnectionList[pos] = newitem;
                int DefPresetPos = ConnectionList.ToList().FindIndex(item => item.Name == DefPresetStr);
                if (DefPresetPos >= 0 && ConnectionList[DefPresetPos].EqualsTo(newitem, true))
                {
                    ConnectionList.RemoveAt(DefPresetPos);
                }
                UpdateNWPreset();
                listView_List.SelectedIndex = pos;
            }
        }

        private void btn_delete_Click(object sender, RoutedEventArgs e)
        {
            NWPresetItem newitem = GetSetting();
            int pos = ConnectionList.ToList().FindIndex(item => item.Name == newitem.Name);
            if (pos >= 0)
            {
                ConnectionList.RemoveAt(pos);
                UpdateNWPreset();
                listView_List.SelectedIndex = Math.Min(pos, ConnectionList.Count - 1);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsTouchedPreset)
            {
                Settings.SaveToXmlFile();
            }
        }
    }
}
