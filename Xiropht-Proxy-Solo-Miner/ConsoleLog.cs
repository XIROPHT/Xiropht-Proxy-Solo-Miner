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
            WriterLog = new StreamWriter(Program.ConvertPath(Directory.GetCurrentDirectory() + "\\proxy_log.log"))
            {
                AutoFlush = true
            };
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
