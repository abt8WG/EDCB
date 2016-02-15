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
using EpgTimer.DefineClass;
using EpgTimer.Common;

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

        public static bool CanReadInifile { get { return CommonManager.Instance.NW.IsConnected == false || IniSetting.Instance.CanReadInifile == true; } }
        public static bool CanUpdateInifile { get { return CommonManager.Instance.NW.IsConnected == false || IniSetting.Instance.CanUpdateInifile == true; } }
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
            get { return ModulePath; }
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
            get
            {
                return ModulePath.TrimEnd('\\') + "\\Setting"; // + (CommonManager.Instance.NWMode == false ? "" : "NW");
            }
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
            get
            {
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }
        }
        public static string ModuleName
        {
            get { return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location); }
        }
    }

    public class Settings
    {
        private bool useCustomEpgView;
        private List<CustomEpgTabInfo> customEpgTabList;
        private double minHeight;
        private double minimumHeight;
        private double serviceWidth;
        private double scrollSize;
        private string fontName;
        private double fontSize;
        private string fontNameTitle;
        private double fontSizeTitle;
        private bool fontBoldTitle;
        private bool noToolTip;
        private bool noBallonTips;
        private int forceHideBalloonTipSec;
        private bool playDClick;
        private bool recinfoErrCriticalDrops;
        private double dragScroll;
        private List<string> contentColorList;
        private List<UInt32> contentCustColorList;
        private List<string> epgEtcColors;
        private List<UInt32> epgEtcCustColors;
        private string reserveRectColorNormal;
        private string reserveRectColorNo;
        private string reserveRectColorNoTuner;
        private string reserveRectColorWarning;
        private string reserveRectColorAutoAddMissing;
        private string titleColor1;
        private string titleColor2;
        private UInt32 titleCustColor1;
        private UInt32 titleCustColor2;
        private bool reserveRectBackground;
        private string tunerFontNameService;
        private double tunerFontSizeService;
        private bool tunerFontBoldService;
        private string tunerFontName;
        private double tunerFontSize;
        private List<string> tunerServiceColors;
        private List<UInt32> tunerServiceCustColors;
        private double tunerMinHeight;
        private double tunerMinimumLine;
        private double tunerDragScroll;
        private double tunerScrollSize;
        private bool tunerMouseScrollAuto;
        private double tunerWidth;
        private bool tunerServiceNoWrap;
        private bool tunerTitleIndent;
        private bool tunerPopup;
        private bool tunerPopupRecinfo;
        private bool tunerInfoSingleClick;
        private bool tunerColorModeUse;
        private bool tunerDisplayOffReserve;
        private bool epgTitleIndent;
        private bool epgPopup;
        private bool epgPopupResOnly;
        private bool epgGradation;
        private bool epgGradationHeader;
        private string resColumnHead;
        private ListSortDirection resSortDirection;
        private System.Windows.WindowState lastWindowState;
        private double mainWndLeft;
        private double mainWndTop;
        private double mainWndWidth;
        private double mainWndHeight;
        private bool closeMin;
        private bool wakeMin;
        private bool applyMultiInstance;
        private bool viewButtonShowAsTab;
        private List<string> viewButtonList;
        private List<string> taskMenuList;
        private string cust1BtnName;
        private string cust1BtnCmd;
        private string cust1BtnCmdOpt;
        private string cust2BtnName;
        private string cust2BtnCmd;
        private string cust2BtnCmdOpt;
        private List<string> andKeyList;
        private List<string> notKeyList;
        private EpgSearchKeyInfo defSearchKey;
        private List<RecPresetItem> recPresetList;
        private string recInfoColumnHead;
        private ListSortDirection recInfoSortDirection;
        private long recInfoDropErrIgnore;
        private long recInfoDropWrnIgnore;
        private long recInfoScrambleIgnore;
        private List<string> recInfoDropExclude;
        private bool recInfoNoYear;
        private bool recInfoNoSecond;
        private bool recInfoNoDurSecond;
        private bool resInfoNoYear;
        private bool resInfoNoSecond;
        private bool resInfoNoDurSecond;
        private string tvTestExe;
        private string tvTestCmd;
        private bool nwTvMode;
        private bool nwTvModeUDP;
        private bool nwTvModeTCP;
        private string filePlayExe;
        private string filePlayCmd;
        private bool filePlayOnAirWithExe;
        private bool openFolderWithFileDialog;
        private List<IEPGStationInfo> iEpgStationList;
        private MenuSettingData menuSet;
        private bool nwMode;
        private string nwServerIP;
        private UInt32 nwServerPort;
        private UInt32 nwWaitPort;
        private string nwMacAdd;
        private SerializableSecureString nwPassword;
        private List<NWPresetItem> nwPreset;
        private bool wakeReconnectNW;
        private bool suspendCloseNW;
        private bool ngAutoEpgLoadNW;
        private bool chkSrvRegistTCP;
        private double chkSrvRegistInterval;
        private Int32 tvTestOpenWait;
        private Int32 tvTestChgBonWait;
        private List<string> recEndColors;          //録画済リストの背景色
        private List<UInt32> recEndCustColors;      //録画済リストのカスタム背景色
        private string listDefColor;                //各画面のリストのデフォルト文字色
        private UInt32 listDefCustColor;            //各画面のリストのデフォルトカスタム文字色
        private List<string> recModeFontColors;     //予約リストなどの録画モードごとの文字色
        private List<UInt32> recModeFontCustColors; //予約リストなどの録画モードごとのカスタム文字色
        private List<string> resBackColors;         //予約リストなどの背景色
        private List<UInt32> resBackCustColors;     //予約リストなどのカスタム背景色
        private List<string> statColors;            //予約リストなどの「状態」列の予約色
        private List<UInt32> statCustColors;        //予約リストなどの「状態」列のカスタム予約色
        private bool epgInfoSingleClick;
        private byte epgInfoOpenMode;
        private UInt32 execBat;
        private UInt32 suspendChk;
        private UInt32 suspendChkTime;
        private List<ListColumnInfo> reserveListColumn;
        private List<ListColumnInfo> recInfoListColumn;
        private List<ListColumnInfo> autoAddEpgColumn;
        private List<ListColumnInfo> autoAddManualColumn;
        private double searchWndLeft;
        private double searchWndTop;
        private double searchWndWidth;
        private double searchWndHeight;
        private bool searchWndPinned;
        private short autoSaveNotifyLog;
        private bool showTray;
        private bool minHide;
        private bool mouseScrollAuto;
        private int noStyle;
        private double reserveMinHeight;
        private bool reservePopup;
        private bool alwaysSaveEpgSetting;
        private bool cautionManyChange;
        private int cautionManyNum;
        private bool cautionOnRecChange;
        private int cautionOnRecMarginMin;
        private int keyDeleteDisplayItemNum;
        private bool displayNotifyEpgChange;
        private double displayNotifyJumpTime;
        private bool displayReserveAutoAddMissing;
        private SerializableDictionary<string, WINDOWPLACEMENT> placement;
        private bool infoWindowTopMost;
        private bool infoWindowEnabled;
        private bool tryEpgSetting;

        public bool UseCustomEpgView
        {
            get { return useCustomEpgView; }
            set { useCustomEpgView = value; }
        }
        public List<CustomEpgTabInfo> CustomEpgTabList
        {
            get { return customEpgTabList; }
            set { customEpgTabList = value; }
        }
        public double MinHeight
        {
            get { return minHeight; }
            set { minHeight = value; }
        }
        public double ServiceWidth
        {
            get { return serviceWidth; }
            set { serviceWidth = value; }
        }
        public double ScrollSize
        {
            get { return scrollSize; }
            set { scrollSize = value; }
        }
        public double MinimumHeight
        {
            get { return minimumHeight; }
            set { minimumHeight = value; }
        }
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
        public bool NoToolTip
        {
            get { return noToolTip; }
            set { noToolTip = value; }
        }
        public bool NoBallonTips
        {
            get { return noBallonTips; }
            set { noBallonTips = value; }
        }
        public int ForceHideBalloonTipSec
        {
            get { return forceHideBalloonTipSec; }
            set { forceHideBalloonTipSec = value; }
        }
        public bool PlayDClick
        {
            get { return playDClick; }
            set { playDClick = value; }
        }
        public bool RecinfoErrCriticalDrops
        {
            get { return recinfoErrCriticalDrops; }
            set { recinfoErrCriticalDrops = value; }
        }
        public double DragScroll
        {
            get { return dragScroll; }
            set { dragScroll = value; }
        }
        public List<string> ContentColorList
        {
            get { return contentColorList; }
            set { contentColorList = value; }
        }
        public List<UInt32> ContentCustColorList
        {
            get { return contentCustColorList; }
            set { contentCustColorList = value; }
        }
        public List<string> EpgEtcColors
        {
            get { return epgEtcColors; }
            set { epgEtcColors = value; }
        }
        public List<UInt32> EpgEtcCustColors
        {
            get { return epgEtcCustColors; }
            set { epgEtcCustColors = value; }
        }
        public string ReserveRectColorNormal
        {
            get { return reserveRectColorNormal; }
            set { reserveRectColorNormal = value; }
        }
        public string ReserveRectColorNo
        {
            get { return reserveRectColorNo; }
            set { reserveRectColorNo = value; }
        }
        public string ReserveRectColorNoTuner
        {
            get { return reserveRectColorNoTuner; }
            set { reserveRectColorNoTuner = value; }
        }
        public string ReserveRectColorWarning
        {
            get { return reserveRectColorWarning; }
            set { reserveRectColorWarning = value; }
        }
        public string ReserveRectColorAutoAddMissing
        {
            get { return reserveRectColorAutoAddMissing; }
            set { reserveRectColorAutoAddMissing = value; }
        }
        public bool ReserveRectBackground
        {
            get { return reserveRectBackground; }
            set { reserveRectBackground = value; }
        }
        public string TitleColor1
        {
            get { return titleColor1; }
            set { titleColor1 = value; }
        }
        public string TitleColor2
        {
            get { return titleColor2; }
            set { titleColor2 = value; }
        }
        public UInt32 TitleCustColor1
        {
            get { return titleCustColor1; }
            set { titleCustColor1 = value; }
        }
        public UInt32 TitleCustColor2
        {
            get { return titleCustColor2; }
            set { titleCustColor2 = value; }
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
        public List<string> TunerServiceColors
        {
            get { return tunerServiceColors; }
            set { tunerServiceColors = value; }
        }
        public List<UInt32> TunerServiceCustColors
        {
            get { return tunerServiceCustColors; }
            set { tunerServiceCustColors = value; }
        }
        public double TunerMinHeight
        {
            get { return tunerMinHeight; }
            set { tunerMinHeight = value; }
        }
        public double TunerMinimumLine
        {
            get { return tunerMinimumLine; }
            set { tunerMinimumLine = value; }
        }
        public double TunerDragScroll
        {
            get { return tunerDragScroll; }
            set { tunerDragScroll = value; }
        }
        public double TunerScrollSize
        {
            get { return tunerScrollSize; }
            set { tunerScrollSize = value; }
        }
        public bool TunerMouseScrollAuto
        {
            get { return tunerMouseScrollAuto; }
            set { tunerMouseScrollAuto = value; }
        }
        public double TunerWidth
        {
            get { return tunerWidth; }
            set { tunerWidth = value; }
        }
        public bool TunerServiceNoWrap
        {
            get { return tunerServiceNoWrap; }
            set { tunerServiceNoWrap = value; }
        }
        public bool TunerTitleIndent
        {
            get { return tunerTitleIndent; }
            set { tunerTitleIndent = value; }
        }
        public bool TunerPopup
        {
            get { return tunerPopup; }
            set { tunerPopup = value; }
        }
        public bool TunerPopupRecinfo
        {
            get { return tunerPopupRecinfo; }
            set { tunerPopupRecinfo = value; }
        }
        public bool TunerInfoSingleClick
        {
            get { return tunerInfoSingleClick; }
            set { tunerInfoSingleClick = value; }
        }
        public bool TunerColorModeUse
        {
            get { return tunerColorModeUse; }
            set { tunerColorModeUse = value; }
        }
        public bool TunerDisplayOffReserve
        {
            get { return tunerDisplayOffReserve; }
            set { tunerDisplayOffReserve = value; }
        }
        public bool EpgTitleIndent
        {
            get { return epgTitleIndent; }
            set { epgTitleIndent = value; }
        }
        public bool EpgPopup
        {
            get { return epgPopup; }
            set { epgPopup = value; }
        }
        public bool EpgPopupResOnly
        {
            get { return epgPopupResOnly; }
            set { epgPopupResOnly = value; }
        }
        public bool EpgGradation
        {
            get { return epgGradation; }
            set { epgGradation = value; }
        }
        public bool EpgGradationHeader
        {
            get { return epgGradationHeader; }
            set { epgGradationHeader = value; }
        }
        public string ResColumnHead
        {
            get { return resColumnHead; }
            set { resColumnHead = value; }
        }
        public ListSortDirection ResSortDirection
        {
            get { return resSortDirection; }
            set { resSortDirection = value; }
        }
        public System.Windows.WindowState LastWindowState
        {
            get { return lastWindowState; }
            set { lastWindowState = value; }
        }
        public double MainWndLeft
        {
            get { return mainWndLeft; }
            set { mainWndLeft = value; }
        }
        public double MainWndTop
        {
            get { return mainWndTop; }
            set { mainWndTop = value; }
        }
        public double MainWndWidth
        {
            get { return mainWndWidth; }
            set { mainWndWidth = value; }
        }
        public double MainWndHeight
        {
            get { return mainWndHeight; }
            set { mainWndHeight = value; }
        }
        public bool CloseMin
        {
            get { return closeMin; }
            set { closeMin = value; }
        }
        public bool WakeMin
        {
            get { return wakeMin; }
            set { wakeMin = value; }
        }
        public bool ApplyMultiInstance
        {
            get { return applyMultiInstance; }
            set { applyMultiInstance = value; }
        }
        public bool ViewButtonShowAsTab
        {
            get { return viewButtonShowAsTab; }
            set { viewButtonShowAsTab = value; }
        }
        public List<string> ViewButtonList
        {
            get { return viewButtonList; }
            set { viewButtonList = value; }
        }
        public List<string> TaskMenuList
        {
            get { return taskMenuList; }
            set { taskMenuList = value; }
        }
        public string Cust1BtnName
        {
            get { return cust1BtnName; }
            set { cust1BtnName = value; }
        }
        public string Cust1BtnCmd
        {
            get { return cust1BtnCmd; }
            set { cust1BtnCmd = value; }
        }
        public string Cust1BtnCmdOpt
        {
            get { return cust1BtnCmdOpt; }
            set { cust1BtnCmdOpt = value; }
        }
        public string Cust2BtnName
        {
            get { return cust2BtnName; }
            set { cust2BtnName = value; }
        }
        public string Cust2BtnCmd
        {
            get { return cust2BtnCmd; }
            set { cust2BtnCmd = value; }
        }
        public string Cust2BtnCmdOpt
        {
            get { return cust2BtnCmdOpt; }
            set { cust2BtnCmdOpt = value; }
        }
        public List<string> AndKeyList
        {
            get { return andKeyList; }
            set { andKeyList = value; }
        }
        public List<string> NotKeyList
        {
            get { return notKeyList; }
            set { notKeyList = value; }
        }
        public EpgSearchKeyInfo DefSearchKey
        {
            get { return defSearchKey; }
            set { defSearchKey = value; }
        }
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
        public string RecInfoColumnHead
        {
            get { return recInfoColumnHead; }
            set { recInfoColumnHead = value; }
        }
        public ListSortDirection RecInfoSortDirection
        {
            get { return recInfoSortDirection; }
            set { recInfoSortDirection = value; }
        }
        public long RecInfoDropErrIgnore
        {
            get { return recInfoDropErrIgnore; }
            set { recInfoDropErrIgnore = value; }
        }
        public long RecInfoDropWrnIgnore
        {
            get { return recInfoDropWrnIgnore; }
            set { recInfoDropWrnIgnore = value; }
        }
        public long RecInfoScrambleIgnore
        {
            get { return recInfoScrambleIgnore; }
            set { recInfoScrambleIgnore = value; }
        }
        public List<string> RecInfoDropExclude
        {
            get { return recInfoDropExclude; }
            set { recInfoDropExclude = value; }
        }
        public bool RecInfoNoYear
        {
            get { return recInfoNoYear; }
            set { recInfoNoYear = value; }
        }
        public bool RecInfoNoSecond
        {
            get { return recInfoNoSecond; }
            set { recInfoNoSecond = value; }
        }
        public bool RecInfoNoDurSecond
        {
            get { return recInfoNoDurSecond; }
            set { recInfoNoDurSecond = value; }
        }
        public bool ResInfoNoYear
        {
            get { return resInfoNoYear; }
            set { resInfoNoYear = value; }
        }
        public bool ResInfoNoSecond
        {
            get { return resInfoNoSecond; }
            set { resInfoNoSecond = value; }
        }
        public bool ResInfoNoDurSecond
        {
            get { return resInfoNoDurSecond; }
            set { resInfoNoDurSecond = value; }
        }
        public string TvTestExe
        {
            get { return tvTestExe; }
            set { tvTestExe = value; }
        }
        public string TvTestCmd
        {
            get { return tvTestCmd; }
            set { tvTestCmd = value; }
        }
        public bool NwTvMode
        {
            get { return nwTvMode; }
            set { nwTvMode = value; }
        }
        public bool NwTvModeUDP
        {
            get { return nwTvModeUDP; }
            set { nwTvModeUDP = value; }
        }
        public bool NwTvModeTCP
        {
            get { return nwTvModeTCP; }
            set { nwTvModeTCP = value; }
        }
        public string FilePlayExe
        {
            get { return filePlayExe; }
            set { filePlayExe = value; }
        }
        public string FilePlayCmd
        {
            get { return filePlayCmd; }
            set { filePlayCmd = value; }
        }
        public bool FilePlayOnAirWithExe
        {
            get { return filePlayOnAirWithExe; }
            set { filePlayOnAirWithExe = value; }
        }
        public bool OpenFolderWithFileDialog
        {
            get { return openFolderWithFileDialog; }
            set { openFolderWithFileDialog = value; }
        }
        public List<IEPGStationInfo> IEpgStationList
        {
            get { return iEpgStationList; }
            set { iEpgStationList = value; }
        }
        public MenuSettingData MenuSet
        {
            get { return menuSet; }
            set { menuSet = value; }
        }
        public bool NWMode
        {
            get { return nwMode; }
            set { nwMode = value; }
        }
        public string NWServerIP
        {
            get { return nwServerIP; }
            set { nwServerIP = value; }
        }
        public UInt32 NWServerPort
        {
            get { return nwServerPort; }
            set { nwServerPort = value; }
        }
        public UInt32 NWWaitPort
        {
            get { return nwWaitPort; }
            set { nwWaitPort = value; }
        }
        public string NWMacAdd
        {
            get { return nwMacAdd; }
            set { nwMacAdd = value; }
        }
        public SerializableSecureString NWPassword
        {
            get { return nwPassword; }
            set { nwPassword = value; }
        }
        public List<NWPresetItem> NWPerset
        {
            get { return nwPreset; }
            set { nwPreset = value; }
        }
        public bool WakeReconnectNW
        {
            get { return wakeReconnectNW; }
            set { wakeReconnectNW = value; }
        }
        public bool SuspendCloseNW
        {
            get { return suspendCloseNW; }
            set { suspendCloseNW = value; }
        }
        public bool NgAutoEpgLoadNW
        {
            get { return ngAutoEpgLoadNW; }
            set { ngAutoEpgLoadNW = value; }
        }
        public bool ChkSrvRegistTCP
        {
            get { return chkSrvRegistTCP; }
            set { chkSrvRegistTCP = value; }
        }
        public double ChkSrvRegistInterval
        {
            get { return chkSrvRegistInterval; }
            set { chkSrvRegistInterval = value; }
        }
        public Int32 TvTestOpenWait
        {
            get { return tvTestOpenWait; }
            set { tvTestOpenWait = value; }
        }
        public Int32 TvTestChgBonWait
        {
            get { return tvTestChgBonWait; }
            set { tvTestChgBonWait = value; }
        }
        public List<string> RecEndColors
        {
            get { return recEndColors; }
            set { recEndColors = value; }
        }
        public List<uint> RecEndCustColors
        {
            get { return recEndCustColors; }
            set { recEndCustColors = value; }
        }
        public string ListDefColor
        {
            get { return listDefColor; }
            set { listDefColor = value; }
        }
        public UInt32 ListDefCustColor
        {
            get { return listDefCustColor; }
            set { listDefCustColor = value; }
        }
        public List<string> RecModeFontColors
        {
            get { return recModeFontColors; }
            set { recModeFontColors = value; }
        }
        public List<uint> RecModeFontCustColors
        {
            get { return recModeFontCustColors; }
            set { recModeFontCustColors = value; }
        }
        public List<string> ResBackColors
        {
            get { return resBackColors; }
            set { resBackColors = value; }
        }
        public List<uint> ResBackCustColors
        {
            get { return resBackCustColors; }
            set { resBackCustColors = value; }
        }
        public List<string> StatColors
        {
            get { return statColors; }
            set { statColors = value; }
        }
        public List<uint> StatCustColors
        {
            get { return statCustColors; }
            set { statCustColors = value; }
        }
        public bool EpgInfoSingleClick
        {
            get { return epgInfoSingleClick; }
            set { epgInfoSingleClick = value; }
        }
        public byte EpgInfoOpenMode
        {
            get { return epgInfoOpenMode; }
            set { epgInfoOpenMode = value; }
        }
        public UInt32 ExecBat
        {
            get { return execBat; }
            set { execBat = value; }
        }
        public UInt32 SuspendChk
        {
            get { return suspendChk; }
            set { suspendChk = value; }
        }
        public UInt32 SuspendChkTime
        {
            get { return suspendChkTime; }
            set { suspendChkTime = value; }
        }
        public List<ListColumnInfo> ReserveListColumn
        {
            get { return reserveListColumn; }
            set { reserveListColumn = value; }
        }
        public List<ListColumnInfo> RecInfoListColumn
        {
            get { return recInfoListColumn; }
            set { recInfoListColumn = value; }
        }
        public List<ListColumnInfo> AutoAddEpgColumn
        {
            get { return autoAddEpgColumn; }
            set { autoAddEpgColumn = value; }
        }
        public List<ListColumnInfo> AutoAddManualColumn
        {
            get { return autoAddManualColumn; }
            set { autoAddManualColumn = value; }
        }
        public double SearchWndLeft
        {
            get { return searchWndLeft; }
            set { searchWndLeft = value; }
        }
        public double SearchWndTop
        {
            get { return searchWndTop; }
            set { searchWndTop = value; }
        }
        public double SearchWndWidth
        {
            get { return searchWndWidth; }
            set { searchWndWidth = value; }
        }
        public double SearchWndHeight
        {
            get { return searchWndHeight; }
            set { searchWndHeight = value; }
        }
        public bool SearchWndPinned
        {
            get { return searchWndPinned; }
            set { searchWndPinned = value; }
        }
        public short AutoSaveNotifyLog
        {
            get { return autoSaveNotifyLog; }
            set { autoSaveNotifyLog = value; }
        }
        public bool ShowTray
        {
            get { return showTray; }
            set { showTray = value; }
        }
        public bool MinHide
        {
            get { return minHide; }
            set { minHide = value; }
        }
        public bool MouseScrollAuto
        {
            get { return mouseScrollAuto; }
            set { mouseScrollAuto = value; }
        }
        public int NoStyle
        {
            get { return noStyle; }
            set { noStyle = value; }
        }
        public double ReserveMinHeight
        {
            get { return reserveMinHeight; }
            set { reserveMinHeight = value; }
        }
        public bool ReservePopup
        {
            get { return reservePopup; }
            set { reservePopup = value; }
        }
        public bool AlwaysSaveEpgSetting
        {
            get { return alwaysSaveEpgSetting; }
            set { alwaysSaveEpgSetting = value; }
        }
        public bool CautionManyChange
        {
            get { return cautionManyChange; }
            set { cautionManyChange = value; }
        }
        public int CautionManyNum
        {
            get { return cautionManyNum; }
            set { cautionManyNum = value; }
        }
        public bool CautionOnRecChange
        {
            get { return cautionOnRecChange; }
            set { cautionOnRecChange = value; }
        }
        public int CautionOnRecMarginMin
        {
            get { return cautionOnRecMarginMin; }
            set { cautionOnRecMarginMin = value; }
        }
        public int KeyDeleteDisplayItemNum
        {
            get { return keyDeleteDisplayItemNum; }
            set { keyDeleteDisplayItemNum = value; }
        }
        public bool DisplayNotifyEpgChange
        {
            get { return displayNotifyEpgChange; }
            set { displayNotifyEpgChange = value; }
        }
        public double DisplayNotifyJumpTime
        {
            get { return displayNotifyJumpTime; }
            set { displayNotifyJumpTime = value; }
        }
        public bool DisplayReserveAutoAddMissing
        {
            get { return displayReserveAutoAddMissing; }
            set { displayReserveAutoAddMissing = value; }
        }
        public SerializableDictionary<string, WINDOWPLACEMENT> Placement
        {
            get { return placement; }
            set { placement = value; }
        }
        public bool InfoWindowTopMost
        {
            get { return infoWindowTopMost; }
            set { infoWindowTopMost = value; }
        }
        public bool InfoWindowEnabled
        {
            get { return infoWindowEnabled; }
            set { infoWindowEnabled = value; }
        }
        public bool TryEpgSetting
        {
            get { return tryEpgSetting; }
            set { tryEpgSetting = value; }
        }
        /// <summary>
        /// 
        /// </summary>
        public bool RecLog_Enabled
        {
            get { return _RecLog_Enabled; }
            set { _RecLog_Enabled = value; }
        }
        bool _RecLog_Enabled = false;
        /// <summary>
        /// 
        /// </summary>
        public string RecLog_DB_MachineName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_RecLog_DB_MachineName))
                {
                    return Environment.MachineName;
                }
                else {
                    return _RecLog_DB_MachineName;
                }
            }
            set { _RecLog_DB_MachineName = value; }
        }
        string _RecLog_DB_MachineName = null;
        /// <summary>
        /// 
        /// </summary>
        public string RecLog_DB_InstanceName
        {
            get { return _RecLog_DB_InstanceName; }
            set { _RecLog_DB_InstanceName = value; }
        }
        string _RecLog_DB_InstanceName = "SQLEXPRESS";
        /// <summary>
        /// 
        /// </summary>
        public RecLogView.searchMethods RecLog_SearchMethod
        {
            get { return _RecLog_SearchMethod; }
            set { _RecLog_SearchMethod = value; }
        }
        RecLogView.searchMethods _RecLog_SearchMethod = RecLogView.searchMethods.Contrains;
        /// <summary>
        /// 
        /// </summary>
        public int RecLog_SearchColumn
        {
            get { return _RecLog_SearchColumn; }
            set { _RecLog_SearchColumn = value; }
        }
        int _RecLog_SearchColumn = (int)DB_RecLog.searchColumns.ALL;
        /// <summary>
        /// 
        /// </summary>
        public int RecLog_RecodeStatus
        {
            get { return _RecLog_RecodeStatuses; }
            set { _RecLog_RecodeStatuses = value; }
        }
        int _RecLog_RecodeStatuses = (int)RecLogItem.RecodeStatuses.ALL;
        /// <summary>
        /// 
        /// </summary>
        public int RecLog_SearchResultLimit
        {
            get { return _RecLog_SearchResultLimit; }
            set { _RecLog_SearchResultLimit = value; }
        }
        int _RecLog_SearchResultLimit = 50;
        /// <summary>
        /// 
        /// </summary>
        public int RecLogWindow_SearchResultLimit
        {
            get { return _RecLogWindow_SearchResultLimit; }
            set { _RecLogWindow_SearchResultLimit = value; }
        }
        int _RecLogWindow_SearchResultLimit = 20;

        public Settings()
        {
            useCustomEpgView = false;
            customEpgTabList = new List<CustomEpgTabInfo>();
            minHeight = 2;
            minimumHeight = 0;
            serviceWidth = 150;
            scrollSize = 240;
            fontName = System.Drawing.SystemFonts.DefaultFont.Name;
            fontSize = 12;
            fontNameTitle = System.Drawing.SystemFonts.DefaultFont.Name;
            fontSizeTitle = 12;
            fontBoldTitle = true;
            noToolTip = false;
            playDClick = false;
            recinfoErrCriticalDrops = false;
            dragScroll = 1.5;
            contentColorList = new List<string>();
            contentCustColorList = new List<uint>();
            epgEtcColors = new List<string>();
            epgEtcCustColors = new List<uint>();
            reserveRectColorNormal = "Lime";
            reserveRectColorNo = "Black";
            reserveRectColorNoTuner = "Red";
            reserveRectColorWarning = "Yellow";
            reserveRectColorAutoAddMissing = "Blue";
            titleColor1 = "Black";
            titleColor2 = "Black";
            titleCustColor1 = 0xFFFFFFFF;
            titleCustColor2 = 0xFFFFFFFF;
            reserveRectBackground = false;
            tunerFontNameService = System.Drawing.SystemFonts.DefaultFont.Name;
            tunerFontSizeService = 12;
            tunerFontBoldService = true;
            tunerFontName = System.Drawing.SystemFonts.DefaultFont.Name;
            tunerFontSize = 12;
            tunerServiceColors = new List<string>();
            tunerServiceCustColors = new List<uint>();
            tunerMinHeight = 2;
            tunerMinimumLine = 0;
            tunerDragScroll = 1.5;
            tunerScrollSize = 240;
            tunerMouseScrollAuto = false;
            tunerWidth = 150;
            tunerServiceNoWrap = true;
            tunerTitleIndent = true;
            tunerPopup = false;
            tunerPopupRecinfo = false;
            tunerInfoSingleClick = false;
            tunerColorModeUse = false;
            tunerDisplayOffReserve = false;
            epgTitleIndent = true;
            epgPopup = true;
            epgPopupResOnly = false;
            epgGradation = true;
            epgGradationHeader = true;
            resColumnHead = "";
            resSortDirection = ListSortDirection.Ascending;
            lastWindowState = System.Windows.WindowState.Normal;
            mainWndLeft = -100;
            mainWndTop = -100;
            mainWndWidth = -100;
            mainWndHeight = -100;
            closeMin = false;
            wakeMin = false;
            applyMultiInstance = false;
            viewButtonShowAsTab = false;
            viewButtonList = new List<string>();
            taskMenuList = new List<string>();
            cust1BtnName = "";
            cust1BtnCmd = "";
            cust1BtnCmdOpt = "";
            cust2BtnName = "";
            cust2BtnCmd = "";
            cust2BtnCmdOpt = "";
            andKeyList = new List<string>();
            notKeyList = new List<string>();
            defSearchKey = new EpgSearchKeyInfo();
            recPresetList = null;
            recInfoColumnHead = "";
            recInfoSortDirection = ListSortDirection.Ascending;
            recInfoDropErrIgnore = 0;
            recInfoDropWrnIgnore = 0;
            recInfoScrambleIgnore = 0;
            recInfoDropExclude = new List<string>();
            recInfoNoYear = false;
            recInfoNoSecond = false;
            recInfoNoDurSecond = false;
            resInfoNoYear = false;
            resInfoNoSecond = false;
            resInfoNoDurSecond = false;
            tvTestExe = "";
            tvTestCmd = "";
            nwTvMode = false;
            nwTvModeUDP = false;
            nwTvModeTCP = false;
            filePlayExe = "";
            filePlayCmd = "\"$FilePath$\"";
            filePlayOnAirWithExe = false;
            openFolderWithFileDialog = false;
            iEpgStationList = new List<IEPGStationInfo>();
            menuSet = new MenuSettingData();
            nwServerIP = "";
            nwServerPort = 4510;
            nwWaitPort = 4520;
            nwMacAdd = "";
            nwPassword = new SerializableSecureString();
            nwPreset = new List<NWPresetItem>();
            wakeReconnectNW = false;
            suspendCloseNW = false;
            ngAutoEpgLoadNW = false;
            chkSrvRegistTCP = false;
            chkSrvRegistInterval = 5;
            tvTestOpenWait = 2000;
            tvTestChgBonWait = 2000;
            recEndColors = new List<string>();
            recEndCustColors = new List<uint>();
            listDefColor = "カスタム";
            listDefCustColor= 0xFF042271;
            recModeFontColors = new List<string>();
            recModeFontCustColors= new List<uint>();
            resBackColors = new List<string>();
            resBackCustColors= new List<uint>();
            statColors = new List<string>();
            statCustColors= new List<uint>();
            epgInfoSingleClick = false;
            epgInfoOpenMode = 0;
            execBat = 0;
            suspendChk = 0;
            suspendChkTime = 15;
            reserveListColumn = new List<ListColumnInfo>();
            recInfoListColumn = new List<ListColumnInfo>();
            autoAddEpgColumn = new List<ListColumnInfo>();
            autoAddManualColumn = new List<ListColumnInfo>();
            searchWndLeft = -100;
            searchWndTop = -100;
            searchWndWidth = -100;
            searchWndHeight = -100;
            searchWndPinned = false;
            autoSaveNotifyLog = 0;
            showTray = true;
            minHide = true;
            mouseScrollAuto = false;
            noStyle = 0;
            reserveMinHeight = 2;
            reservePopup = false;
            alwaysSaveEpgSetting = false;
            cautionManyChange = true;
            cautionManyNum = 10;
            cautionOnRecChange = true;
            cautionOnRecMarginMin = 5;
            keyDeleteDisplayItemNum = 10;
            displayNotifyEpgChange = false;
            displayNotifyJumpTime = 3;
            displayReserveAutoAddMissing = false;
            placement = new SerializableDictionary<string, WINDOWPLACEMENT>();
            infoWindowTopMost = true;
            infoWindowEnabled = false;
            tryEpgSetting = true;
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

            try
            {
                // タイミング合わせにくいので、メニュー系のデータチェックは
                // MenuManager側のワークデータ作成時に実行する。

                SetCustomEpgTabInfoID();

                int num;
                List<string> defColors = new List<string>();

                //番組表の背景色
                num = 0x11;//番組表17色。過去に16色時代があった。
                if (Instance.contentColorList.Count < num)
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
                    _FillList(Instance.contentColorList, defColors);
                }
                num = 0x11 + 5;//番組表17色+予約枠5色
                _FillList(Instance.contentCustColorList, 0xFFFFFFFF, num);

                //チューナ画面各フォント色
                num = 2 + 5;//固定色2+優先度色5
                _FillList(Instance.tunerServiceColors, "Black", num);
                _FillList(Instance.tunerServiceCustColors, 0xFFFFFFFF, num);

                //番組表の時間軸のデフォルトの背景色、その他色
                num = 5;
                if (Instance.epgEtcColors.Count < num)
                {
                    defColors = new List<string>{
                        "MediumPurple"      //00-05時
                        ,"LightSeaGreen"    //06-11時
                        ,"LightSalmon"      //12-17時
                        ,"CornflowerBlue"   //18-23時
                        ,"LightSlateGray"   //サービス色
                    };
                    _FillList(Instance.epgEtcColors, defColors);
                }
                _FillList(Instance.epgEtcCustColors, 0xFFFFFFFF, num);

                //録画済み一覧背景色
                num = 3;
                if (Instance.recEndColors.Count < num)
                {
                    defColors = new List<string>{
                        "White"//デフォルト
                        ,"LightPink"//エラー
                        ,"LightYellow"//ワーニング
                    };
                    _FillList(Instance.recEndColors, defColors);
                }
                _FillList(Instance.recEndCustColors, 0xFFFFFFFF, num);

                //録画モード別予約文字色
                num = 6;
                _FillList(Instance.recModeFontColors, "カスタム", num);
                _FillList(Instance.recModeFontCustColors, 0xFF042271, num);

                //状態別予約背景色
                num = 5;
                if (Instance.resBackColors.Count < num)
                {
                    defColors = new List<string>{
                        "White"//通常
                        ,"LightGray"//無効
                        ,"LightPink"//チューナー不足
                        ,"LightYellow"//一部実行
                        ,"LightBlue"//自動予約登録不明
                    };
                    _FillList(Instance.resBackColors, defColors);
                }
                _FillList(Instance.resBackCustColors, 0xFFFFFFFF, num);

                //予約状態列文字色
                num = 3;
                if (Instance.statColors.Count < num)
                {
                    defColors = new List<string>{
                        "Blue"//予約中
                        ,"OrangeRed"//録画中
                        ,"LimeGreen"//放送中
                    };
                    _FillList(Instance.statColors, defColors);
                }
                _FillList(Instance.statCustColors, 0xFFFFFFFF, num);

                if (Instance.viewButtonList.Count == 0)
                {
                    Instance.viewButtonList.Add("設定");
                    Instance.viewButtonList.Add("（空白）");
                    Instance.viewButtonList.Add("再接続");
                    Instance.viewButtonList.Add("（空白）");
                    Instance.viewButtonList.Add("検索");
                    Instance.viewButtonList.Add("（空白）");
                    Instance.viewButtonList.Add("スタンバイ");
                    Instance.viewButtonList.Add("休止");
                    Instance.viewButtonList.Add("（空白）");
                    Instance.viewButtonList.Add("EPG取得");
                    Instance.viewButtonList.Add("（空白）");
                    Instance.viewButtonList.Add("EPG再読み込み");
                    Instance.viewButtonList.Add("（空白）");
                    Instance.viewButtonList.Add("終了");
                }
                if (Instance.taskMenuList.Count == 0)
                {
                    Instance.taskMenuList.Add("設定");
                    Instance.taskMenuList.Add("（セパレータ）");
                    Instance.taskMenuList.Add("再接続");
                    Instance.taskMenuList.Add("（セパレータ）");
                    Instance.taskMenuList.Add("スタンバイ");
                    Instance.taskMenuList.Add("休止");
                    Instance.taskMenuList.Add("（セパレータ）");
                    Instance.taskMenuList.Add("終了");
                }
                if (Instance.reserveListColumn.Count == 0)
                {
                    Instance.reserveListColumn.Add(new ListColumnInfo("StartTime", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("NetworkName", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("ServiceName", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("EventName", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("RecMode", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("Priority", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("Tuijyu", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("Comment", double.NaN));
                    Instance.reserveListColumn.Add(new ListColumnInfo("RecFileName", double.NaN));
                }
                if (Instance.recInfoListColumn.Count == 0)
                {
                    Instance.recInfoListColumn.Add(new ListColumnInfo("IsProtect", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("StartTime", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("NetworkName", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("ServiceName", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("EventName", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("Drops", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("Scrambles", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("Result", double.NaN));
                    Instance.recInfoListColumn.Add(new ListColumnInfo("RecFilePath", double.NaN));
                }
                if (Instance.autoAddEpgColumn.Count == 0)
                {
                    Instance.autoAddEpgColumn.Add(new ListColumnInfo("AndKey", double.NaN));
                    Instance.autoAddEpgColumn.Add(new ListColumnInfo("NotKey", double.NaN));
                    Instance.autoAddEpgColumn.Add(new ListColumnInfo("RegExp", double.NaN));
                    Instance.autoAddEpgColumn.Add(new ListColumnInfo("RecMode", double.NaN));
                    Instance.autoAddEpgColumn.Add(new ListColumnInfo("Priority", double.NaN));
                    Instance.autoAddEpgColumn.Add(new ListColumnInfo("Tuijyu", double.NaN));
                }
                if (Instance.autoAddManualColumn.Count == 0)
                {
                    Instance.autoAddManualColumn.Add(new ListColumnInfo("DayOfWeek", double.NaN));
                    Instance.autoAddManualColumn.Add(new ListColumnInfo("Time", double.NaN));
                    Instance.autoAddManualColumn.Add(new ListColumnInfo("Title", double.NaN));
                    Instance.autoAddManualColumn.Add(new ListColumnInfo("StationName", double.NaN));
                    Instance.autoAddManualColumn.Add(new ListColumnInfo("RecMode", double.NaN));
                    Instance.autoAddManualColumn.Add(new ListColumnInfo("Priority", double.NaN));
                }
                if (Instance.recInfoDropExclude.Count == 0)
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

