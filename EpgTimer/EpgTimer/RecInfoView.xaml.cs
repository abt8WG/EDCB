﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using CtrlCmdCLI;
using CtrlCmdCLI.Def;

namespace EpgTimer
{
    /// <summary>
    /// RecInfoView.xaml の相互作用ロジック
    /// </summary>
    public partial class RecInfoView : UserControl
    {
        private MenuUtil mutil = CommonManager.Instance.MUtil;
        private ViewUtil vutil = CommonManager.Instance.VUtil;
        private MenuManager mm = CommonManager.Instance.MM;
        private bool ReloadInfo = true;

        private CmdExeRecinfo mc;
        private MenuBinds mBinds = new MenuBinds();

        private ListViewController<RecInfoItem> lstCtrl = null;

        public RecInfoView()
        {
            InitializeComponent();

            try
            {
                //リストビュー関連の設定
                lstCtrl = new ListViewController<RecInfoItem>(this);
                lstCtrl.SetSavePath(mutil.GetMemberName(() => Settings.Instance.RecInfoListColumn)
                    , mutil.GetMemberName(() => Settings.Instance.RecInfoColumnHead)
                    , mutil.GetMemberName(() => Settings.Instance.RecInfoSortDirection));
                lstCtrl.SetViewSetting(listView_recinfo, gridView_recinfo, true);

                //最初にコマンド集の初期化
                mc = new CmdExeRecinfo(this);
                mc.SetFuncGetDataList(isAll => (isAll == true ? lstCtrl.dataList : lstCtrl.GetSelectedItemsList()).RecInfoList());
                mc.SetFuncSelectSingleData((noChange) =>
                {
                    var item = lstCtrl.SelectSingleItem(noChange);
                    return item == null ? null : item.RecInfo;
                });
                mc.SetFuncReleaseSelectedData(() => listView_recinfo.UnselectAll());

                //コマンド集からコマンドを登録
                mc.ResetCommandBindings(this, listView_recinfo.ContextMenu);

                //コンテキストメニューを開く時の設定
                listView_recinfo.ContextMenu.Opened += new RoutedEventHandler(mc.SupportContextMenuLoading);

                //ボタンの設定
                mBinds.View = CtxmCode.RecInfoView;
                mBinds.SetCommandToButton(button_del, EpgCmds.Delete);
                mBinds.SetCommandToButton(button_delAll, EpgCmds.DeleteAll);
                mBinds.SetCommandToButton(button_play, EpgCmds.Play);

                //メニューの作成、ショートカットの登録
                RefreshMenu();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
        public void RefreshMenu()
        {
            mBinds.ResetInputBindings(this, listView_recinfo);
            mm.CtxmGenerateContextMenu(listView_recinfo.ContextMenu, CtxmCode.RecInfoView, true);
        }
        public void SaveViewData()
        {
            lstCtrl.SaveViewDataToSettings();
        }
        /// <summary>情報の更新通知</summary>
        public void UpdateInfo()
        {
            ReloadInfo = true;
            if (ReloadInfo == true && this.IsVisible == true) ReloadInfo = !ReloadInfoData();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ReloadInfo == true && this.IsVisible == true) ReloadInfo = !ReloadInfoData();
        }
        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ReloadInfo == true && this.IsVisible == true) ReloadInfo = !ReloadInfoData();
        }
        private bool ReloadInfoData()
        {
            return lstCtrl.ReloadInfoData(dataList =>
            {
                ErrCode err = CommonManager.Instance.DB.ReloadrecFileInfo();
                if (CommonManager.CmdErrMsgTypical(err, "録画情報の取得", this) == false) return false;

                foreach (RecFileInfo info in CommonManager.Instance.DB.RecFileInfo.Values)
                {
                    dataList.Add(new RecInfoItem(info));
                }
                return true;
            });
        }
        private void listView_recinfo_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Settings.Instance.PlayDClick == true)
            {
                EpgCmds.Play.Execute(sender, this);
            }
            else
            {
                EpgCmds.ShowDialog.Execute(sender, this);
            }
        }
        //リストのカギマークからの呼び出し
        public bool ChgProtectRecInfoForMark(RecInfoItem hitItem)
        {
            if (listView_recinfo.SelectedItems.Contains(hitItem) == true)
            {
                EpgCmds.ProtectChange.Execute(listView_recinfo, this);
                return true;
            }
            return false;
        }
    }
}
