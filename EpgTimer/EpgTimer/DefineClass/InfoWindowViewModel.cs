using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

namespace EpgTimer
{
    public class InfoWindowViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged実装

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
        }

        #endregion

        #region ReserveMode 変更通知プロパティ

        private IEnumerable<ReserveItem> _ReserveList = null;

        public IEnumerable<ReserveItem> ReserveList
        {
            get { return this._ReserveList; }
            set
            {
                if (this._ReserveList != value)
                {
                    this._ReserveList = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        #endregion

        #region IsTopMost 変更通知プロパティ

        private bool _IsTopMost = Settings.Instance.InfoWindowTopMost;

        public bool IsTopMost
        {
            get { return this._IsTopMost; }
            set
            {
                if (this._IsTopMost != value)
                {
                    this._IsTopMost = value;
                    if (this._IsTopMost)
                    {
                        this.IsBottomMost = false;
                    }
                    this.RaisePropertyChanged();
                }
            }
        }

        #endregion

        #region IsBottomMost 変更通知プロパティ

        private bool _IsBottomMost = Settings.Instance.InfoWindowBottomMost;

        public bool IsBottomMost
        {
            get { return this._IsBottomMost; }
            set
            {
                if (this._IsBottomMost != value)
                {
                    this._IsBottomMost = value;
                    if (this._IsBottomMost)
                    {
                        this.IsTopMost = false;
                    }
                    this.RaisePropertyChanged();
                }
            }
        }

        #endregion

        public void UpdateInfo()
        {
            ReserveList = CommonManager.Instance.DB.ReserveList.Values.
                Where(x => x.RecSetting.RecMode != 5). // 無効予約を除外
                OrderBy(x => x.StartTime). // 開始時刻でソート
                Select(x => new ReserveItem(x));
        }
    }
}
