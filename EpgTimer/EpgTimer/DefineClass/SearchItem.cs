﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;

namespace EpgTimer
{
    public class SearchItem : RecSettingItem
    {
        protected EpgEventInfo eventInfo = null;
        public virtual EpgEventInfo EventInfo { get { return eventInfo; } set { eventInfo = value; } }
        public ReserveData ReserveInfo { get; set; }

        public SearchItem() { }
        public SearchItem(EpgEventInfo item) { eventInfo = item; }

        public override void Reset()
        {
            reserveTuner = null;
            base.Reset();
        }

        public override ulong KeyID { get { return EventInfo == null ? 0 : EventInfo.CurrentPgUID(); } }
        public bool IsReserved { get { return (ReserveInfo != null); } }
        public override RecSettingData RecSettingInfo { get { return ReserveInfo != null ? ReserveInfo.RecSetting : null; } }
        public override bool IsManual { get { return ReserveInfo != null ? ReserveInfo.IsManual : false; } }

        public virtual String EventName
        {
            get
            {
                if (EventInfo == null) return "";
                //
                return EventInfo.DataTitle;
            }
        }
        public virtual String ServiceName
        {
            get
            {
                if (EventInfo != null)
                {
                    UInt64 serviceKey = EventInfo.Create64Key();
                    if (ChSet5.ChList.ContainsKey(serviceKey) == true)
                    {
                        return ChSet5.ChList[serviceKey].ServiceName;
                    }
                }
                return "";
            }
        }
        public virtual String NetworkName
        {
            get
            {
                if (EventInfo == null) return "";
                //
                return CommonManager.ConvertNetworkNameText(EventInfo.original_network_id);
            }
        }
        public virtual String StartTime
        {
            get
            {
                if (EventInfo == null) return "";
                if (EventInfo.StartTimeFlag == 0) return "未定";
                //
                return CommonManager.ConvertTimeText(EventInfo.start_time, Settings.Instance.ResInfoNoYear, Settings.Instance.ResInfoNoSecond);
            }
        }
        public virtual long StartTimeValue
        {
            get
            {
                if (EventInfo == null || EventInfo.StartTimeFlag == 0) return long.MinValue;
                //
                return EventInfo.start_time.Ticks;
            }
        }
        /// <summary>
        /// 番組長
        /// </summary>
        public virtual String ProgramDuration
        {
            get
            {
                if (EventInfo == null) return "";
                if (EventInfo.DurationFlag == 0) return "不明";
                //
                return CommonManager.ConvertDurationText(EventInfo.durationSec, Settings.Instance.ResInfoNoDurSecond);
            }
        }
        public virtual UInt32 ProgramDurationValue
        {
            get
            {
                if (EventInfo == null || EventInfo.DurationFlag == 0) return UInt32.MinValue;
                //
                return EventInfo.durationSec;
            }
        }
        /// <summary>
        /// 番組内容
        /// </summary>
        public String ProgramContent
        {
            get
            {
                if (EventInfo == null || EventInfo.ShortInfo == null) return "";
                //
                return EventInfo.ShortInfo.text_char.Replace("\r\n", " ");
            }
        }
        public String JyanruKey
        {
            get
            {
                if (EventInfo == null) return "";
                //
                return CommonManager.ConvertJyanruText(EventInfo);
            }
        }
        public bool IsEnabled
        {
            set
            {
                EpgCmds.ChgOnOffCheck.Execute(this, null);
            }
            get
            {
                if (ReserveInfo == null) return false;
                //
                return ReserveInfo.IsEnabled;
            }
        }
        public String Comment
        {
            get
            {
                if (ReserveInfo == null) return "";
                //
                if (ReserveInfo.IsAutoAdded == false)
                {
                    return "個別予約(" + (ReserveInfo.IsEpgReserve == true ? "EPG" : "プログラム") + ")";
                }
                else
                {
                    string s = ReserveInfo.Comment;
                    return (ReserveInfo.IsAutoAddMissing == true ? "不明な" : ReserveInfo.IsAutoAddInvalid == true ? "無効の" : "")
                            + (s.StartsWith("EPG自動予約(") == true ? "キーワード予約(" + AutoAddInfo + ")" : s);
                }
            }
        }
        public String AutoAddInfo
        {
            get
            {
                if (ReserveInfo == null) return "";
                //
                String info = "";
                var reserveList = ReserveInfo.AutoAddInfo;
                foreach (var data in reserveList)
                {
                    if (info.Length > 0) info += ",";
                    info += data.andKey;
                }
                return info;
            }
        }
        public List<String> RecFileName
        {
            get
            {
                if (ReserveInfo == null) return new List<string>();
                //
                return ReserveInfo.RecFileNameList;
            }
        }
        private string reserveTuner = null;
        public string ReserveTuner
        {
            get
            {
                if (ReserveInfo == null) return "";
                //
                if (reserveTuner == null)
                {
                    TunerReserveInfo info = CommonManager.Instance.DB.TunerReserveList.Values.Where(r => r.reserveList.Contains(ReserveInfo.ReserveID)).FirstOrDefault();
                    uint tID = info == null ? 0xFFFFFFFF : info.tunerID;
                    string tName = ReserveInfo.IsEnabled == false ? "無効予約" : info == null ? "不明" : info.tunerName;
                    reserveTuner = new TunerSelectInfo(tName, tID).ToString();
                }
                return reserveTuner;
            }
        }
        public override String ConvertInfoText()
        {
            return CommonManager.ConvertProgramText(EventInfo, EventInfoTextMode.All);
        }
        public virtual String Status
        {
            get
            {
                String[] wiewString = { "", "予", "無", "放", "予+", "無+", "録*", "無*" };
                int index = 0;
                if (EventInfo != null)
                {
                    if (EventInfo.IsOnAir() == true)
                    {
                        index = 3;
                    }
                    if (IsReserved == true)
                    {
                        if (ReserveInfo.IsOnRec() == true)//マージンがあるので、IsOnAir==trueとは限らない
                        {
                            index = 5;
                        }
                        if (ReserveInfo.IsEnabled == false) //無効の判定
                        {
                            index += 2;
                        }
                        else
                        {
                            index += 1;
                        }
                    }
                }
                return wiewString[index];
            }
        }
        public virtual Brush StatusColor
        {
            get
            {
                if (EventInfo != null)
                {
                    if (IsReserved == true)
                    {
                        if (ReserveInfo.IsOnRec() == true)
                        {
                            return CommonManager.Instance.StatRecForeColor;
                        }
                    }
                    if (EventInfo.IsOnAir() == true)
                    {
                        return CommonManager.Instance.StatOnAirForeColor;
                    }
                }
                return CommonManager.Instance.StatResForeColor;
            }
        }
        public override Brush ForeColor
        {
            get
            {
                //番組表へジャンプ時の強調表示
                if (NowJumpingTable != 0 || ReserveInfo == null) return base.ForeColor;
                //
                return CommonManager.Instance.RecModeForeColor[ReserveInfo.RecSetting.RecMode];
            }
        }
        public override Brush BackColor
        {
            get
            {
                //番組表へジャンプ時の強調表示
                if (NowJumpingTable != 0 || ReserveInfo == null) return base.BackColor;

                //通常表示
                return ViewUtil.ReserveErrBrush(ReserveInfo);
            }
        }
        public override Brush BorderBrush
        {
            get
            {
                return ViewUtil.EpgDataContentBrush(EventInfo);
            }
        }
    }

    public static class SearchItemEx
    {
        public static List<EpgEventInfo> GetEventList(this IEnumerable<SearchItem> list)
        {
            return list.Where(item => item != null).Select(item => item.EventInfo).ToList();
        }
        public static List<EpgEventInfo> GetNoReserveList(this IEnumerable<SearchItem> list)
        {
            return list.Where(item => item != null && item.IsReserved == false).Select(item => item.EventInfo).ToList();
        }
        public static List<ReserveData> GetReserveList(this IEnumerable<SearchItem> list)
        {
            return list.Where(item => item != null && item.IsReserved == true).Select(item => item.ReserveInfo).ToList();
        }
        //public static bool HasReserved(this IEnumerable<SearchItem> list)
        //{
        //    return list.Any(info => item != null && item.IsReserved == false);
        //}
        //public static bool HasNoReserved(this IEnumerable<SearchItem> list)
        //{
        //    return list.Any(info => item != null && item.IsReserved == true);
        //}
        public static void AddFromEventList(this ICollection<SearchItem> itemlist, IEnumerable<EpgEventInfo> eventList, bool isExceptUnknownStartTime, bool isExceptEnded)
        {
            if (eventList == null) return;
            //
            foreach (EpgEventInfo info in eventList.OfAvailable(isExceptUnknownStartTime, isExceptEnded == true ? (DateTime?)DateTime.Now : null))
            {
                itemlist.Add(new SearchItem(info));
            }
            itemlist.SetReserveData();
        }

        public static void SetReserveData(this ICollection<SearchItem> list)
        {
            var listKeys = new Dictionary<UInt64, SearchItem>();

            foreach (SearchItem listItem1 in list)
            {
                //重複するキーは基本的に無いという前提
                try
                {
                    listItem1.Reset();
                    listKeys.Add(listItem1.EventInfo.CurrentPgUID(), listItem1);
                    listItem1.ReserveInfo = null;
                }
                catch { }
            }

            SearchItem setItem;
            foreach (ReserveData data in CommonManager.Instance.DB.ReserveList.Values)
            {
                if (listKeys.TryGetValue(data.CurrentPgUID(), out setItem))
                {
                    setItem.ReserveInfo = data;
                }
            }
        }

    }
}
