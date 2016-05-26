using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
using System.Windows.Media;

namespace EpgTimer
{
    class CommonUtil
    {
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // Struct we'll need to pass to the function
        internal struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public static int NumBits(long bits)
        {
            bits = (bits & 0x55555555) + (bits >> 1 & 0x55555555);
            bits = (bits & 0x33333333) + (bits >> 2 & 0x33333333);
            bits = (bits & 0x0f0f0f0f) + (bits >> 4 & 0x0f0f0f0f);
            bits = (bits & 0x00ff00ff) + (bits >> 8 & 0x00ff00ff);
            return (int)((bits & 0x0000ffff) + (bits >> 16 & 0x0000ffff));
        }
        
        public static int GetIdleTimeSec()
        {
            // Get the system uptime
            int systemUptime = Environment.TickCount;
            // The tick at which the last input was recorded
            int LastInputTicks = 0;
            // The number of ticks that passed since last input
            int IdleTicks = 0;

            // Set the struct
            LASTINPUTINFO LastInputInfo = new LASTINPUTINFO();
            LastInputInfo.cbSize = (uint)Marshal.SizeOf(LastInputInfo);
            LastInputInfo.dwTime = 0;

            // If we have a value from the function
            if (GetLastInputInfo(ref LastInputInfo))
            {
                // Get the number of ticks at the point when the last activity was seen
                LastInputTicks = (int)LastInputInfo.dwTime;
                // Number of idle ticks = system uptime ticks - number of ticks at last input
                IdleTicks = systemUptime - LastInputTicks;
            }
            return IdleTicks / 1000;
        }

        /// <summary>�����o����Ԃ��B</summary>
        public static string GetMemberName<T>(Expression<Func<T>> e)
        {
            var member = (MemberExpression)e.Body;
            return member.Member.Name;
        }

        /// <summary>���X�g�ɂ��ĕԂ��B(return new List&lt;T&gt; { item })</summary>
        public static List<T> ToList<T>(T item)
        {
            return new List<T> { item };
        }

        /// <summary>�񓯊��̃��b�Z�[�W�{�b�N�X��\��</summary>
        public static void ModelessMsgBoxShow(DispatcherObject obj, string message, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            if (obj == null)
            {
                MessageBox.Show(message, caption, button, icon);
            }
            else
            {
                obj.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(message, caption, button, icon)));
            }
        }

        /// <summary>�E�B���h�E������Ύ擾����</summary>
        public static Window GetTopWindow(Visual obj)
        {
            if (obj == null) return null;
            var topWindow = PresentationSource.FromVisual(obj);
            return topWindow == null ? null : topWindow.RootVisual as Window;
        }

        /// <summary>�e�[�}��ݒ肷��</summary>
        public static void ApplyStyle(string themeFile)
        {
            // �f�U�C���p�X�^�C�����e�[�}���}�[�W����O�ɍ폜���Ă���
            App.Current.Resources.MergedDictionaries.Clear();

            if (!string.IsNullOrEmpty(themeFile))
            {
                ResourceDictionary rd = null;
                if (System.IO.File.Exists(themeFile))
                {
                    try
                    {
                        // ResourceDictionary���`�����t�@�C��������̂Ń��[�h����
                        rd = System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(themeFile)) as ResourceDictionary;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
                else
                {
                    // ����̃e�[�}(Aero)�����[�h����
                    rd = Application.LoadComponent(new Uri("/PresentationFramework.Aero, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35;component/themes/aero.normalcolor.xaml", UriKind.Relative)) as ResourceDictionary;
                }
                if (rd != null)
                {
                    // ���[�h�����e�[�}���}�[�W����
                    App.Current.Resources.MergedDictionaries.Add(rd);
                }
            }

            // ���C�A�E�g�p�̃X�^�C�����}�[�W����
            App.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/UserCtrlView/UiLayoutStyles.xaml") });
        }

    }
}
