using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Xiropht_Proxy_Solo_Miner
{
    public class ConsoleLog
    {
        private static StreamWriter WriterLog;
        private static List<string> ListLog;
        private static Thread ThreadWriteLog;

        public static void InitializeLog()
        {
            ListLog = new List<string>();
            if (!File.Exists(Program.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory + "\\proxy_log.log")))
            {
                File.Create(Program.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory + "\\proxy_log.log")).Close();
            }
            WriterLog = new StreamWriter(Program.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory + "\\proxy_log.log"))
            {
                AutoFlush = true
            };

            AutoWriteLog();

        }

        public static void WriteLine(string log, int colorId)
        {
            ClassConsole.ConsoleWriteLine(DateTime.Now + " - " + log, colorId);
            if (Config.WriteLog)
            {
                try
                {
                    //await WriterLog.WriteLineAsync(DateTime.Now + " - " + log);
                    if (ListLog.Count < int.MaxValue)
                    {
                        ListLog.Add(DateTime.Now + " - " + log);
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Autowrite log function.
        /// </summary>
        private static void AutoWriteLog()
        {
            ThreadWriteLog = new Thread(async delegate ()
            {
                while(true)
                {
                    try
                    {
                        if (WriterLog == null)
                        {
                            WriterLog = new StreamWriter(Program.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory + "\\proxy_log.log"))
                            {
                                AutoFlush = true
                            };
                        }
                        var copyLog = new List<string>(ListLog);
                        ListLog.Clear();
                        for(int i = 0; i < copyLog.Count; i++)
                        {
                            if (i < copyLog.Count)
                            {
                                if (copyLog[i] != null)
                                {
                                    await WriterLog.WriteLineAsync(copyLog[i]);
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (WriterLog != null)
                        {
                            try
                            {
                                WriterLog?.Close();
                                WriterLog?.Dispose();
                                WriterLog = null;
                            }
                            catch
                            {

                            }
                        }
                    }
                    Thread.Sleep(1000 * 10);
                }
            });
            ThreadWriteLog.Start();
        }
    }
}
