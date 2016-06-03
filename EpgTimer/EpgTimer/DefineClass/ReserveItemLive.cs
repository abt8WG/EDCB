using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace EpgTimer
{
    public class ReserveItemLive : ReserveItem, INotifyPropertyChanged
    {
        public enum ReserveItemBgColorIndex
        {
            Level1BgColor = 0,
            Level2BgColor = 1,
            Level3BgColor = 2,
            DisabledBgColor = 3,
            ErrorBgColor = 4,
        }

        private static readonly Brush DefaultBgColor = new SolidColorBrush(Colors.Transparent);

        private static Brush GetItemBgColor(ReserveItemBgColorIndex index)
        {
            return CommonManager.Instance.InfoWindowItemBgColors[(int)index];
        }

        private int TickToSecond(long ticks) { return (int)(ticks / 10000000L); }

        #region プロパティ用のフィールド

        private Brush _Background;
        private Visibility _Visibility;
        private int _OnAirOrRecStart;
        private int _OnAirOrRecEnd;
        private int _OnAirOrRecProgress;
        private Visibility _OnAirOrRecProgressVisibility;
        private Visibility _ProgressBar1Visibility = Visibility.Collapsed;
        private Visibility _ProgressBar2Visibility = Visibility.Collapsed;

        #endregion

        public ReserveItemLive() : this(null) { }

        public ReserveItemLive(ReserveData item) : base(item)
        {
            if (ReserveInfo == null) return;

            if (Settings.Instance.InfoWindowBasedOnBroadcast)
            {
                OnAirOrRecStart = TickToSecond(ReserveInfo.StartTime.Ticks);
                OnAirOrRecEnd = TickToSecond(ReserveInfo.StartTime.AddSeconds(ReserveInfo.DurationSecond).Ticks);
            }
            else
            {
                OnAirOrRecStart = TickToSecond(ReserveInfo.StartTimeWithMargin(0).Ticks);
                OnAirOrRecEnd = TickToSecond(ReserveInfo.EndTimeWithMargin().Ticks);
            }
        }

        #region INotifyPropertyChangedの実装

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected void SetProperty<T>(ref T field, T value, [CallerMemberName]string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;
            RaisePropertyChanged(propertyName);
        }

        #endregion

        #region Methods

        /// <summary>
        /// ReserveItemの状態と放送または録画までの時刻に応じてBrushを決定して返します。
        /// 決定できなかった場合はDefaultBgColorを返します。
        /// </summary>
        /// <returns>背景用のBrush</returns>
        private Brush CalcBackground()
        {
            Brush result = DefaultBgColor;

            if (ReserveInfo == null) return result;

            int secondsToStart = OnAirOrRecStart - OnAirOrRecProgress;
            if (secondsToStart <= Settings.Instance.InfoWindowItemLevel1Seconds)
            {
                if (Settings.Instance.InfoWindowItemFilterLevel < 1)
                    return null;
                result = GetItemBgColor(ReserveItemBgColorIndex.Level1BgColor);
            }
            else if (secondsToStart <= Settings.Instance.InfoWindowItemLevel2Seconds)
            {
                if (Settings.Instance.InfoWindowItemFilterLevel < 2)
                    return null;
                result = GetItemBgColor(ReserveItemBgColorIndex.Level2BgColor);
            }
            else if (secondsToStart <= Settings.Instance.InfoWindowItemLevel3Seconds)
            {
                if (Settings.Instance.InfoWindowItemFilterLevel < 3)
                    return null;
                result = GetItemBgColor(ReserveItemBgColorIndex.Level3BgColor);
            }
            else
            {
                if (Settings.Instance.InfoWindowItemFilterLevel < 4)
                    return null;
                result = DefaultBgColor;
            }

            if (IsEnabled)
            {
                if (ReserveInfo.OverlapMode == 1 || ReserveInfo.OverlapMode == 2 || ReserveInfo.IsAutoAddInvalid)
                {
                    result = GetItemBgColor(ReserveItemBgColorIndex.ErrorBgColor);
                }
            }
            else
            {
                result = GetItemBgColor(ReserveItemBgColorIndex.DisabledBgColor);
            }

            return result;
        }

        /// <summary>
        /// 基準となる時刻 ticks の値に応じてプロパティを更新します。
        /// </summary>
        public void Update(int seconds)
        {
            OnAirOrRecProgress = seconds;
            Brush bgBrush = CalcBackground();
            if (bgBrush != null)
            {
                Background = bgBrush;
                OnAirOrRecProgressVisibility = (OnAirOrRecStart <= OnAirOrRecProgress && OnAirOrRecProgress <= OnAirOrRecEnd) ? Visibility.Visible : Visibility.Collapsed;
                switch (Settings.Instance.InfoWindowItemProgressBarType)
                {
                    case 1: ProgressBar1Visibility = OnAirOrRecProgressVisibility; break;
                    case 2: ProgressBar2Visibility = OnAirOrRecProgressVisibility; break;
                }
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// ReserveItemの状態と放送または録画までの時刻に応じてBrushを決定します。
        /// 決定できなかった場合はDefaultBgColor。
        /// </summary>
        public Brush Background
        {
            get { return _Background; }
            private set { SetProperty(ref _Background, value); }
        }

        /// <summary>
        /// 
        /// </summary>
        public Visibility Visibility
        {
            get { return _Visibility; }
            private set { SetProperty(ref _Visibility, value); }
        }

        /// <summary>
        /// 放送または録画の開始時刻を示す値。(単位は秒)
        /// </summary>
        public int OnAirOrRecStart
        {
            get { return _OnAirOrRecStart; }
            private set { SetProperty(ref _OnAirOrRecStart, value); }
        }

        /// <summary>
        /// 放送または録画の終了時刻を示す値。(単位は秒)
        /// </summary>
        public int OnAirOrRecEnd
        {
            get { return _OnAirOrRecEnd; }
            private set { SetProperty(ref _OnAirOrRecEnd, value); }
        }

        /// <summary>
        /// 放送または録画の進行度を示す時刻の値。(単位は秒)
        /// </summary>
        public int OnAirOrRecProgress
        {
            get { return _OnAirOrRecProgress; }
            private set { SetProperty(ref _OnAirOrRecProgress, value); }
        }

        /// <summary>
        /// 放送または録画中ならVisible、そうでなければCollapsed。
        /// </summary>
        public Visibility OnAirOrRecProgressVisibility
        {
            get { return _OnAirOrRecProgressVisibility; }
            private set { SetProperty(ref _OnAirOrRecProgressVisibility, value); }
        }

        /// <summary>
        /// 日時の下の ProgressBar の表示状態
        /// </summary>
        public Visibility ProgressBar1Visibility
        {
            get { return _ProgressBar1Visibility; }
            private set { SetProperty(ref _ProgressBar1Visibility, value); }
        }

        /// <summary>
        /// 録画項目の背景の ProgressBar の表示状態
        /// </summary>
        public Visibility ProgressBar2Visibility
        {
            get { return _ProgressBar2Visibility; }
            private set { SetProperty(ref _ProgressBar2Visibility, value); }
        }

        #endregion
    }
}
