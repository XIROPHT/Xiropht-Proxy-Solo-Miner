using System;
using System.IO;
using System.Threading.Tasks;

namespace Xiropht_Proxy_Solo_Miner
{
    public class ConsoleLog
    {
        private static StreamWriter WriterLog;

        public static void InitializeLog()
        {
            WriterLog = new StreamWriter(Program.ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory + "\\proxy_log.log"))
            {
                AutoFlush = true
            };
        }

        public static async void WriteLineAsync(string log, int colorId)
        {
            ClassConsole.ConsoleWriteLine(DateTime.Now + " - " + log, colorId);
            if (Config.WriteLog)
            {
                await WriterLog.WriteLineAsync(DateTime.Now + " - " + log);
            }
        }
    
    }
}
