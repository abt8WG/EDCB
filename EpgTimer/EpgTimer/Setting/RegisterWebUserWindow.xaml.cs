using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EpgTimer.Setting
{
    public class auth_user
    {
        public string User { get; set; }
        public string Domain { get; set; }
        public string Hash { get; set; }

        public auth_user(string userinfo)
        {
            string[] info = userinfo.Split(':');
            if (info.Length == 3)
            {
                User = info[0];
                Domain = info[1];
                Hash = info[2];
            }
        }
        public auth_user(string user, string domain, string password)
        {
            User = user;
            Domain = domain;
            string info = user + ":" + domain + ":" + password;
            Hash = BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(info))).Replace("-", "").ToLower();
        }
    }

    /// <summary>
    /// RegisterWebUserWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class RegisterWebUserWindow : Window
    {
        private const string NewUserStr = "<新規登録>";

        private ObservableCollection<auth_user> UserList = new ObservableCollection<auth_user>();
        private string Realm;
        private bool IsUpdated = false;

        public RegisterWebUserWindow(string realm)
        {
            InitializeComponent();

            UserList = new ObservableCollection<auth_user>();
            Realm = realm;

            try
            {
                using (StreamReader sr = File.OpenText(glpasswdFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        UserList.Add(new auth_user(line));
                    }
                }
            }
            catch { }

            listView.ItemsSource = UserList;

            UserList.Add(new auth_user(NewUserStr + "::"));
            ResetUserInfo(null);
        }

        private void ResetUserInfo(auth_user user)
        {
            if (user == null || user.User == NewUserStr)
            {
                textBox_User.Text = "";
                textBox_Domain.Text = Realm;
                passwordBox_Password.Password = "";

                button_Add.IsEnabled = false;
                button_Add.Visibility = Visibility.Visible;
                button_Edit.Visibility = Visibility.Collapsed;
                button_Del.Visibility = Visibility.Collapsed;
            }
            else
            {
                textBox_User.Text = user.User;
                textBox_Domain.Text = user.Domain;
                passwordBox_Password.Password = "";

                button_Edit.IsEnabled = false;
                button_Add.Visibility = Visibility.Collapsed;
                button_Edit.Visibility = Visibility.Visible;
                button_Del.Visibility = Visibility.Visible;
            }
        }

        private string glpasswdFile { get { return Path.GetDirectoryName(SettingPath.TimerSrvIniPath) + @"\glpasswd"; } }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsUpdated)
            {
                try
                {
                    if (UserList.Count > 1)
                    {
                        using (StreamWriter sw = File.CreateText(glpasswdFile))
                        {
                            foreach (auth_user user in UserList)
                            {
                                if (user.Hash != "")
                                {
                                    sw.WriteLine(user.User + ":" + user.Domain + ":" + user.Hash);
                                }
                            }
                        }
                    }
                    else
                    {
                        File.Delete(glpasswdFile);
                    }
                }
                catch { }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        this.Close();
                        break;
                }
            }
            base.OnKeyDown(e);
        }

        private void textBox_TextChanged(object sender, RoutedEventArgs e)
        {
            bool enable = textBox_User.Text.Length > 0 && passwordBox_Password.Password.Length > 0;
            button_Add.IsEnabled = enable;
            button_Edit.IsEnabled = enable;
        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv != null)
            {
                ResetUserInfo(lv.SelectedItem as auth_user);
            }
        }

        private void button_Add_Click(object sender, RoutedEventArgs e)
        {
            if (UserList.Where(u => u.User == textBox_User.Text && u.Domain == textBox_Domain.Text).Count() == 0)
            {
                UserList.Insert(UserList.Count - 1, new auth_user(textBox_User.Text, textBox_Domain.Text, passwordBox_Password.Password));
                ResetUserInfo(null);
                IsUpdated = true;
            }
            else
            {
                MessageBox.Show("同じユーザーが存在するので追加できませんでした。");
            }
        }

        private void button_Edit_Click(object sender, RoutedEventArgs e)
        {
            UserList.Insert(listView.SelectedIndex, new auth_user(textBox_User.Text, textBox_Domain.Text, passwordBox_Password.Password));
            UserList.RemoveAt(listView.SelectedIndex);
            ResetUserInfo(null);
            IsUpdated = true;
        }

        private void button_Del_Click(object sender, RoutedEventArgs e)
        {
            int index = listView.SelectedIndex;
            string user = UserList[index].User;
            if (MessageBox.Show(user + " を消してもよいですか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                UserList.RemoveAt(index);
                ResetUserInfo(null);
                IsUpdated = true;
            }
        }
    }
}
