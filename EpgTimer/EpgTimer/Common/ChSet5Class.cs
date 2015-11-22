﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    class ChSet5
    {
        private bool _loaded;
        private Dictionary<UInt64, ChSet5Item> _chList;
        public Dictionary<UInt64, ChSet5Item> ChList
        {
            get
            {
                if (_loaded == false)
                {
                    _loaded = LoadFile();
                }
                return _chList;
            }
        }
        
        private static ChSet5 _instance;
        public static ChSet5 Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ChSet5();
                return _instance;
            }
        }

        public ChSet5()
        {
            _instance = null;
            _loaded = false;
            _chList = null;
        }

        public static bool IsVideo(UInt16 ServiceType)
        {
            return ServiceType == 0x01 || ServiceType == 0xA5;
        }
        public static bool IsDttv(UInt16 ONID)
        {
            return 0x7880 <= ONID && ONID <= 0x7FE8;
        }
        public static bool IsBS(UInt16 ONID)
        {
            return ONID == 0x0004;
        }
        public static bool IsCS(UInt16 ONID)
        {
            return IsCS1(ONID) || IsCS2(ONID);
        }
        public static bool IsCS1(UInt16 ONID)
        {
            return ONID == 0x0006;
        }
        public static bool IsCS2(UInt16 ONID)
        {
            return ONID == 0x0007;
        }
        public static bool IsOther(UInt16 ONID)
        {
            return IsDttv(ONID) == false && IsBS(ONID) == false && IsCS(ONID) == false;
        }

        public static bool LoadFile()
        {
            try
            {
                if (Instance._chList == null)
                {
                    Instance._chList = new Dictionary<UInt64, ChSet5Item>();
                }
                else
                {
                    Instance._chList.Clear();
                }

                // 直接ファイルを読まずに EpgTimerSrv.exe に問い合わせる
                byte[] binData;
                if (CommonManager.Instance.CtrlCmd.SendFileCopy("ChSet5.txt", out binData) == ErrCode.CMD_SUCCESS)
                {
                    System.IO.MemoryStream stream = new System.IO.MemoryStream(binData);
                    System.IO.StreamReader reader = new System.IO.StreamReader(stream, System.Text.Encoding.Default);
                    while (reader.Peek() >= 0)
                    {
                        string buff = reader.ReadLine();
                        if (buff.IndexOf(";") == 0)
                        {
                            //コメント行
                        }
                        else
                        {
                            string[] list = buff.Split('\t');
                            ChSet5Item item = new ChSet5Item();
                            try
                            {
                                item.ServiceName = list[0];
                                item.NetworkName = list[1];
                                item.ONID = Convert.ToUInt16(list[2]);
                                item.TSID = Convert.ToUInt16(list[3]);
                                item.SID = Convert.ToUInt16(list[4]);
                                item.ServiceType = Convert.ToUInt16(list[5]);
                                item.PartialFlag = Convert.ToByte(list[6]);
                                item.EpgCapFlag = Convert.ToByte(list[7]);
                                item.SearchFlag = Convert.ToByte(list[8]);
                            }
                            finally
                            {
                                UInt64 key = item.Key;
                                Instance._chList.Add(key, item);
                            }
                        }
                    }

                    reader.Close();
                }

            }
            catch
            {
                return false;
            }
            return true;
        }

#if false
// EpgTimer 側から変更することはないはず...
        public static bool SaveFile()
        {
            try
            {
                String filePath = SettingPath.SettingFolderPath + "\\ChSet5.txt";
                System.IO.StreamWriter writer = (new System.IO.StreamWriter(filePath, false, System.Text.Encoding.Default));
                if (Instance.ChList != null)
                {
                    foreach (ChSet5Item info in Instance.ChList.Values)
                    {
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}",
                            info.ServiceName,
                            info.NetworkName,
                            info.ONID,
                            info.TSID,
                            info.SID,
                            info.ServiceType,
                            info.PartialFlag,
                            info.EpgCapFlag,
                            info.SearchFlag);
                    }
                }
                writer.Close();
            }
            catch
            {
                return false;
            }
            return true;
        }
#endif
    }

    public class ChSet5Item
    {
        public ChSet5Item() { }

        public UInt64 Key { get { return CommonManager.Create64Key(ONID, TSID, SID); } }
        public UInt16 ONID { get; set; }
        public UInt16 TSID { get; set; }
        public UInt16 SID { get; set; }
        public UInt16 ServiceType { get; set; }
        public Byte PartialFlag { get; set; }
        public String ServiceName { get; set; }
        public String NetworkName { get; set; }
        public Byte EpgCapFlag { get; set; }
        public Byte SearchFlag { get; set; }
        public Byte RemoconID { get; set; }

        public bool IsVideo { get { return ChSet5.IsVideo(ServiceType); } }
        public bool IsDttv { get { return ChSet5.IsDttv(ONID); } }
        public bool IsBS { get { return ChSet5.IsBS(ONID); } }
        public bool IsCS { get { return ChSet5.IsCS(ONID); } }
        public bool IsCS1 { get { return ChSet5.IsCS1(ONID); } }
        public bool IsCS2 { get { return ChSet5.IsCS2(ONID); } }
        public bool IsOther { get { return ChSet5.IsOther(ONID); } }

        public override string ToString()
        {
            return ServiceName;
        }
    }
}
