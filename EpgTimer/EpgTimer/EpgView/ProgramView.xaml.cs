﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EpgTimer.EpgView
{
    /// <summary>
    /// ProgramView.xaml の相互作用ロジック
    /// </summary>
    public partial class ProgramView : EpgTimer.UserCtrlView.PanelViewBase
    {
        protected override bool IsSingleClickOpen { get { return Settings.Instance.EpgInfoSingleClick; } }
        protected override double DragScroll { get { return Settings.Instance.DragScroll; } }
        protected override bool IsPopupEnabled { get { return Settings.Instance.EpgPopup; } }
        protected override FrameworkElement PopUp { get { return popupItem; } }

        private SortedList<DateTime, List<ProgramViewItem>> programTimeList = null;
        private List<ReserveViewItem> reserveList = null;
        private List<Rectangle> rectBorder = new List<Rectangle>();
        private ReserveViewItem resPopItem = null;

        public ProgramView()
        {
            InitializeComponent();

            base.scroll = scrollViewer;
            base.viewPanel = epgViewPanel;
            base.cnvs = canvas;
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            rectBorder.ForEach(item => canvas.Children.Remove(item));
            rectBorder.Clear();
        }

        protected override void PopupClear()
        {
            base.PopupClear();
            resPopItem = null;
        }
        protected override object GetPopupItem(Point cursorPos)
        {
            if (programTimeList == null) return null;
            if (reserveList == null) return null;

            ReserveViewItem lastresPopItem = resPopItem;
            resPopItem = reserveList.Find(pg => pg.IsPicked(cursorPos));

            if (Settings.Instance.EpgPopupResOnly == true && resPopItem == null) return null;

            int index = (int)(cursorPos.Y / epgViewPanel.Height * programTimeList.Count);
            if ((0 <= index && index < programTimeList.Count) == false) return null;

            //予約枠を通過したので同じ番組でもポップアップを書き直させる。
            if (lastresPopItem != resPopItem)
            {
                base.PopupClear();
            }

            return programTimeList.Values[index].Find(pg => pg.IsPicked(cursorPos));
        }

        protected override void SetPopup(object item)
        {
            var viewInfo = (ProgramViewItem)item;
            EpgEventInfo epgInfo = viewInfo.EventInfo;

            popupItem.Background = viewInfo.ContentColor;

            Canvas.SetLeft(popupItem, Math.Floor(viewInfo.LeftPos));
            Canvas.SetTop(popupItem, Math.Floor(viewInfo.TopPos));
            popupItem.Width = viewInfo.Width;
            popupItem.MinHeight = viewInfo.Height;

            double sizeMin = Settings.Instance.FontSizeTitle;
            double sizeTitle = Settings.Instance.FontSizeTitle;
            double sizeNormal = Settings.Instance.FontSize;
            double indentTitle = Math.Floor(sizeMin * 1.7 + 1);
            double indentNormal = Math.Floor(Settings.Instance.EpgTitleIndent ? indentTitle : 3);
            var fontNormal = new FontFamily(Settings.Instance.FontName);
            var fontTitle = new FontFamily(Settings.Instance.FontNameTitle);
            FontWeight titleWeight = Settings.Instance.FontBoldTitle == true ? FontWeights.Bold : FontWeights.Normal;

            minText.Text = (epgInfo.StartTimeFlag != 1 ? "未定" : epgInfo.start_time.Minute.ToString("d02"));
            minText.FontFamily = fontTitle;
            minText.FontSize = sizeMin;
            minText.FontWeight = titleWeight;
            minText.Foreground = CommonManager.Instance.CustTitle1Color;
            minText.Margin = new Thickness(1, 1, 0, 0);
            minText.LineHeight = Settings.Instance.FontHeightTitle;

            if (epgInfo.ShortInfo != null)
            {
                //必ず文字単位で折り返すためにZWSPを挿入  (\\w を使うと記号の間にZWSPが入らない)
                titleText.Text = System.Text.RegularExpressions.Regex.Replace(epgInfo.ShortInfo.event_name, ".", "$0\u200b");
                titleText.FontFamily = fontTitle;
                titleText.FontSize = sizeTitle;
                titleText.FontWeight = titleWeight;
                titleText.Foreground = CommonManager.Instance.CustTitle1Color;
                titleText.Margin = new Thickness(indentTitle, 1, 3, 3);
                titleText.LineHeight = Math.Max(Settings.Instance.FontHeightTitle, sizeTitle + 2);

                if (epgInfo.ShortInfo.text_char.Length == 0)
                {
                    infoText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    infoText.Visibility = Visibility.Visible;
                    infoText.Text = System.Text.RegularExpressions.Regex.Replace(epgInfo.ShortInfo.text_char, ".", "$0\u200b");
                    infoText.FontFamily = fontNormal;
                    infoText.FontSize = sizeNormal;
                    //infoText.FontWeight = FontWeights.Normal;
                    infoText.Foreground = CommonManager.Instance.CustTitle2Color;
                    infoText.Margin = new Thickness(indentNormal, 1, 3, 3);
                    infoText.LineHeight = Math.Max(Settings.Instance.FontHeight, sizeNormal + 2);
                }
            }
            else
            {
                titleText.Text = null;
                infoText.Text = null;
            }

            //予約枠の表示
            double marginEpg = 1;
            double marginRes = marginEpg + 3;
            popupItemTextArea.Margin = new Thickness(marginEpg, marginEpg - 2, marginEpg, marginEpg);
            if (resPopItem != null)
            {
                SetReserveBorder(popupItemBorder, resPopItem);
                popupItemBorder.Visibility = Visibility.Visible;
                if (Settings.Instance.ReserveRectBackground == false)
                {
                    popupItemTextArea.Margin = new Thickness(marginRes, marginRes - 1, marginRes, marginRes);
                }
            }
            else
            {
                popupItemBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void SetReserveBorder(Rectangle rect, ReserveViewItem info)
        {
            Brush color = info.BorderBrush;

            if (Settings.Instance.ReserveRectBackground == false)
            {
                rect.Opacity = 0.5;
                rect.Effect = new System.Windows.Media.Effects.DropShadowEffect() { BlurRadius = 10 };
                rect.Fill = Brushes.Transparent;
                rect.StrokeThickness = 3;
                rect.Stroke = color;
            }
            else
            {
                rect.Opacity = 0.3;
                rect.Effect = new System.Windows.Media.Effects.DropShadowEffect() { BlurRadius = 6 };
                rect.Fill = color;
            }
        }
        public void SetReserveList(List<ReserveViewItem> resList)
        {
            try
            {
                reserveList = resList;
                rectBorder.ForEach(item => canvas.Children.Remove(item));
                rectBorder.Clear();

                foreach (ReserveViewItem info in reserveList)
                {
                    var rect = new Rectangle();
                    rect.Width = info.Width;
                    rect.Height = info.Height;
                    rect.IsHitTestVisible = false;

                    SetReserveBorder(rect, info);

                    Canvas.SetLeft(rect, info.LeftPos);
                    Canvas.SetTop(rect, info.TopPos);
                    Canvas.SetZIndex(rect, 10);
                    canvas.Children.Add(rect);
                    rectBorder.Add(rect);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        public void SetProgramList(List<ProgramViewItem> programList, SortedList<DateTime, List<ProgramViewItem>> timeList, double width, double height)
        {
            try
            {
                programTimeList = timeList;
                canvas.Height = Math.Ceiling(height);
                canvas.Width = Math.Ceiling(width);
                epgViewPanel.Height = canvas.Height;
                epgViewPanel.Width = canvas.Width;
                epgViewPanel.Items = programList;
                epgViewPanel.InvalidateVisual();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

    }
}
