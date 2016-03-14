using EpgTimer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace EpgTimer.DefineClass
{
    /// <summary>
    /// DBのレコード
    /// </summary>
    public class RecLogItem: IDBRecord
    {

        public enum RecodeStatuses
        {
            無し = 0,
            予約済み = 1,
            録画完了 = 2,
            視聴済み = 4,
            録画異常 = 8,
            無効登録 = 16,
            ALL = 31
        };

        public long ID { get; set; }

        public string recFilePath
        {
            get { return _recFilePath; }
            set { _recFilePath = value; }
        }
        string _recFilePath = string.Empty;

        public bool epgAlllowOverWrite
        {
            get { return _epgAlllowOverWrite; }
            set { _epgAlllowOverWrite = value; }
        }
        bool _epgAlllowOverWrite = true;

        /// <summary>
        /// DB EpgEventInfo Table ID
        /// </summary>
        public long epgEventInfoID
        {
            get { return _epgEventInfoID; }
            set { _epgEventInfoID = value; }
        }
        long _epgEventInfoID = 0;

        public EpgEventInfoR epgEventInfoR
        {
            get { return _epgEventInfoR; }
            set { _epgEventInfoR = value; }
        }
        EpgEventInfoR _epgEventInfoR = new EpgEventInfoR();

        /// <summary>
        /// 
        /// </summary>
        public RecodeStatuses recodeStatus { get; set; }

        /// <summary>
        /// 略記
        /// </summary>
        public string recodeStatus_Abbr
        {
            get
            {
                switch (recodeStatus)
                {
                    case RecodeStatuses.予約済み:
                        return "予";
                    case RecodeStatuses.録画完了:
                        return "録";
                    case RecodeStatuses.視聴済み:
                        return "視";
                    case RecodeStatuses.録画異常:
                        return "異";
                    case RecodeStatuses.無効登録:
                        return "無";
                    default:
                        return "?";
                }
            }
        }

        public string dateStr
        {
            get { return epgEventInfoR.start_time.ToString("yyyy/MM/dd HH:mm"); }
        }

        /// <summary>
        /// DB更新日時
        /// </summary>
        public DateTime lastUpdate
        {
            get { return _lastUpdate; }
            set { _lastUpdate = value; }
        }
        DateTime _lastUpdate;

        public string tvProgramTitle
        {
            get { return epgEventInfoR.ShortInfo.event_name; }
        }

        /// <summary>
        /// 番組情報
        /// </summary>
        public string tvProgramSummary
        {
            get { return epgEventInfoR.ShortInfo.text_char; }
        }

        public string ExtInfo_text_char
        {
            get { return epgEventInfoR.ExtInfo.text_char; }
        }

        /// <summary>
        /// コメント
        /// </summary>
        public string comment
        {
            get { return _comment; }
            set { _comment = value; }
        }
        string _comment = string.Empty;

        public string tvStationName
        {
            get
            {
                if (_tvStationName == null)
                {
                    UInt64 key = _epgEventInfoR.Create64Key();
                    if (ChSet5.Instance.ChList.ContainsKey(key) == true)
                    {
                        _tvStationName = ChSet5.Instance.ChList[key].ServiceName;
                    }
                    else
                    {
                        _tvStationName = string.Empty;
                    }
                }
                return _tvStationName;
            }
        }
        string _tvStationName = null;

        public Brush borderBrush
        {
            get
            {
                if (epgEventInfoR == null) return Brushes.White;
                //
                if (epgEventInfoR.ContentInfo.nibbleList.Count == 0)
                {
                    return Brushes.Gainsboro;
                }
                return CommonManager.Instance.VUtil.EpgDataContentBrush(epgEventInfoR.ContentInfo.nibbleList);
            }
        }

    }

}
