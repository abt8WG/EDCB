using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    class ChSet5
    {
        private Dictionary<UInt64, ChSet5Item> _chList = null;
        public Dictionary<UInt64, ChSet5Item> ChList
        {
            get
            {
                if (_chList == null)
                {
                    _chList = LoadFile();
                }
                return _chList;
            }
            private set { _chList = value; }
        }
        public static void Clear() { Instance._chList = null; }
        
        private static ChSet5 _instance = null;
        public static ChSet5 Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ChSet5();
                return _instance;
            }
        }

        public ChSet5() { }

        public static bool IsVideo(UInt16 ServiceType)
        {
            return ServiceType == 0x01 || ServiceType == 0xA5 || ServiceType == 0xAD;
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
        public static bool IsSkyPerfectv(UInt16 ONID)
        {
            return ONID == 0x000A;
        }
        public static bool IsOther(UInt16 ONID)
        {
            return IsDttv(ONID) == false && IsBS(ONID) == false && IsCS(ONID) == false;
        }

        private Dictionary<UInt64, ChSet5Item> LoadFile()
        {
            try
            {
                Dictionary<UInt64, ChSet5Item> chlist = new Dictionary<UInt64, ChSet5Item>();

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
                                chlist.Add(key, item);
                            }
                        }
                    }

                    reader.Close();
                }
                HasChanged = false;
                return chlist;
            }
            catch
            {
                HasChanged = false;
                return new Dictionary<UInt64, ChSet5Item>();
            }
        }

        public static bool SaveFile()
        {
            try
            {
                if (IniFileHandler.CanUpdateInifile == false)
                {
                    return false;
                }

                if (Instance.HasChanged && Instance.ChList != null)
                {
                    string output = "";
                    foreach (ChSet5Item info in Instance.ChList.Values)
                    {
                        output += string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\r\n",
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
                    if (output.Length > 0)
                    {
                        string header = ";<ChSet5.txt>\r\n";
                        if (CommonManager.Instance.CtrlCmd.SendUpdateSetting(header + output) != ErrCode.CMD_SUCCESS)
                        {
                            // サーバーが対応していないので直接書く。
                            String filePath = SettingPath.SettingFolderPath + "\\ChSet5.txt";
                            System.IO.StreamWriter writer = (new System.IO.StreamWriter(filePath, false, System.Text.Encoding.Default));
                            writer.Write(output);
                            writer.Close();
                        }
                    }
                }
                Instance.HasChanged = false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool HasChanged { get; set; }
    }

    public class ChSet5Item
    {
        public ChSet5Item() { }

        private UInt16 onid;
        private UInt16 tsid;
        private UInt16 sid;
        private UInt16 serviceType;
        private Byte partialFlag;
        private String serviceName;
        private String networkName;
        private Byte epgCapFlag;
        private Byte searchFlag;
        private Byte remoconID;

        public UInt64 Key { get { return CommonManager.Create64Key(ONID, TSID, SID); } }
        public UInt16 ONID
        {
            get { return onid; }
            set { ChSet5.Instance.HasChanged |= onid != value; onid = value; }
        }
        public UInt16 TSID
        {
            get { return tsid; }
            set { ChSet5.Instance.HasChanged |= tsid != value; tsid = value; }
        }
        public UInt16 SID
        {
            get { return sid; }
            set { ChSet5.Instance.HasChanged |= sid != value; sid = value; }
        }
        public UInt16 ServiceType
        {
            get { return serviceType; }
            set { ChSet5.Instance.HasChanged |= serviceType != value; serviceType = value; }
        }
        public Byte PartialFlag
        {
            get { return partialFlag; }
            set { ChSet5.Instance.HasChanged |= partialFlag != value; partialFlag = value; }
        }
        public String ServiceName
        {
            get { return serviceName; }
            set { ChSet5.Instance.HasChanged |= serviceName != value; serviceName = value; }
        }
        public String NetworkName
        {
            get { return networkName; }
            set { ChSet5.Instance.HasChanged |= networkName != value; networkName = value; }
        }
        public Byte EpgCapFlag
        {
            get { return epgCapFlag; }
            set { ChSet5.Instance.HasChanged |= epgCapFlag != value; epgCapFlag = value; }
        }
        public Byte SearchFlag
        {
            get { return searchFlag; }
            set { ChSet5.Instance.HasChanged |= searchFlag != value; searchFlag = value; }
        }
        public Byte RemoconID
        {
            get { return remoconID; }
            set { ChSet5.Instance.HasChanged |= remoconID != value; remoconID = value; }
        }

        public bool IsVideo { get { return ChSet5.IsVideo(ServiceType); } }
        public bool IsDttv { get { return ChSet5.IsDttv(ONID); } }
        public bool IsBS { get { return ChSet5.IsBS(ONID); } }
        public bool IsCS { get { return ChSet5.IsCS(ONID); } }
        public bool IsCS1 { get { return ChSet5.IsCS1(ONID); } }
        public bool IsCS2 { get { return ChSet5.IsCS2(ONID); } }
        public bool IsSkyPerfectv { get { return ChSet5.IsSkyPerfectv(ONID); } }
        public bool IsOther { get { return ChSet5.IsOther(ONID); } }

        public override string ToString()
        {
            return ServiceName;
        }
    }
}
