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
using System.Windows.Controls;

namespace EpgTimer
{
    using UserCtrlView;

    // EpgTimerNW では参照も更新もしない
    // CtlCmd 経由で取得、更新するよう書き換えるのが望ましい。
    class IniFileHandler
    {
        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint
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
        private static extern uint
            GetPrivateProfileStringByByteArray(string lpAppName,
            string lpKeyName, string lpDefault,
            byte[] lpReturnedString, uint nSize,
            string lpFileName);
        */

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int
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
        private static extern uint WritePrivateProfileStringW(
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
                        string old = null;
                        _items.TryGetValue(key, out old);
                        if (old != value)
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
                                if (list.Length != 2) {
                                    continue;
                                }
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
                    var o = this[SettingPath.CommonIniPath];
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
                    var o = this[SettingPath.CommonIniPath];
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

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint WritePrivateProfileStringW(string lpAppName, string lpKeyName, string lpString, string lpFileName);
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
                                        WritePrivateProfileStringW(lpAppName, lpKeyName, lpString, lpFileName);
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
        internal static bool IniPathExceptionSkip = false;
        private static string IniPath
        {
            // サーバー側のINIファイルの直接参照をしなくなったので、IniPath が必要になるのは
            // INIファイル更新の非対応サーバーに対してローカル接続(PIPE接続)した場合のみ。
            // ローカル接続する EpgTimer.exe と EpgTimerSrv.exe のバージョンは揃えるべきだとは思う。
            get
            {
                if (CommonManager.Instance.NWMode == false && IniPathExceptionSkip == false)
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
                        IniPathExceptionSkip = true;
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
            set
            {
                string path = value.Trim();
                bool isDefaultPath = string.Compare(path.TrimEnd('\\'), SettingPath.DefSettingFolderPath.TrimEnd('\\'), true) == 0;
                if (IniFileHandler.CanUpdateInifile == true)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "DataSavePath", isDefaultPath == true ? null : path, SettingPath.CommonIniPath);
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

        /// <summary>番組表のタイトル名以外に使うフォント名</summary>
        public string FontName
        {
            get { return fontName; }
            set { fontName = value; ViewUtil.ItemFontNormal = null; }
        }
        /// <summary>番組表のタイトル名以外に使うフォントサイズ</summary>
        public double FontSize
        {
            get { return fontSize; }
            set { fontSize = value; ViewUtil.ItemFontNormal = null; }
        }
        /// <summary>番組表のタイトル名に使うフォント名</summary>
        public string FontNameTitle
        {
            get { return fontNameTitle; }
            set { fontNameTitle = value; ViewUtil.ItemFontTitle = null; }
        }
        /// <summary>番組表のタイトル名に使うフォントサイズ</summary>
        public double FontSizeTitle
        {
            get { return fontSizeTitle; }
            set { fontSizeTitle = value; ViewUtil.ItemFontTitle = null; }
        }
        /// <summary>番組表のタイトル名に太字を使う</summary>
        public bool FontBoldTitle
        {
            get { return fontBoldTitle; }
            set { fontBoldTitle = value; ViewUtil.ItemFontTitle = null; }
        }
        /// <summary>使用予定チューナーのサービス名に使うフォント名</summary>
        public string TunerFontNameService
        {
            get { return tunerFontNameService; }
            set { tunerFontNameService = value; ViewUtil.ItemFontTunerService = null; }
        }
        /// <summary>使用予定チューナーのサービス名に使うフォントサイズ</summary>
        public double TunerFontSizeService
        {
            get { return tunerFontSizeService; }
            set { tunerFontSizeService = value; ViewUtil.ItemFontTunerService = null; }
        }
        /// <summary>使用予定チューナーのサービス名に太字を使う</summary>
        public bool TunerFontBoldService
        {
            get { return tunerFontBoldService; }
            set { tunerFontBoldService = value; ViewUtil.ItemFontTunerService = null; }
        }
        /// <summary>使用予定チューナーのサービス名以外に使うフォント名</summary>
        public string TunerFontName
        {
            get { return tunerFontName; }
            set { tunerFontName = value; ViewUtil.ItemFontTunerNormal = null; }
        }
        /// <summary>使用予定チューナーのサービス名以外に使うフォントサイズ</summary>
        public double TunerFontSize
        {
            get { return tunerFontSize; }
            set { tunerFontSize = value; ViewUtil.ItemFontTunerNormal = null; }
        }

        /// <summary>番組表でカスタマイズ表示を行う</summary>
        public bool UseCustomEpgView { get; set; }
        /// <summary>番組表のカスタマイズ表示用の情報</summary>
        public List<CustomEpgTabInfo> CustomEpgTabList { get; set; }
        /// <summary>番組表の1分当たりの高さ</summary>
        public double MinHeight { get; set; }
        /// <summary>番組表の最低表示行数</summary>
        public double MinimumHeight { get; set; }
        /// <summary>番組表のサービス１つの幅</summary>
        public double ServiceWidth { get; set; }
        /// <summary>番組表のマウススクロールサイズ</summary>
        public double ScrollSize { get; set; }
        //public string FontName { get; set; }
        //public double FontSize { get; set; }
        //public string FontNameTitle { get; set; }
        //public double FontSizeTitle { get; set; }
        //public bool FontBoldTitle { get; set; }
        /// <summary>ツールチップの表示を抑制する</summary>
        public bool NoToolTip { get; set; }
        /// <summary>バルーンチップでの動作通知を抑制する</summary>
        public bool NoBallonTips { get; set; }
        /// <summary>指定タイムアウトでバルーンチップを強制的に閉じる[秒]</summary>
        public int ForceHideBalloonTipSec { get; set; }
        /// <summary>録画済み一覧のダブルクリックで再生を行う</summary>
        public bool PlayDClick { get; set; }
        /// <summary>エラー・警告表示にドロップ(映像・音声)、スクランブル未解除(映像・音声)を使う</summary>
        public bool RecinfoErrCriticalDrops { get; set; }
        /// <summary>番組表のドラッグスクロール倍率</summary>
        public double DragScroll { get; set; }
        /// <summary>ジャンル別背景色</summary>
        public List<string> ContentColorList { get; set; }
        /// <summary>ジャンル別背景色・予約枠の色 (カスタム用)</summary>
        public List<UInt32> ContentCustColorList { get; set; }
        /// <summary>時刻表示背景色・サービス背景色</summary>
        public List<string> EpgEtcColors { get; set; }
        /// <summary>時刻表示背景色・サービス背景色 (カスタム用)</summary>
        public List<UInt32> EpgEtcCustColors { get; set; }
        /// <summary>通常の予約枠の色</summary>
        public string ReserveRectColorNormal { get; set; }
        /// <summary>無効の予約枠の色</summary>
        public string ReserveRectColorNo { get; set; }
        /// <summary>チューナー不足の予約枠の色</summary>
        public string ReserveRectColorNoTuner { get; set; }
        /// <summary>一部実行の予約枠の色</summary>
        public string ReserveRectColorWarning { get; set; }
        /// <summary>自動予約登録不明の予約枠の色</summary>
        public string ReserveRectColorAutoAddMissing { get; set; }
        /// <summary>予約枠を塗りつぶしで表示</summary>
        public bool ReserveRectBackground { get; set; }
        /// <summary>番組表のタイトル名に使う色</summary>
        public string TitleColor1 { get; set; }
        /// <summary>番組表のタイトル名以外に使う使う色</summary>
        public string TitleColor2 { get; set; }
        /// <summary>番組表のタイトル名に使う色 (カスタム用)</summary>
        public UInt32 TitleCustColor1 { get; set; }
        /// <summary>番組表のタイトル名以外に使う使う色 (カスタム用)</summary>
        public UInt32 TitleCustColor2 { get; set; }
        //public string TunerFontNameService { get; set; }
        //public double TunerFontSizeService { get; set; }
        //public bool TunerFontBoldService { get; set; }
        //public string TunerFontName { get; set; }
        //public double TunerFontSize { get; set; }
        /// <summary>使用予定チューナーで使う色</summary>
        public List<string> TunerServiceColors { get; set; }
        /// <summary>使用予定チューナーで使う色 (カスタム用)</summary>
        public List<UInt32> TunerServiceCustColors { get; set; }
        /// <summary>使用予定チューナーの1分当たりの高さ</summary>
        public double TunerMinHeight { get; set; }
        /// <summary>使用予定チューナーの最低表示行数</summary>
        public double TunerMinimumLine { get; set; }
        /// <summary>使用予定チューナーのドラッグスクロール倍率</summary>
        public double TunerDragScroll { get; set; }
        /// <summary>使用予定チューナーのマウススクロールサイズ</summary>
        public double TunerScrollSize { get; set; }
        /// <summary>使用予定チューナーのマウススクロールサイズを自動で決める</summary>
        public bool TunerMouseScrollAuto { get; set; }
        /// <summary>使用予定チューナーのサービス１つの幅</summary>
        public double TunerWidth { get; set; }
        /// <summary>使用予定チューナーのサービス名を改行しない</summary>
        public bool TunerServiceNoWrap { get; set; }
        /// <summary>使用予定チューナーのサービス名と番組名の表示位置をあわせる</summary>
        public bool TunerTitleIndent { get; set; }
        /// <summary>使用予定チューナーの予約をポップアップ表示する</summary>
        public bool TunerPopup { get; set; }
        /// <summary>予約のポップアップ表示 (マウスオーバー/クリックしたとき)</summary>
        public int TunerPopupMode { get; set; }
        /// <summary>使用予定チューナーのポップアップに優先度と録画モードを表示する</summary>
        public bool TunerPopupRecinfo { get; set; }
        /// <summary>使用予定チューナーのシングルクリックで予約ダイアログを開く</summary>
        public bool TunerInfoSingleClick { get; set; }
        /// <summary>使用予定チューナーのサービスのフォント色を優先度で変える</summary>
        public bool TunerColorModeUse { get; set; }
        /// <summary>使用予定チューナーで無効の予約も表示する</summary>
        public bool TunerDisplayOffReserve { get; set; }
        /// <summary>番組表の番組名と番組内容の表示位置をあわせる</summary>
        public bool EpgTitleIndent { get; set; }
        /// <summary>番組表の番組内容をポップアップ表示する</summary>
        public bool EpgPopup { get; set; }
        /// <summary>番組表の予約のある番組のみポップアップする</summary>
        public bool EpgPopupResOnly { get; set; }
        /// <summary>番組内容のポップアップ表示 (マウスオーバー/クリックしたとき/予約のみマウスオーバーで、他はクリックしたとき)</summary>
        public int EpgPopupMode { get; set; }
        /// <summary>番組表の番組内容をグラデーション表示する</summary>
        public bool EpgGradation { get; set; }
        /// <summary>番組表のサービス・時刻軸をグラデーション表示する</summary>
        public bool EpgGradationHeader { get; set; }
        /// <summary>古い番組データの表示を抑制する</summary>
        public bool EpgNoDisplayOld { get; set; }
        /// <summary>古い番組データをn日前まで表示</summary>
        public double EpgNoDisplayOldDays { get; set; }
        /// <summary>番組表の並べ替えする列のヘッダー名</summary>
        public string ResColumnHead { get; set; }
        /// <summary>番組表の並べ替え操作の方向</summary>
        public ListSortDirection ResSortDirection { get; set; }
        /// <summary>メインウィンドウの状態</summary>
        public WindowState LastWindowState { get; set; }
        /// <summary>メインウィンドウの左端の位置</summary>
        public double MainWndLeft { get; set; }
        /// <summary>メインウィンドウの上端の位置</summary>
        public double MainWndTop { get; set; }
        /// <summary>メインウィンドウの幅</summary>
        public double MainWndWidth { get; set; }
        /// <summary>メインウィンドウの高さ</summary>
        public double MainWndHeight { get; set; }
        /// <summary>×ボタンで最小化する</summary>
        public bool CloseMin { get; set; }
        /// <summary>最小化で起動する</summary>
        public bool WakeMin { get; set; }
        /// <summary>上部表示ボタンをタブの位置に表示</summary>
        public bool ViewButtonShowAsTab { get; set; }
        /// <summary>上部表示ボタンの表示項目</summary>
        public List<string> ViewButtonList { get; set; }
        /// <summary>タスクアイコンの右クリック表示項目</summary>
        public List<string> TaskMenuList { get; set; }
        /// <summary>カスタム1ボタンの表示名</summary>
        public string Cust1BtnName { get; set; }
        /// <summary>カスタム1ボタンの実行exe</summary>
        public string Cust1BtnCmd { get; set; }
        /// <summary>カスタム1ボタンのコマンドラインオプション</summary>
        public string Cust1BtnCmdOpt { get; set; }
        /// <summary>カスタム2ボタンの表示名</summary>
        public string Cust2BtnName { get; set; }
        /// <summary>カスタム2ボタンの実行exe</summary>
        public string Cust2BtnCmd { get; set; }
        /// <summary>カスタム2ボタンのコマンドラインオプション</summary>
        public string Cust2BtnCmdOpt { get; set; }
        /// <summary>カスタム3ボタンの表示名</summary>
        public string Cust3BtnName { get; set; }
        /// <summary>カスタム3ボタンの実行exe</summary>
        public string Cust3BtnCmd { get; set; }
        /// <summary>カスタム3ボタンのコマンドラインオプション</summary>
        public string Cust3BtnCmdOpt { get; set; }
        /// <summary>検索用検索キーワードの履歴</summary>
        public List<string> AndKeyList { get; set; }
        /// <summary>検索用NOTキーワードの履歴</summary>
        public List<string> NotKeyList { get; set; }
        /// <summary>検索のデフォルト設定</summary>
        public EpgSearchKeyInfo DefSearchKey { get; set; }
        private List<RecPresetItem> recPresetList = null;
        /// <summary>録画プリセット情報</summary>
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
        /// <summary>録画済み一覧の並べ替えする列のヘッダー名</summary>
        public string RecInfoColumnHead { get; set; }
        /// <summary>録画済み一覧の並べ替え操作の方向</summary>
        public ListSortDirection RecInfoSortDirection { get; set; }
        /// <summary>ドロップをエラー扱いしない閾値</summary>
        public long RecInfoDropErrIgnore { get; set; }
        /// <summary>ドロップを警告扱いしない閾値</summary>
        public long RecInfoDropWrnIgnore { get; set; }
        /// <summary>スクランブル未解除を警告扱いしない閾値</summary>
        public long RecInfoScrambleIgnore { get; set; }
        /// <summary>エラーカウントに含めないドロップ・スクランブル未解除に含めない項目のリスト</summary>
        public List<string> RecInfoDropExclude { get; set; }
        /// <summary>録画済み一覧のリスト表示で'年'の表示を省略する</summary>
        public bool RecInfoNoYear { get; set; }
        /// <summary>録画済み一覧のリスト表示で'秒'の表示を省略する</summary>
        public bool RecInfoNoSecond { get; set; }
        /// <summary>録画済み一覧のリスト表示で長さの'秒'の表示を省略する</summary>
        public bool RecInfoNoDurSecond { get; set; }
        /// <summary>録画済み一覧以外のリスト表示で'年'の表示を省略する</summary>
        public bool ResInfoNoYear { get; set; }
        /// <summary>録画済み一覧以外のリスト表示で'秒'の表示を省略する</summary>
        public bool ResInfoNoSecond { get; set; }
        /// <summary>録画済み一覧以外のリスト表示で長さの'秒'の表示を省略する</summary>
        public bool ResInfoNoDurSecond { get; set; }
        /// <summary>TvTest.exeのパス</summary>
        public string TvTestExe { get; set; }
        /// <summary>TvTest.exeのコマンドライン引数</summary>
        public string TvTestCmd { get; set; }
        /// <summary>NetworkTVモード(録画用アプリやEpgTimerSrvからのUDP、TCP送信)で視聴する</summary>
        public bool NwTvMode { get; set; }
        /// <summary>NetworkTVモードでUDPを使う</summary>
        public bool NwTvModeUDP { get; set; }
        /// <summary>NetworkTVモードでTCPを使う</summary>
        public bool NwTvModeTCP { get; set; }
        /// <summary>再生アプリのexeのパス</summary>
        public string FilePlayExe { get; set; }
        /// <summary>再生アプリのexeのコマンドライン引数</summary>
        public string FilePlayCmd { get; set; }
        /// <summary>再生アプリを追っかけ再生にも使用する</summary>
        public bool FilePlayOnAirWithExe { get; set; }
        /// <summary>フォルダー選択に OpenFileDialog を使う (設定画面無し)</summary>
        public bool OpenFolderWithFileDialog { get; set; }
        /// <summary>iEPG放送局の項目のリスト</summary>
        public List<IEPGStationInfo> IEpgStationList { get; set; }
        /// <summary>メニューの設定情報</summary>
        public MenuSettingData MenuSet { get; set; }
        /// <summary>リモート接続をする</summary>
        public bool NWMode { get; set; }
        /// <summary>リモート接続先の IP アドレス</summary>
        public string NWServerIP { get; set; }
        /// <summary>リモート接続先のポート番号</summary>
        public UInt32 NWServerPort { get; set; }
        /// <summary>リモート接続時の待ち受けポート (0で待ち受けしない)</summary>
        public UInt32 NWWaitPort { get; set; }
        /// <summary>リモート接続先の Wake on LAN 用 MAC アドレス</summary>
        public string NWMacAdd { get; set; }
        /// <summary>リモート接続時の認証用パスワード</summary>
        public SerializableSecureString NWPassword { get; set; }
        /// <summary>旧接続先プリセット</summary>
        public List<NWPresetItem> NWPerset { get; set; }            // obsolete because of typo of 'NWPreset'
        /// <summary>接続先プリセットのリスト</summary>
        public List<NWPresetItem> NWPreset { get; set; }
        /// <summary>起動時に前回接続サーバーに接続する</summary>
        public bool WakeReconnectNW { get; set; }
        /// <summary>Wake on LAN してから接続する</summary>
        public bool WoLWait { get; set; }
        /// <summary>接続失敗時に Wake on LAN で再接続を試みる</summary>
        public bool WoLWaitRecconect { get; set; }
        /// <summary>Wake on LAN 待機時間[秒]</summary>
        public double WoLWaitSecond { get; set; }
        /// <summary>休止／スタンバイ移行時にEpgTimerNWを終了する</summary>
        public bool SuspendCloseNW { get; set; }
        /// <summary>EPGデータを自動的に読み込まない</summary>
        public bool NgAutoEpgLoadNW { get; set; }
        /// <summary>EpgTimerSrvとの接続維持を試みる</summary>
        public bool ChkSrvRegistTCP { get; set; }
        /// <summary>EpgTimerSrvとの接続維持を試みる時間間隔[分]</summary>
        public double ChkSrvRegistInterval { get; set; }
        /// <summary>TvTestの起動を待つ時間[ミリ秒]</summary>
        public Int32 TvTestOpenWait { get; set; }
        /// <summary>BonDriverの切り替えを待つ時間[ミリ秒]</summary>
        public Int32 TvTestChgBonWait { get; set; }
        /// <summary>録画済一覧の背景色</summary>
        public List<string> RecEndColors { get; set; }
        /// <summary>録画済一覧の背景色 (カスタム用)</summary>
        public List<uint> RecEndCustColors { get; set; }
        /// <summary>各画面のリストのデフォルト文字色</summary>
        public string ListDefColor { get; set; }
        /// <summary>各画面のリストのデフォルト文字色 (カスタム用)</summary>
        public UInt32 ListDefCustColor { get; set; }
        /// <summary>予約リストなどの録画モードごとの文字色</summary>
        public List<string> RecModeFontColors { get; set; }
        /// <summary>予約リストなどの録画モードごとの文字色 (カスタム用)</summary>
        public List<uint> RecModeFontCustColors { get; set; }
        /// <summary>予約リストなどの背景色</summary>
        public List<string> ResBackColors { get; set; }
        /// <summary>予約リストなどの背景色 (カスタム用)</summary>
        public List<uint> ResBackCustColors { get; set; }
        /// <summary>予約リストなどの「状態」列の予約色</summary>
        public List<string> StatColors { get; set; }
        /// <summary>予約リストなどの「状態」列の予約色 (カスタム用)</summary>
        public List<uint> StatCustColors { get; set; }
        /// <summary>番組表のシングルクリックで予約ダイアログを開く</summary>
        public bool EpgInfoSingleClick { get; set; }
        /// <summary>番組表の番組詳細タブを選択した状態で予約ダイアログを開く</summary>
        public byte EpgInfoOpenMode { get; set; }
        /// <summary>BATファイルの起動方法 (0:最小化, 1:非表示) (設定画面無し)</summary>
        public UInt32 ExecBat { get; set; }
        /// <summary>スタンバイ、休止ボタンでもカウントダウンを表示</summary>
        public UInt32 SuspendChk { get; set; }
        /// <summary>スタンバイ、休止ボタンでもカウントダウンを表示する時間[秒]</summary>
        public UInt32 SuspendChkTime { get; set; }
        /// <summary>予約一覧で表示する列項目のリスト</summary>
        public List<ListColumnInfo> ReserveListColumn { get; set; }
        /// <summary>録画済み一覧で表示する列項目のリスト</summary>
        public List<ListColumnInfo> RecInfoListColumn { get; set; }
        /// <summary>キーワード予約で表示する列項目のリスト</summary>
        public List<ListColumnInfo> AutoAddEpgColumn { get; set; }
        /// <summary>プログラム予約で表示する列項目のリスト</summary>
        public List<ListColumnInfo> AutoAddManualColumn { get; set; }
        /// <summary>番組表のリスト表示で表示する列項目のリスト</summary>
        public List<ListColumnInfo> EpgListColumn { get; set; }
        /// <summary>番組表のリスト表示の並べ替えする列のヘッダー名</summary>
        public string EpgListColumnHead { get; set; }
        /// <summary>番組表のリスト表示の並べ替え操作の方向</summary>
        public ListSortDirection EpgListSortDirection { get; set; }
        /// <summary>検索で表示する列項目のリスト</summary>
        public List<ListColumnInfo> SearchWndColumn { get; set; }
        /// <summary>検索の並べ替えする列のヘッダー名</summary>
        public string SearchColumnHead { get; set; }
        /// <summary>検索の並べ替え操作の方向</summary>
        public ListSortDirection SearchSortDirection { get; set; }
        /// <summary>検索ウィンドウ用</summary>
        public HidableWindowSet SearchWndSet { get; set; }
        /// <summary>検索/キーワード予約ダイアログで検索語を保存する</summary>
        public bool SaveSearchKeyword { get; set; }
        /// <summary>予約簡易検索で表示する列項目のリスト</summary>
        public List<ListColumnInfo> InfoSearchWndColumn { get; set; }
        /// <summary>予約簡易検索の並べ替えする列のヘッダー名</summary>
        public string InfoSearchColumnHead { get; set; }
        /// <summary>予約簡易検索の並べ替え操作の方向</summary>
        public ListSortDirection InfoSearchSortDirection { get; set; }
        /// <summary>予約簡易検索の検索語</summary>
        public string InfoSearchLastWord { get; set; }
        /// <summary>予約簡易検索のタイトルのみ検索</summary>
        public bool InfoSearchTitleOnly { get; set; }
        /// <summary>予約簡易検索で予約を検索対象にする</summary>
        public bool InfoSearchReserveInfo { get; set; }
        /// <summary>予約簡易検索で録画結果を検索対象にする</summary>
        public bool InfoSearchRecInfo { get; set; }
        /// <summary>予約簡易検索でキーワード予約を検索対象にする</summary>
        public bool InfoSearchEpgAutoAddInfo { get; set; }
        /// <summary>予約簡易検索でプログラム予約を検索対象にする</summary>
        public bool InfoSearchManualAutoAddInfo { get; set; }
        /// <summary>予約簡易検索で検索結果のツールチップを表示する</summary>
        public bool InfoSearchItemTooltip { get; set; }
        /// <summary>予約簡易検索ウィンドウ</summary>
        public HidableWindowSet InfoSearchWndSet { get; set; }
        /// <summary>情報通知をファイルに記録する</summary>
        public short AutoSaveNotifyLog { get; set; }
        /// <summary>タスクトレイアイコンを表示する</summary>
        public bool ShowTray { get; set; }
        /// <summary>最小化時にタスクトレイに格納する</summary>
        public bool MinHide { get; set; }
        /// <summary>番組表のマウススクロールサイズを自動で決める</summary>
        public bool MouseScrollAuto { get; set; }
        /// <summary>テーマを適用しない</summary>
        public int NoStyle { get; set; }
        /// <summary>適用するテーマファイルのパス</summary>
        public string StyleXamlPath { get; set; }
        /// <summary>多数の項目を処理するとき警告する</summary>
        public bool CautionManyChange { get; set; }
        /// <summary>処理を警告する時の項目数</summary>
        public int CautionManyNum { get; set; }
        /// <summary>録画中または開始直前の予約を変更・削除するとき警告する</summary>
        public bool CautionOnRecChange { get; set; }
        /// <summary>予約を変更・削除するとき警告するまでの時間[分]</summary>
        public int CautionOnRecMarginMin { get; set; }
        /// <summary>自動予約登録の変更時に合わせて予約を変更する</summary>
        public bool SyncResAutoAddChange { get; set; }
        /// <summary>自動予約登録の変更時、予約を一度削除してから再登録する (無効の予約を除く)</summary>
        public bool SyncResAutoAddChgNewRes { get; set; }
        /// <summary>自動予約登録の削除時に合わせて予約を削除する</summary>
        public bool SyncResAutoAddDelete { get; set; }
        /// <summary>予約削除時の確認時に表示する項目数 (設定画面無し)</summary>
        public int KeyDeleteDisplayItemNum { get; set; }
        /// <summary>番組表の切り替え時にも強調表示をする</summary>
        public bool DisplayNotifyEpgChange { get; set; }
        /// <summary>番組表などへジャンプしたときの強調表示の継続時間[秒]</summary>
        public double DisplayNotifyJumpTime { get; set; }
        /// <summary>自動予約登録(キーワード予約、プログラム予約)を見失った予約を強調表示する</summary>
        public bool DisplayReserveAutoAddMissing { get; set; }
        /// <summary>番組表を一時的に変更する</summary>
        public bool TryEpgSetting { get; set; }
        /// <summary>深夜時間表示をする</summary>
        public bool LaterTimeUse { get; set; }
        /// <summary>深夜時間とする最終時刻[時]</summary>
        public int LaterTimeHour { get; set; }
        /// <summary>検索/キーワード予約ダイアログの録画設定タブにプリセットを表示する</summary>
        public bool DisplayPresetOnSearch { get; set; }
        /// <summary>番組情報・エラーログをキャッシュする</summary>
        public bool RecInfoExtraDataCache { get; set; }
        /// <summary>番組情報・エラーログのキャッシュ時、不要データを削除する</summary>
        public bool RecInfoExtraDataCacheOptimize { get; set; }
        /// <summary>番組情報・エラーログのキャッシュ時、再接続時も保持する</summary>
        public bool RecInfoExtraDataCacheKeepConnect { get; set; }
        /// <summary>タスクトレイアイコンのツールチップを更新する</summary>
        public bool UpdateTaskText { get; set; }
        /// <summary>ステータス表示をする</summary>
        public bool DisplayStatus { get; set; }
        /// <summary>主要操作の操作結果を表示をする</summary>
        public bool DisplayStatusNotify { get; set; }
        /// <summary>予約一覧のボタンを表示する</summary>
        public bool IsVisibleReserveView { get; set; }
        /// <summary>録画済み一覧のボタンを表示する</summary>
        public bool IsVisibleRecInfoView { get; set; }
        /// <summary>自動予約登録のボタンを表示する</summary>
        public bool IsVisibleAutoAddView { get; set; }
        /// <summary>自動予約登録の「並び替え」のボタンのみを表示する</summary>
        public bool IsVisibleAutoAddViewMoveOnly { get; set; }
        /// <summary>各画面のボタン列の位置</summary>
        public Dock MainViewButtonsDock { get; set; }
        /// <summary>初期表示画面</summary>
        public CtxmCode StartTab { get; set; }

        /// <summary>多重起動を許す</summary>
        public bool ApplyMultiInstance { get; set; }
        /// <summary>ウィンドウのサイズや位置、状態などを保存するリスト</summary>
        public SerializableDictionary<string, WINDOWPLACEMENT> Placement { get; set; }
        /// <summary>予約簡易表示で表示する列項目のリスト</summary>
        public List<ListColumnInfo> InfoWindowListColumn { get; set; }
        /// <summary>予約簡易表示のウィンドウタイトルを表示する</summary>
        public bool InfoWindowTitleIsVisible { get; set; }
        /// <summary>予約簡易表示のリストのヘッダーを表示する</summary>
        public bool InfoWindowHeaderIsVisible { get; set; }
        /// <summary>予約簡易表示を最前面に表示する</summary>
        public bool InfoWindowTopMost { get; set; }
        /// <summary>予約簡易表示を最背面に表示する</summary>
        public bool InfoWindowBottomMost { get; set; }
        /// <summary>予約簡易表示で無効予約を表示する</summary>
        public bool InfoWindowDisabledReserveItemVisible { get; set; }
        /// <summary>予約簡易表示を起動時に表示する</summary>
        public bool InfoWindowEnabled { get; set; }
        /// <summary>予約簡易表示のプログレスバーの更新間隔[秒]</summary>
        public int InfoWindowRefreshInterval { get; set; }
        /// <summary>予約簡易表示の進行状況の表示基準を放送時間とする(⇔録画マージンを考慮する)</summary>
        public bool InfoWindowBasedOnBroadcast { get; set; }
        /// <summary>予約簡易表示の表示範囲の選択</summary>
        public int InfoWindowItemFilterLevel { get; set; }
        /// <summary>予約簡易表示のプログレスバーの選択</summary>
        public int InfoWindowItemProgressBarType { get; set; }
        /// <summary>予約簡易表示のプログレスバーの色</summary>
        public List<string> InfoWindowItemProgressBarColors { get; set; }
        /// <summary>予約簡易表示のプログレスバーの色 (カスタム用)</summary>
        public List<UInt32> InfoWindowItemProgressBarCustColors { get; set; }
        /// <summary>予約簡易表示のプログレスバーの背景色を透明にする</summary>
        public bool InfoWindowItemProgressBarTransparent { get; set; }
        /// <summary>予約簡易表示の表示制限件数</summary>
        public int InfoWindowItemTopN { get; set; }
        /// <summary>予約簡易表示の表示範囲1[秒]</summary>
        public int InfoWindowItemLevel1Seconds { get; set; }
        /// <summary>予約簡易表示の表示範囲2[秒]</summary>
        public int InfoWindowItemLevel2Seconds { get; set; }
        /// <summary>予約簡易表示の表示範囲3[秒]</summary>
        public int InfoWindowItemLevel3Seconds { get; set; }
        /// <summary>予約簡易表示の背景色</summary>
        public List<string> InfoWindowItemBgColors { get; set; }
        /// <summary>予約簡易表示の背景色 (カスタム用)</summary>
        public List<UInt32> InfoWindowItemBgCustColors { get; set; }
        /// <summary>自動予約登録画面で録画予約項目をツールチップ表示する</summary>
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
            TunerPopupMode = 0;
            TunerPopupRecinfo = false;
            TunerInfoSingleClick = false;
            TunerColorModeUse = false;
            TunerDisplayOffReserve = false;
            EpgTitleIndent = true;
            EpgPopup = true;
            EpgPopupMode = 0;
            EpgGradation = true;
            EpgGradationHeader = true;
            EpgNoDisplayOld = false;
            EpgNoDisplayOldDays = 1;
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
            Cust3BtnName = "";
            Cust3BtnCmd = "";
            Cust3BtnCmdOpt = "";
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
            NWWaitPort = 0;
            NWMacAdd = "";
            NWPassword = new SerializableSecureString();
            NWPerset = new List<NWPresetItem>();
            NWPreset = new List<NWPresetItem>();
            WakeReconnectNW = false;
            WoLWaitRecconect = false;
            WoLWaitSecond= 30;
            WoLWait = false;
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
            SearchWndSet = new HidableWindowSet();
            SaveSearchKeyword = true;
            InfoSearchWndColumn = new List<ListColumnInfo>();
            InfoSearchColumnHead = "";
            InfoSearchSortDirection = ListSortDirection.Ascending;
            InfoSearchLastWord = "";
            InfoSearchTitleOnly = true;
            InfoSearchReserveInfo = true;
            InfoSearchRecInfo = true;
            InfoSearchEpgAutoAddInfo = true;
            InfoSearchManualAutoAddInfo = true;
            InfoSearchItemTooltip = true;
            InfoSearchWndSet = new HidableWindowSet();
            AutoSaveNotifyLog = 0;
            ShowTray = true;
            MinHide = true;
            MouseScrollAuto = false;
            NoStyle = 0;
            StyleXamlPath = Assembly.GetEntryAssembly().Location + ".rd.xaml";
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
            RecInfoExtraDataCache = true;
            RecInfoExtraDataCacheOptimize = true;
            RecInfoExtraDataCacheKeepConnect = false;

            recPresetList = null;
            ApplyMultiInstance = false;
            Placement = new SerializableDictionary<string, WINDOWPLACEMENT>();

            InfoWindowListColumn = new List<ListColumnInfo>();
            InfoWindowTitleIsVisible = true;
            InfoWindowHeaderIsVisible = true;
            InfoWindowTopMost = true;
            InfoWindowBottomMost = false;
            InfoWindowDisabledReserveItemVisible = false;
            InfoWindowEnabled = false;
            InfoWindowRefreshInterval = 10;
            InfoWindowBasedOnBroadcast = true;
            InfoWindowItemFilterLevel = 10;
            InfoWindowItemProgressBarType = 1;
            InfoWindowItemProgressBarColors = new List<string>();
            InfoWindowItemProgressBarCustColors = new List<UInt32>();
            InfoWindowItemProgressBarTransparent = true;
            InfoWindowItemTopN = 10;
            InfoWindowItemLevel1Seconds = 0;
            InfoWindowItemLevel2Seconds = 15 * 60;
            InfoWindowItemLevel3Seconds = 8 * 60 * 60;
            InfoWindowItemBgColors = new List<string>();
            InfoWindowItemBgCustColors = new List<UInt32>();

            RecItemToolTip = false;
            UpdateTaskText = false;
            DisplayStatus = false;
            DisplayStatusNotify = false;
            IsVisibleReserveView = true;
            IsVisibleRecInfoView = true;
            IsVisibleAutoAddView = true;
            IsVisibleAutoAddViewMoveOnly = false;
            MainViewButtonsDock = Dock.Right;
            StartTab = CtxmCode.ReserveView;
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

                //予約簡易表示のプログレスバー色
                num = 2;
                if (Instance.InfoWindowItemProgressBarColors.Count < num)
                {
                    defColors = new List<string>
                    {
                        "LightGreen", // Foreground
                        "Transparent" // Background
                    };
                    _FillList(Instance.InfoWindowItemProgressBarColors, defColors);
                }
                _FillList(Instance.InfoWindowItemProgressBarCustColors, 0xFFFFFFFF, num);

                //予約簡易表示の予約状態列背景色
                num = 5;
                if (Instance.InfoWindowItemBgColors.Count < num)
                {
                    defColors = new List<string>{
                        "OrangeRed",    // ReserveItemLevel1
                        "DarkOrange",   // ReserveItemLevel2
                        "LightCyan",    // ReserveItemLevel3
                        "LightGray",    // ReserveItemDisabled
                        "LightPink",    // ReserveItemError
                    };
                    _FillList(Instance.InfoWindowItemBgColors, defColors);
                }
                _FillList(Instance.InfoWindowItemBgCustColors, 0xFFFFFFFF, num);

                if (Instance.ViewButtonList.Count == 0)
                {
                    Instance.ViewButtonList.AddRange(GetViewButtonDefIDs(false));
                }
                if (Instance.TaskMenuList.Count == 0)
                {
                    Instance.TaskMenuList.AddRange(GetTaskMenuDefIDs(false));
                }
                if (Instance.ReserveListColumn.Count == 0)
                {
                    var obj = new ReserveItem();
                    Instance.ReserveListColumn.AddRange(GetDefaultColumn(typeof(ReserveView)));
                    Instance.ResColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                    Instance.ResSortDirection = ListSortDirection.Ascending;
                }
                if (Instance.RecInfoListColumn.Count == 0)
                {
                    var obj = new RecInfoItem();
                    Instance.RecInfoListColumn.AddRange(GetDefaultColumn(typeof(RecInfoView)));
                    Instance.RecInfoColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                    Instance.RecInfoSortDirection = ListSortDirection.Descending;
                }
                if (Instance.AutoAddEpgColumn.Count == 0)
                {
                    Instance.AutoAddEpgColumn.AddRange(GetDefaultColumn(typeof(EpgAutoAddView)));
                }
                if (Instance.AutoAddManualColumn.Count == 0)
                {
                    Instance.AutoAddManualColumn.AddRange(GetDefaultColumn(typeof(ManualAutoAddView)));
                }
                if (Instance.EpgListColumn.Count == 0)
                {
                    var obj = new SearchItem();
                    Instance.EpgListColumn.AddRange(GetDefaultColumn(typeof(EpgListMainView)));
                    Instance.EpgListColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                    Instance.EpgListSortDirection = ListSortDirection.Ascending;
                }
                if (Instance.SearchWndColumn.Count == 0)
                {
                    var obj = new SearchItem();
                    Instance.SearchWndColumn.AddRange(GetDefaultColumn(typeof(SearchWindow)));
                    Instance.SearchColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                    Instance.SearchSortDirection = ListSortDirection.Ascending;
                }
                if (Instance.InfoSearchWndColumn.Count == 0)
                {
                    var obj = new InfoSearchItem();
                    Instance.InfoSearchWndColumn.AddRange(GetDefaultColumn(typeof(InfoSearchWindow)));
                    Instance.InfoSearchColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                    Instance.InfoSearchSortDirection = ListSortDirection.Ascending;
                }

                if (Instance.InfoWindowListColumn.Count == 0)
                {
                    var obj = new ReserveItem();
                    Instance.InfoWindowListColumn.Add(new ListColumnInfo(CommonUtil.NameOf(() => obj.Status), double.NaN));
                    Instance.InfoWindowListColumn.Add(new ListColumnInfo(CommonUtil.NameOf(() => obj.StartTime), double.NaN));
                    Instance.InfoWindowListColumn.Add(new ListColumnInfo(CommonUtil.NameOf(() => obj.ServiceName), 80));
                    Instance.InfoWindowListColumn.Add(new ListColumnInfo(CommonUtil.NameOf(() => obj.EventName), 300));
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

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    //シリアル化して書き込む
                    var xs = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
                    xs.Serialize(fs, Instance);
                }
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
            get { return FontSize * ViewUtil.ItemFontNormal.GlyphType.Height; }
        }
        public double FontHeightTitle
        {
            get { return FontSizeTitle * ViewUtil.ItemFontTitle.GlyphType.Height; }
        }
        public double TunerFontHeight
        {
            get { return tunerFontSize * ViewUtil.ItemFontTunerNormal.GlyphType.Height; }
        }
        public double TunerFontHeightService
        {
            get { return tunerFontSizeService * ViewUtil.ItemFontTunerService.GlyphType.Height; }
        }

        private static List<string> viewButtonIDs = new List<string>();
        public const string ViewButtonSpacer = "（空白）";
        public const string TaskMenuSeparator = "（セパレータ）";
        public static void ResisterViewButtonIDs(IEnumerable<string> list)
        {
            viewButtonIDs = list == null ? new List<string>() : list.ToList();
        } 
        public static List<string> GetViewButtonAllIDs()
        {
            return new List<string> { ViewButtonSpacer }.Concat(viewButtonIDs).ToList();
        }
        public static List<string> GetTaskMenuAllIDs()
        {
            return new List<string> { TaskMenuSeparator }.Concat(viewButtonIDs).ToList();
        }
        public static List<string> GetViewButtonDefIDs(bool nwMode)
        {
            return new List<string>
            {
                "設定",
                ViewButtonSpacer,
                "再接続",
                ViewButtonSpacer,
                "検索",
                ViewButtonSpacer,
                "スタンバイ",
                "休止",
                ViewButtonSpacer,
                "EPG取得",
                ViewButtonSpacer,
                "EPG再読み込み",
                ViewButtonSpacer,
                "終了",
            };
        }

        public static List<string> GetTaskMenuDefIDs(bool nwMode)
        {
            return new List<string>
            {
                "設定",
                TaskMenuSeparator,
                "再接続",
                TaskMenuSeparator,
                "スタンバイ",
                "休止",
                TaskMenuSeparator,
                "終了",
            };
        }

        public static List<ListColumnInfo> GetDefaultColumn(Type t)
        {
            if (t == typeof(ReserveView))
            {
                var obj = new ReserveItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecMode) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Priority) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Tuijyu) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Comment) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecFileName) },
                };
            }
            else if (t == typeof(RecInfoView))
            {
                var obj = new RecInfoItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.IsProtect) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Drops) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Scrambles) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Result) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecFilePath) },
                };
            }
            else if (t == typeof(EpgAutoAddView))
            {
                var obj = new EpgAutoDataItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NotKey) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RegExp) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecMode) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Priority) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Tuijyu) },
                };
            }
            else if (t == typeof(ManualAutoAddView))
            {
                var obj = new ManualAutoAddDataItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.DayOfWeek) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecMode) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Priority) },
                };
            }
            else if (t == typeof(EpgListMainView))
            {
                var obj = new SearchItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Status) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                };
            }
            else if (t == typeof(SearchWindow))
            {
                var obj = new SearchItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Status) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ProgramDuration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ProgramContent) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.JyanruKey) },
                };
            }
            else if (t == typeof(InfoSearchWindow))
            {
                var obj = new InfoSearchItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ViewItemName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Status) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ProgramDuration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ProgramContent) },
                };
            }
            return null;
        }
    }
}

