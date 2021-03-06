﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EpgTimer.UserCtrlView
{
    public class PanelBase : FrameworkElement //とりあえず。TunerReservePanelとEpgViewPanel結構実装違う
    {
        public static readonly DependencyProperty BackgroundProperty =
            Panel.BackgroundProperty.AddOwner(typeof(PanelBase));

        public Brush Background
        {
            set { SetValue(BackgroundProperty, value); }
            get { return (Brush)GetValue(BackgroundProperty); }
        }
    }
    
    public class PanelViewBase : UserControl
    {
        public delegate void PanelViewClickHandler(object sender, Point cursorPos);
        public event PanelViewClickHandler RightClick = null;
        public event PanelViewClickHandler LeftDoubleClick = null;
        public event ScrollChangedEventHandler ScrollChanged = null;

        protected Point lastDownMousePos;
        protected double lastDownHOffset;
        protected double lastDownVOffset;
        protected bool isDrag = false;
        protected bool isDragMoved = false;

        protected virtual bool IsSingleClickOpen { get { return false; } }
        protected virtual double DragScroll { get { return 1; } }
        protected virtual bool IsMouseScrollAuto { get { return false; } }
        protected virtual double ScrollSize { get { return 240; } }

        protected virtual bool IsPopEnabled { get { return false; } }
        protected virtual bool PopOnOver { get { return false; } }
        protected virtual bool PopOnClick { get { return false; } }
        protected virtual ViewPanelItemBase GetPopupItem(Point cursorPos, bool onClick) { return null; }
        protected virtual FrameworkElement Popup { get { return new FrameworkElement(); } }
        protected ViewPanelItemBase lastPopInfo = null;
        protected virtual double PopWidth { get { return 150; } }
        protected ScrollViewer scroll;
        protected Canvas cnvs;

        protected virtual bool IsTooltipEnabled { get { return false; } }
        protected virtual int TooltipViweWait { get { return 0; } }
        protected Rectangle Tooltip { get; private set; }
        protected ViewPanelItemBase lastToolInfo = null;
        private DispatcherTimer toolTipTimer;//連続して出現するのを防止する
        protected virtual ViewPanelItemBase GetTooltipItem(Point cursorPos) { return null; }

        public PanelViewBase()
        {
            toolTipTimer = new DispatcherTimer(DispatcherPriority.Normal);
            toolTipTimer.Tick += new EventHandler(toolTipTimer_Tick);

            Tooltip = new Rectangle();
            Tooltip.Fill = Brushes.Transparent;
            Tooltip.Stroke = Brushes.Transparent;
            //Tooltip.ToolTipClosing += new ToolTipEventHandler((sender, e) => TooltipClear());//何度でも出せるようにする
            ToolTipService.SetShowDuration(Tooltip, 300000);
            ToolTipService.SetInitialShowDelay(Tooltip, 0);
        }
        public virtual void ClearInfo()
        {
            cnvs.ReleaseMouseCapture();
            isDrag = false;
            isDragMoved = false;

            cnvs.Height = 0;
            cnvs.Width = 0;

            PopupClear();
            TooltipClear();
        }

        protected virtual void PopupClear()
        {
            Popup.Visibility = Visibility.Hidden;
            lastPopInfo = null;
        }
        protected virtual void PopUpWork(bool onClick = false)
        {
            if (IsPopEnabled == false || PopOnOver == false && PopOnClick == false) return;

            try
            {
                Point cursorPos = Mouse.GetPosition(scroll);
                if (PopOnOver == false && onClick == true && lastPopInfo != null ||
                    cursorPos.X < 0 || cursorPos.Y < 0 ||
                    scroll.ViewportWidth < cursorPos.X || scroll.ViewportHeight < cursorPos.Y)
                {
                    PopupClear();
                    return;
                }

                ViewPanelItemBase popInfo = GetPopupItem(Mouse.GetPosition(cnvs), onClick);
                if (popInfo != lastPopInfo)
                {
                    lastPopInfo = popInfo;

                    if (popInfo == null || PopOnOver == false && onClick == false)
                    {
                        PopupClear();
                        return;
                    }

                    SetPopupItem(popInfo);
                    Popup.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex) 
            {
                PopupClear();
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
        // PopUpの初期化
        protected void SetPopupItem(ViewPanelItemBase popInfo)
        {
            UpdatePopupPosition(popInfo);

            Popup.Width = Math.Max(16, PopWidth);
            if (popInfo.TopPos < scroll.ContentVerticalOffset)
            {
                Popup.MinHeight = Math.Max(0, popInfo.TopPos + popInfo.Height - scroll.ContentVerticalOffset);
            }
            else
            {
                Popup.MinHeight = Math.Max(0, Math.Min(scroll.ContentVerticalOffset + scroll.ViewportHeight - popInfo.TopPos, popInfo.Height));
            }

            SetPopup(popInfo);
        }
        protected virtual void SetPopup(ViewPanelItemBase popInfo) { }

        // PopUp が画面内に収まるように調整する
        protected void UpdatePopupPosition(ViewPanelItemBase popInfo)
        {
            // ツールチップ表示中だったら消す
            TooltipClear();

            // offsetHが正のとき右にはみ出している
            double offsetH = popInfo.LeftPos + Popup.ActualWidth - (scroll.ContentHorizontalOffset + scroll.ViewportWidth);
            // 右にはみ出した分だけ左にずらす
            double left = popInfo.LeftPos - Math.Max(0, offsetH);
            // 左にはみ出てる場合はscrollエリアの左端から表示する
            Canvas.SetLeft(Popup, Math.Floor(Math.Max(left, scroll.ContentHorizontalOffset)));

            // offsetVが正のとき下にはみ出している
            double offsetV = popInfo.TopPos + Popup.ActualHeight - (scroll.ContentVerticalOffset + scroll.ViewportHeight);
            // 下にはみ出した分だけ上にずらす
            double top = popInfo.TopPos - Math.Max(0, offsetV);
            // 上にはみ出てる場合はscrollエリアの上端から表示する
            Canvas.SetTop(Popup, Math.Floor(Math.Max(top, scroll.ContentVerticalOffset - 1)));
        }
        // PopUp の ActualWidth と ActualHeight を取得するために SizeChanged イベントを捕捉する
        protected virtual void popupItem_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (lastPopInfo != null)
            {
                UpdatePopupPosition(lastPopInfo);
            }
        }

        protected virtual void TooltipClear()
        {
            cnvs.Children.Remove(Tooltip);
            Tooltip.Visibility = Visibility.Hidden;
            lastToolInfo = null;
            toolTipTimer.Stop();
        }
        protected virtual void TooltipWork()
        {
            if (IsTooltipEnabled == false) return;

            try
            {
                ViewPanelItemBase toolInfo = GetTooltipItem(Mouse.GetPosition(cnvs));
                if (toolInfo != lastToolInfo)
                {
                    TooltipClear();
                    if (toolInfo == null) return;

                    // ポップアップ表示中だったらツールチップを出さない
                    if (Popup.Visibility == Visibility.Visible) return;

                    lastToolInfo = toolInfo;

                    //ToolTipService.SetBetweenShowDelay()がいまいち思い通り動かないので、タイマーを挟む
                    toolTipTimer.Interval = TimeSpan.FromMilliseconds(TooltipViweWait);
                    toolTipTimer.Start();
                }
            }
            catch (Exception ex) 
            {
                TooltipClear();
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); 
            }
        }
        void toolTipTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                toolTipTimer.Stop();
                cnvs.Children.Remove(Tooltip);
                SetTooltipItem(lastToolInfo);
                cnvs.Children.Add(Tooltip);
            }
            catch (Exception ex)
            {
                TooltipClear();
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
        //Tooltipの初期化
        protected void SetTooltipItem(ViewPanelItemBase toolInfo)
        {
            Tooltip.Width = toolInfo.Width;
            Tooltip.Height = toolInfo.Height;
            Canvas.SetLeft(Tooltip, Math.Floor(toolInfo.LeftPos));
            Canvas.SetTop(Tooltip, Math.Floor(toolInfo.TopPos));

            bool b = SetTooltip(toolInfo);
            Tooltip.Visibility = b ? Visibility.Visible : Visibility.Hidden;
            return;
        }
        protected virtual bool SetTooltip(ViewPanelItemBase toolInfo) { return true; }

        /// <summary>マウスホイールイベント呼び出し</summary>
        protected virtual void scrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                e.Handled = true;

                if (IsMouseScrollAuto == true)
                {
                    scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta);
                }
                else
                {
                    if (e.Delta < 0)
                    {
                        //下方向
                        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + ScrollSize);
                    }
                    else
                    {
                        //上方向
                        if (scroll.VerticalOffset < ScrollSize)
                        {
                            scroll.ScrollToVerticalOffset(0);
                        }
                        else
                        {
                            scroll.ScrollToVerticalOffset(scroll.VerticalOffset - ScrollSize);
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        protected virtual void scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ScrollChanged != null)
            {
                scroll.ScrollToHorizontalOffset(Math.Floor(scroll.HorizontalOffset));
                scroll.ScrollToVerticalOffset(Math.Floor(scroll.VerticalOffset));
                ScrollChanged(this, e);
            }
        }

        public void view_ScrollChanged(ScrollViewer main_scroll, ScrollViewer v_scroll, ScrollViewer h_scroll)
        {
            try
            {
                //時間軸の表示もスクロール
                v_scroll.ScrollToVerticalOffset(main_scroll.VerticalOffset);
                //サービス名表示もスクロール
                h_scroll.ScrollToHorizontalOffset(main_scroll.HorizontalOffset);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        protected virtual void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && isDrag == true)
                {
                    isDragMoved = true;

                    Point CursorPos = Mouse.GetPosition(null);
                    double MoveX = lastDownMousePos.X - CursorPos.X;
                    double MoveY = lastDownMousePos.Y - CursorPos.Y;

                    double OffsetH = 0;
                    double OffsetV = 0;
                    MoveX *= DragScroll;
                    MoveY *= DragScroll;
                    OffsetH = lastDownHOffset + MoveX;
                    OffsetV = lastDownVOffset + MoveY;
                    if (OffsetH < 0) OffsetH = 0;
                    if (OffsetV < 0) OffsetV = 0;

                    scroll.ScrollToHorizontalOffset(Math.Floor(OffsetH));
                    scroll.ScrollToVerticalOffset(Math.Floor(OffsetV));
                }
                else
                {
                    PopUpWork();
                    TooltipWork();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }
        protected virtual void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                lastDownMousePos = Mouse.GetPosition(null);
                lastDownHOffset = scroll.HorizontalOffset;
                lastDownVOffset = scroll.VerticalOffset;
                cnvs.CaptureMouse();
                isDrag = true;
                isDragMoved = false;

                if (e.ClickCount == 2)
                {
                    Point cursorPos = Mouse.GetPosition(cnvs);
                    if (LeftDoubleClick != null)
                    {
                        LeftDoubleClick(sender, cursorPos);
                    }
                }
                else if (PopOnClick == true)
                {
                    PopUpWork(true);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }
        protected virtual void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                cnvs.ReleaseMouseCapture();
                isDrag = false;
                if (isDragMoved == false)
                {
                    if (IsSingleClickOpen == true)
                    {
                        Point cursorPos = Mouse.GetPosition(cnvs);
                        if (LeftDoubleClick != null)
                        {
                            LeftDoubleClick(sender, cursorPos);
                        }
                    }
                }
                isDragMoved = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        protected virtual void canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            cnvs.ReleaseMouseCapture();
            isDrag = false;
            lastDownMousePos = Mouse.GetPosition(null);
            lastDownHOffset = scroll.HorizontalOffset;
            lastDownVOffset = scroll.VerticalOffset;
            if (e.ClickCount == 1)
            {
                Point cursorPos = Mouse.GetPosition(cnvs);
                if (RightClick != null)
                {
                    RightClick(sender, cursorPos);
                }
            }
        }
        protected virtual void canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (IsPopEnabled == true)
            {
                //右クリック時はポップアップを維持
                if (e.RightButton != MouseButtonState.Pressed)
                {
                    PopupClear();
                }
            }
            if (IsTooltipEnabled == true)
            {
                TooltipClear();
            }
        }

        public virtual void ScrollToFindItem(ViewPanelItemBase target_item, bool IsMarking)
        {
            try
            {
                //可能性低いが0では無さそう
                if (target_item == null) return;

                scroll.ScrollToHorizontalOffset(target_item.LeftPos - 100);
                scroll.ScrollToVerticalOffset(target_item.TopPos - 100);

                //マーキング要求のあるとき
                if (IsMarking == true)
                {
                    var rect = new Rectangle();

                    rect.Stroke = Brushes.Red;
                    rect.StrokeThickness = 5;
                    rect.Opacity = 1;
                    rect.Fill = Brushes.Transparent;
                    rect.Effect = new System.Windows.Media.Effects.DropShadowEffect() { BlurRadius = 10 };

                    rect.Width = target_item.Width + 20;
                    rect.Height = target_item.Height + 20;
                    rect.IsHitTestVisible = false;

                    Canvas.SetLeft(rect, target_item.LeftPos - 10);
                    Canvas.SetTop(rect, target_item.TopPos - 10);
                    Canvas.SetZIndex(rect, 20);

                    // 一定時間枠を表示する
                    var notifyTimer = new System.Windows.Threading.DispatcherTimer();
                    notifyTimer.Interval = TimeSpan.FromSeconds(0.1);
                    int Brinks = 2 * 3;
                    cnvs.Children.Add(rect);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    notifyTimer.Tick += (sender, e) =>
                    {
                        if (sw.ElapsedMilliseconds > Settings.Instance.DisplayNotifyJumpTime * 1000)
                        {
                            notifyTimer.Stop();
                            cnvs.Children.Remove(rect);
                        }
                        else if (--Brinks >= 0)
                        {
                            rect.Visibility = (Brinks % 2) == 0 ? Visibility.Visible : Visibility.Hidden;
                        }
                    };
                    notifyTimer.Start();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

    }
}
