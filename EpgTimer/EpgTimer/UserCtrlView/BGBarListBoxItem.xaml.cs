using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EpgTimer.UserCtrlView
{
    /// <summary>
    /// BGBarListBoxItem.xaml の相互作用ロジック
    /// SetBasicView.xaml の listBox_recFolder で使われる ListBoxItem コントロール
    /// </summary>
    public partial class BGBarListBoxItem : UserControl
    {
        public BGBarListBoxItem(RecFolderInfo item)
        {
            InitializeComponent();

            labelFolder.Content = item.recFolder;
            progressBar.Value = item.freeBytes;
            progressBar.Maximum = item.totalBytes;

            GradientStopCollection stops = new GradientStopCollection();
            stops.Add(new GradientStop(Colors.Red, 0.1));
            stops.Add(new GradientStop(Colors.Yellow, 0.3));
            stops.Add(new GradientStop(Colors.Lime, 0.4));
            LinearGradientBrush brush = new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 0));
            brush.ColorInterpolationMode = ColorInterpolationMode.SRgbLinearInterpolation;
            progressBar.Background = brush;

            if (item.freeBytes > 0 && item.totalBytes > 0)
            {
                List<string> units = new List<string> { "Bytes", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };
                int unit_base = 1024;

                int n = (int)Math.Floor(Math.Min(Math.Log(item.freeBytes) / Math.Log(unit_base), units.Count - 1));
                int m = (int)Math.Floor(Math.Min(Math.Log(item.totalBytes) / Math.Log(unit_base), units.Count - 1));
                ToolTip = "空き容量: " + Math.Round(item.freeBytes / Math.Pow(unit_base, n), 1).ToString() + " " + units[n]
                 + "/" + Math.Round(item.totalBytes / Math.Pow(unit_base, m), 1).ToString() + " " + units[m];
            }
        }
        public string Text
        {
            get { return labelFolder.Content as string; }
            set { labelFolder.Content = value; }
        }
        public override string ToString() { return labelFolder.Content as string;  }
    }
}
