﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetDefRecSettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SetDefRecSettingWindow : Window
    {
        public SetDefRecSettingWindow()
        {
            InitializeComponent();

            if (IniFileHandler.CanUpdateInifile == false)
            {
                label1.Content += "　(EpgTimerNWからはプリセットの設定は出来ません)";
            }
        }

        public void SetSettingMode(string title = "")
        {
            Title = (title == "") ? "録画設定変更" : title;
            button_ok.Visibility = Visibility.Visible;
            button_cancel.Content = "キャンセル";
        }

        private void button_ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void button_cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
