using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace EpgTimer
{
    public static class ExceptionLogger
    {
        private static object LockObj = new object();

        public static readonly string LogName = "EpgTimerExceptionLog.txt";

        public static string LogPath { get; private set; }
        public static int ExceptionCounter { get; private set; }

        static ExceptionLogger()
        {
            string logDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            LogPath = Path.Combine(logDir, LogName);
        }

        /// <summary>
        /// 例外をログファイルに記録します。
        /// </summary>
        /// <param name="ex">ログに記録する例外</param>
        /// <remarks>例外が例外を誘発する等、例外が頻発してログファイルが急激に肥大化することを防ぐため、
        /// 一定回数(100回)ログに記録すると以降の例外はログファイルに記録されません。</remarks>
        public static void Log(Exception ex, bool isUnhandledException = false)
        {
            lock (LockObj)
            {
                ExceptionCounter++;
                if (ExceptionCounter > 100)
                {
                    return;
                }

                try
                {
                    using (TextWriter writer = new StreamWriter(LogPath, true, Encoding.Unicode))
                    {
                        string header =
                            DateTime.Now +
                            " (" + ExceptionCounter + "回目)" +
                            ((isUnhandledException) ? " !!捕捉されていない例外!!" : "");

                        writer.WriteLine(header);
                        writer.WriteLine(ex);
                        writer.WriteLine(new string('-', 80));
                    }
                }
                catch
                {
                    // 何もしない
                }
            }
        }
    }
}
