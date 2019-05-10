using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Xiropht_Connector_All.Utils;
using Xiropht_Proxy_Solo_Miner.API;

namespace Xiropht_Proxy_Solo_Miner
{
    class Program
    {
        public static string NetworkCertificate;
        private static Thread ThreadCheckNetworkConnection;

        /// Get Current Path of the program.
        public static string GetCurrentPath()
        {
            string path = Directory.GetCurrentDirectory();
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

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
            string path = Directory.GetCurrentDirectory() + "\\config.ini";
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

        public static void ReadConfig()
        {
            if (File.Exists(GetCurrentPathFile()))
            {
                StreamReader reader = new StreamReader(GetCurrentPathFile());

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
            else // First initialization
            {
                File.Create(GetCurrentPathFile()).Close();
                Console.WriteLine("No config.ini found, first initialization:");
                Console.WriteLine("Write your wallet address: ");
                Config.WalletAddress = Console.ReadLine();
                Console.WriteLine("Write an IP to bind [0.0.0.0 for listen on every network cards]: ");
                Config.ProxyIP = Console.ReadLine();
                Console.WriteLine("Select a port to bind: ");
                Config.ProxyPort = int.Parse(Console.ReadLine());
                Console.WriteLine("Do you want enable log system ? [Y/N]: ");
                string choose = Console.ReadLine();
                if (choose.ToLower() == "y")
                {
                    Config.WriteLog = true;
                }
                Console.WriteLine("Do you want to enable the API system? [Y/N]: ");
                choose = Console.ReadLine();
                if (choose.ToLower() == "y")
                {
                    Config.EnableApi = true;
                }
                if (Config.EnableApi)
                {
                    Console.WriteLine("Then, do you want to select your own API port? [Default 8000]: ");
                    choose = Console.ReadLine();
                    int port = 0;
                    if (int.TryParse(choose, out port))
                    {
                        Config.ProxyApiPort = port;
                    }
                }
                StreamWriter writeConfig = new StreamWriter(GetCurrentPathFile());
                writeConfig.WriteLine("WALLET_ADDRESS=" + Config.WalletAddress);
                writeConfig.Flush();
                writeConfig.WriteLine("PROXY_PORT=" + Config.ProxyPort);
                writeConfig.Flush();
                writeConfig.WriteLine("PROXY_IP=" + Config.ProxyIP);
                writeConfig.Flush();
                if (Config.WriteLog)
                {
                    writeConfig.WriteLine("WRITE_LOG=Y");
                    writeConfig.Flush();
                }
                else
                {
                    writeConfig.WriteLine("WRITE_LOG=N");
                    writeConfig.Flush();
                }
                if (Config.EnableApi)
                {
                    writeConfig.WriteLine("ENABLE_API=Y");
                    writeConfig.Flush();
                    writeConfig.WriteLine("API_PORT=" + Config.ProxyApiPort);
                    writeConfig.Flush();
                }
                else
                {
                    writeConfig.WriteLine("ENABLE_API=N");
                    writeConfig.Flush();

                    writeConfig.WriteLine("API_PORT=" + Config.ProxyApiPort);
                    writeConfig.Flush();
                }
                writeConfig.Close();
            }
        }


        /// <summary>
        /// Event for detect Cancel Key pressed by the user for close the program.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Console.WriteLine("Close proxy solo miner tool.");
            Process.GetCurrentProcess().Kill();
        }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args2)
            {
                var filePath = ConvertPath(Directory.GetCurrentDirectory() + "\\error_proxy_miner.txt");
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
            Thread.CurrentThread.Name = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            ConsoleLog.WriteLine("Xiropht Proxy Solo Miner - " + Assembly.GetExecutingAssembly().GetName().Version + "R");

            ReadConfig();
            if (Config.WriteLog)
            {
                ConsoleLog.InitializeLog();
                ConsoleLog.WriteLine("Write Log Enabled.");
            }
            ConsoleLog.WriteLine("Wallet Address selected: " + Config.WalletAddress);
            ConsoleLog.WriteLine("Proxy IP Selected: " + Config.ProxyIP);
            ConsoleLog.WriteLine("Proxy Port Selected: " + Config.ProxyPort);

            if (Config.EnableApi)
            {
                ConsoleLog.WriteLine("Start HTTP API..");
                ClassApi.StartApiHttpServer();
                ConsoleLog.WriteLine("HTTP API started.");
            }

            ThreadCheckNetworkConnection = new Thread(async delegate ()
            {
                bool connectSuccess = false;
                while (!connectSuccess)
                {
                    while (!await NetworkBlockchain.ConnectToBlockchainAsync())
                    {
                        ConsoleLog.WriteLine("Can't connect to the network, retry in 5 seconds..");
                        Thread.Sleep(5000);
                    }
                    ConsoleLog.WriteLine("Connection success, generate dynamic certificate for the network.");
                    NetworkCertificate = ClassUtils.GenerateCertificate();
                    ConsoleLog.WriteLine("Certificate generate, send to the network..");
                    if (!await NetworkBlockchain.SendPacketAsync(NetworkCertificate, false))
                    {
                        ConsoleLog.WriteLine("Can't send certificate, reconnect now..");
                    }
                    else
                    {
                        ConsoleLog.WriteLine("Certificate sent, start to login..");
                        NetworkBlockchain.ListenBlockchain();
                        Thread.Sleep(1000);
                        if (!await NetworkBlockchain.SendPacketAsync("MINER|" + Config.WalletAddress, true))
                        {
                            ConsoleLog.WriteLine("Can't login to the network, reconnect now.");
                        }
                        else
                        {
                            ConsoleLog.WriteLine("Login successfully sent, waiting confirmation..");
                            connectSuccess = true;
                        }
                    }
                }
            });
            ThreadCheckNetworkConnection.Start();
            new Thread(delegate ()
            {
                while (true)
                {
                    StringBuilder input = new StringBuilder();
                    var key = Console.ReadKey(true);
                    input.Append(key.KeyChar);
                    CommandLine(input.ToString());
                    input.Clear();
                }
            }).Start();
        }

        private static void CommandLine(string command)
        {
            switch (command)
            {
                case "h":
                    ConsoleLog.WriteLine("h - Show command list.");
                    ConsoleLog.WriteLine("s - Show proxy stats with miners stats.");
                    break;
                case "s":
                    ConsoleLog.WriteLine(">> Invalid share can mean you have get orphaned block <<");
                    ConsoleLog.WriteLine("Total block unlock: " + NetworkBlockchain.TotalBlockUnlocked);
                    ConsoleLog.WriteLine("Total block bad unlock: " + NetworkBlockchain.TotalBlockWrong);
                    if (NetworkBlockchain.IsConnected)
                    {
                        ConsoleLog.WriteLine("Network proxy connection to the network status: Connected.");
                    }
                    else
                    {
                        ConsoleLog.WriteLine("Network proxy connection to the network status: Disconnected.");
                    }
                    int totalMinerConnected = 0;

                    if (NetworkBlockchain.ListMinerStats.Count > 0)
                    {
                        foreach (var minerStats in NetworkBlockchain.ListMinerStats)
                        {
                            if (minerStats.Value.MinerDifficultyStart == 0 && minerStats.Value.MinerDifficultyEnd == 0)
                            {
                                ConsoleLog.WriteLine("Miner name: " + minerStats.Key + " - Select range: Automatic - IP: " + minerStats.Value.MinerIp);
                            }
                            else
                            {
                                ConsoleLog.WriteLine("Miner name: " + minerStats.Key + " - Select range: " + minerStats.Value.MinerDifficultyStart + "|" + minerStats.Value.MinerDifficultyEnd + " - IP: " + minerStats.Value.MinerIp);
                            }
                            if (minerStats.Value.MinerConnectionStatus)
                            {
                                ConsoleLog.WriteLine("Miner status: Connected.");
                                totalMinerConnected++;
                            }
                            else
                            {
                                ConsoleLog.WriteLine("Miner status: Disconnected.");
                            }
                            ConsoleLog.WriteLine("Miner total share: " + minerStats.Value.MinerTotalShare);
                            ConsoleLog.WriteLine("Miner total good share: " + minerStats.Value.MinerTotalGoodShare);
                            ConsoleLog.WriteLine("Miner total invalid share: " + minerStats.Value.MinerTotalInvalidShare);
                            ConsoleLog.WriteLine("Miner Hashrate Expected: " + minerStats.Value.MinerHashrateExpected);



                            ConsoleLog.WriteLine("Miner Hashrate Calculated from blocks found: " + minerStats.Value.MinerHashrateCalculated);

                            string version = minerStats.Value.MinerVersion;
                            if (string.IsNullOrEmpty(version))
                            {
                                version = "Unknown";
                            }
                            ConsoleLog.WriteLine("Miner version: " + version);
                        }
                    }
                    ConsoleLog.WriteLine("Total miners connected: " + totalMinerConnected);

                    break;
            }


        }
    }
}
