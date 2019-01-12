using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xiropht_Proxy_Solo_Miner
{
    public class ConsoleLog
    {
        private static StreamWriter WriterLog;

        public static void InitializeLog()
        {
            WriterLog = new StreamWriter(Program.GetCurrentPath() + "\\proxy_log.log");
            WriterLog.AutoFlush = true;
        }

        public static void WriteLine(string log)
        {
            Console.WriteLine(DateTime.Now + " - " + log);
            if (Config.WriteLog)
            {
                Task.Run(async delegate
                    {
                        await WriterLog.WriteLineAsync(DateTime.Now + " - " + log).ConfigureAwait(false);
                    });
            }
        }
    }
}
