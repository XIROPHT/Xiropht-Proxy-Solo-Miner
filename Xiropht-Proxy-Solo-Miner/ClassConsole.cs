using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xiropht_Proxy_Solo_Miner
{
    public class ClassConsoleColorEnumeration
    {
        public const int IndexConsoleGreenLog = 0;
        public const int IndexConsoleYellowLog = 1;
        public const int IndexConsoleRedLog = 2;
        public const int IndexConsoleWhiteLog = 3;
        public const int IndexConsoleBlueLog = 4;
        public const int IndexConsoleMagentaLog = 5;
    }

    public class ClassConsole
    {

        public static void ConsoleWriteLine(string text, int colorId = -1)
        {
            text = DateTime.Now + " - " + text;

            switch (colorId)
            {
                case ClassConsoleColorEnumeration.IndexConsoleGreenLog:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case ClassConsoleColorEnumeration.IndexConsoleYellowLog:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case ClassConsoleColorEnumeration.IndexConsoleRedLog:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case ClassConsoleColorEnumeration.IndexConsoleBlueLog:
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    break;
                case ClassConsoleColorEnumeration.IndexConsoleMagentaLog:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case ClassConsoleColorEnumeration.IndexConsoleWhiteLog:
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }
            Console.WriteLine(text);
        }
    }
}
