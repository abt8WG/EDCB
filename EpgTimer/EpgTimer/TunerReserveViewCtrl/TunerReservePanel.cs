using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace EpgTimer.TunerReserveViewCtrl
{
    class TunerReservePanel : EpgTimer.UserCtrlView.PanelBase
    {
        public List<ReserveViewItem> Items { get; set; }

        public ViewUtil.ItemFont ItemFontNormal { get; set; }
        public ViewUtil.ItemFont ItemFontTitle { get; set; }

        public TunerReservePanel()
        {
            this.VisualTextRenderingMode = TextRenderingMode.ClearType;
            this.VisualTextHintingMode = TextHintingMode.Fixed;
            this.UseLayoutRounding = true;
        }

        protected bool RenderText(String text, DrawingContext dc, ViewUtil.ItemFont itemFont, SolidColorBrush brush, double fontSize, double maxWidth, double maxHeight, double x, double baseline, ref double useHeight, bool nowrap = false)
        {
            if (x <= 0 || maxWidth <= 0)
            {
                useHeight = 0;
                return false;
            }

            double totalHeight = 0;
            double fontHeight = fontSize * itemFont.GlyphType.Height;

            string[] lineText = text.Replace("\r", "").Split('\n');
            foreach (string line in lineText)
            {
                //高さ確認
                if (totalHeight + fontHeight > maxHeight)
                {
                    //これ以上は無理
                    useHeight = totalHeight;
                    return false;
                }

                // ベースラインの位置の計算
                // ビットマップフォントがかすれる問題 とりあえず整数にしておく
                Point origin = new Point(Math.Round(x), Math.Round(totalHeight + baseline));

                //メイリオみたいに行間のあるフォントと MS P ゴシックみたいな行間のないフォントがあるので
                //なんとなく行間を作ってみる。
                totalHeight += Math.Max(fontHeight, fontSize + 2);
                double totalWidth = 0;

                List<ushort> glyphIndexes = new List<ushort>();
                List<double> advanceWidths = new List<double>();
                for (int n = 0; n < line.Length; n++)
                {
                    // XAML に合わせて、行頭の空白を無視する
                    if (glyphIndexes.Count == 0 && (line[n] == ' ' || line[n] == '\x3000'))
                        continue;

                    //ushort glyphIndex = glyphType.CharacterToGlyphMap[line[n]];
                    //double width = glyphType.AdvanceWidths[glyphIndex] * fontSize;

                    ushort glyphIndex = itemFont.GlyphIndexCache[line[n]];
                    if (glyphIndex == 0)
                    {
                        itemFont.GlyphType.CharacterToGlyphMap.TryGetValue(line[n], out glyphIndex);
                        itemFont.GlyphIndexCache[line[n]] = glyphIndex;
                        itemFont.GlyphWidthCache[glyphIndex] = (float)itemFont.GlyphType.AdvanceWidths[glyphIndex];
                    }
                    double width = itemFont.GlyphWidthCache[glyphIndex] * fontSize;

                    if (totalWidth + width > maxWidth)
                    {
                        if (nowrap == true) break;//改行しない場合ここで終り
                        if (totalWidth == 0) return false;//一文字も置けなかった(glyphIndexesなどのCount=0のまま)

                        if (totalHeight + fontHeight > maxHeight)
                        {
                            //次の行無理
                            //glyphIndex = glyphType.CharacterToGlyphMap['…'];
                            //double widthEllipsis = glyphType.AdvanceWidths[glyphIndex] * fontSize;
                            glyphIndex = itemFont.GlyphType.CharacterToGlyphMap['…'];
                            double widthEllipsis = itemFont.GlyphType.AdvanceWidths[glyphIndex] * fontSize;
                            while (totalWidth - advanceWidths.Last() + widthEllipsis > maxWidth)
                            {
                                totalWidth -= advanceWidths.Last();
                                glyphIndexes.RemoveAt(glyphIndexes.Count-1);
                                advanceWidths.RemoveAt(advanceWidths.Count - 1);
                            }
                            glyphIndexes[glyphIndexes.Count - 1] = glyphIndex;
                            advanceWidths[advanceWidths.Count - 1] = widthEllipsis;

                            GlyphRun glyphRun = new GlyphRun(itemFont.GlyphType, 0, false, fontSize,
                                glyphIndexes, origin, advanceWidths, null, null, null, null,
                                null, null);

                            dc.DrawGlyphRun(brush, glyphRun);

                            useHeight = totalHeight;
                            return false;
                        }
                        else
                        {
                            //次の行いけるので今までの分出力
                            GlyphRun glyphRun = new GlyphRun(itemFont.GlyphType, 0, false, fontSize,
                                glyphIndexes, origin, advanceWidths, null, null, null, null,
                                null, null);

                            dc.DrawGlyphRun(brush, glyphRun);

                            origin = new Point(Math.Round(x), Math.Round(totalHeight + baseline));
                            totalHeight += Math.Max(fontHeight, fontSize + 2);
                            totalWidth = 0;
                            glyphIndexes = new List<ushort>();
                            advanceWidths = new List<double>();

                            // XAML に合わせて、行頭の空白を無視する
                            if (line[n] == ' ' || line[n] == '\x3000')
                                continue;
                        }
                    }
                    glyphIndexes.Add(glyphIndex);
                    advanceWidths.Add(width);
                    totalWidth += width;
                }
                if (glyphIndexes.Count > 0)
                {
                    GlyphRun glyphRun = new GlyphRun(itemFont.GlyphType, 0, false, fontSize,
                        glyphIndexes, origin, advanceWidths, null, null, null, null,
                        null, null);

                    dc.DrawGlyphRun(brush, glyphRun);
                }
            }
            useHeight = totalHeight;
            return true;
        }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Background, null, new Rect(RenderSize));

            if (Items == null) return;

            if (ItemFontNormal == null || ItemFontNormal.GlyphType == null ||
                ItemFontTitle == null || ItemFontTitle.GlyphType == null)
            {
                return;
            }

            try
            {
                double sizeMin = Settings.Instance.TunerFontSize;
                double sizeTitle = Settings.Instance.TunerFontSizeService;
                double sizeNormal = Settings.Instance.TunerFontSize;
                double indentTitle = sizeMin * 1.7;
                double indentNormal = Settings.Instance.TunerTitleIndent ? indentTitle : 2;
                SolidColorBrush colorTitle = CommonManager.Instance.CustTunerServiceColor;
                SolidColorBrush colorNormal = CommonManager.Instance.CustTunerTextColor;

                double heightMin = Settings.Instance.TunerFontHeight;
                double heightTitle = Settings.Instance.TunerFontHeightService;
                double heightNormal = Settings.Instance.TunerFontHeight;
                double height1stLine = Math.Max(heightMin, heightTitle);

                // 起点はベースラインになるので、それぞれのベースラインを計算しておく。
                // 分とサービス名はフォントが異なることができるのでセンタリングして合うように調整しておく。
                // (ベースラインを合わせてみたら XAML と違うので PopupItem の表示との差が大きかった。)
                double baselineMin = sizeMin * ItemFontNormal.GlyphType.Baseline + (height1stLine - heightMin) / 2;
                double baselineTitle = sizeTitle * ItemFontTitle.GlyphType.Baseline + (height1stLine - heightTitle) / 2;
                double baselineNormal = sizeNormal * ItemFontNormal.GlyphType.Baseline;

                //録画中のものを後で描画する
                List<ReserveViewItem> postdrawList = Items.Where(info => info.ReserveInfo.IsOnRec()).ToList();
                postdrawList.ForEach(info => Items.Remove(info));
                Items.AddRange(postdrawList);

                foreach (ReserveViewItem info in Items)
                {
                    colorTitle = Settings.Instance.TunerColorModeUse == true ? info.ForeColorPriTuner : colorTitle;

                    double x = info.LeftPos;
                    double y = info.TopPos;
                    double height = Math.Max(info.Height, 0) + 1;
                    double width = info.Width + 1;

                    dc.DrawRectangle(info.BorderBrushTuner, null, new Rect(x, y, width, height));
                    if (height > 2)
                    {
                        // 枠の内側を計算
                        x += 1;
                        y += 1;
                        width -= 2;
                        height -= 2;
                        dc.DrawRectangle(info.BackColorTuner, null, new Rect(x, y, width, height));

                        if (height < height1stLine) // ModifierMinimumHeight してるので足りなくならないハズ。
                        {
                            //高さ足りない
                            info.TitleDrawErr = true;
                            continue;
                        }

                        // margin 設定
                        x += 2;
                        width -= 6;

                        double useHeight = 0;

                        //分
                        string min = info.ReserveInfo.StartTime.Minute.ToString("d02");
                        if (RenderText(min, dc, ItemFontNormal, colorTitle, sizeMin, width, height, x, y + baselineMin, ref useHeight) == false)
                        {
                            info.TitleDrawErr = true;
                            continue;
                        }

                        //サービス名
                        if (info.ReserveInfo.StationName.Length > 0)
                        {
                            string serviceName = info.ReserveInfo.StationName
                                + "(" + CommonManager.ConvertNetworkNameText(info.ReserveInfo.OriginalNetworkID) + ")";
                            if (RenderText(serviceName, dc, ItemFontTitle, colorTitle, sizeTitle, width - indentTitle, height, x + indentTitle, y + baselineTitle, ref useHeight, Settings.Instance.TunerServiceNoWrap) == false)
                            {
                                info.TitleDrawErr = true;
                                continue;
                            }
                            useHeight += 2; // margin: 番組名との間隔は 2px にする
                        }

                        //番組名
                        if (info.ReserveInfo.Title.Length > 0)
                        {
                            if (RenderText(info.ReserveInfo.Title, dc, ItemFontNormal, colorNormal, sizeNormal, width - indentNormal, height - useHeight, x + indentNormal, y + useHeight + baselineNormal, ref useHeight) == false)
                            {
                                info.TitleDrawErr = true;
                                continue;
                            }
                        }
                    }
                }

                ItemFontNormal.ClearCache();
                ItemFontTitle.ClearCache();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
    }
}
