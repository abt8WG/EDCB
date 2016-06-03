using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetEpgView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetEpgView : UserControl
    {
        private class ColorReferenceViewItem
        {
            public ColorReferenceViewItem(string name, Brush c) { ColorName = name; Color = c; }
            public string ColorName { get; private set; }
            public Brush Color { get; private set; }
        }

        private MenuUtil mutil = CommonManager.Instance.MUtil;
        private BoxExchangeEditor bx = new BoxExchangeEditor();

        private MenuSettingData ctxmSetInfo;

        private string styleFile;

        public SetEpgView()
        {
            InitializeComponent();

            try
            {
                textBox_mouse_scroll.Text = Settings.Instance.ScrollSize.ToString();
                textBox_service_width.Text = Settings.Instance.ServiceWidth.ToString();
                textBox_minHeight.Text = Settings.Instance.MinHeight.ToString();
                textBox_dragScroll.Text = Settings.Instance.DragScroll.ToString();
                textBox_minimumHeight.Text = Settings.Instance.MinimumHeight.ToString();
                checkBox_epg_popup.IsChecked = Settings.Instance.EpgPopup;
                checkBox_epg_popup_resOnly.IsEnabled = Settings.Instance.EpgPopup;
                checkBox_epg_popup_resOnly.IsChecked = Settings.Instance.EpgPopupResOnly;
                checkBox_title_indent.IsChecked = Settings.Instance.EpgTitleIndent;
                checkBox_singleOpen.IsChecked = Settings.Instance.EpgInfoSingleClick;
                checkBox_scrollAuto.IsChecked = Settings.Instance.MouseScrollAuto;
                checkBox_gradation.IsChecked = Settings.Instance.EpgGradation;
                checkBox_gradationHeader.IsChecked = Settings.Instance.EpgGradationHeader;

                checkBox_openInfo.IsChecked = (Settings.Instance.EpgInfoOpenMode != 0);
                checkBox_displayNotifyChange.IsChecked = Settings.Instance.DisplayNotifyEpgChange;
                checkBox_reserveBackground.IsChecked = Settings.Instance.ReserveRectBackground;

                textBox_tuner_mouse_scroll.Text = Settings.Instance.TunerScrollSize.ToString();
                textBox_tuner_width.Text = Settings.Instance.TunerWidth.ToString();
                textBox_tuner_minHeight.Text = Settings.Instance.TunerMinHeight.ToString();
                textBox_tunerDdragScroll.Text = Settings.Instance.TunerDragScroll.ToString();
                textBox_tunerMinLineHeight.Text = Settings.Instance.TunerMinimumLine.ToString();
                checkBox_tuner_popup.IsChecked = Settings.Instance.TunerPopup;
                checkBox_tuner_popup_recInfo.IsEnabled = Settings.Instance.TunerPopup;
                checkBox_tuner_popup_recInfo.IsChecked = Settings.Instance.TunerPopupRecinfo;
                checkBox_tuner_title_indent.IsChecked = Settings.Instance.TunerTitleIndent;
                checkBox_tunerSingleOpen.IsChecked = Settings.Instance.TunerInfoSingleClick;
                checkBox_tuner_scrollAuto.IsChecked = Settings.Instance.TunerMouseScrollAuto;
                checkBox_tuner_service_nowrap.IsChecked = Settings.Instance.TunerServiceNoWrap;
                checkBox_tunerColorModeUse.IsChecked = Settings.Instance.TunerColorModeUse;
                comboBox_tunerFontColorService.IsEnabled = !Settings.Instance.TunerColorModeUse;
                button_tunerFontCustColorService.IsEnabled = !Settings.Instance.TunerColorModeUse;
                checkBox_tuner_display_offres.IsChecked = Settings.Instance.TunerDisplayOffReserve;

                bx.TargetBox = this.listBox_tab;
                button_tab_del.Click += new RoutedEventHandler(bx.button_del_Click);
                button_tab_up.Click += new RoutedEventHandler(bx.button_up_Click);
                button_tab_down.Click += new RoutedEventHandler(bx.button_down_Click);

                radioButton_1_def.IsChecked = (Settings.Instance.UseCustomEpgView == false);
                radioButton_1_cust.IsChecked = (Settings.Instance.UseCustomEpgView != false);

                Settings.Instance.CustomEpgTabList.ForEach(info => listBox_tab.Items.Add(info));
                if (listBox_tab.Items.Count > 0) listBox_tab.SelectedIndex = 0;

                XmlLanguage FLanguage = XmlLanguage.GetLanguage("ja-JP");
                List<string> fontList = Fonts.SystemFontFamilies
                    .Where(f => f.FamilyNames.ContainsKey(FLanguage) == true)
                    .Select(f => f.FamilyNames[FLanguage]).ToList();

                var setCmboFont = new Action<string, ComboBox>((name, cmb) =>
                {
                    cmb.ItemsSource = fontList;
                    cmb.SelectedItem = name;
                    if (cmb.SelectedItem == null) cmb.SelectedIndex = 0;
                });
                setCmboFont(Settings.Instance.FontNameTitle, comboBox_fontTitle);
                setCmboFont(Settings.Instance.FontName, comboBox_font);
                setCmboFont(Settings.Instance.TunerFontNameService, comboBox_fontTunerService);
                setCmboFont(Settings.Instance.TunerFontName, comboBox_fontTuner);

                textBox_fontSize.Text = Settings.Instance.FontSize.ToString();
                textBox_fontSizeTitle.Text = Settings.Instance.FontSizeTitle.ToString();
                checkBox_fontBoldTitle.IsChecked = Settings.Instance.FontBoldTitle;
                textBox_fontTunerSize.Text = Settings.Instance.TunerFontSize.ToString();
                textBox_fontTunerSizeService.Text = Settings.Instance.TunerFontSizeService.ToString();
                checkBox_fontTunerBoldService.IsChecked = Settings.Instance.TunerFontBoldService;

                var colorReference = ColorDef.ColorNames.ToDictionary
                    (name => name, name => new ColorReferenceViewItem(name, ColorDef.Instance.ColorTable[name]));
                colorReference["カスタム"] = new ColorReferenceViewItem("カスタム", this.Resources["HatchBrush"] as VisualBrush);

                var setComboColor1 = new Action<string, ComboBox>((name, cmb) =>
                {
                    cmb.ItemsSource = colorReference.Values;
                    cmb.SelectedItem = colorReference.ContainsKey(name) ? colorReference[name] : colorReference["カスタム"];
                });
                var setButtonColor1 = new Action<uint, Button>((clr, btn) => btn.Background = new SolidColorBrush(ColorDef.FromUInt(clr)));
                var setColors = new Action<UIElement, List<string>, List<uint>>((ui, stockColors, custColors) =>
                {
                    List<UIElement> uiList = new List<UIElement>();
                    uiList.Add(ui);
                    for (int n = 0; n < uiList.Count; n++)
                    {
                        foreach (var child in LogicalTreeHelper.GetChildren(uiList[n]))
                        {
                            if (child is Control)
                            {
                                int index = int.Parse((string)(child as Control).Tag ?? "-1");
                                if (index >= 0)
                                {
                                    if (child is ComboBox && index < stockColors.Count)
                                    {
                                        setComboColor1(stockColors[index], child as ComboBox);
                                    }
                                    else if (child is Button && index < custColors.Count)
                                    {
                                        setButtonColor1(custColors[index], child as Button);
                                    }
                                }
                            }
                            else if (child is UIElement)
                            {
                                uiList.Add(child as UIElement);
                            }
                        }
                    }
                });

                //番組表のフォント色と予約枠色はSettingsが個別のため個別処理。
                //これをまとめて出来るようにSettingsを変えると以前の設定が消える。
                // [番組表] - [基本]
                setComboColor1(Settings.Instance.TitleColor1, comboBox_colorTitle1);
                setButtonColor1(Settings.Instance.TitleCustColor1, button_colorTitle1);
                setComboColor1(Settings.Instance.TitleColor2, comboBox_colorTitle2);
                setButtonColor1(Settings.Instance.TitleCustColor2, button_colorTitle2);
                // [番組表] - [色1]
                setColors(groupEpgColors, Settings.Instance.ContentColorList, Settings.Instance.ContentCustColorList);
                setComboColor1(Settings.Instance.ReserveRectColorNormal, comboBox_reserveNormal);
                setComboColor1(Settings.Instance.ReserveRectColorNo, comboBox_reserveNo);
                setComboColor1(Settings.Instance.ReserveRectColorNoTuner, comboBox_reserveNoTuner);
                setComboColor1(Settings.Instance.ReserveRectColorWarning, comboBox_reserveWarning);
                setComboColor1(Settings.Instance.ReserveRectColorAutoAddMissing, comboBox_reserveAutoAddMissing);
                setColors(groupEpgColorsReserve, null, Settings.Instance.ContentCustColorList);
                // [番組表] - [色2]
                setColors(groupEpgTimeColors, Settings.Instance.EpgEtcColors, Settings.Instance.EpgEtcCustColors);
                setColors(groupEpgEtcColors, Settings.Instance.EpgEtcColors, Settings.Instance.EpgEtcCustColors);

                // [使用予定チューナー] - [基本]
                setColors(groupTunerFontColor, Settings.Instance.TunerServiceColors, Settings.Instance.TunerServiceCustColors);
                // [使用予定チューナー] - [色]
                setColors(groupTunerColors, Settings.Instance.TunerServiceColors, Settings.Instance.TunerServiceCustColors);

                // [録画済み一覧]
                checkBox_playDClick.IsChecked = Settings.Instance.PlayDClick;
                checkBox_recNoYear.IsChecked = Settings.Instance.RecInfoNoYear;
                checkBox_recNoSecond.IsChecked = Settings.Instance.RecInfoNoSecond;
                checkBox_recNoDurSecond.IsChecked = Settings.Instance.RecInfoNoDurSecond;
                checkBox_ChacheOn.IsChecked = Settings.Instance.RecInfoExtraDataCache;
                checkBox_CacheOptimize.IsChecked = Settings.Instance.RecInfoExtraDataCacheOptimize;
                checkBox_CacheKeepConnect.IsChecked = Settings.Instance.RecInfoExtraDataCacheKeepConnect;
                if (CommonManager.Instance.NWMode == false)
                {
                    checkBox_CacheKeepConnect.IsEnabled = false;//{Binding}を破棄しているので注意
                }
                textBox_dropErrIgnore.Text = Settings.Instance.RecInfoDropErrIgnore.ToString();
                textBox_dropWrnIgnore.Text = Settings.Instance.RecInfoDropWrnIgnore.ToString();
                textBox_scrambleIgnore.Text = Settings.Instance.RecInfoScrambleIgnore.ToString();
                checkBox_recinfo_errCritical.IsChecked = Settings.Instance.RecinfoErrCriticalDrops;
                setColors(groupRecInfoBackColors, Settings.Instance.RecEndColors, Settings.Instance.RecEndCustColors);

                // [予約一覧・共通] - [基本]
                this.ctxmSetInfo = Settings.Instance.MenuSet.Clone();
                checkBox_displayAutoAddMissing.IsChecked = Settings.Instance.DisplayReserveAutoAddMissing;
                textBox_DisplayJumpTime.Text = Settings.Instance.DisplayNotifyJumpTime.ToString();
                checkBox_resNoYear.IsChecked = Settings.Instance.ResInfoNoYear;
                checkBox_resNoSecond.IsChecked = Settings.Instance.ResInfoNoSecond;
                checkBox_resNoDurSecond.IsChecked = Settings.Instance.ResInfoNoDurSecond;
                checkBox_LaterTimeUse.IsChecked = Settings.Instance.LaterTimeUse;
                textBox_LaterTimeHour.Text = (Settings.Instance.LaterTimeHour + 24).ToString();
                checkBox_displayPresetOnSearch.IsChecked = Settings.Instance.DisplayPresetOnSearch;
                checkBox_nekopandaToolTip.IsChecked = Settings.Instance.RecItemToolTip;
                checkBox_displayStatus.IsChecked = Settings.Instance.DisplayStatus;
                checkBox_displayStatusNotify.IsChecked = Settings.Instance.DisplayStatusNotify;
                InitializeStyleList();
                // [予約一覧・共通] - [色]
                setComboColor1(Settings.Instance.ListDefColor, cmb_ListDefFontColor);
                setButtonColor1(Settings.Instance.ListDefCustColor, btn_ListDefFontColor);
                setColors(groupReserveRecModeColors, Settings.Instance.RecModeFontColors, Settings.Instance.RecModeFontCustColors);
                setColors(groupReserveBackColors, Settings.Instance.ResBackColors, Settings.Instance.ResBackCustColors);
                setColors(groupStatColors, Settings.Instance.StatColors, Settings.Instance.StatCustColors);

                // [予約簡易表示]
                textBox_iw_refresh_interval.Text = Settings.Instance.InfoWindowRefreshInterval.ToString();
                radioButton_iw_based_on_bcst.IsChecked = Settings.Instance.InfoWindowBasedOnBroadcast;
                radioButton_iw_based_on_rec.IsChecked = !Settings.Instance.InfoWindowBasedOnBroadcast;
                switch(Settings.Instance.InfoWindowItemFilterLevel)
                {
                    default: radioButton_All.IsChecked = true; break;
                    case 1: radioButton_Level1.IsChecked = true; break;
                    case 2: radioButton_Level2.IsChecked = true; break;
                    case 3: radioButton_Level3.IsChecked = true; break;
                    case int.MaxValue: radioButton_TopN.IsChecked = true; break;
                }
                switch (Settings.Instance.InfoWindowItemProgressBarType)
                {
                    default: radioButton_ProgressBarOff.IsChecked = true; break;
                    case 1: radioButton_ProgressBarType1.IsChecked = true; break;
                    case 2: radioButton_ProgressBarType2.IsChecked = true; break;
                }
                textBox_TopN.Text = Settings.Instance.InfoWindowItemTopN.ToString();
                textBox_iw_item_level1.Text = (Settings.Instance.InfoWindowItemLevel1Seconds / 60.0).ToString();
                textBox_iw_item_level2.Text = (Settings.Instance.InfoWindowItemLevel2Seconds / 60.0).ToString();
                textBox_iw_item_level3.Text = (Settings.Instance.InfoWindowItemLevel3Seconds / 60.0).ToString();
                setColors(groupInfoWinItemBgColors, Settings.Instance.InfoWindowItemBgColors, Settings.Instance.InfoWindowItemBgCustColors);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        public void SaveSetting()
        {
            try
            {
                Settings.Instance.ScrollSize = mutil.MyToNumerical(textBox_mouse_scroll, Convert.ToDouble, 240);
                Settings.Instance.ServiceWidth = mutil.MyToNumerical(textBox_service_width, Convert.ToDouble, double.MaxValue, 16, 16);//小さいと描画で落ちる
                Settings.Instance.MinHeight = mutil.MyToNumerical(textBox_minHeight, Convert.ToDouble, double.MaxValue, 0.1, 2);
                Settings.Instance.MinimumHeight = mutil.MyToNumerical(textBox_minimumHeight, Convert.ToDouble, double.MaxValue, 0, 0);
                Settings.Instance.DragScroll = mutil.MyToNumerical(textBox_dragScroll, Convert.ToDouble, 1.5);
                Settings.Instance.EpgTitleIndent = (checkBox_title_indent.IsChecked == true);
                Settings.Instance.EpgPopup = (checkBox_epg_popup.IsChecked == true);
                Settings.Instance.EpgPopupResOnly = (checkBox_epg_popup_resOnly.IsChecked == true);
                Settings.Instance.EpgGradation = (checkBox_gradation.IsChecked == true);
                Settings.Instance.EpgGradationHeader = (checkBox_gradationHeader.IsChecked == true);


                Settings.Instance.EpgInfoSingleClick = (checkBox_singleOpen.IsChecked == true);
                Settings.Instance.EpgInfoOpenMode = (byte)(checkBox_openInfo.IsChecked == true ? 1 : 0);
                Settings.Instance.MouseScrollAuto = (checkBox_scrollAuto.IsChecked == true);
                Settings.Instance.DisplayNotifyEpgChange = (checkBox_displayNotifyChange.IsChecked == true);
                Settings.Instance.ReserveRectBackground = (checkBox_reserveBackground.IsChecked == true);
                Settings.Instance.TunerScrollSize = mutil.MyToNumerical(textBox_tuner_mouse_scroll, Convert.ToDouble, 240);
                Settings.Instance.TunerWidth = mutil.MyToNumerical(textBox_tuner_width, Convert.ToDouble, double.MaxValue, 16, 150);//小さいと描画で落ちる
                Settings.Instance.TunerMinHeight = mutil.MyToNumerical(textBox_tuner_minHeight, Convert.ToDouble, double.MaxValue, 0.1, 2);
                Settings.Instance.TunerMinimumLine = mutil.MyToNumerical(textBox_tunerMinLineHeight, Convert.ToDouble, double.MaxValue, 0, 0);
                Settings.Instance.TunerDragScroll = mutil.MyToNumerical(textBox_tunerDdragScroll, Convert.ToDouble, 1.5);
                Settings.Instance.TunerMouseScrollAuto = (checkBox_tuner_scrollAuto.IsChecked == true);
                Settings.Instance.TunerServiceNoWrap = (checkBox_tuner_service_nowrap.IsChecked == true);
                Settings.Instance.TunerTitleIndent = (checkBox_tuner_title_indent.IsChecked == true);
                Settings.Instance.TunerPopup = (checkBox_tuner_popup.IsChecked == true);
                Settings.Instance.TunerPopupRecinfo = (checkBox_tuner_popup_recInfo.IsChecked == true);
                Settings.Instance.TunerInfoSingleClick = (checkBox_tunerSingleOpen.IsChecked == true);
                Settings.Instance.TunerColorModeUse = (checkBox_tunerColorModeUse.IsChecked == true);
                Settings.Instance.TunerDisplayOffReserve = (checkBox_tuner_display_offres.IsChecked == true);

                if (comboBox_font.SelectedItem != null)
                {
                    Settings.Instance.FontName = comboBox_font.SelectedItem as string;
                }
                Settings.Instance.FontSize = mutil.MyToNumerical(textBox_fontSize, Convert.ToDouble, 72, 1, 12);
                if (comboBox_fontTitle.SelectedItem != null)
                {
                    Settings.Instance.FontNameTitle = comboBox_fontTitle.SelectedItem as string;
                }
                Settings.Instance.FontSizeTitle = mutil.MyToNumerical(textBox_fontSizeTitle, Convert.ToDouble, 72, 1, 12);
                Settings.Instance.FontBoldTitle = (checkBox_fontBoldTitle.IsChecked == true);

#if false
                Settings.Instance.UseCustomEpgView = (radioButton_1_cust.IsChecked == true);
                Settings.Instance.CustomEpgTabList.Clear();
                foreach (CustomEpgTabInfo info in listBox_tab.Items)
                {
                    Settings.Instance.CustomEpgTabList.Add(info);
                }

                if (CommonManager.Instance.NWMode == false)
                {
                    string iniValue = "";
                    iniValue = (radioButton_1_cust.IsChecked == true ? "1" : "0");
                    IniFileHandler.WritePrivateProfileString("HTTP", "HttpCustEpg", iniValue, SettingPath.TimerSrvIniPath);

                    int custCount = listBox_tab.Items.Count;
                    IniFileHandler.WritePrivateProfileString("HTTP", "HttpCustCount", custCount.ToString(), SettingPath.TimerSrvIniPath);
                    custCount = 0;
                    foreach (CustomEpgTabInfo info in listBox_tab.Items)
                    {
                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "Name", info.TabName, SettingPath.TimerSrvIniPath);
                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "ViewServiceCount", info.ViewServiceList.Count.ToString(), SettingPath.TimerSrvIniPath);
                        int serviceCount = 0;
                        foreach (Int64 id in info.ViewServiceList)
                        {
                            IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "ViewService" + serviceCount.ToString(), id.ToString(), SettingPath.TimerSrvIniPath);
                            serviceCount++;
                        }

                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "ContentCount", info.ViewContentKindList.Count.ToString(), SettingPath.TimerSrvIniPath);
                        int contentCount = 0;
                        foreach (UInt16 id in info.ViewContentKindList)
                        {
                            IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "Content" + contentCount.ToString(), id.ToString(), SettingPath.TimerSrvIniPath);
                            contentCount++;
                        }
                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "ViewMode", info.ViewMode.ToString(), SettingPath.TimerSrvIniPath);

                        iniValue = (info.NeedTimeOnlyBasic == true ? "1" : "0");
                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "NeedTimeOnlyBasic", iniValue, SettingPath.TimerSrvIniPath);

                        iniValue = (info.NeedTimeOnlyWeek == true ? "1" : "0");
                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "NeedTimeOnlyWeek", iniValue, SettingPath.TimerSrvIniPath);

                        iniValue = (info.SearchMode == true ? "1" : "0");
                        IniFileHandler.WritePrivateProfileString("HTTP_CUST" + custCount.ToString(), "SearchMode", iniValue, SettingPath.TimerSrvIniPath);

                        custCount++;
                    }
                }

                Settings.Instance.ContentCustColorList = custColorList.Select(c => ColorDef.ToUInt(c)).ToList();
                Settings.Instance.TitleCustColor1 = ColorDef.ToUInt(custTitleColorList[0]);
                Settings.Instance.TitleCustColor2 = ColorDef.ToUInt(custTitleColorList[1]);
#endif
                if (comboBox_fontTuner.SelectedItem != null)
                {
                    Settings.Instance.TunerFontName = comboBox_fontTuner.SelectedItem as string;
                }
                Settings.Instance.TunerFontSize = mutil.MyToNumerical(textBox_fontTunerSize, Convert.ToDouble, 72, 1, 12);
                if (comboBox_fontTunerService.SelectedItem != null)
                {
                    Settings.Instance.TunerFontNameService = comboBox_fontTunerService.SelectedItem as string;
                }
                Settings.Instance.TunerFontSizeService = mutil.MyToNumerical(textBox_fontTunerSizeService, Convert.ToDouble, 72, 1, 12);
                Settings.Instance.TunerFontBoldService = (checkBox_fontTunerBoldService.IsChecked == true);

                Settings.Instance.UseCustomEpgView = (radioButton_1_cust.IsChecked == true);
                Settings.Instance.CustomEpgTabList = listBox_tab.Items.OfType<CustomEpgTabInfo>().ToList();
                Settings.SetCustomEpgTabInfoID();

                var getComboColor1 = new Func<ComboBox, string>((cmb) => ((ColorReferenceViewItem)(cmb.SelectedItem)).ColorName);
                var getButtonColor1 = new Func<Button, uint>((btn) => ColorDef.ToUInt((btn.Background as SolidColorBrush).Color));
                var getColors = new Action<UIElement, List<string>, List<uint>>((ui, stockColors, custColors) =>
                {
                    List<UIElement> uiList = new List<UIElement>();
                    uiList.Add(ui);
                    for (int n = 0; n < uiList.Count; n++)
                    {
                        foreach (var child in LogicalTreeHelper.GetChildren(uiList[n]))
                        {
                            if (child is Control)
                            {
                                int index = int.Parse((string)(child as Control).Tag ?? "-1");
                                if (index >= 0)
                                {
                                    if (child is ComboBox && index < stockColors.Count)
                                    {
                                        stockColors[index] = getComboColor1(child as ComboBox);
                                    }
                                    else if (child is Button && index < custColors.Count)
                                    {
                                        custColors[index] = getButtonColor1(child as Button);
                                    }
                                }
                            }
                            else if (child is UIElement)
                            {
                                uiList.Add(child as UIElement);
                            }
                        }
                    }
                });

                // [番組表] - [基本]
                Settings.Instance.TitleColor1 = getComboColor1(comboBox_colorTitle1);
                Settings.Instance.TitleCustColor1 = getButtonColor1(button_colorTitle1);
                Settings.Instance.TitleColor2 = getComboColor1(comboBox_colorTitle2);
                Settings.Instance.TitleCustColor2 = getButtonColor1(button_colorTitle2);
                // [番組表] - [色1]
                getColors(groupEpgColors, Settings.Instance.ContentColorList, Settings.Instance.ContentCustColorList);
                Settings.Instance.ReserveRectColorNormal = getComboColor1(comboBox_reserveNormal);
                Settings.Instance.ReserveRectColorNo = getComboColor1(comboBox_reserveNo);
                Settings.Instance.ReserveRectColorNoTuner = getComboColor1(comboBox_reserveNoTuner);
                Settings.Instance.ReserveRectColorWarning = getComboColor1(comboBox_reserveWarning);
                Settings.Instance.ReserveRectColorAutoAddMissing = getComboColor1(comboBox_reserveAutoAddMissing);
                getColors(groupEpgColorsReserve, null, Settings.Instance.ContentCustColorList);
                // [番組表] - [色2]
                getColors(groupEpgTimeColors, Settings.Instance.EpgEtcColors, Settings.Instance.EpgEtcCustColors);
                getColors(groupEpgEtcColors, Settings.Instance.EpgEtcColors, Settings.Instance.EpgEtcCustColors);

                // [使用予定チューナー] - [基本]
                getColors(groupTunerFontColor, Settings.Instance.TunerServiceColors, Settings.Instance.TunerServiceCustColors);
                // [使用予定チューナー] - [色]
                getColors(groupTunerColors, Settings.Instance.TunerServiceColors, Settings.Instance.TunerServiceCustColors);

                // [録画済み一覧]
                Settings.Instance.PlayDClick = (checkBox_playDClick.IsChecked == true);
                Settings.Instance.RecInfoNoYear = (checkBox_recNoYear.IsChecked == true);
                Settings.Instance.RecInfoNoSecond = (checkBox_recNoSecond.IsChecked == true);
                Settings.Instance.RecInfoNoDurSecond = (checkBox_recNoDurSecond.IsChecked == true);
                Settings.Instance.RecInfoExtraDataCache = (checkBox_ChacheOn.IsChecked == true);
                Settings.Instance.RecInfoExtraDataCacheOptimize = (checkBox_CacheOptimize.IsChecked == true);
                if (checkBox_CacheKeepConnect.IsEnabled)
                {
                    Settings.Instance.RecInfoExtraDataCacheKeepConnect = (checkBox_CacheKeepConnect.IsChecked == true);
                }
                Settings.Instance.RecInfoDropErrIgnore = mutil.MyToNumerical(textBox_dropErrIgnore, Convert.ToInt64, Settings.Instance.RecInfoDropErrIgnore);
                Settings.Instance.RecInfoDropWrnIgnore = mutil.MyToNumerical(textBox_dropWrnIgnore, Convert.ToInt64, Settings.Instance.RecInfoDropWrnIgnore);
                Settings.Instance.RecInfoScrambleIgnore = mutil.MyToNumerical(textBox_scrambleIgnore, Convert.ToInt64, Settings.Instance.RecInfoScrambleIgnore);
                Settings.Instance.RecinfoErrCriticalDrops = (checkBox_recinfo_errCritical.IsChecked == true);
                getColors(groupRecInfoBackColors, Settings.Instance.RecEndColors, Settings.Instance.RecEndCustColors);

                // [予約一覧・共通] - [基本]
                Settings.Instance.MenuSet = this.ctxmSetInfo.Clone();
                Settings.Instance.DisplayReserveAutoAddMissing = (checkBox_displayAutoAddMissing.IsChecked != false);
                Settings.Instance.DisplayNotifyJumpTime = mutil.MyToNumerical(textBox_DisplayJumpTime, Convert.ToDouble, Double.MaxValue, 0, 3);
                Settings.Instance.ResInfoNoYear = (checkBox_resNoYear.IsChecked == true);
                Settings.Instance.ResInfoNoSecond = (checkBox_resNoSecond.IsChecked == true);
                Settings.Instance.ResInfoNoDurSecond = (checkBox_resNoDurSecond.IsChecked == true);
                Settings.Instance.LaterTimeUse = (checkBox_LaterTimeUse.IsChecked == true);
                Settings.Instance.LaterTimeHour = mutil.MyToNumerical(textBox_LaterTimeHour, Convert.ToInt32, 36, 24, 28) - 24;
                Settings.Instance.DisplayPresetOnSearch = (checkBox_displayPresetOnSearch.IsChecked == true);
                Settings.Instance.RecItemToolTip = (checkBox_nekopandaToolTip.IsChecked == true);
                Settings.Instance.NoStyle = (checkBox_NotNoStyle.IsChecked == true ? 0 : 1);
                if (Settings.Instance.NoStyle == 0)
                {
                    Settings.Instance.StyleXamlPath = (comboBox_Style.SelectedItem as ComboBoxItem).Tag as string;
                }
                Settings.Instance.DisplayStatus = (checkBox_displayStatus.IsChecked == true);
                Settings.Instance.DisplayStatusNotify = (checkBox_displayStatusNotify.IsChecked == true);
                // [予約一覧・共通] - [色]
                Settings.Instance.ListDefColor = getComboColor1(cmb_ListDefFontColor);
                Settings.Instance.ListDefCustColor = getButtonColor1(btn_ListDefFontColor);
                getColors(groupReserveRecModeColors, Settings.Instance.RecModeFontColors, Settings.Instance.RecModeFontCustColors);
                getColors(groupReserveBackColors, Settings.Instance.ResBackColors, Settings.Instance.ResBackCustColors);
                getColors(groupStatColors, Settings.Instance.StatColors, Settings.Instance.StatCustColors);

                // [予約簡易表示]
                Settings.Instance.InfoWindowRefreshInterval = mutil.MyToNumerical(textBox_iw_refresh_interval, Convert.ToInt32, 60, 1, 10);
                Settings.Instance.InfoWindowBasedOnBroadcast = (radioButton_iw_based_on_bcst.IsChecked == true);
                string level = (string)new RadioButton[] {
                    radioButton_Level1, radioButton_Level2, radioButton_Level3, radioButton_All, radioButton_TopN
                }.Where(x => x.IsChecked == true).Select(x => x.Tag).FirstOrDefault();
                Settings.Instance.InfoWindowItemFilterLevel = level != null ? int.Parse(level) : int.MaxValue;
                string progbar = (string)new RadioButton[] {
                    radioButton_ProgressBarOff, radioButton_ProgressBarType1, radioButton_ProgressBarType2,
                }.Where(x => x.IsChecked == true).Select(x => x.Tag).FirstOrDefault();
                Settings.Instance.InfoWindowItemProgressBarType = progbar != null ? int.Parse(progbar) : 0;
                Settings.Instance.InfoWindowItemTopN = mutil.MyToNumerical(textBox_TopN, Convert.ToInt32, 10);
                Settings.Instance.InfoWindowItemLevel1Seconds = mutil.MyToNumerical(textBox_iw_item_level1, Convert.ToInt32, 0) * 60;
                Settings.Instance.InfoWindowItemLevel2Seconds = mutil.MyToNumerical(textBox_iw_item_level2, Convert.ToInt32, 15) * 60;
                Settings.Instance.InfoWindowItemLevel3Seconds = mutil.MyToNumerical(textBox_iw_item_level3, Convert.ToInt32, 480) * 60;
                getColors(groupInfoWinItemBgColors, Settings.Instance.InfoWindowItemBgColors, Settings.Instance.InfoWindowItemBgCustColors);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace); }
        }

        private void InitializeStyleList()
        {
            var defaultStyle = System.Reflection.Assembly.GetEntryAssembly().Location + ".rd.xaml";
            comboBox_Style.Items.Add(new ComboBoxItem
            {
                Content = File.Exists(defaultStyle) ? System.IO.Path.GetFileName(defaultStyle) : "",
                Tag = defaultStyle
            });
            var resourceFolder = SettingPath.ModulePath + "\\Resources";
            if (Directory.Exists(resourceFolder))
            {
                foreach (var file in Directory.EnumerateFiles(resourceFolder, "*.xaml").OrderBy(x => x))
                {
                    comboBox_Style.Items.Add(new ComboBoxItem
                    {
                        Content = System.IO.Path.GetFileNameWithoutExtension(file),
                        Tag = file
                    });
                }
            }
            var style = System.IO.Path.GetFileNameWithoutExtension(Settings.Instance.StyleXamlPath);
            if (comboBox_Style.Items
                              .OfType<ComboBoxItem>()
                              .Where(x => x.Content.Equals(style))
                              .Select(x => comboBox_Style.SelectedItem = x)
                              .GetEnumerator()
                              .MoveNext() == false)
            {
                comboBox_Style.SelectedIndex = 0;
            }
            checkBox_NotNoStyle.ToolTip = string.Format("チェック時、テーマファイル「{0}」があればそれを、無ければ既定のテーマ(Aero)を適用します。", System.IO.Path.GetFileName(defaultStyle));
            checkBox_NotNoStyle.IsChecked = Settings.Instance.NoStyle == 0;
        }
        public void UpdateStyle(string file)
        {
            if (!file.Equals(styleFile))
            {
                styleFile = file;
                CommonUtil.ApplyStyle(file);
            }
        }

        private void button_tab_add_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new EpgDataViewSettingWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            if (dlg.ShowDialog() == true)
            {
                var info = new CustomEpgTabInfo();
                dlg.GetSetting(ref info);
                listBox_tab.Items.Add(info);
                listBox_tab.SelectedItem = info;
                listBox_tab.ScrollIntoView(info);
            }
        }
        private void button_tab_chg_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_tab.SelectedItem == null)
            {
                if (listBox_tab.Items.Count != 0)
                {
                    listBox_tab.SelectedIndex = 0;
                }
            }
            var setInfo = listBox_tab.SelectedItem as CustomEpgTabInfo;
            if (setInfo != null)
            {
                listBox_tab.UnselectAll();
                listBox_tab.SelectedItem = setInfo;
                var dlg = new EpgDataViewSettingWindow();
                dlg.Owner = CommonUtil.GetTopWindow(this);
                dlg.SetDefSetting(setInfo);
                if (dlg.ShowDialog() == true)
                {
                    dlg.GetSetting(ref setInfo);
                    listBox_tab.Items.Refresh();
                }
            }
            else
            {
                button_tab_add_Click(null, null);
            }
        }

        private void button_tab_clone_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_tab.SelectedItem != null)
            {
                List<CustomEpgTabInfo> items = listBox_tab.SelectedItems.OfType<CustomEpgTabInfo>().ToList().Clone();
                items.ForEach(info => info.TabName += "～コピー");
                button_tab_copyAdd(items);
            }
        }

        private void button_tab_defaultCopy_Click(object sender, RoutedEventArgs e)
        {
            button_tab_copyAdd(CommonManager.Instance.CreateDefaultTabInfo());
        }

        private void button_tab_copyAdd(List<CustomEpgTabInfo> items)
        {
            if (items.Count != 0)
            {
                items.ForEach(info => listBox_tab.Items.Add(info));
                listBox_tab.ScrollIntoView(listBox_tab.Items[listBox_tab.Items.Count - 1]);
                listBox_tab.SelectedItem = items[0];
                items.ForEach(info => listBox_tab.SelectedItems.Add(info));
            }
        }

        private void listBox_tab_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            button_tab_chg_Click(null, null);
        }

        private void button_Color_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            var dlg = new ColorSetWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            Color item = (btn.Background as SolidColorBrush).Color;
            dlg.SetColor(item);
            if (dlg.ShowDialog() == true)
            {
                dlg.GetColor(ref item);
                (btn.Background as SolidColorBrush).Color = item;
            }
        }

        private void checkBox_epg_popup_Click(object sender, RoutedEventArgs e)
        {
            checkBox_epg_popup_resOnly.IsEnabled = (checkBox_epg_popup.IsChecked == true);
        }

        private void checkBox_tuner_popup_Click(object sender, RoutedEventArgs e)
        {
            checkBox_tuner_popup_recInfo.IsEnabled = (checkBox_tuner_popup.IsChecked == true);
        }

        private void checkBox_tunerColorModeUse_Click(object sender, RoutedEventArgs e)
        {
            comboBox_tunerFontColorService.IsEnabled = (checkBox_tunerColorModeUse.IsChecked == false);
            button_tunerFontCustColorService.IsEnabled = (checkBox_tunerColorModeUse.IsChecked == false);
        }

        private void button_set_cm_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetContextMenuWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            dlg.info = this.ctxmSetInfo.Clone();
            if (dlg.ShowDialog() == true)
            {
                this.ctxmSetInfo = dlg.info.Clone();
            }
        }

        private void checkBox_NotNoStyle_Checked(object sender, RoutedEventArgs e)
        {
            if (comboBox_Style.SelectedIndex >= 0)
            {
                UpdateStyle((comboBox_Style.SelectedItem as ComboBoxItem).Tag as string);
            }
        }

        private void checkBox_NotNoStyle_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateStyle("");
        }

        private void comboBox_Style_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStyle(checkBox_NotNoStyle.IsChecked == true ?(comboBox_Style.SelectedItem as ComboBoxItem).Tag as string : "");
        }
    }
}
