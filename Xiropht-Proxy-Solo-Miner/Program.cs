using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Connector_All.Utils;

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
            if (Environment.OSVersion.Platform == PlatformID.Unix)
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
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                path = path.Replace("\\", "/");
            }
            return path;
        }

        public static void ReadConfig()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args2)
            {
                var filePath = ".\\error_proxyminer.txt";
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

                System.Diagnostics.Trace.TraceError(exception.StackTrace);

                Environment.Exit(1);

            };
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
                    else if (line.Contains("CHECK_SHARE="))
                    {
                        if (line.Replace("CHECK_SHARE=", "") == "Y")
                        {
                            Config.CheckShare = true;
                        }
                    }
                    else if (line.Contains("WRITE_LOG="))
                    {
                        if (line.Replace("WRITE_LOG=", "") == "Y")
                        {
                            Config.WriteLog = true;
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
                Console.WriteLine("Do you want check each share from your miners ? [Y/N]: ");
                if (Console.ReadLine() == "Y" || Console.ReadLine() == "y")
                {
                    Config.CheckShare = true;
                }
                Console.WriteLine("Do you want enable log system ? [Y/N]: ");
                if (Console.ReadLine() == "Y" || Console.ReadLine() == "y")
                {
                    Config.WriteLog = true;
                }
                StreamWriter writeConfig = new StreamWriter(GetCurrentPathFile());
                writeConfig.WriteLine("WALLET_ADDRESS=" + Config.WalletAddress);
                writeConfig.Flush();
                writeConfig.WriteLine("PROXY_PORT=" + Config.ProxyPort);
                writeConfig.Flush();
                writeConfig.WriteLine("PROXY_IP=" + Config.ProxyIP);
                writeConfig.Flush();
                if (Config.CheckShare)
                {
                    writeConfig.WriteLine("CHECK_SHARE=Y");
                    writeConfig.Flush();
                }
                else
                {
                    writeConfig.WriteLine("CHECK_SHARE=N");
                    writeConfig.Flush();
                }
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
                writeConfig.Close();
            }
        }

        static void Main(string[] args)
        {
            ReadConfig();
            if (Config.WriteLog)
            {
                ConsoleLog.InitializeLog();
                ConsoleLog.WriteLine("Write Log Enabled.");
            }
            if (Config.CheckShare)
            {
                ConsoleLog.WriteLine("Check Share Enabled.");
            }
            ConsoleLog.WriteLine("Wallet Address selected: " + Config.WalletAddress);
            ConsoleLog.WriteLine("Proxy IP Selected: " + Config.ProxyIP);
            ConsoleLog.WriteLine("Proxy Port Selected: " + Config.ProxyPort);


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
            new Thread(async delegate ()
            {
                while(true)
                {
                    Thread.Sleep(100);
                    var command = Console.ReadLine().Split(new[] { " " }, StringSplitOptions.None);


                    switch(command[0])
                    {
                        case "h":
                            ConsoleLog.WriteLine("h - Show command list.");
                            ConsoleLog.WriteLine("s - Show proxy stats with miners stats.");
                            ConsoleLog.WriteLine("d - Disable Check Share.");
                            ConsoleLog.WriteLine("e - Enable Check Share. --> This mode has been made for prove this is impossible to make a pool (Ex: You can't handle 2000 share per second per miners)");
                            break;
                        case "s":
                            ConsoleLog.WriteLine(">> If you don't use check share system, that's mean invalid share give orphaned block <<");
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
                            if (NetworkProxy.ListOfMiners.Count > 0)
                            {

                                for (int i = 0; i < NetworkProxy.ListOfMiners.Count; i++)
                                {
                                    if (i < NetworkProxy.ListOfMiners.Count)
                                    {
                                        ConsoleLog.WriteLine("Miner ID: " + i);
                                        ConsoleLog.WriteLine("Miner name: " + NetworkProxy.ListOfMiners[i].MinerName);
                                        if (NetworkProxy.ListOfMiners[i].MinerConnected)
                                        {
                                            ConsoleLog.WriteLine("Miner status: Connected.");
                                            totalMinerConnected++;
                                        }
                                        else
                                        {
                                            ConsoleLog.WriteLine("Miner status: Disconnected.");
                                        }
                                        ConsoleLog.WriteLine("Miner total share: " + NetworkProxy.ListOfMiners[i].TotalShare);
                                        ConsoleLog.WriteLine("Miner total good share: " + NetworkProxy.ListOfMiners[i].TotalGoodShare);
                                        ConsoleLog.WriteLine("Miner total invalid share: " + NetworkProxy.ListOfMiners[i].TotalInvalidShare);

                                    }
                                }
                            }
                            ConsoleLog.WriteLine("Total miners connected: " + totalMinerConnected);

                            break;
                        case "e":
                            if (NetworkProxy.ListOfMiners.Count > 0)
                            {
                                for (int i = 0; i < NetworkProxy.ListOfMiners.Count; i++)
                                {
                                    if (i < NetworkProxy.ListOfMiners.Count)
                                    {
                                        if (NetworkProxy.ListOfMiners[i].MinerConnected)
                                        {

                                            if (!await NetworkProxy.ListOfMiners[i].SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendEnableCheckShare))
                                            {
                                                NetworkProxy.ListOfMiners[i].DisconnectMiner();
                                            }
                                        }

                                    }
                                }
                                Config.CheckShare = true;
                            }
                            break;
                        case "d":
                            if (NetworkProxy.ListOfMiners.Count > 0)
                            {
                                for (int i = 0; i < NetworkProxy.ListOfMiners.Count; i++)
                                {
                                    if (i < NetworkProxy.ListOfMiners.Count)
                                    {
                                        if (NetworkProxy.ListOfMiners[i].MinerConnected)
                                        {
                                            if (!await NetworkProxy.ListOfMiners[i].SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendDisableCheckShare))
                                            {
                                                NetworkProxy.ListOfMiners[i].DisconnectMiner();
                                            }
                                        }

                                    }
                                }
                                Config.CheckShare = false;
                            }
                            break;
                    }
                }
            }).Start();
        }
    }
}
