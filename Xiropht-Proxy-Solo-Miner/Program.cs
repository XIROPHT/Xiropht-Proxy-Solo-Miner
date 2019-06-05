using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;
using Xiropht_Proxy_Solo_Miner.API;

namespace Xiropht_Proxy_Solo_Miner
{
    class Program
    {
        public static string NetworkCertificate;
        private static Thread ThreadCheckNetworkConnection;
        private static Thread ThreadProxyCommandLine;
        public static long ProxyDateStart;

        public static void Main(string[] args)
        {
            ProxyDateStart = DateTimeOffset.Now.ToUnixTimeSeconds();
            Console.CancelKeyPress += Console_CancelKeyPress;
            ExceptionUnexpectedHandler();

            Thread.CurrentThread.Name = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            ConsoleLog.WriteLineAsync("Xiropht Proxy Solo Miner - " + Assembly.GetExecutingAssembly().GetName().Version + "R", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);

            ReadConfig();


            if (Config.WriteLog)
            {
                ConsoleLog.InitializeLog();
                ConsoleLog.WriteLineAsync("Write Log Enabled.", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
            }

            ConsoleLog.WriteLineAsync("Wallet Address selected: " + Config.WalletAddress, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
            ConsoleLog.WriteLineAsync("Proxy IP Selected: " + Config.ProxyIP, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
            ConsoleLog.WriteLineAsync("Proxy Port Selected: " + Config.ProxyPort, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);

            if (Config.EnableApi)
            {
                ConsoleLog.WriteLineAsync("Start HTTP API..", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                ClassApi.StartApiHttpServer();
                ConsoleLog.WriteLineAsync("HTTP API started.", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
            }

            ThreadCheckNetworkConnection = new Thread(async delegate ()
            {
                bool connectSuccess = false;
                while (!connectSuccess)
                {
                    while (!await NetworkBlockchain.ConnectToBlockchainAsync())
                    {
                        ConsoleLog.WriteLineAsync("Can't connect to the network, retry in 5 seconds..", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                        Thread.Sleep(5000);
                    }
                    ConsoleLog.WriteLineAsync("Connection success, generate dynamic certificate for the network.", ClassConsoleColorEnumeration.IndexConsoleGreenLog);
                    NetworkCertificate = ClassUtils.GenerateCertificate();
                    ConsoleLog.WriteLineAsync("Certificate generate, send to the network..", ClassConsoleColorEnumeration.IndexConsoleYellowLog);
                    if (!await NetworkBlockchain.SendPacketAsync(NetworkCertificate, false))
                    {
                        ConsoleLog.WriteLineAsync("Can't send certificate, reconnect now..", ClassConsoleColorEnumeration.IndexConsoleRedLog);
                    }
                    else
                    {
                        ConsoleLog.WriteLineAsync("Certificate sent, start to login..", ClassConsoleColorEnumeration.IndexConsoleYellowLog);
                        NetworkBlockchain.ListenBlockchain();
                        Thread.Sleep(1000);
                        if (!await NetworkBlockchain.SendPacketAsync(ClassConnectorSettingEnumeration.MinerLoginType + "|" + Config.WalletAddress, true))
                        {
                            ConsoleLog.WriteLineAsync("Can't login to the network, reconnect now.", ClassConsoleColorEnumeration.IndexConsoleRedLog);
                        }
                        else
                        {
                            ConsoleLog.WriteLineAsync("Login successfully sent, waiting confirmation..", ClassConsoleColorEnumeration.IndexConsoleYellowLog);
                            connectSuccess = true;
                        }
                    }
                }
            });
            ThreadCheckNetworkConnection.Start();

            ThreadProxyCommandLine = new Thread(delegate ()
            {
                while (true)
                {
                    string commandLine = GetHiddenConsoleInput();
                    CommandLine(commandLine);
                }
            });
            ThreadProxyCommandLine.Start();
        }

        /// <summary>
        /// Return only one key char input from the user and return it has a command line.
        /// </summary>
        /// <returns></returns>
        public static string GetHiddenConsoleInput()
        {
            string command = string.Empty;
            StringBuilder input = new StringBuilder();

            var key = Console.ReadKey(true);
            input.Append(key.KeyChar);

            command = input.ToString();
            input.Clear();
            return command;
        }

        /// <summary>
        /// Handle unexpected exception.
        /// </summary>
        private static void ExceptionUnexpectedHandler()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args2)
            {
                var filePath = ConvertPath(System.AppDomain.CurrentDomain.BaseDirectory + "\\error_proxy_miner.txt");
                var exception = (Exception)args2.ExceptionObject;
                using (var writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine("Message :" + exception.Message + "<br/>" + Environment.NewLine +
                                     "StackTrace :" +
                                     exception.StackTrace +
                                     "" + Environment.NewLine + "Date :" + DateTime.Now);
                    writer.WriteLine(Environment.NewLine +
                                     "-----------------------------------------------------------------------------" +
                                     Environment.NewLine);
                }

                Trace.TraceError(exception.StackTrace);

                Environment.Exit(1);

            };
        }

        /// <summary>
        /// Event for detect Cancel Key pressed by the user for close the program.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            ClassConsole.ConsoleWriteLine("Close proxy solo miner tool.", ClassConsoleColorEnumeration.IndexConsoleRedLog);
            Process.GetCurrentProcess().Kill();
        }

        /// <summary>
        /// Execute command lines.
        /// </summary>
        /// <param name="command"></param>
        private static void CommandLine(string command)
        {
            switch (command)
            {
                case "h":
                    ConsoleLog.WriteLineAsync("h - Show command list.", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                    ConsoleLog.WriteLineAsync("s - Show proxy stats with miners stats.", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                    break;
                case "s":


                    int totalMinerConnected = 0;

                    if (NetworkBlockchain.ListMinerStats.Count > 0)
                    {
                        foreach (var minerStats in NetworkBlockchain.ListMinerStats)
                        {
                            if (minerStats.Value.MinerDifficultyStart == 0 && minerStats.Value.MinerDifficultyEnd == 0)
                            {
                                ConsoleLog.WriteLineAsync("Miner name: " + minerStats.Key + " - Select range: Automatic - IP: " + minerStats.Value.MinerIp, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                            }
                            else
                            {
                                ConsoleLog.WriteLineAsync("Miner name: " + minerStats.Key + " - Select range: " + minerStats.Value.MinerDifficultyStart + "|" + minerStats.Value.MinerDifficultyEnd + " - IP: " + minerStats.Value.MinerIp, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                            }
                            if (minerStats.Value.MinerConnectionStatus)
                            {
                                ConsoleLog.WriteLineAsync("Miner status: Connected.", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                                totalMinerConnected++;
                            }
                            else
                            {
                                ConsoleLog.WriteLineAsync("Miner status: Disconnected.", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                            }
                            ConsoleLog.WriteLineAsync("Miner total share: " + minerStats.Value.MinerTotalShare, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                            ConsoleLog.WriteLineAsync("Miner total good share: " + minerStats.Value.MinerTotalGoodShare, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                            ConsoleLog.WriteLineAsync("Miner total invalid share: " + minerStats.Value.MinerTotalInvalidShare, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                            ConsoleLog.WriteLineAsync("Miner Hashrate Expected: " + minerStats.Value.MinerHashrateExpected, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);



                            ConsoleLog.WriteLineAsync("Miner Hashrate Calculated from blocks found: " + minerStats.Value.MinerHashrateCalculated, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);

                            string version = minerStats.Value.MinerVersion;
                            if (string.IsNullOrEmpty(version))
                            {
                                version = "Unknown";
                            }
                            ConsoleLog.WriteLineAsync("Miner version: " + version, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                        }
                    }

                    if (NetworkBlockchain.IsConnected)
                    {
                        ConsoleLog.WriteLineAsync("Network proxy connection to the network status: Connected.", ClassConsoleColorEnumeration.IndexConsoleGreenLog);
                    }
                    else
                    {
                        ConsoleLog.WriteLineAsync("Network proxy connection to the network status: Disconnected.", ClassConsoleColorEnumeration.IndexConsoleRedLog);
                    }
                    ConsoleLog.WriteLineAsync("Total miners connected: " + totalMinerConnected, ClassConsoleColorEnumeration.IndexConsoleMagentaLog);

                    ConsoleLog.WriteLineAsync(">> Invalid share can mean the share is invalid or already found.<<", ClassConsoleColorEnumeration.IndexConsoleYellowLog);
                    ConsoleLog.WriteLineAsync(">> Only total block unlocked confirmed counter retrieve you the right amount of blocks found.<<", ClassConsoleColorEnumeration.IndexConsoleYellowLog);
                    ConsoleLog.WriteLineAsync("Total block unlocked confirmed: " + NetworkBlockchain.TotalBlockUnlocked, ClassConsoleColorEnumeration.IndexConsoleGreenLog);
                    ConsoleLog.WriteLineAsync("Total block bad/orphan received: " + NetworkBlockchain.TotalBlockWrong, ClassConsoleColorEnumeration.IndexConsoleRedLog);
                    break;
            }


        }

        /// <summary>
        /// Get current path of the program.
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentPath()
        {
            string path = System.AppDomain.CurrentDomain.BaseDirectory;
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

        /// <summary>
        /// Convert path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ConvertPath(string path)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

        /// <summary>
        /// Get Current Path of the program config file.
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentPathFile()
        {
            string path = System.AppDomain.CurrentDomain.BaseDirectory + "\\config.ini";
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

        /// <summary>
        /// Read config file.
        /// </summary>
        public static void ReadConfig()
        {
            if (File.Exists(GetCurrentPathFile()))
            {
                using (StreamReader reader = new StreamReader(GetCurrentPathFile()))
                {
                    string line = string.Empty;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("WALLET_ADDRESS="))
                        {
                            Config.WalletAddress = line.Replace("WALLET_ADDRESS=", "");
                        }
                        else if (line.Contains("PROXY_PORT="))
                        {
                            Config.ProxyPort = int.Parse(line.Replace("PROXY_PORT=", ""));
                        }
                        else if (line.Contains("PROXY_IP="))
                        {
                            Config.ProxyIP = line.Replace("PROXY_IP=", "");
                        }
                        else if (line.Contains("WRITE_LOG="))
                        {
                            string choose = line.Replace("WRITE_LOG=", "").ToLower();
                            if (choose == "y")
                            {
                                Config.WriteLog = true;
                            }
                        }
                        else if (line.Contains("ENABLE_API="))
                        {
                            string choose = line.Replace("ENABLE_API=", "").ToLower();
                            if (choose == "y")
                            {
                                Config.EnableApi = true;
                            }
                        }
                        else if (line.Contains("API_PORT="))
                        {
                            string choose = line.Replace("API_PORT=", "").ToLower();
                            if (int.TryParse(choose, out var port))
                            {
                                if (port > 0)
                                {
                                    Config.ProxyApiPort = port;
                                }
                            }
                        }
                    }
                }
            }
            else // First initialization
            {
                File.Create(GetCurrentPathFile()).Close();
                ClassConsole.ConsoleWriteLine("No config.ini found, first initialization:", ClassConsoleColorEnumeration.IndexConsoleRedLog);
                ClassConsole.ConsoleWriteLine("Write your wallet address: ", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                Config.WalletAddress = Console.ReadLine();
                ClassConsole.ConsoleWriteLine("Write an IP to bind [0.0.0.0 for listen on every network cards]: ", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                Config.ProxyIP = Console.ReadLine();
                ClassConsole.ConsoleWriteLine("Select a port to bind: ", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                Config.ProxyPort = int.Parse(Console.ReadLine());
                ClassConsole.ConsoleWriteLine("Do you want enable log system ? [Y/N]: ", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                string choose = Console.ReadLine();
                if (choose.ToLower() == "y")
                {
                    Config.WriteLog = true;
                }
                ClassConsole.ConsoleWriteLine("Do you want to enable the API system? [Y/N]: ", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                choose = Console.ReadLine();
                if (choose.ToLower() == "y")
                {
                    Config.EnableApi = true;
                }
                if (Config.EnableApi)
                {
                    ClassConsole.ConsoleWriteLine("Then, do you want to select your own API port? [Default 8000]: ", ClassConsoleColorEnumeration.IndexConsoleMagentaLog);
                    choose = Console.ReadLine();
                    int port = 0;
                    if (int.TryParse(choose, out port))
                    {
                        Config.ProxyApiPort = port;
                    }
                }
                using (StreamWriter writeConfig = new StreamWriter(GetCurrentPathFile()) { AutoFlush = true })
                {
                    writeConfig.WriteLine("WALLET_ADDRESS=" + Config.WalletAddress);
                    writeConfig.WriteLine("PROXY_PORT=" + Config.ProxyPort);
                    writeConfig.WriteLine("PROXY_IP=" + Config.ProxyIP);
                    if (Config.WriteLog)
                    {
                        writeConfig.WriteLine("WRITE_LOG=Y");
                    }
                    else
                    {
                        writeConfig.WriteLine("WRITE_LOG=N");
                    }
                    if (Config.EnableApi)
                    {
                        writeConfig.WriteLine("ENABLE_API=Y");
                        writeConfig.WriteLine("API_PORT=" + Config.ProxyApiPort);
                    }
                    else
                    {
                        writeConfig.WriteLine("ENABLE_API=N");
                        writeConfig.WriteLine("API_PORT=" + Config.ProxyApiPort);
                    }
                    writeConfig.Close();
                }
            }
        }
    }
}
