using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EpgTimer
{
    static class ChSet5
    {
        private static Dictionary<UInt64, ChSet5Item> chList = null;
        public static Dictionary<UInt64, ChSet5Item> ChList
        {
            get
            {
                if (chList == null) LoadFile();
                return chList ?? new Dictionary<UInt64, ChSet5Item>();
            }
        }
        public static void Clear() { chList = null; HasChanged = false; }
        
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

        public static bool LoadFile()
        {
            try
            {
                // 直接ファイルを読まずに EpgTimerSrv.exe に問い合わせる
                byte[] binData;
                if (CommonManager.Instance.CtrlCmd.SendFileCopy("ChSet5.txt", out binData) == ErrCode.CMD_SUCCESS)
                {
                    using (var stream = new MemoryStream(binData))
                    using (var sr = new StreamReader(stream, Encoding.Default))
                    {
                        return ChSet5.Load(sr);
                    }
                }
                else if (CommonManager.Instance.NWMode == false)
                {
                    // サーバーが対応していないので直接読む
                    String filePath = SettingPath.SettingFolderPath + "\\ChSet5.txt";
                    using (var sr = new StreamReader(filePath, Encoding.Default))
                    {
                        return ChSet5.Load(sr);
                    }
                }
            }
            catch { }
            return false;
        }
        public static bool Load(StreamReader reader)
        {
            try
            {
                chList = new Dictionary<UInt64, ChSet5Item>();
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
                        var item = new ChSet5Item();
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
                            chList.Add(item.Key, item);
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
        public static bool SaveFile()
        {
            try
            {
                if (!HasChanged || chList == null) return false;
                //
                string output = "";
                foreach (ChSet5Item info in chList.Values)
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
                        if (CommonManager.Instance.NWMode == true)
                            return false;

                        // サーバーが対応していないので直接書く。
                        String filePath = SettingPath.SettingFolderPath + "\\ChSet5.txt";
                        using (var writer = new System.IO.StreamWriter(filePath, false, Encoding.Default))
                        {
                            writer.Write(output);
                        }
                    }
                }
                HasChanged = false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static bool HasChanged { get; set; }
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
            set { ChSet5.HasChanged |= onid != value; onid = value; }
        }
        public UInt16 TSID
        {
            get { return tsid; }
            set { ChSet5.HasChanged |= tsid != value; tsid = value; }
        }
        public UInt16 SID
        {
            get { return sid; }
            set { ChSet5.HasChanged |= sid != value; sid = value; }
        }
        public UInt16 ServiceType
        {
            get { return serviceType; }
            set { ChSet5.HasChanged |= serviceType != value; serviceType = value; }
        }
        public Byte PartialFlag
        {
            get { return partialFlag; }
            set { ChSet5.HasChanged |= partialFlag != value; partialFlag = value; }
        }
        public String ServiceName
        {
            get { return serviceName; }
            set { ChSet5.HasChanged |= serviceName != value; serviceName = value; }
        }
        public String NetworkName
        {
            get { return networkName; }
            set { ChSet5.HasChanged |= networkName != value; networkName = value; }
        }
        public Byte EpgCapFlag
        {
            get { return epgCapFlag; }
            set { ChSet5.HasChanged |= epgCapFlag != value; epgCapFlag = value; }
        }
        public Byte SearchFlag
        {
            get { return searchFlag; }
            set { ChSet5.HasChanged |= searchFlag != value; searchFlag = value; }
        }
        public Byte RemoconID
        {
            get { return remoconID; }
            set { ChSet5.HasChanged |= remoconID != value; remoconID = value; }
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
