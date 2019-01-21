using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.SoloMining;

namespace Xiropht_Proxy_Solo_Miner
{
    public class NetworkProxy
    {
        public static TcpListener ProxyListener;
        private static Thread ThreadProxyListen;
        public static bool ProxyStarted;
        public static int TotalConnectedMiner;
        public static List<Miner> ListOfMiners;

        public static void StartProxy()
        {
            ListOfMiners = new List<Miner>();
            ProxyListener = new TcpListener(IPAddress.Parse(Config.ProxyIP), Config.ProxyPort);
            ProxyListener.Start();

            ProxyStarted = true;

            ThreadProxyListen = new Thread(async delegate ()
            {
                while (NetworkBlockchain.IsConnected && ProxyStarted)
                {
                    try
                    {

                        var tcpMiner = await ProxyListener.AcceptTcpClientAsync().ConfigureAwait(false);
                        string ip = ((IPEndPoint)(tcpMiner.Client.RemoteEndPoint)).Address.ToString();
                        TotalConnectedMiner++;
                        ConsoleLog.WriteLine("New miner connected: " + ip);

                        int idAvailable = -1;
                        var cw = new Miner(tcpMiner, ListOfMiners.Count + 1);
                        if (ListOfMiners.Count > 1)
                        {
                            for (int i = 0; i < ListOfMiners.Count; i++)
                            {
                                if (i < ListOfMiners.Count)
                                {
                                    if (ListOfMiners[i] == null)
                                    {
                                        idAvailable = i;
                                    }
                                }
                            }
                            if (idAvailable != -1)
                            {
                                ListOfMiners[idAvailable] = cw;
                            }
                            else
                            {
                                for (int i = 0; i < ListOfMiners.Count; i++)
                                {
                                    if (i < ListOfMiners.Count)
                                    {
                                        if (ListOfMiners[i] != null)
                                        {
                                            if (!ListOfMiners[i].MinerConnected)
                                            {
                                                idAvailable = i;
                                            }
                                        }
                                    }
                                }
                                if (idAvailable != -1)
                                {
                                    ListOfMiners[idAvailable] = cw;
                                }
                                else
                                {
                                    ListOfMiners.Add(cw);
                                }
                            }
                        }
                        else
                        {
                            ListOfMiners.Add(cw);
                        }
                        new Thread(async () => await cw.HandleMinerAsync()).Start();

                    }
                    catch
                    {
                    }
                }
            });
            ThreadProxyListen.Start();
        }

        public static void StopProxy()
        {
            ProxyStarted = false;
            try
            {
                ProxyListener.Stop();
            }
            catch
            {

            }
            if (ThreadProxyListen != null && (ThreadProxyListen.IsAlive || ThreadProxyListen != null))
            {
                ThreadProxyListen.Abort();
                GC.SuppressFinalize(ThreadProxyListen);
            }
        }
    }

    public class Miner
    {
        /// <summary>
        /// Miner setting and status.
        /// </summary>
        public TcpClient tcpMiner;
        public bool MinerConnected;
        public bool MinerInitialized;
        public int MinerDifficulty;
        public int MinerDifficultyPosition;
        public string MinerName;
        public int MinerId;


        /// <summary>
        /// Miner stats.
        /// </summary>
        public int TotalGoodShare;
        public int TotalInvalidShare;
        public int TotalShare;

        public Miner(TcpClient tcpClient, int id)
        {
            tcpMiner = tcpClient;
            MinerId = id;
        }

        private async Task CheckMinerConnectionAsync()
        {
            while(true)
            {
                if (!MinerConnected)
                {
                    break;
                }
                if (!NetworkBlockchain.IsConnected)
                {
                    break;
                }
                try
                {
                    if (!Utils.SocketIsConnected(tcpMiner))
                    {
                        MinerConnected = false;
                        break;
                    }
                }
                catch
                {
                    MinerConnected = false;
                    break;
                }
                await Task.Delay(1000);
            }
            try
            {
                DisconnectMiner();
            }
            catch
            {

            }
        }

        /// <summary>
        /// Handle Miner
        /// </summary>
        /// <param name="client"></param>
        public async Task HandleMinerAsync()
        {
            MinerConnected = true;
            //await Task.Run(() => CheckMinerConnectionAsync()).ConfigureAwait(false);
            await Task.Factory.StartNew(CheckMinerConnectionAsync, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).ConfigureAwait(false);

            while (MinerConnected && NetworkBlockchain.IsConnected)
            {
                if (!MinerConnected)
                {
                    break;
                }
                try
                {
                    var reader = new StreamReader(new NetworkStream(tcpMiner.Client, true));
                    var buffer = new char[8192];

                    int received;
                    while ((received = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        string packet = new string(buffer, 0, received);
                        await Task.Run(async () => await HandlePacketMinerAsync(packet)).ConfigureAwait(false);
                    }
                }
                catch
                {
                    MinerConnected = false;
                    break;
                }
            }
            try
            {
                DisconnectMiner();
            }
            catch
            {

            }
        }

        /// <summary>
        /// Disconnect the miner.
        /// </summary>
        /// <param name="tcpMiner"></param>
        public void DisconnectMiner()
        {
            if (MinerInitialized)
            {
                if (NetworkProxy.TotalConnectedMiner > 0)
                {
                    NetworkProxy.TotalConnectedMiner--;
                }
            }
            MinerConnected = false;
            MinerInitialized = false;
            tcpMiner?.Close();
            tcpMiner?.Dispose();
            MinerDifficultyPosition = 0;
            MinerDifficulty = 0;
            Task.Run(async () => await NetworkBlockchain.SpreadJobAsync());
        }

        /// <summary>
        /// Handle packet received from miner.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="tcpMiner"></param>
        private async Task HandlePacketMinerAsync(string packet)
        {
            try
            {
                var splitPacket = packet.Split(new[] { "|" }, StringSplitOptions.None);
                switch (splitPacket[0])
                {
                    case "MINER": // For Login.
                        MinerName = splitPacket[1];
                        MinerDifficulty = int.Parse(splitPacket[2]);
                        MinerDifficultyPosition = int.Parse(splitPacket[3]);
                        if (Config.CheckShare)
                        {
                            if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted + "|YES"))
                            {
                                MinerConnected = false;
                            }


                        }
                        else
                        {
                            if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted + "|NO"))
                            {
                                MinerConnected = false;
                            }

                        }

                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod: // Receive ask to know content of selected mining method.
                        string dataMethod = null;
                        bool methodExist = false;
                        if (NetworkBlockchain.ListOfMiningMethodName.Count > 0)
                        {
                            for (int i = 0; i < NetworkBlockchain.ListOfMiningMethodName.Count; i++)
                            {
                                if (i < NetworkBlockchain.ListOfMiningMethodName.Count)
                                {
                                    if (NetworkBlockchain.ListOfMiningMethodName[i] == splitPacket[1])
                                    {
                                        methodExist = true;
                                        dataMethod = NetworkBlockchain.ListOfMiningMethodContent[i];
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (NetworkBlockchain.ListOfMiningMethodName[0] == splitPacket[1])
                            {
                                methodExist = true;
                                dataMethod = NetworkBlockchain.ListOfMiningMethodContent[0];
                            }
                        }
                        if (methodExist)
                        {
                            if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendContentBlockMethod + "|" + dataMethod))
                            {
                                MinerConnected = false;
                            }
                        }
                        break;

                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskListBlockMethod: // Receive ask to know list of mining method.
                        string dateListMethod = "";
                        if (NetworkBlockchain.ListOfMiningMethodName.Count == 1)
                        {
                            dateListMethod = NetworkBlockchain.ListOfMiningMethodName[0];
                        }
                        else
                        {
                            for (int i = 0; i < NetworkBlockchain.ListOfMiningMethodName.Count; i++)
                            {
                                if (i < NetworkBlockchain.ListOfMiningMethodName.Count)
                                {
                                    if (i < NetworkBlockchain.ListOfMiningMethodName.Count - 1)
                                    {
                                        dateListMethod += NetworkBlockchain.ListOfMiningMethodName[i] + "#";
                                    }
                                    else
                                    {
                                        dateListMethod += NetworkBlockchain.ListOfMiningMethodName[i];
                                    }
                                }
                            }
                        }
                        if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendListBlockMethod + "|" + dateListMethod))
                        {
                            MinerConnected = false;
                        }
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskCurrentBlockMining:
                        MinerInitialized = true;
                        await NetworkBlockchain.SpreadJobAsync();
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveJob:
                        TotalShare++;
                        if (Config.CheckShare)
                        {
                            try
                            {
                                var encryptedShare = splitPacket[1];
                                var jobTarget = float.Parse(splitPacket[2]);
                                var calculation = splitPacket[3];
                                var hashShare = splitPacket[4];
                                var blockId = splitPacket[5];
                                if (NetworkBlockchain.CurrentBlockIndication == Utils.ConvertStringtoMD5(encryptedShare))
                                {
                                    if (!await NetworkBlockchain.SendPacketAsync(packet, true).ConfigureAwait(false))
                                    {
                                        NetworkBlockchain.IsConnected = false;
                                    }
                                }
                                else
                                {

                                    blockId = blockId.Replace(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveJob, "");
                                    if (blockId == NetworkBlockchain.CurrentBlockId)
                                    {
                                        var splitCurrentBlockJob = NetworkBlockchain.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None);
                                        var minRange = float.Parse(splitCurrentBlockJob[0]);
                                        var maxRange = float.Parse(splitCurrentBlockJob[1]);
                                        if (jobTarget >= minRange && jobTarget <= maxRange)
                                        {
                                            var splitCalculation = calculation.Split(new[] { " " }, StringSplitOptions.None);
                                            var calculationCheck = Utils.Evaluate(splitCalculation[0], splitCalculation[2], splitCalculation[1]);
                                            if (calculationCheck >= minRange && calculationCheck <= maxRange)
                                            {
                                                int idMethod = 0;
                                                if (NetworkBlockchain.ListOfMiningMethodName.Count >= 1)
                                                {
                                                    for (int i = 0; i < NetworkBlockchain.ListOfMiningMethodName.Count; i++)
                                                    {
                                                        if (i < NetworkBlockchain.ListOfMiningMethodName.Count)
                                                        {
                                                            if (NetworkBlockchain.ListOfMiningMethodName[i] == NetworkBlockchain.CurrentBlockMethod)
                                                            {
                                                                idMethod = i;
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    idMethod = 0;
                                                }
                                                var splitMethod = NetworkBlockchain.ListOfMiningMethodContent[idMethod].Split(new[] { "#" }, StringSplitOptions.None);

                                                int roundMethod = int.Parse(splitMethod[0]);
                                                int roundSize = int.Parse(splitMethod[1]);
                                                string roundKey = splitMethod[2];
                                                int keyXorMethod = int.Parse(splitMethod[3]);

                                                string encryptedShareTest = calculation;
                                                encryptedShareTest = Utils.FromHex(encryptedShareTest + NetworkBlockchain.CurrentBlockTimestampCreate);
                                                encryptedShareTest = ClassAlgo.GetEncryptedResult(ClassAlgoEnumeration.Xor, encryptedShareTest, "" + keyXorMethod, roundSize, null);
                                                for (int i = 0; i < roundMethod; i++)
                                                {
                                                    encryptedShareTest = ClassAlgo.GetEncryptedResult(NetworkBlockchain.CurrentBlockAlgorithm, encryptedShareTest, NetworkBlockchain.CurrentBlockKey, roundSize, Encoding.ASCII.GetBytes(roundKey));

                                                }

                                                encryptedShareTest = ClassAlgo.GetEncryptedResult(NetworkBlockchain.CurrentBlockAlgorithm, encryptedShareTest, NetworkBlockchain.CurrentBlockKey, roundSize, Encoding.ASCII.GetBytes(roundKey));

                                                encryptedShareTest = Utils.SHA512(encryptedShareTest);

                                                if (encryptedShare == encryptedShareTest)
                                                {
                                                    string hashShareTest = Utils.ConvertStringtoMD5(encryptedShareTest);
                                                    if (hashShare == hashShareTest)
                                                    {
                                                        /*  if (!SendPacket(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareGood))
                                                          {
                                                              MinerConnected = false;
                                                          }
                                                          */
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine("Share hash not correct: " + hashShareTest);
                                                        if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                                        {
                                                            MinerConnected = false;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Share encrypted wrong: " + encryptedShare);
                                                    if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                                    {
                                                        MinerConnected = false;
                                                    }
                                                }

                                            }
                                            else
                                            {
                                                Console.WriteLine("Share calculation wrong: " + calculation);

                                                if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                                {
                                                    MinerConnected = false;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Share not between the range of job: " + jobTarget);
                                            if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                            {
                                                MinerConnected = false;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Share not target the right block id: " + blockId);

                                        if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                        {
                                            MinerConnected = false;
                                        }
                                    }
                                }
                            }
                            catch (Exception error)
                            {
                                ConsoleLog.WriteLine("Wrong share syntax from miner name: " + MinerName + " | Exception: " + error.Message);
                                if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                {
                                    MinerConnected = false;
                                }
                            }
                        }
                        else
                        {
                            var encryptedShare = splitPacket[1];
                            if (NetworkBlockchain.CurrentBlockIndication == Utils.ConvertStringtoMD5(encryptedShare))
                            {
                                TotalGoodShare++;
                                if (!await NetworkBlockchain.SendPacketAsync(packet, true).ConfigureAwait(false))
                                {
                                    NetworkBlockchain.IsConnected = false;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Share md5 wrong: " + encryptedShare);
                                TotalInvalidShare++;
                                if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad))
                                {
                                    MinerConnected = false;
                                }
                            }

                        }
                        break;
                }
            }
            catch
            {
                try
                {
                    DisconnectMiner();
                }
                catch
                {

                }
            }
        }

        /// <summary>
        /// Send packet to the target.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public async Task<bool> SendPacketAsync(string packet)
        {
            try
            {
                var bytePacket = Encoding.UTF8.GetBytes(packet);
                await tcpMiner.GetStream().WriteAsync(bytePacket, 0, bytePacket.Length);
                await tcpMiner.GetStream().FlushAsync();
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
    
}
