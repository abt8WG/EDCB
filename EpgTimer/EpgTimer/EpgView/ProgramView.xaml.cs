using System;
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
        protected override bool IsMouseScrollAuto { get { return Settings.Instance.MouseScrollAuto; } }
        protected override double ScrollSize { get { return Settings.Instance.ScrollSize; } }
        protected override bool IsPopEnabled { get { return Settings.Instance.EpgPopup == true; } }
        protected override bool PopOnOver { get { return Settings.Instance.EpgPopupMode != 1; } }
        protected override bool PopOnClick { get { return Settings.Instance.EpgPopupMode != 0; } }
        protected override FrameworkElement Popup { get { return popupItem; } }
        protected override double PopWidth { get { return Settings.Instance.ServiceWidth; } }

        private List<ReserveViewItem> reserveList = null;
        private List<Rectangle> rectBorder = new List<Rectangle>();
        private ReserveViewItem popInfoRes = null;

        /*/ tooltip suppport
        private DispatcherTimer toolTipTimer;
        private DispatcherTimer toolTipOffTimer; 
        private Popup toolTip = new Popup();
		*/

        public ProgramView()
        {
            InitializeComponent();

            base.scroll = scrollViewer;
            base.cnvs = canvas;

            /*/ tooltip suppport
            toolTipTimer = new DispatcherTimer(DispatcherPriority.Normal);
            toolTipTimer.Tick += new EventHandler(toolTipTimer_Tick);
            toolTipOffTimer = new DispatcherTimer(DispatcherPriority.Normal);
            toolTipOffTimer.Tick += new EventHandler(toolTipOffTimer_Tick);

            toolTip.Placement = PlacementMode.MousePoint;
            toolTip.PopupAnimation = PopupAnimation.Fade;
            toolTip.PlacementTarget = canvas;
            toolTip.AllowsTransparency = true;
            toolTip.MouseLeftButtonDown += new MouseButtonEventHandler(toolTip_MouseLeftButtonDown);
            toolTip.MouseRightButtonDown += new MouseButtonEventHandler(toolTip_MouseRightButtonDown);
            toolTip.PreviewMouseWheel += new MouseWheelEventHandler(toolTip_PreviewMouseWheel);
			*/
        }

        public override void ClearInfo()
        {
            base.ClearInfo();
            reserveList = null;
            rectBorder.ForEach(item => canvas.Children.Remove(item));
            rectBorder.Clear();

            for (int i = 0; i < canvas.Children.Count; i++)
            {
                if (canvas.Children[i] is EpgViewPanel)
                {
                    canvas.Children.RemoveAt(i--);
                }
            }

            /* tooltip support
            toolTipTimer.Stop();
            toolTip.IsOpen = false;
			*/
        }

        protected override void PopupClear()
        {
            base.PopupClear();
            popInfoRes = null;
        }
        protected override ViewPanelItemBase GetPopupItem(Point cursorPos, bool onClick)
        {
            ProgramViewItem popInfo = GetProgramViewData(cursorPos);
            ReserveViewItem lastPopInfoRes = popInfoRes;
            popInfoRes = reserveList == null ? null : reserveList.Find(pg => pg.IsPicked(cursorPos));

            if (Settings.Instance.EpgPopupMode == 2 && popInfoRes == null && (
                onClick == false && !(lastPopInfoRes == null && popInfo == lastPopInfo) ||
                onClick == true && lastPopInfo != null)) return null;

            //予約枠を通過したので同じ番組でもポップアップを書き直させる。
            if (lastPopInfoRes != popInfoRes)
            {
                base.PopupClear();
            }

            return popInfo;
        }

        protected override void SetPopup(ViewPanelItemBase item)
        {
            base.SetPopup(item);

            var viewInfo = (ProgramViewItem)item;
            EpgEventInfo epgInfo = viewInfo.EventInfo;

            popupItem.Background = viewInfo.ContentColor;

            double sizeMin = Settings.Instance.FontSizeTitle - 1;
            double sizeTitle = Settings.Instance.FontSizeTitle;
            double sizeNormal = Settings.Instance.FontSize;
            double indentTitle = Math.Floor(sizeMin * 1.7 + 1);
            double indentNormal = Math.Floor(Settings.Instance.EpgTitleIndent ? indentTitle : 3);
            var fontNormal = new FontFamily(Settings.Instance.FontName);
            var fontTitle = new FontFamily(Settings.Instance.FontNameTitle);
            FontWeight titleWeight = Settings.Instance.FontBoldTitle == true ? FontWeights.Bold : FontWeights.Normal;

            minText.Text = (epgInfo.StartTimeFlag == 0 ? "未定" : epgInfo.start_time.Minute.ToString("d02"));
            minText.FontFamily = fontTitle;
            minText.FontSize = sizeMin;
            minText.FontWeight = titleWeight;
            minText.Foreground = CommonManager.Instance.CustTitle1Color;
            minText.Margin = new Thickness(1, 1, 0, 0);
            minText.LineHeight = Settings.Instance.FontHeightTitle;

            if (epgInfo.ShortInfo != null)
            {
                //必ず文字単位で折り返すためにZWSPを挿入 (\\w を使うと記号の間にZWSPが入らない)
                titleText.Text = System.Text.RegularExpressions.Regex.Replace(epgInfo.ShortInfo.event_name, ".", "$0\u200b");
                titleText.FontFamily = fontTitle;
                titleText.FontSize = sizeTitle;
                titleText.FontWeight = titleWeight;
                titleText.Foreground = CommonManager.Instance.CustTitle1Color;
                titleText.Margin = new Thickness(indentTitle, 1, 0, 3);
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
                    infoText.Margin = new Thickness(indentNormal, 1, 0, 3);
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
            popupItemTextArea.Margin = new Thickness(marginEpg, marginEpg - 2, marginEpg + 3, marginEpg);
            if (popInfoRes != null)
            {
                SetReserveBorder(popupItemBorder, popInfoRes);
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

        public ProgramViewItem GetProgramViewData(Point cursorPos)
        {
            try
            {
                foreach (var childPanel in canvas.Children.OfType<EpgViewPanel>())
                {
                    if (childPanel.Items != null && Canvas.GetLeft(childPanel) <= cursorPos.X && cursorPos.X < Canvas.GetLeft(childPanel) + childPanel.Width)
                    {
                        return childPanel.Items.OfType<ProgramViewItem>().FirstOrDefault(pg => pg.IsPicked(cursorPos));
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }

            return null;
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

                PopUpWork();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        public void SetProgramList(List<ProgramViewItem> programList, double width, double height)
        {
            var programGroupList = new List<Tuple<double, List<ProgramViewItem>>>();
            programGroupList.Add(new Tuple<double, List<ProgramViewItem>>(width, programList));
            SetProgramList(programGroupList, height);
        }

        public void SetProgramList(List<Tuple<double, List<ProgramViewItem>>> programGroupList, double height)
        {
            try
            {
                for (int i = 0; i < canvas.Children.Count; i++)
                {
                    if (canvas.Children[i] is EpgViewPanel)
                    {
                        canvas.Children.RemoveAt(i--);
                    }
                }
                var itemFontNormal = ViewUtil.ItemFontNormal;
                var itemFontTitle = ViewUtil.ItemFontTitle;
                double totalWidth = 0;
                foreach (var programList in programGroupList)
                {
                    EpgViewPanel item = new EpgViewPanel();
                    item.Background = epgViewPanel.Background;
                    item.Height = Math.Ceiling(height);
                    item.Width = programList.Item1;
                    item.ItemFontNormal = itemFontNormal;
                    item.ItemFontTitle = itemFontTitle;
                    Canvas.SetLeft(item, totalWidth);
                    item.Items = programList.Item2;
                    item.InvalidateVisual();
                    canvas.Children.Add(item);
                    totalWidth += programList.Item1;
                }
                canvas.Height = Math.Ceiling(height);
                canvas.Width = totalWidth;
                itemFontNormal.ClearCache();
                itemFontTitle.ClearCache();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

#if false
		void toolTip_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            toolTip.IsOpen = false;
            RaiseEvent(e);
        }

        void toolTip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                toolTip.IsOpen = false;
                if (LeftDoubleClick != null)
                {
                    LeftDoubleClick(sender, lastPopupPos);
                }
            }
            else if (Settings.Instance.EpgInfoSingleClick)
            {
                toolTip.IsOpen = false;
            }
        }

        void toolTip_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            toolTip.IsOpen = false;
            if (RightClick != null)
            {
                RightClick(sender, lastPopupPos);
            }
        }

        void toolTipOffTimer_Tick(object sender, EventArgs e)
        {
            toolTipOffTimer.Stop();
            toolTip.IsOpen = false;
        }

        void toolTipTimer_Tick(object sender, EventArgs e)
        {
            toolTipTimer.Stop();
            try
            {
                if (Settings.Instance.EpgPopup || Settings.Instance.EpgToolTip == false)
                {
                    return;
                }
                if (Window.GetWindow(this).IsActive == false)
                {
                    return;
                }
                Point cursorPos2 = Mouse.GetPosition(scrollViewer);
                if (cursorPos2.X < 0 || cursorPos2.Y < 0 ||
                    scrollViewer.ViewportWidth < cursorPos2.X || scrollViewer.ViewportHeight < cursorPos2.Y)
                {
                    return;
                }
                Point cursorPos = Mouse.GetPosition(canvas);
                foreach (UIElement child in canvas.Children)
                {
                    EpgViewPanel childPanel = child as EpgViewPanel;
                    if (childPanel != null && childPanel.Items != null && Canvas.GetLeft(child) <= cursorPos.X && cursorPos.X < Canvas.GetLeft(child) + childPanel.Width)
                    {
                        foreach (ProgramViewItem info in childPanel.Items)
                        {
                            if (info.LeftPos <= cursorPos.X && cursorPos.X < info.LeftPos + info.Width &&
                                info.TopPos <= cursorPos.Y && cursorPos.Y < info.TopPos + info.Height)
                            {
                                if (info.TitleDrawErr == false && Settings.Instance.EpgToolTipNoViewOnly == true)
                                {
                                    break;
                                }
                                String viewTip = "";

                                if (info != null)
                                {
                                    if (info.EventInfo.StartTimeFlag == 1)
                                    {
                                        viewTip += info.EventInfo.start_time.ToString("yyyy/MM/dd(ddd) HH:mm:ss ～ ");
                                    }
                                    else
                                    {
                                        viewTip += "未定 ～ ";
                                    }
                                    if (info.EventInfo.DurationFlag == 1)
                                    {
                                        DateTime endTime = info.EventInfo.start_time + TimeSpan.FromSeconds(info.EventInfo.durationSec);
                                        viewTip += endTime.ToString("yyyy/MM/dd(ddd) HH:mm:ss") + "\r\n";
                                    }
                                    else
                                    {
                                        viewTip += "未定\r\n";
                                    }

                                    if (info.EventInfo.ShortInfo != null)
                                    {
                                        viewTip += info.EventInfo.ShortInfo.event_name + "\r\n\r\n";
                                        viewTip += info.EventInfo.ShortInfo.text_char + "\r\n\r\n";
                                    }
                                    if (info.EventInfo.ExtInfo != null)
                                    {
                                        viewTip += info.EventInfo.ExtInfo.text_char;
                                    }
                                }
                                Border border = new Border();
                                border.Background = Brushes.DarkGray;

                                TextBlock block = new TextBlock();
                                block.Text = viewTip;
                                block.MaxWidth = 400;
                                block.TextWrapping = TextWrapping.Wrap;
                                block.Margin = new Thickness(2);

								/*
								block.Background = CommonManager.Instance.EpgTipsBackColor;
                                block.Foreground = CommonManager.Instance.EpgTipsForeColor;
                                border.Child = block;
                                toolTip.Child = border;
                                toolTip.IsOpen = true;
                                toolTipOffTimer.Stop();
                                toolTipOffTimer.Interval = TimeSpan.FromSeconds(10);
                                toolTipOffTimer.Start();

                                lastPopupInfo = info;
                                lastPopupPos = cursorPos;
								*/
							}
						}
                        break;
                    }
                }
            }
            catch
            {
                //toolTip.IsOpen = false;
            }
        }
#endif

	}
}
