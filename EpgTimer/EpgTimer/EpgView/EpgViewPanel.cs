using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EpgTimer.EpgView
{
    class EpgViewPanel : EpgTimer.UserCtrlView.PanelBase
    {
        private Dictionary<ProgramViewItem, List<TextDrawItem>> textDrawDict = new Dictionary<ProgramViewItem, List<TextDrawItem>>();

        //ProgramViewItemの座標系は番組表基準なので、この時点でCanvas.SetLeft()によりEpgViewPanel自身の座標を添付済みでなければならない
        public List<ProgramViewItem> Items { get; set; }

        public ViewUtil.ItemFont ItemFontNormal { get; set; }
        public ViewUtil.ItemFont ItemFontTitle { get; set; }

        public EpgViewPanel()
        {
            // これらの設定を OnRender 中に行うと、再度 OnRender イベントが発生してしまうようだ。
            // 2度同じ Render を行うことになりパフォーマンスを落とすので、OnRender の外に出しておく。
            this.VisualTextRenderingMode = TextRenderingMode.ClearType;
            this.VisualTextHintingMode = TextHintingMode.Fixed;
            this.UseLayoutRounding = true;
        }

        protected void CreateDrawTextList()
        {
            textDrawDict = new Dictionary<ProgramViewItem, List<TextDrawItem>>();
            Matrix m = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice;

            if (Items == null) return;

            if (ItemFontNormal == null || ItemFontNormal.GlyphType == null ||
                ItemFontTitle == null || ItemFontTitle.GlyphType == null)
            {
                return;
            }
            ItemFontNormal.PrepareCache();
            ItemFontTitle.PrepareCache();

            try
            {
                double selfLeft = Canvas.GetLeft(this); 
                double sizeMin = Settings.Instance.FontSizeTitle - 1;
                double sizeTitle = Settings.Instance.FontSizeTitle;
                double sizeNormal = Settings.Instance.FontSize;
                double indentTitle = Math.Floor(sizeMin * 1.7);
                double indentNormal = Math.Floor(Settings.Instance.EpgTitleIndent ? indentTitle : 2);
                Brush colorTitle = CommonManager.Instance.CustTitle1Color;
                Brush colorNormal = CommonManager.Instance.CustTitle2Color;

                double height1stLine = Math.Max(Settings.Instance.FontHeightTitle, Settings.Instance.FontHeightTitle);

                // あらかじめベースラインをそろえるために計算しておく。
                // 今は sizeMin と sizeTitle 同じだけどね…
                double baselineMin = Math.Max(sizeMin * ItemFontTitle.GlyphType.Baseline, sizeTitle * ItemFontTitle.GlyphType.Baseline);
                double baselineNormal = sizeNormal * ItemFontNormal.GlyphType.Baseline;

                foreach (ProgramViewItem info in Items)
                {
                    List<TextDrawItem> textDrawList = new List<TextDrawItem>();
                    textDrawDict[info] = textDrawList;
                    if (info.Height > 2)
                    {
                        if (info.Height < height1stLine) // ModifierMinimumHeight してるので足りなくならないハズ。
                        {
                            //高さ足りない
                            info.TitleDrawErr = true;
                        }

                        double x = info.LeftPos - selfLeft;
                        double y = info.TopPos;
                        double height = info.Height;
                        double width = info.Width;
                        double totalHeight = 0;

                        // offset
                        x += 2;
                        width -= 2;
                        y += 1;
                        height -= 1;

                        // margin 設定
                        width -= 4;

                        //分
                        string min = (info.EventInfo.StartTimeFlag == 0 ? "未定 " : info.EventInfo.start_time.Minute.ToString("d02"));
                        double useHeight = 0;
                        if (RenderText(min, ref textDrawList, ItemFontTitle, sizeMin, width, height, x, y + baselineMin, ref useHeight, colorTitle, m) == false)
                        {
                            info.TitleDrawErr = true;
                            continue;
                        }

                        //番組情報
                        if (info.EventInfo.ShortInfo != null)
                        {
                            //タイトル
                            if (info.EventInfo.ShortInfo.event_name.Length > 0)
                            {
                                if (RenderText(info.EventInfo.ShortInfo.event_name, ref textDrawList, ItemFontTitle, sizeTitle, width - indentTitle, height - totalHeight, x + indentTitle, y + totalHeight + baselineMin, ref useHeight, colorTitle, m) == false)
                                {
                                    info.TitleDrawErr = true;
                                    continue;
                                }
                                totalHeight += Math.Floor(useHeight + 4); // 説明との間隔は 4px にする
                            }
                            //説明
                            if (info.EventInfo.ShortInfo.text_char.Length > 0)
                            {
                                if (RenderText(info.EventInfo.ShortInfo.text_char, ref textDrawList, ItemFontNormal, sizeNormal, width - indentNormal, height - totalHeight, x + indentNormal, y + totalHeight + baselineNormal, ref useHeight, colorNormal, m) == false)
                                {
                                    continue;
                                }
                            }

                            //詳細
                            if (Settings.Instance.EpgExtInfoTable == true && info.EventInfo.ExtInfo != null && info.EventInfo.ExtInfo.text_char.Length > 0)
                            {
                                totalHeight += Math.Floor(useHeight + 4); // 説明との間隔は 4px にする
                                if (RenderText(info.EventInfo.ExtInfo.text_char, ref textDrawList, ItemFontNormal, sizeNormal, width - indentNormal, height - totalHeight, x + indentNormal, y + totalHeight + baselineNormal, ref useHeight, colorNormal, m) == false)
                                {
                                    continue;
                                }
                                //totalHeight += useHeight + sizeNormal;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        protected bool RenderText(String text, ref List<TextDrawItem> textDrawList, ViewUtil.ItemFont itemFont, double fontSize, double maxWidth, double maxHeight, double x, double baseline, ref double useHeight, Brush fontColor, Matrix m)
        {
            double totalHeight = 0;
            double fontHeight = fontSize * itemFont.GlyphType.Height;

            string[] lineText = text.Replace("\r", "").Split('\n');
            foreach (string line in lineText)
            {
                List<ushort> glyphIndexes = new List<ushort>();
                List<double> advanceWidths = new List<double>();

                //ベースラインの位置
                double dpix = x * m.M11;
                double dpiy = (totalHeight + baseline) * m.M22;
                // ビットマップフォントがかすれる問題 とりあえず整数にしておく
                Point origin = new Point(Math.Round(dpix / m.M11), Math.Round(dpiy / m.M22)); 

                //メイリオみたいに行間のあるフォントと MS P ゴシックみたいな行間のないフォントがあるので
                //なんとなく行間を作ってみる。
                totalHeight += Math.Max(fontHeight, fontSize + 2);
                double totalWidth = 0;

                for (int n = 0; n < line.Length; n++)
                {
                    // XAML に合わせて、行頭の空白を無視する
                    if (glyphIndexes.Count == 0 && (line[n] == ' ' || line[n] == '\x3000'))
                        continue;

                    //この辞書検索が負荷の大部分を占めているのでテーブルルックアップする
                    //(簡単なパフォーマンス計測をした結果、2～10倍くらい速くなったようだ)
                    //ushort glyphIndex = itemFont.GlyphType.CharacterToGlyphMap[line[n]];
                    //double width = itemFont.GlyphType.AdvanceWidths[glyphIndex] * fontSize;
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
                        if (glyphIndexes.Count > 0)
                        {
                            TextDrawItem item = new TextDrawItem();
                            item.FontColor = fontColor;
                            item.Text = new GlyphRun(itemFont.GlyphType, 0, false, fontSize,
                                glyphIndexes, origin, advanceWidths, null, null, null, null,
                                null, null);
                            textDrawList.Add(item);
                        }
                        if (totalHeight > maxHeight)
                        {
                            //次の行無理
                            useHeight = totalHeight;
                            return false;
                        }
                        else
                        {
                            //次の行いける
                            dpiy = (totalHeight + baseline) * m.M22;
                            origin = new Point(Math.Round(dpix / m.M11), Math.Round(dpiy / m.M22));
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
                    TextDrawItem item = new TextDrawItem();
                    item.FontColor = fontColor;
                    item.Text = new GlyphRun(itemFont.GlyphType, 0, false, fontSize,
                        glyphIndexes, origin, advanceWidths, null, null, null, null,
                        null, null);
                    textDrawList.Add(item);
                }
            }
            useHeight = Math.Floor(totalHeight);
            return true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            Brush bgBrush = Background;
            dc.DrawRectangle(bgBrush, null, new Rect(RenderSize));

            if (Items == null)
            {
                return;
            }
            
            try
            {
                //long tickStart = DateTime.Now.Ticks; //パフォーマンス計測用
                double selfLeft = Canvas.GetLeft(this);

                // Items 設定時に CreateDrawTextList を行うと、番組表に複数のタブを設定していると
                // 全てのタブの GlyphRun を一度に生成しようとするので、最初の表示までに多くの時間がかかる。
                // 表示しようとするタブのみ GlyphRun を行うことで、最初の応答時間を削減することにする。
                CreateDrawTextList();
                //long tickGlyphRun = DateTime.Now.Ticks; //パフォーマンス計測用

                foreach (ProgramViewItem info in Items)
                {
                    dc.DrawRectangle(bgBrush, null, new Rect(info.LeftPos - selfLeft, info.TopPos, info.Width, 1));
                    dc.DrawRectangle(bgBrush, null, new Rect(info.LeftPos - selfLeft, info.TopPos + info.Height, info.Width, 1));
                    if (info.Height > 1)
                    {
                        dc.DrawRectangle(info.ContentColor, null, new Rect(info.LeftPos - selfLeft, info.TopPos + 0.5, info.Width - 1, info.Height - 0.5));
                        if (textDrawDict.ContainsKey(info))
                        {
                            dc.PushClip(new RectangleGeometry(new Rect(info.LeftPos - selfLeft, info.TopPos + 0.5, info.Width - 1, info.Height - 0.5)));
                            foreach (TextDrawItem txtinfo in textDrawDict[info])
                            {
                                dc.DrawGlyphRun(txtinfo.FontColor, txtinfo.Text);
                            }
                            dc.Pop();
                        }
                    }
                }

                // EpgViewPanel は複数に分けて Render するので、最後のパネルが Render し終わったら
                // 些細なメモリ節約のために cache をクリアする
                ItemFontNormal.ClearCache();
                ItemFontTitle.ClearCache();

                //パフォーマンス計測用
                //long tickDraw = DateTime.Now.Ticks;
                //Console.Write("GlyphRun = " + Math.Round((tickGlyphRun - tickStart)/10000D).ToString()
                //    + ", Draw = " + Math.Round((tickDraw - tickGlyphRun)/10000D).ToString()
                //    + ", Total = " + Math.Round((tickDraw-tickStart)/10000D).ToString() + "\n"); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
    }

    class TextDrawItem
    {
        public Brush FontColor;
        public GlyphRun Text;
    }
}
