using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows;

namespace EpgTimer
{
    // EpgTimerNW では参照も更新もしない
    // CtlCmd 経由で取得、更新するよう書き換えるのが望ましい。
    class IniFileHandler
    {
        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern uint
          GetPrivateProfileStringW(string lpAppName,
          string lpKeyName, string lpDefault,
          StringBuilder lpReturnedString, uint nSize,
          string lpFileName);

        public static uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName)
        {
            if (IniSetting.Instance[lpFileName].IsAvailable)
            {
                string s = IniSetting.Instance[lpFileName][lpAppName][lpKeyName];
                lpReturnedString.Append(s == null ? lpDefault : s);
                return (uint)lpReturnedString.Length;
            }

            if (CommonManager.Instance.NWMode == false)
            {
                return GetPrivateProfileStringW(lpAppName, lpKeyName, lpDefault, lpReturnedString, nSize, lpFileName);
            }
            else
            {
                // lpDefault を返すほうが親切かも。
                // ただしここに来ないように修正するほうが望ましい。
                return 0;
            }
        }

        /*
        // 現在使われていないようなので、コメントアウトしておく。
        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true,
            EntryPoint = "GetPrivateProfileStringW")]
        public static extern uint
            GetPrivateProfileStringByByteArray(string lpAppName,
            string lpKeyName, string lpDefault,
            byte[] lpReturnedString, uint nSize,
            string lpFileName);
        */

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int
          GetPrivateProfileIntW(string lpAppName,
          string lpKeyName, int nDefault, string lpFileName);

        public static int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName)
        {
            if (IniSetting.Instance[lpFileName].IsAvailable)
            {
                try
                {
                    string s = IniSetting.Instance[lpFileName][lpAppName][lpKeyName];
                    return s == null ? nDefault : Convert.ToInt32(s);
                }
                catch (FormatException)
                {
                    // Convert.ToInt32("") などするとここに来る
                    return nDefault;
                }
            }

            if (CommonManager.Instance.NWMode == false)
            {
                return GetPrivateProfileIntW(lpAppName, lpKeyName, nDefault, lpFileName);
            }
            else
            {
                // 一応 nDefault を返す。
                // ただしここに来ないように修正するほうが望ましい。
                return nDefault;
            }
        }

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern uint WritePrivateProfileStringW(
          string lpAppName,
          string lpKeyName,
          string lpString,
          string lpFileName);

        public static uint WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName)
        {
            if (IniSetting.Instance[lpFileName].IsAvailable)
            {
                IniSetting.Instance[lpFileName][lpAppName][lpKeyName] = lpString;
                return lpString == null ? 0 : (uint)lpString.Length;
            }
            if (CommonManager.Instance.NWMode == false)
            {
                return WritePrivateProfileStringW(lpAppName, lpKeyName, lpString, lpFileName);
            }
            else
            {
                // ここに来ないように修正するほうが望ましい。
                return 0;
            }
        }

        public static string
          GetPrivateProfileString(string lpAppName,
          string lpKeyName, string lpDefault, string lpFileName)
        {
            StringBuilder buff = null;
            for (uint n = 512; n <= 1024 * 1024; n *= 2)
            {
                //セクション名取得などのNUL文字分割された結果は先頭要素のみ格納される
                buff = new StringBuilder((int)n);
                if (GetPrivateProfileString(lpAppName, lpKeyName, lpDefault, buff, n, lpFileName) < n - 2)
                {
                    break;
                }
            }
            return buff.ToString();
        }

        public static void UpdateSrvProfileIniNW(List<string> iniList = null)
        {
            if (CommonManager.Instance.NW.IsConnected == false) return;

            // tkntrec版
            //ReloadSettingFilesNW(iniList);

            ChSet5.Clear();
            Settings.Instance.RecPresetList = null;
            Settings.Instance.ReloadOtherOptions();
        }

#if false // for tkntrec版
        public static void ReloadSettingFilesNW(List<string> iniList = null)
        {
            if (iniList == null)
            {
                iniList = new List<string> {
                    "EpgTimerSrv.ini"
                    ,"Common.ini"
                    ,"EpgDataCap_Bon.ini"
                    ,"ChSet5.txt"
                };
            }

            try
            {
                var datalist = new List<FileData>();
                if (CommonManager.Instance.CtrlCmd.SendFileCopy2(iniList, ref datalist) == ErrCode.CMD_SUCCESS)
                {
                    Directory.CreateDirectory(SettingPath.SettingFolderPath);
                    foreach (var data in datalist.Where(d1 => d1.Size != 0))
                    {
                        try
                        {
                            using (var w = new BinaryWriter(File.Create(Path.Combine(SettingPath.SettingFolderPath, data.Name))))
                            {
                                w.Write(data.Data);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
#endif

        public static bool CanReadInifile { get { return IniSetting.Instance.CanReadInifile == true || CommonManager.Instance.NWMode == false; } }
        public static bool CanUpdateInifile { get { return IniSetting.Instance.CanUpdateInifile == true || CommonManager.Instance.NWMode == false; } }
    }

    // サーバーから取得したINIファイルをパースして構造体で保持する
    // (Get/Write)PrivateProfileString の代わりに IniSetting.Instance[lpFileName][lpAppName][lpKeyName] アクセサを用意
    class IniSetting
    {
        public class SectionList
        {
            public class PairList
            {
                private Dictionary<string, string> _items;
                private Dictionary<string, string> _updates;
                public string this[string key]
                {
                    get
                    {
                        return _updates.ContainsKey(key) ? _updates[key] : _items.ContainsKey(key) ? _items[key] : null;
                    }
                    set
                    {
                        if (!_items.ContainsKey(key) || _items[key] != value)
                        {
                            _updates[key] = value;
                        }
                    }
                }
                public Dictionary<string, string>.KeyCollection UpdatedKeys
                {
                    get { return _updates.Keys; }
                }
                public PairList()
                {
                    _items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                internal void addPair(string key, string value)
                {
                    _items.Add(key, value);
                }
                public void Clear()
                {
                    _items.Clear();
                }
            }

            private string _file;
            public SectionList(string file)
            {
                _file = file;
            }

            private Dictionary<string, PairList> _sections;
            internal Dictionary<string, PairList>.KeyCollection Keys { get { return _sections.Keys; } }
            public PairList this[string section]
            {
                get
                {
                    if (_sections == null)
                    {
                        // EpgTimerSrv の FILE_COPY にパッチが当たってないと _sections が null のままなので例外を投げて下層に捕捉させる
                        throw new NotImplementedException("EpgTimerSrv.exe が INIファイル読み込みに対応していません");
                    }
                    if (_sections.ContainsKey(section) == false)
                    {
                        _sections.Add(section, new PairList());
                    }
                    return _sections[section];
                }
            }
            public bool IsAvailable
            {
                get
                {
                    return _sections != null;
                }
            }

            internal void loadFile()
            {
                try
                {
                    string filename = _file.Substring(_file.LastIndexOf('\\') + 1);
                    byte[] binData;
                    if (CommonManager.Instance.CtrlCmd.SendFileCopy(filename, out binData) == ErrCode.CMD_SUCCESS)
                    {
                        IniSetting.Instance.CanReadInifile = true;
                        IniSetting.Instance.CanUpdateInifile = CommonManager.Instance.CtrlCmd.SendUpdateSetting("") == ErrCode.CMD_SUCCESS;

                        System.IO.MemoryStream stream = new System.IO.MemoryStream(binData);
                        System.IO.StreamReader reader = new System.IO.StreamReader(stream, System.Text.Encoding.Default);

                        if (_sections == null)
                        {
                            _sections = new Dictionary<string, PairList>(StringComparer.OrdinalIgnoreCase);
                        }
                        else
                        {
                            foreach (string s in _sections.Keys)
                            {
                                _sections[s].Clear();
                            }
                        }

                        string section = "";

                        while (reader.Peek() >= 0)
                        {
                            string buff = reader.ReadLine();
                            if (buff.IndexOf(";") == 0)
                            {
                                //コメント行
                            }
                            else if (buff.IndexOf("[") == 0)
                            {
                                //セクション開始
                                int last = buff.IndexOf(']', 1);
                                if (last > 1)
                                {
                                    section = buff.Substring(1, last - 1);
                                }
                            }
                            else if (section.Length > 0)
                            {
                                string[] list = buff.Split(new char[] { '=' }, 2);
                                this[section].addPair(list[0], list[1]);
                            }
                        }
                        reader.Close();
                    }
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex);
                }
            }

            internal string getIniData()
            {
                string ini = "";
                if (_sections != null)
                {
                    // 更新があった差分だけのINIファイルの中身を組み立てる
                    foreach (string s in _sections.Keys)
                    {
                        if (_sections[s].UpdatedKeys.Count > 0)
                        {
                            //セクション名を追加
                            ini += "[" + s + "]\r\n";
                            foreach (string k in _sections[s].UpdatedKeys)
                            {
                                string v = _sections[s][k];
                                if (v == null)
                                {
                                    //削除するキーを ";key=" の形式で指定する
                                    ini += ";" + k + "=\r\n";
                                }
                                else
                                {
                                    //データペアを追加
                                    ini += k + "=" + _sections[s][k] + "\r\n";
                                }
                            }
                        }
                    }

                    //保存すべきデータがあれば、ファイル名を ";<ファイル名>" の形式で先頭に追加
                    if (ini.Length > 0)
                    {
                        string filename = _file.Substring(_file.LastIndexOf('\\') + 1);
                        ini = ";<" + filename + ">\r\n" + ini;
                    }
                }
                return ini;
            }
        }

        private static IniSetting _instance;
        public static IniSetting Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IniSetting();
                }
                return _instance;
            }
        }

        private bool canRead;
        public bool CanReadInifile
        {
            get
            {
                if (_files == null)
                {
                    var o = this["Common.Ini"];
                }
                return canRead;
            }
            internal set
            {
                canRead = value;
            }
        }
        private bool canUpdate;
        public bool CanUpdateInifile
        {
            get
            {
                if (_files == null)
                {
                    var o = this["Common.Ini"];
                }
                return canUpdate;
            }
            internal set { canUpdate = value; }
        }

        private Dictionary<string, SectionList> _files;
        public SectionList this [string file]
        {
            get
            {
                if (_files == null)
                {
                    _files = new Dictionary<string, SectionList>(StringComparer.OrdinalIgnoreCase);
                }

                //保存時 WritePrivateProfileStriingW するときにフルパスが必要なので短縮しない
                // file = file.Substring(file.LastIndexOf('\\') + 1);
                if (_files.ContainsKey(file) == false)
                {
                    _files.Add(file, new SectionList(file));
                    _files[file].loadFile();
                }
                return _files[file];
            }
        }

        public void UpToDate()
        {
            if (_files != null && CanReadInifile)
            {
                string output = "";
                foreach (string f in _files.Keys)
                {
                    output += _files[f].getIniData();
                }
                if (output.Length > 0)
                {
                    // 更新が必要な差分だけサーバーに送信。 
                    if (CommonManager.Instance.CtrlCmd.SendUpdateSetting(output) != ErrCode.CMD_SUCCESS)
                    {
                        //tkntrec版 EpgTimerSrv.exe はここに来る
                        if (CommonManager.Instance.NWMode == false)
                        {
                            //サーバー側更新ができないので、直接更新する。
                            foreach (string lpFileName in _files.Keys)
                            {
                                foreach (string lpAppName in _files[lpFileName].Keys)
                                {
                                    foreach (string lpKeyName in _files[lpFileName][lpAppName].UpdatedKeys)
                                    {
                                        string lpString = _files[lpFileName][lpAppName][lpKeyName];
                                        IniFileHandler.WritePrivateProfileStringW(lpAppName, lpKeyName, lpString, lpFileName);
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new NotImplementedException("EpgTimerSrv.exe が INIファイル更新に対応していません");
                        }
                    }
                }
                _files.Clear();
            }
        }

        public void Clear()
        {
            _instance = null;
        }
    }

    class SettingPath
    {
        private static string IniPath
        {
            // サーバー側のINIファイルの直接参照をしなくなったので、IniPath が必要になるのは
            // INIファイル更新の非対応サーバーに対してローカル接続(PIPE接続)した場合のみ。
            // ローカル接続する EpgTimer.exe と EpgTimerSrv.exe のバージョンは揃えるべきだとは思う。
            get
            {
                if (CommonManager.Instance.NWMode == false)
                {
                    try
                    {
                        var TimerSrv = System.Diagnostics.Process.GetProcessesByName("EpgTimerSrv");
                        if (TimerSrv.Count() > 0)
                        {
                            string exePath = ServiceCtrlClass.QueryServiceExePath("EpgTimer Service");
                            return Path.GetDirectoryName(exePath ?? TimerSrv[0].MainModule.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                    }
                }
                return ModulePath;
            }
        }
        public static string CommonIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\Common.ini"; }
        }
        public static string TimerSrvIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\EpgTimerSrv.ini"; }
        }
        public static string EdcbExePath
        {
            get
            {
                if (IniFileHandler.CanReadInifile == true)
                {
                    string defRecExe = ModulePath.TrimEnd('\\') + "\\EpgDataCap_Bon.exe";
                    return IniFileHandler.GetPrivateProfileString("SET", "RecExePath", defRecExe, CommonIniPath);
                }
                else
                {
                    return "";
                }
            }
        }
        public static string EdcbIniPath
        {
            get { return EdcbExePath.TrimEnd("exe".ToArray()) + "ini"; }
        }
        public static string DefSettingFolderPath
        {
            get { return ModulePath.TrimEnd('\\') + "\\Setting"; }
        }
        public static string SettingFolderPath
        {
            get
            {
                if (IniFileHandler.CanReadInifile == true)
                {
                    string path = IniFileHandler.GetPrivateProfileString("SET", "DataSavePath", DefSettingFolderPath, CommonIniPath);
                    return (Path.IsPathRooted(path) ? "" : ModulePath.TrimEnd('\\') + "\\") + path;
                }
                else
                {
                    return "";
                }
            }
        }
        public static string ModulePath
        {
            get { return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location); }
        }
        public static string ModuleName
        {
            get { return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location); }
        }
    }

    public class Settings
    {
        private string fontName;
        private double fontSize;
        private string fontNameTitle;
        private double fontSizeTitle;
        private bool fontBoldTitle;
        private string tunerFontNameService;
        private double tunerFontSizeService;
        private bool tunerFontBoldService;
        private string tunerFontName;
        private double tunerFontSize;

        public string FontName
        {
            get { return fontName; }
            set { fontName = value; CommonManager.Instance.VUtil.ItemFontNormal = null; }
        }
        public double FontSize
        {
            get { return fontSize; }
            set { fontSize = value; CommonManager.Instance.VUtil.ItemFontNormal = null; }
        }
        public string FontNameTitle
        {
            get { return fontNameTitle; }
            set { fontNameTitle = value; CommonManager.Instance.VUtil.ItemFontTitle = null; }
        }
        public double FontSizeTitle
        {
            get { return fontSizeTitle; }
            set { fontSizeTitle = value; CommonManager.Instance.VUtil.ItemFontTitle = null; }
        }
        public bool FontBoldTitle
        {
            get { return fontBoldTitle; }
            set { fontBoldTitle = value; CommonManager.Instance.VUtil.ItemFontTitle = null; }
        }
        public string TunerFontNameService
        {
            get { return tunerFontNameService; }
            set { tunerFontNameService = value; CommonManager.Instance.VUtil.ItemFontTunerService = null; }
        }
        public double TunerFontSizeService
        {
            get { return tunerFontSizeService; }
            set { tunerFontSizeService = value; CommonManager.Instance.VUtil.ItemFontTunerService = null; }
        }
        public bool TunerFontBoldService
        {
            get { return tunerFontBoldService; }
            set { tunerFontBoldService = value; CommonManager.Instance.VUtil.ItemFontTunerService = null; }
        }
        public string TunerFontName
        {
            get { return tunerFontName; }
            set { tunerFontName = value; CommonManager.Instance.VUtil.ItemFontTunerNormal = null; }
        }
        public double TunerFontSize
        {
            get { return tunerFontSize; }
            set { tunerFontSize = value; CommonManager.Instance.VUtil.ItemFontTunerNormal = null; }
        }

        public bool UseCustomEpgView { get; set; }
        public List<CustomEpgTabInfo> CustomEpgTabList { get; set; }
        public double MinHeight { get; set; }
        public double MinimumHeight { get; set; }
        public double ServiceWidth { get; set; }
        public double ScrollSize { get; set; }
        //public string FontName { get; set; }
        //public double FontSize { get; set; }
        //public string FontNameTitle { get; set; }
        //public double FontSizeTitle { get; set; }
        //public bool FontBoldTitle { get; set; }
        public bool NoToolTip { get; set; }
        public bool NoBallonTips { get; set; }
        public int ForceHideBalloonTipSec { get; set; }
        public bool PlayDClick { get; set; }
        public bool RecinfoErrCriticalDrops { get; set; }
        public double DragScroll { get; set; }
        public List<string> ContentColorList { get; set; }
        public List<UInt32> ContentCustColorList { get; set; }
        public List<string> EpgEtcColors { get; set; }
        public List<UInt32> EpgEtcCustColors { get; set; }
        public string ReserveRectColorNormal { get; set; }
        public string ReserveRectColorNo { get; set; }
        public string ReserveRectColorNoTuner { get; set; }
        public string ReserveRectColorWarning { get; set; }
        public string ReserveRectColorAutoAddMissing { get; set; }
        public bool ReserveRectBackground { get; set; }
        public string TitleColor1 { get; set; }
        public string TitleColor2 { get; set; }
        public UInt32 TitleCustColor1 { get; set; }
        public UInt32 TitleCustColor2 { get; set; }
        //public string TunerFontNameService { get; set; }
        //public double TunerFontSizeService { get; set; }
        //public bool TunerFontBoldService { get; set; }
        //public string TunerFontName { get; set; }
        //public double TunerFontSize { get; set; }
        public List<string> TunerServiceColors { get; set; }
        public List<UInt32> TunerServiceCustColors { get; set; }
        public double TunerMinHeight { get; set; }
        public double TunerMinimumLine { get; set; }
        public double TunerDragScroll { get; set; }
        public double TunerScrollSize { get; set; }
        public bool TunerMouseScrollAuto { get; set; }
        public double TunerWidth { get; set; }
        public bool TunerServiceNoWrap { get; set; }
        public bool TunerTitleIndent { get; set; }
        public bool TunerPopup { get; set; }
        public bool TunerPopupRecinfo { get; set; }
        public bool TunerInfoSingleClick { get; set; }
        public bool TunerColorModeUse { get; set; }
        public bool TunerDisplayOffReserve { get; set; }
        public bool EpgTitleIndent { get; set; }
        public bool EpgPopup { get; set; }
        public bool EpgPopupResOnly { get; set; }
        public bool EpgGradation { get; set; }
        public bool EpgGradationHeader { get; set; }
        public string ResColumnHead { get; set; }
        public ListSortDirection ResSortDirection { get; set; }
        public WindowState LastWindowState { get; set; }
        public double MainWndLeft { get; set; }
        public double MainWndTop { get; set; }
        public double MainWndWidth { get; set; }
        public double MainWndHeight { get; set; }
        public bool CloseMin { get; set; }
        public bool WakeMin { get; set; }
        public bool ViewButtonShowAsTab { get; set; }
        public List<string> ViewButtonList { get; set; }
        public List<string> TaskMenuList { get; set; }
        public string Cust1BtnName { get; set; }
        public string Cust1BtnCmd { get; set; }
        public string Cust1BtnCmdOpt { get; set; }
        public string Cust2BtnName { get; set; }
        public string Cust2BtnCmd { get; set; }
        public string Cust2BtnCmdOpt { get; set; }
        public List<string> AndKeyList { get; set; }
        public List<string> NotKeyList { get; set; }
        public EpgSearchKeyInfo DefSearchKey { get; set; }
        private List<RecPresetItem> recPresetList = null;
        [System.Xml.Serialization.XmlIgnore]
        public List<RecPresetItem> RecPresetList
        {
            get
            {
                if (recPresetList == null)
                {
                    recPresetList = new List<RecPresetItem>();
                    recPresetList.Add(new RecPresetItem("デフォルト", 0));
                    foreach (string s in IniFileHandler.GetPrivateProfileString("SET", "PresetID", "", SettingPath.TimerSrvIniPath).Split(','))
                    {
                        uint id;
                        uint.TryParse(s, out id);
                        if (recPresetList.Exists(p => p.ID == id) == false)
                        {
                            recPresetList.Add(new RecPresetItem(
                                IniFileHandler.GetPrivateProfileString("REC_DEF" + id, "SetName", "", SettingPath.TimerSrvIniPath)
                                , id));
                        }
                    }
                }
                return recPresetList;
            }
            set { recPresetList = value; }
        }
        public string RecInfoColumnHead { get; set; }
        public ListSortDirection RecInfoSortDirection { get; set; }
        public long RecInfoDropErrIgnore { get; set; }
        public long RecInfoDropWrnIgnore { get; set; }
        public long RecInfoScrambleIgnore { get; set; }
        public List<string> RecInfoDropExclude { get; set; }
        public bool RecInfoNoYear { get; set; }
        public bool RecInfoNoSecond { get; set; }
        public bool RecInfoNoDurSecond { get; set; }
        public bool ResInfoNoYear { get; set; }
        public bool ResInfoNoSecond { get; set; }
        public bool ResInfoNoDurSecond { get; set; }
        public string TvTestExe { get; set; }
        public string TvTestCmd { get; set; }
        public bool NwTvMode { get; set; }
        public bool NwTvModeUDP { get; set; }
        public bool NwTvModeTCP { get; set; }
        public string FilePlayExe { get; set; }
        public string FilePlayCmd { get; set; }
        public bool FilePlayOnAirWithExe { get; set; }
        public bool OpenFolderWithFileDialog { get; set; }
        public List<IEPGStationInfo> IEpgStationList { get; set; }
        public MenuSettingData MenuSet { get; set; }
        public bool NWMode { get; set; }
        public string NWServerIP { get; set; }
        public UInt32 NWServerPort { get; set; }
        public UInt32 NWWaitPort { get; set; }
        public string NWMacAdd { get; set; }
        public SerializableSecureString NWPassword { get; set; }
        public List<NWPresetItem> NWPerset { get; set; }            // obsolete because of typo of 'NWPreset'
        public List<NWPresetItem> NWPreset { get; set; }
        public bool WakeReconnectNW { get; set; }
        public bool SuspendCloseNW { get; set; }
        public bool NgAutoEpgLoadNW { get; set; }
        public bool ChkSrvRegistTCP { get; set; }
        public double ChkSrvRegistInterval { get; set; }
        public Int32 TvTestOpenWait { get; set; }
        public Int32 TvTestChgBonWait { get; set; }
        public List<string> RecEndColors { get; set; }                //録画済リストの背景色
        public List<uint> RecEndCustColors { get; set; }              //録画済リストのカスタム背景色
        public string ListDefColor { get; set; }                      //各画面のリストのデフォルト文字色
        public UInt32 ListDefCustColor { get; set; }                  //各画面のリストのデフォルトカスタム文字色
        public List<string> RecModeFontColors { get; set; }           //予約リストなどの録画モードごとの文字色
        public List<uint> RecModeFontCustColors { get; set; }         //予約リストなどの録画モードごとのカスタム文字色
        public List<string> ResBackColors { get; set; }               //予約リストなどの背景色
        public List<uint> ResBackCustColors { get; set; }             //予約リストなどのカスタム背景色
        public List<string> StatColors { get; set; }                  //予約リストなどの「状態」列の予約色
        public List<uint> StatCustColors { get; set; }                //予約リストなどの「状態」列のカスタム予約色
        public bool EpgInfoSingleClick { get; set; }
        public byte EpgInfoOpenMode { get; set; }
        public UInt32 ExecBat { get; set; }
        public UInt32 SuspendChk { get; set; }
        public UInt32 SuspendChkTime { get; set; }
        public List<ListColumnInfo> ReserveListColumn { get; set; }
        public List<ListColumnInfo> RecInfoListColumn { get; set; }
        public List<ListColumnInfo> AutoAddEpgColumn { get; set; }
        public List<ListColumnInfo> AutoAddManualColumn { get; set; }
        public List<ListColumnInfo> EpgListColumn { get; set; }
        public string EpgListColumnHead { get; set; }
        public ListSortDirection EpgListSortDirection { get; set; }
        public List<ListColumnInfo> SearchWndColumn { get; set; }
        public string SearchColumnHead { get; set; }
        public ListSortDirection SearchSortDirection { get; set; }
        public double SearchWndLeft { get; set; }
        public double SearchWndTop { get; set; }
        public double SearchWndWidth { get; set; }
        public double SearchWndHeight { get; set; }
        public bool SearchWndPinned { get; set; }
        public short AutoSaveNotifyLog { get; set; }
        public bool ShowTray { get; set; }
        public bool MinHide { get; set; }
        public bool MouseScrollAuto { get; set; }
        public int NoStyle { get; set; }
        public bool CautionManyChange { get; set; }
        public int CautionManyNum { get; set; }
        public bool CautionOnRecChange { get; set; }
        public int CautionOnRecMarginMin { get; set; }
        public bool SyncResAutoAddChange { get; set; }
        public bool SyncResAutoAddChgNewRes { get; set; }
        public bool SyncResAutoAddDelete { get; set; }
        public int KeyDeleteDisplayItemNum { get; set; }
        public bool DisplayNotifyEpgChange { get; set; }
        public double DisplayNotifyJumpTime { get; set; }
        public bool DisplayReserveAutoAddMissing { get; set; }
        public bool TryEpgSetting { get; set; }
        public bool LaterTimeUse { get; set; }
        public int LaterTimeHour { get; set; }
        public bool DisplayPresetOnSearch { get; set; }

        public bool ApplyMultiInstance { get; set; }
        public double ReserveMinHeight { get; set; }
        public bool ReservePopup { get; set; }
        public bool AlwaysSaveEpgSetting { get; set; }
        public SerializableDictionary<string, WINDOWPLACEMENT> Placement { get; set; }
        public bool InfoWindowTopMost { get; set; }
        public bool InfoWindowEnabled { get; set; }
        public bool RecItemToolTip { get; set; }

        public Settings()
        {
            UseCustomEpgView = false;
            CustomEpgTabList = new List<CustomEpgTabInfo>();
            MinHeight = 2;
            MinimumHeight = 0;
            ServiceWidth = 150;
            ScrollSize = 240;
            fontName = System.Drawing.SystemFonts.DefaultFont.Name;
            fontSize = 12;
            fontNameTitle = System.Drawing.SystemFonts.DefaultFont.Name;
            fontSizeTitle = 12;
            fontBoldTitle = true;
            NoToolTip = false;
            NoBallonTips = false;
            ForceHideBalloonTipSec = 0;
            PlayDClick = false;
            RecinfoErrCriticalDrops = false;
            DragScroll = 1.5;
            ContentColorList = new List<string>();
            ContentCustColorList = new List<uint>();
            EpgEtcColors = new List<string>();
            EpgEtcCustColors = new List<uint>();
            ReserveRectColorNormal = "Lime";
            ReserveRectColorNo = "Black";
            ReserveRectColorNoTuner = "Red";
            ReserveRectColorWarning = "Yellow";
            ReserveRectColorAutoAddMissing = "Blue";
            ReserveRectBackground = false;
            TitleColor1 = "Black";
            TitleColor2 = "Black";
            TitleCustColor1 = 0xFFFFFFFF;
            TitleCustColor2 = 0xFFFFFFFF;
            tunerFontNameService = System.Drawing.SystemFonts.DefaultFont.Name;
            tunerFontSizeService = 12;
            tunerFontBoldService = true;
            tunerFontName = System.Drawing.SystemFonts.DefaultFont.Name;
            tunerFontSize = 12;
            TunerServiceColors = new List<string>();
            TunerServiceCustColors = new List<uint>();
            TunerMinHeight = 2;
            TunerMinimumLine = 0;
            TunerDragScroll = 1.5;
            TunerScrollSize = 240;
            TunerMouseScrollAuto = false;
            TunerWidth = 150;
            TunerServiceNoWrap = true;
            TunerTitleIndent = true;
            TunerPopup = false;
            TunerPopupRecinfo = false;
            TunerInfoSingleClick = false;
            TunerColorModeUse = false;
            TunerDisplayOffReserve = false;
            EpgTitleIndent = true;
            EpgPopup = true;
            EpgPopupResOnly = false;
            EpgGradation = true;
            EpgGradationHeader = true;
            ResColumnHead = "";
            ResSortDirection = ListSortDirection.Ascending;
            LastWindowState = WindowState.Normal;
            MainWndLeft = -100;
            MainWndTop = -100;
            MainWndWidth = -100;
            MainWndHeight = -100;
            CloseMin = false;
            WakeMin = false;
            ViewButtonShowAsTab = false;
            ViewButtonList = new List<string>();
            TaskMenuList = new List<string>();
            Cust1BtnName = "";
            Cust1BtnCmd = "";
            Cust1BtnCmdOpt = "";
            Cust2BtnName = "";
            Cust2BtnCmd = "";
            Cust2BtnCmdOpt = "";
            AndKeyList = new List<string>();
            NotKeyList = new List<string>();
            DefSearchKey = new EpgSearchKeyInfo();
            RecInfoColumnHead = "";
            RecInfoSortDirection = ListSortDirection.Ascending;
            RecInfoDropErrIgnore = 0;
            RecInfoDropWrnIgnore = 0;
            RecInfoScrambleIgnore = 0;
            RecInfoDropExclude = new List<string>();
            RecInfoNoYear = false;
            RecInfoNoSecond = false;
            RecInfoNoDurSecond = false;
            ResInfoNoYear = false;
            ResInfoNoSecond = false;
            ResInfoNoDurSecond = false;
            TvTestExe = "";
            TvTestCmd = "";
            NwTvMode = false;
            NwTvModeUDP = false;
            NwTvModeTCP = false;
            FilePlayExe = "";
            FilePlayCmd = "\"$FilePath$\"";
            FilePlayOnAirWithExe = false;
            OpenFolderWithFileDialog = false;
            IEpgStationList = new List<IEPGStationInfo>();
            MenuSet = new MenuSettingData();
            NWServerIP = "";
            NWServerPort = 4510;
            NWWaitPort = 4520;
            NWMacAdd = "";
            NWPassword = new SerializableSecureString();
            NWPerset = new List<NWPresetItem>();
            NWPreset = new List<NWPresetItem>();
            WakeReconnectNW = false;
            SuspendCloseNW = false;
            NgAutoEpgLoadNW = false;
            ChkSrvRegistTCP = false;
            ChkSrvRegistInterval = 5;
            TvTestOpenWait = 2000;
            TvTestChgBonWait = 2000;
            RecEndColors = new List<string>();
            RecEndCustColors = new List<uint>();
            ListDefColor = "カスタム";
            ListDefCustColor= 0xFF042271;
            RecModeFontColors = new List<string>();
            RecModeFontCustColors= new List<uint>();
            ResBackColors = new List<string>();
            ResBackCustColors= new List<uint>();
            StatColors = new List<string>();
            StatCustColors= new List<uint>();
            EpgInfoSingleClick = false;
            EpgInfoOpenMode = 0;
            ExecBat = 0;
            SuspendChk = 0;
            SuspendChkTime = 15;
            ReserveListColumn = new List<ListColumnInfo>();
            RecInfoListColumn = new List<ListColumnInfo>();
            AutoAddEpgColumn = new List<ListColumnInfo>();
            AutoAddManualColumn = new List<ListColumnInfo>();
            EpgListColumn = new List<ListColumnInfo>();
            EpgListColumnHead = "";
            EpgListSortDirection = ListSortDirection.Ascending;
            SearchWndColumn = new List<ListColumnInfo>();
            SearchColumnHead = "";
            SearchSortDirection = ListSortDirection.Ascending;
            SearchWndLeft = -100;
            SearchWndTop = -100;
            SearchWndWidth = -100;
            SearchWndHeight = -100;
            SearchWndPinned = false;
            AutoSaveNotifyLog = 0;
            ShowTray = true;
            MinHide = true;
            MouseScrollAuto = false;
            NoStyle = 0;
            CautionManyChange = true;
            CautionManyNum = 10;
            CautionOnRecChange = true;
            CautionOnRecMarginMin = 5;
            SyncResAutoAddChange = false;
            SyncResAutoAddChgNewRes = false;
            SyncResAutoAddDelete = false;
            KeyDeleteDisplayItemNum = 10;
            DisplayNotifyEpgChange = false;
            DisplayNotifyJumpTime = 3;
            DisplayReserveAutoAddMissing = false;
            TryEpgSetting = true;
            LaterTimeUse = false;
            LaterTimeHour = 28 - 24;
            DisplayPresetOnSearch = false;

            recPresetList = null;
            ApplyMultiInstance = false;
            ReserveMinHeight = 2;
            ReservePopup = false;
            AlwaysSaveEpgSetting = false;
            Placement = new SerializableDictionary<string, WINDOWPLACEMENT>();
            InfoWindowTopMost = true;
            InfoWindowEnabled = false;
            RecItemToolTip = false;
        }

        [NonSerialized()]
        private static Settings _instance;
        [System.Xml.Serialization.XmlIgnore]
        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Settings();
                return _instance;
            }
            set { _instance = value; }
        }

        //色リストの初期化用
        private static void _FillList<T>(List<T> list, T val, int num)
        {
            if (list.Count < num)
            {
                list.AddRange(Enumerable.Repeat(val, num - list.Count));
            }
        }
        private static void _FillList<T>(List<T> list, List<T> val)
        {
            if (list.Count < val.Count)
            {
                list.AddRange(val.Skip(list.Count));
            }
        }

        /// <summary>
        /// 設定ファイルロード関数
        /// </summary>
        public static void LoadFromXmlFile()
        {
            _LoadFromXmlFile(GetSettingPath());
        }
        private static void _LoadFromXmlFile(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    //読み込んで逆シリアル化する
                    var xs = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                    Instance = (Settings)(xs.Deserialize(fs));
                }
            }
            catch (Exception ex)
            {
                if (ex.GetBaseException().GetType() != typeof(System.IO.FileNotFoundException))
                {
                    string backPath = path + ".back";
                    if (System.IO.File.Exists(backPath) == true)
                    {
                        if (MessageBox.Show("設定ファイルが異常な可能性があります。\r\nバックアップファイルから読み込みますか？", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            _LoadFromXmlFile(backPath);
                            return;
                        }
                    }
                    MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                }
            }

            // typo のXMLデータの引っ越し
            if (Instance.NWPerset.Count > 0 && Instance.NWPreset.Count == 0)
            {
                Instance.NWPreset = Instance.NWPerset;
            }
            Instance.NWPerset = null; // Serializeはしない

            try
            {
                // タイミング合わせにくいので、メニュー系のデータチェックは
                // MenuManager側のワークデータ作成時に実行する。

                SetCustomEpgTabInfoID();

                int num;
                List<string> defColors = new List<string>();

                //番組表の背景色
                num = 0x11;//番組表17色。過去に16色時代があった。
                if (Instance.ContentColorList.Count < num)
                {
                    defColors = new List<string>{
                        "LightYellow"
                        ,"Lavender"
                        ,"LavenderBlush"
                        ,"MistyRose"
                        ,"Honeydew"
                        ,"LightCyan"
                        ,"PapayaWhip"
                        ,"Pink"
                        ,"LightYellow"
                        ,"PapayaWhip"
                        ,"AliceBlue"
                        ,"AliceBlue"
                        ,"White"
                        ,"White"
                        ,"White"
                        ,"WhiteSmoke"
                        ,"White"
                    };
                    _FillList(Instance.ContentColorList, defColors);
                }
                num = 0x11 + 5;//番組表17色+予約枠5色
                _FillList(Instance.ContentCustColorList, 0xFFFFFFFF, num);

                //チューナ画面各フォント色
                num = 2 + 5;//固定色2+優先度色5
                _FillList(Instance.TunerServiceColors, "Black", num);
                _FillList(Instance.TunerServiceCustColors, 0xFFFFFFFF, num);

                //番組表の時間軸のデフォルトの背景色、その他色
                num = 5;
                if (Instance.EpgEtcColors.Count < num)
                {
                    defColors = new List<string>{
                        "MediumPurple"      //00-05時
                        ,"LightSeaGreen"    //06-11時
                        ,"LightSalmon"      //12-17時
                        ,"CornflowerBlue"   //18-23時
                        ,"LightSlateGray"   //サービス色
                    };
                    _FillList(Instance.EpgEtcColors, defColors);
                }
                _FillList(Instance.EpgEtcCustColors, 0xFFFFFFFF, num);

                //録画済み一覧背景色
                num = 3;
                if (Instance.RecEndColors.Count < num)
                {
                    defColors = new List<string>{
                        "White"//デフォルト
                        ,"LightPink"//エラー
                        ,"LightYellow"//ワーニング
                    };
                    _FillList(Instance.RecEndColors, defColors);
                }
                _FillList(Instance.RecEndCustColors, 0xFFFFFFFF, num);

                //録画モード別予約文字色
                num = 6;
                _FillList(Instance.RecModeFontColors, "カスタム", num);
                _FillList(Instance.RecModeFontCustColors, 0xFF042271, num);

                //状態別予約背景色
                num = 5;
                if (Instance.ResBackColors.Count < num)
                {
                    defColors = new List<string>{
                        "White"//通常
                        ,"LightGray"//無効
                        ,"LightPink"//チューナー不足
                        ,"LightYellow"//一部実行
                        ,"LightBlue"//自動予約登録不明
                    };
                    _FillList(Instance.ResBackColors, defColors);
                }
                _FillList(Instance.ResBackCustColors, 0xFFFFFFFF, num);

                //予約状態列文字色
                num = 3;
                if (Instance.StatColors.Count < num)
                {
                    defColors = new List<string>{
                        "Blue"//予約中
                        ,"OrangeRed"//録画中
                        ,"LimeGreen"//放送中
                    };
                    _FillList(Instance.StatColors, defColors);
                }
                _FillList(Instance.StatCustColors, 0xFFFFFFFF, num);

                if (Instance.ViewButtonList.Count == 0)
                {
                    Instance.ViewButtonList.Add("設定");
                    Instance.ViewButtonList.Add("（空白）");
                    Instance.ViewButtonList.Add("再接続");
                    Instance.ViewButtonList.Add("（空白）");
                    Instance.ViewButtonList.Add("検索");
                    Instance.ViewButtonList.Add("（空白）");
                    Instance.ViewButtonList.Add("スタンバイ");
                    Instance.ViewButtonList.Add("休止");
                    Instance.ViewButtonList.Add("（空白）");
                    Instance.ViewButtonList.Add("EPG取得");
                    Instance.ViewButtonList.Add("（空白）");
                    Instance.ViewButtonList.Add("EPG再読み込み");
                    Instance.ViewButtonList.Add("（空白）");
                    Instance.ViewButtonList.Add("終了");
                }
                if (Instance.TaskMenuList.Count == 0)
                {
                    Instance.TaskMenuList.Add("設定");
                    Instance.TaskMenuList.Add("（セパレータ）");
                    Instance.TaskMenuList.Add("再接続");
                    Instance.TaskMenuList.Add("（セパレータ）");
                    Instance.TaskMenuList.Add("スタンバイ");
                    Instance.TaskMenuList.Add("休止");
                    Instance.TaskMenuList.Add("（セパレータ）");
                    Instance.TaskMenuList.Add("終了");
                }
                if (Instance.ReserveListColumn.Count == 0)
                {
                    var obj = new ReserveItem();
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.StartTime), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.NetworkName), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ServiceName), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.EventName), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.RecMode), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Priority), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Tuijyu), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Comment), double.NaN));
                    Instance.ReserveListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.RecFileName), double.NaN));
                    Instance.ResColumnHead = CommonUtil.GetMemberName(() => obj.StartTime);
                    Instance.ResSortDirection = ListSortDirection.Ascending;
                }
                if (Instance.RecInfoListColumn.Count == 0)
                {
                    var obj = new RecInfoItem();
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.IsProtect), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.StartTime), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.NetworkName), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ServiceName), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.EventName), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Drops), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Scrambles), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Result), double.NaN));
                    Instance.RecInfoListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.RecFilePath), double.NaN));
                    Instance.RecInfoColumnHead = CommonUtil.GetMemberName(() => obj.StartTime);
                    Instance.RecInfoSortDirection = ListSortDirection.Descending;
                }
                if (Instance.AutoAddEpgColumn.Count == 0)
                {
                    var obj = new EpgAutoDataItem();
                    Instance.AutoAddEpgColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.EventName), double.NaN));
                    Instance.AutoAddEpgColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.NotKey), double.NaN));
                    Instance.AutoAddEpgColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.RegExp), double.NaN));
                    Instance.AutoAddEpgColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.RecMode), double.NaN));
                    Instance.AutoAddEpgColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Priority), double.NaN));
                    Instance.AutoAddEpgColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Tuijyu), double.NaN));
                }
                if (Instance.AutoAddManualColumn.Count == 0)
                {
                    var obj = new ManualAutoAddDataItem();
                    Instance.AutoAddManualColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.DayOfWeek), double.NaN));
                    Instance.AutoAddManualColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.StartTime), double.NaN));
                    Instance.AutoAddManualColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.EventName), double.NaN));
                    Instance.AutoAddManualColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ServiceName), double.NaN));
                    Instance.AutoAddManualColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.RecMode), double.NaN));
                    Instance.AutoAddManualColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Priority), double.NaN));
                }
                if (Instance.EpgListColumn.Count == 0)
                {
                    var obj = new SearchItem();
                    Instance.EpgListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Status), double.NaN));
                    Instance.EpgListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.StartTime), double.NaN));
                    Instance.EpgListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.NetworkName), double.NaN));
                    Instance.EpgListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ServiceName), double.NaN));
                    Instance.EpgListColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.EventName), double.NaN));
                    Instance.EpgListColumnHead = CommonUtil.GetMemberName(() => obj.StartTime);
                    Instance.EpgListSortDirection = ListSortDirection.Ascending;
                }
                if (Instance.SearchWndColumn.Count == 0)
                {
                    var obj = new SearchItem();
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.Status), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.StartTime), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ProgramDuration), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.EventName), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.NetworkName), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ServiceName), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.ProgramContent), double.NaN));
                    Instance.SearchWndColumn.Add(new ListColumnInfo(CommonUtil.GetMemberName(() => obj.JyanruKey), double.NaN));
                    Instance.SearchColumnHead = CommonUtil.GetMemberName(() => obj.StartTime);
                    Instance.SearchSortDirection = ListSortDirection.Ascending;
                }
                if (Instance.RecInfoDropExclude.Count == 0)
                {
                    Settings.Instance.RecInfoDropExclude = new List<string> { "EIT", "NIT", "CAT", "SDT", "SDTT", "TOT", "ECM", "EMM" };
                }
                if (Instance.RecPresetList.Count == 0)
                {
                    Instance.RecPresetList.Add(new RecPresetItem("デフォルト", 0));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        public static void SaveToXmlFile()
        {
            try
            {
                string path = GetSettingPath();

                if (System.IO.File.Exists(path) == true)
                {
                    string backPath = path + ".back";
                    System.IO.File.Copy(path, backPath, true);
                }

                FileStream fs = new FileStream(path,
                    FileMode.Create,
                    FileAccess.Write, FileShare.None);
                System.Xml.Serialization.XmlSerializer xs =
                    new System.Xml.Serialization.XmlSerializer(
                    typeof(Settings));
                //シリアル化して書き込む
                xs.Serialize(fs, Instance);
                fs.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private static string GetSettingPath()
        {
            Assembly myAssembly = Assembly.GetEntryAssembly();
            string path = myAssembly.Location + ".xml";

            return path;
        }

        public void SetSettings(string propertyName, object value)
        {
            if (propertyName == null) return;
            var info = typeof(Settings).GetProperty(propertyName);
            if (info != null) info.SetValue(this, value, null);
        }

        public object GetSettings(string propertyName)
        {
            if (propertyName == null) return null;
            var info = typeof(Settings).GetProperty(propertyName);
            return (info == null ? null : info.GetValue(this, null));
        }

        public static void GetDefRecSetting(UInt32 presetID, ref RecSettingData defKey)
        {
            RecPresetItem preset = Instance.RecPresetList.FirstOrDefault(item => item.ID == presetID);
            if (preset == null) preset = new RecPresetItem("", presetID);
            preset.RecPresetData.CopyTo(defKey);
        }

        public void ReloadOtherOptions()
        {
            DefStartMargin = IniFileHandler.GetPrivateProfileInt("SET", "StartMargin", 0, SettingPath.TimerSrvIniPath);
            DefEndMargin = IniFileHandler.GetPrivateProfileInt("SET", "EndMargin", 0, SettingPath.TimerSrvIniPath);
            defRecfolders = null;
        }

        //デフォルトマージン
        [System.Xml.Serialization.XmlIgnore]
        public int DefStartMargin { get; private set; }
        [System.Xml.Serialization.XmlIgnore]
        public int DefEndMargin { get; private set; }

        private List<RecFolderInfo> defRecfolders = null;
        [System.Xml.Serialization.XmlIgnore]
        public List<RecFolderInfo> DefRecFolders
        {
            get
            {
                if (defRecfolders == null)
                {
                    defRecfolders = new List<RecFolderInfo>();
                    // サーバーから保存フォルダーを取得してみる
                    if (CommonManager.Instance.CtrlCmd.SendEnumRecFolders("", ref defRecfolders) != ErrCode.CMD_SUCCESS)
                    {
                        // ローカル接続かサーバーから INI ファイルを取得できれば INI ファイルの内容を直接読む
                        if (IniFileHandler.CanReadInifile)
                        {
                            int num = IniFileHandler.GetPrivateProfileInt("SET", "RecFolderNum", 0, SettingPath.CommonIniPath);
                            if (num == 0)
                            {
                                defRecfolders.Add(new RecFolderInfo(SettingPath.SettingFolderPath));
                            }
                            else
                            {
                                for (uint i = 0; i < num; i++)
                                {
                                    string key = "RecFolderPath" + i.ToString();
                                    string folder = IniFileHandler.GetPrivateProfileString("SET", key, "", SettingPath.CommonIniPath);
                                    if (folder.Length > 0)
                                    {
                                        defRecfolders.Add(new RecFolderInfo(folder));
                                    }
                                }
                            }
                        }
                    }
                }
                return defRecfolders;
            }
        }

        public static void SetCustomEpgTabInfoID()
        {
            for (int i = 0; i < Settings.Instance.CustomEpgTabList.Count; i++)
            {
                Settings.Instance.CustomEpgTabList[i].ID = i;
            }
        }

        public double FontHeight
        {
            get { return FontSize * CommonManager.Instance.VUtil.ItemFontNormal.GlyphType.Height; }
        }
        public double FontHeightTitle
        {
            get { return FontSizeTitle * CommonManager.Instance.VUtil.ItemFontTitle.GlyphType.Height; }
        }
        public double TunerFontHeight
        {
            get { return tunerFontSize * CommonManager.Instance.VUtil.ItemFontTunerNormal.GlyphType.Height; }
        }
        public double TunerFontHeightService
        {
            get { return tunerFontSizeService * CommonManager.Instance.VUtil.ItemFontTunerService.GlyphType.Height; }
        }
    }
}

