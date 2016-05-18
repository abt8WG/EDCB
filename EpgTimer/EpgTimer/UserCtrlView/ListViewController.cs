﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;

using System.Reflection;

namespace EpgTimer
{
    public class GridViewColumnList : List<GridViewColumn> { }

    public class ListViewController<T> where T : class
    {
        private MenuUtil mutil = CommonManager.Instance.MUtil;
        private ViewUtil vutil = CommonManager.Instance.VUtil;

        public GridViewSelector gvSelector { get; private set; }
        public GridViewSorter gvSorter { get; private set; }
        public List<T> dataList { get; set; }

        private ListBoxItem ClickTarget = null;

        private string column_SavePath = null;
        private string sort_HeaderSavePath = null;
        private string sort_DirectionSavePath = null;
        private bool IsSortViewOnReload = false;

        private string initialSortKey = null;
        private ListSortDirection initialDirection = ListSortDirection.Ascending;

        private ListView listView = null;
        private GridView gridView = null;

        private Control Owner;

        public ListViewController(Control owner)
        {
            Owner = owner;
            dataList = new List<T>();
        }

        public void SetSelectionChangedEventHandler(SelectionChangedEventHandler hdlr = null)
        {
            if (hdlr == null) return;

            bool onSelectionChanging = false;
            listView.SelectionChanged += (sender, e) =>
            {
                if (onSelectionChanging == true) return;
                onSelectionChanging = true;

                //リスト更新中などに何度も走らないようにしておく。
                //リストビュー自体に遅延実行があるので、イベントハンドラ外しても効果薄いため。
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    hdlr(sender, e);
                    onSelectionChanging = false;
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            };
        }
        public void SetSavePath(string columnSavePath, string sortHeaderSavePath = null, string sortDirectionSavePath = null)
        {
            column_SavePath = columnSavePath;
            sort_HeaderSavePath = sortHeaderSavePath;
            sort_DirectionSavePath = sortDirectionSavePath;

            object direction = Settings.Instance.GetSettings(sort_DirectionSavePath);
            SetInitialSortKey(Settings.Instance.GetSettings(sort_HeaderSavePath) as string,
                direction != null ? (ListSortDirection)direction : ListSortDirection.Ascending);
        }
        public void SetInitialSortKey(string sortKsy, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            initialSortKey = sortKsy;
            initialDirection = sortDirection;
            gvInitialSort();
        }
        private void gvInitialSort()
        {
            //アイテム無くてもカラムに強調表示が付く
            if (gvSorter == null || gridView == null) return;
            this.gvSorter.SortByMultiHeaderWithKey(dataList, gridView.Columns, initialSortKey, true, initialDirection);
        }
        public void SetViewSetting(ListView lv, GridView gv, bool defaultContextMenu, bool isSortOnReload
            , List<GridViewColumn> cols_source = null, RoutedEventHandler headerclick = null, bool hasSortFeature = true)
        {
            listView = lv;
            gridView = gv;
            IsSortViewOnReload = isSortOnReload;

            //グリッド列の差し込み。クリアせずに追加する。
            if (cols_source != null)
            {
                cols_source.ForEach(col => gridView.Columns.Add(col));
            }

            if (hasSortFeature)
            {
                var hclick = headerclick != null ? headerclick : (sender, e) => this.GridViewHeaderClickSort(e);
                foreach (GridViewColumn info in gridView.Columns)
                {
                    var header = info.Header as GridViewColumnHeader;
                    header.Click += new RoutedEventHandler(hclick);
                    if (header.ToolTip == null)
                    {
                        header.ToolTip = "Ctrl+Click(マルチ・ソート)、Shift+Click(解除)";
                    }
                }
            }

            if (defaultContextMenu == true)
            {
                if (lv.ContextMenu == null) lv.ContextMenu = new ContextMenu();

                lv.ContextMenu.Opened += (sender, e) =>
                {
                    //コンテキストメニューを開いたとき、アイテムがあればそれを保存する。無ければNULLになる。
                    var lb = (sender as ContextMenu).PlacementTarget as ListBox;
                    if (lb != null) ClickTarget = lb.PlacementItem();
                };
                lv.ContextMenu.Closed += (sender, e) => ClickTarget = null;
            }

            gvSelector = new GridViewSelector(gv, this.columnSaveList);
            gvSorter = new GridViewSorter();
            gvInitialSort();

            //アイテムの無い場所でクリックしたとき、選択を解除する。
            listView.MouseLeftButtonUp += new MouseButtonEventHandler((sender, e) =>
            {
                if (listView.InputHitTest(e.GetPosition(listView)) is ScrollViewer)//本当にこれで良いのだろうか？
                {
                    listView.UnselectAll();
                }
            });

            //Escapeキーで選択を解除する。
            listView.KeyDown += new KeyEventHandler((sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    switch (e.Key)
                    {
                        case Key.Escape:
                            if (listView.SelectedItem != null)
                            {
                                listView.UnselectAll();
                                e.Handled = true;
                            }
                            break;
                    }
                }
            });
        }

        public void SetSelectedItemDoubleClick(RoutedCommand cmd)
        {
            if (cmd == null) return;
            SetSelectedItemDoubleClick((sender, e) => cmd.Execute(sender, this.Owner));
        }

        public void SetSelectedItemDoubleClick(MouseButtonEventHandler hdlr)
        {
            if (hdlr == null) return;
            listView.MouseDoubleClick += new MouseButtonEventHandler((sender, e) =>
            {
                var hitItem = listView.PlacementItem(e.GetPosition(listView));
                if (hitItem != null) hdlr(hitItem, e);
            });
        }

        public void SaveViewDataToSettings()
        {
            try
            {
                gvSelector.SaveSize(this.columnSaveList);
                Settings.Instance.SetSettings(sort_HeaderSavePath, this.gvSorter.LastHeader);
                Settings.Instance.SetSettings(sort_DirectionSavePath, this.gvSorter.LastDirection);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }
        private List<ListColumnInfo> columnSaveList
        {
            get { return Settings.Instance.GetSettings(column_SavePath) as List<ListColumnInfo>; }
        }

        public bool ReloadInfoData(Func<List<T>, bool> reloadData)
        {
            try
            {
                //更新前の選択情報の保存
                var oldItems = new ListViewSelectedKeeper(listView, true);

                listView.ItemsSource = null;
                dataList.Clear();

                if (CommonManager.Instance.IsConnected == false) return false;

                if (reloadData(dataList) == false) return false;

                if (IsSortViewOnReload == true)
                {
                    this.gvSorter.SortByMultiHeader(dataList);
                }
                else
                {
                    this.gvSorter.ResetSortParams();
                }

                listView.ItemsSource = dataList;

                //選択情報の復元
                oldItems.RestoreListViewSelected();
                return true;
            }
            catch (Exception ex) { CommonUtil.ModelessMsgBoxShow(Owner, ex.Message + "\r\n" + ex.StackTrace); }
            return false;
        }

        public bool GridViewHeaderClickSort(RoutedEventArgs e)
        {
            try
            {
                GridViewColumnHeader headerClicked1 = e.OriginalSource as GridViewColumnHeader;
                if (headerClicked1 != null)
                {
                    if (headerClicked1.Role != GridViewColumnHeaderRole.Padding)
                    {
                        // ソートの実行。無効列の場合ソートはされないが、shiftクリックのマルチソート解除は実行する。
                        if (gvSorter.SortByMultiHeader(dataList, headerClicked1) == true)
                        {
                            listView.Items.Refresh();
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
            return false;
        }

        public List<T> GetSelectedItemsList()
        {
            return listView.SelectedItems.OfType<T>().ToList();
        }
        public T SelectSingleItem(bool notSelectionChange = false)
        {
            if (listView == null) return null;
            if (listView.SelectedItems.Count <= 1) return listView.SelectedItem as T;//SingleMode用

            object item;
            if (ClickTarget == null)
            {
                //保存情報が無ければ、最後に選択したもの。
                item = listView.SelectedItems[listView.SelectedItems.Count - 1];
            }
            else
            {
                item = ClickTarget.Content;
            }
            if (notSelectionChange == false)
            {
                listView.UnselectAll();
                listView.SelectedItem = item;
            }
            return item as T;
        }

        //リストのチェックボックスからの呼び出し
        public void ChgOnOffFromCheckbox(object hitItem, RoutedCommand cmd)
        {
            if (listView.SelectedItems.Contains(hitItem) == false)
            {
                listView.SelectedItem = hitItem;
            }
            cmd.Execute(listView, this.Owner);
        }
    }
}
