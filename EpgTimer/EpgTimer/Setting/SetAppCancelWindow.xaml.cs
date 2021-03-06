﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace EpgTimer
{
    using BoxExchangeEdit;

    /// <summary>
    /// SetAppCancelWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SetAppCancelWindow : Window
    {
        public List<String> processList = new List<string>();
        public String ngMin = "30";
        public bool ngUsePC = false;
        public String ngUsePCMin = "3";
        public bool ngFileStreaming = false;
        public bool ngShareFile = false;

        public SetAppCancelWindow()
        {
            InitializeComponent();

            //button_process_del.IsEnabled = IniFileHandler.CanUpdateInifile;
            //label2.IsEnabled = IniFileHandler.CanUpdateInifile;
            //textBox_process.IsEnabled = IniFileHandler.CanUpdateInifile;
            //button_process_add.IsEnabled = IniFileHandler.CanUpdateInifile;
            //button_process_open.IsEnabled = IniFileHandler.CanUpdateInifile;
            //label3.IsEnabled = IniFileHandler.CanUpdateInifile;
            //textBox_ng_min.IsEnabled = IniFileHandler.CanUpdateInifile;
            //label4.IsEnabled = IniFileHandler.CanUpdateInifile;
            //checkBox_ng_fileStreaming.IsEnabled = IniFileHandler.CanUpdateInifile;
            //checkBox_ng_shareFile.IsEnabled = IniFileHandler.CanUpdateInifile;
            //textBox_ng_usePC_min.IsEnabled = IniFileHandler.CanUpdateInifile;
            //label7.IsEnabled = IniFileHandler.CanUpdateInifile;
            //checkBox_ng_usePC.IsEnabled = IniFileHandler.CanUpdateInifile;
            //button_OK.IsEnabled = IniFileHandler.CanUpdateInifile;
            ViewUtil.ChangeChildren(grid_main, IniFileHandler.CanUpdateInifile);
            //listBox_process.IsEnabled = true;
            //ViewUtil.DisableTextBoxWithMenu(textBox_process);
            //button_cancel.IsEnabled = true;

            var bx = new BoxExchangeEditor(null, listBox_process, true);
            listBox_process.SelectionChanged += ViewUtil.ListBox_TextBoxSyncSelectionChanged(listBox_process, textBox_process);
            if (CommonManager.Instance.NWMode == false)
            {
                bx.AllowKeyAction();
                bx.AllowDragDrop();
                button_process_del.Click += new RoutedEventHandler(bx.button_Delete_Click);
                button_process_add.Click += ViewUtil.ListBox_TextCheckAdd(listBox_process, textBox_process);
                textBox_process.KeyDown += ViewUtil.KeyDown_Enter(button_process_add);
            }
        }

        private void button_process_open_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.GetFileNameByDialog(textBox_process, true, "", ".exe");
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            processList = listBox_process.Items.OfType<string>().ToList();
            ngMin = textBox_ng_min.Text;
            ngUsePC = (checkBox_ng_usePC.IsChecked == true);
            ngUsePCMin = textBox_ng_usePC_min.Text;
            ngFileStreaming = (checkBox_ng_fileStreaming.IsChecked == true);
            ngShareFile = (checkBox_ng_shareFile.IsChecked == true);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            listBox_process.Items.AddItems(processList);
            textBox_ng_min.Text = ngMin;
            checkBox_ng_usePC.IsChecked = ngUsePC;
            textBox_ng_usePC_min.Text = ngUsePCMin;
            checkBox_ng_fileStreaming.IsChecked = ngFileStreaming;
            checkBox_ng_shareFile.IsChecked = ngShareFile;
        }

        private void button_cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
