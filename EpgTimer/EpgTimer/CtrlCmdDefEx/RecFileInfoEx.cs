﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    public partial class RecFileInfo : IAutoAddTargetData
    {
        public string DataTitle { get { return Title; } }
        public DateTime PgStartTime { get { return StartTime; } }
        public uint PgDurationSecond { get { return DurationSecond; } }
        public UInt64 Create64Key()
        {
            return CommonManager.Create64Key(OriginalNetworkID, TransportStreamID, ServiceID);
        }

        public bool HasExtraData { get; set; }
        public bool IsModifiedErrInfo { get; set; }

        //使用箇所少ないので、手動でデータ取得させる。
        //(使用箇所増えるなら、ProgramInfoやErrInfoをプロパティ化して取得させる)
        public void GetExtraData()
        {
            if (this.HasExtraData == false)
            {
                var extraRecInfo = new RecFileInfo();
                if (CommonManager.Instance.CtrlCmd.SendGetRecInfo(this.ID, ref extraRecInfo) == ErrCode.CMD_SUCCESS)
                {
                    this.ProgramInfo = extraRecInfo.ProgramInfo;
                    this.ErrInfo = extraRecInfo.ErrInfo;
                    this.HasExtraData = true;
                    this.IsModifiedErrInfo = false;
                }
            }
            CountCriticalDrops();
        }

        private long dropsCritical = 0;
        public long DropsCritical
        {
            get
            {
                if (this.Drops == 0) return 0;
                CountCriticalDrops2();
                return dropsCritical;
            }
        }
        private long scramblesCritical = 0;
        public long ScramblesCritical
        {
            get
            {
                if (this.Scrambles == 0) return 0;
                CountCriticalDrops2();
                return scramblesCritical;
            }
        }
        private void CountCriticalDrops2()
        {
            if (IsModifiedErrInfo == true) return;

            RecFileInfo refrence;
            if (CommonManager.Instance.DB.RecFileInfo.TryGetValue(this.ID, out refrence) == true)
            {
                if (refrence.HasExtraData == false)
                {
                    //CheckCriticalDropsは通常連続して呼び出されるので、必要なデータをまとめて取得しておく
                    CommonManager.Instance.DB.ReadRecFileExtraData();
                }
                //(現在は無いが)インスタンスがコピーだった場合に問題が起きないようにする。
                if (this != refrence)
                {
                    this.ProgramInfo = refrence.ProgramInfo;
                    this.ErrInfo = refrence.ErrInfo;
                    this.HasExtraData = refrence.HasExtraData;
                    this.dropsCritical = refrence.dropsCritical;
                    this.scramblesCritical = refrence.scramblesCritical;
                    this.IsModifiedErrInfo = refrence.IsModifiedErrInfo;
                }
            }

            CountCriticalDrops();
        }
        private void CountCriticalDrops()
        {
            if (IsModifiedErrInfo == true) return;

            IsModifiedErrInfo = true;
            if (string.IsNullOrEmpty(this.ErrInfo) == false)
            {
                try
                {
                    dropsCritical = 0;
                    scramblesCritical = 0;
                    var newInfo = new StringBuilder("");

                    string[] lines = this.ErrInfo.Split(new char[] { '\n' });
                    foreach (string line1 in lines)
                    {
                        string line_new = line1;
                        if (line1.StartsWith("PID:") == true)
                        {
                            string[] words = line1.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                            //デフォルト { "EIT", "NIT", "CAT", "SDT", "SDTT", "TOT", "ECM", "EMM" }
                            if (Settings.Instance.RecInfoDropExclude.FirstOrDefault(s => words[8].Contains(s)) == null)
                            {
                                dropsCritical += (Int64)Convert.ToUInt64(words[5]);
                                scramblesCritical += (Int64)Convert.ToUInt64(words[7]);
                                line_new = line1.Replace(" " + words[8], "*" + words[8]);
                            }
                        }
                        newInfo.Append(line_new.TrimEnd('\r') + "\r\n");//単に\n付けるだけでも良いが、一応"\r\n"に確定させる
                    }

                    newInfo.AppendFormat("                              * = Critical Drop/Scramble Parameter.\r\n");
                    newInfo.AppendFormat("                              Drop:{0,9}  Scramble:{1,10}  Total\r\n", this.Drops, this.Scrambles);
                    newInfo.AppendFormat("                              Drop:{0,9}  Scramble:{1,10} *Critical\r\n", this.dropsCritical, this.scramblesCritical);
                    this.ErrInfo = newInfo.ToString();

                    return;
                }
                catch { }//エラーがあったときは、ラストへ
            }

            dropsCritical = this.Drops;
            scramblesCritical = this.Scrambles;
        }

        //簡易ステータス
        public RecEndStatusBasic RecStatusBasic
        {
            get
            {
                switch ((RecEndStatus)RecStatus)
                {
                    case RecEndStatus.NORMAL:           //正常終了
                        return RecEndStatusBasic.DEFAULT;
                    case RecEndStatus.OPEN_ERR:         //チューナーのオープンができなかった
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.ERR_END:          //録画中にエラーが発生した
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.NEXT_START_END:   //次の予約開始のため終了
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.START_ERR:        //開始時間が過ぎていた
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.CHG_TIME:         //開始時間が変更された
                        return RecEndStatusBasic.DEFAULT;
                    case RecEndStatus.NO_TUNER:         //チューナーが足りなかった
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.NO_RECMODE:       //無効扱いだった
                        return RecEndStatusBasic.DEFAULT;
                    case RecEndStatus.NOT_FIND_PF:      //p/fに番組情報確認できなかった
                        return RecEndStatusBasic.WARN;
                    case RecEndStatus.NOT_FIND_6H:      //6時間番組情報確認できなかった
                        return RecEndStatusBasic.WARN;
                    case RecEndStatus.END_SUBREC:       //サブフォルダへの録画が発生した
                        return RecEndStatusBasic.WARN;
                    case RecEndStatus.ERR_RECSTART:     //録画開始に失敗した
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.NOT_START_HEAD:   //一部のみ録画された
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.ERR_CH_CHG:       //チャンネル切り替えに失敗した
                        return RecEndStatusBasic.ERR;
                    case RecEndStatus.ERR_END2:         //録画中にエラーが発生した(Writeでexception)
                        return RecEndStatusBasic.ERR;
                    default:                            //状況不明
                        return RecEndStatusBasic.ERR;
                }
            }
        }

        public List<EpgAutoAddData> SearchEpgAutoAddList(bool? IsEnabled = null, bool ByFazy = false)
        {
            //EpgTimerSrv側のSearch()をEpgTimerで実装してないので、簡易な推定によるもの
            return CommonManager.Instance.MUtil.FazySearchEpgAutoAddData(DataTitle, IsEnabled);
        }
        public List<EpgAutoAddData> GetEpgAutoAddList(bool? IsEnabled = null)
        {
            return new List<EpgAutoAddData>();
        }
        public List<ManualAutoAddData> GetManualAutoAddList(bool? IsEnabled = null)
        {
            return CommonManager.Instance.DB.ManualAutoAddList.Values.GetAutoAddList(IsEnabled)
                .FindAll(data => data.CheckPgHit(this) == true);
        }
    }

    public static class RecFileInfoEx
    {
        public static List<RecFileInfo> GetNoProtectedList(this IEnumerable<RecFileInfo> itemlist)
        {
            return itemlist.Where(item => item == null ? false : item.ProtectFlag == 0).ToList();
        }
        //public static bool HasProtected(this IEnumerable<RecInfoItem> list)
        //{
        //    return list.Any(info => info == null ? false : info.RecInfo.ProtectFlag == true);
        //}
        public static bool HasNoProtected(this IEnumerable<RecFileInfo> list)
        {
            return list.Any(info => info == null ? false : info.ProtectFlag == 0);
        }

        public static List<RecFileInfo> Clone(this IEnumerable<RecFileInfo> src) { return CopyObj.Clone(src, CopyData); }
        public static RecFileInfo Clone(this RecFileInfo src) { return CopyObj.Clone(src, CopyData); }
        public static void CopyTo(this RecFileInfo src, RecFileInfo dest) { CopyObj.CopyTo(src, dest, CopyData); }
        private static void CopyData(RecFileInfo src, RecFileInfo dest)
        {
            dest.Comment = src.Comment;
            dest.Drops = src.Drops;
            dest.DurationSecond = src.DurationSecond;
            dest.ErrInfo = src.ErrInfo;
            dest.EventID = src.EventID;
            dest.ID = src.ID;
            dest.OriginalNetworkID = src.OriginalNetworkID;
            dest.ProgramInfo = src.ProgramInfo;
            dest.ProtectFlag = src.ProtectFlag;
            dest.RecFilePath = src.RecFilePath;
            dest.RecStatus = src.RecStatus;
            dest.Scrambles = src.Scrambles;
            dest.ServiceID = src.ServiceID;
            dest.ServiceName = src.ServiceName;
            dest.StartTime = src.StartTime;
            dest.StartTimeEpg = src.StartTimeEpg;
            dest.Title = src.Title;
            dest.TransportStreamID = src.TransportStreamID;
        }

    }
}
