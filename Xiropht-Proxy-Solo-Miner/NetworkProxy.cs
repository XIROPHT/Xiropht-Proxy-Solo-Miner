using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Connector_All.Utils;

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

            if (ThreadProxyListen != null && (ThreadProxyListen.IsAlive || ThreadProxyListen != null))
            {
                ThreadProxyListen.Abort();
                GC.SuppressFinalize(ThreadProxyListen);
            }
            ThreadProxyListen = new Thread(async delegate ()
            {
                while (NetworkBlockchain.IsConnected && ProxyStarted)
                {
                    try
                    {

                        var tcpMiner = await ProxyListener.AcceptTcpClientAsync().ConfigureAwait(false);
                        string ip = ((IPEndPoint)(tcpMiner.Client.RemoteEndPoint)).Address.ToString();
                        TotalConnectedMiner++;

                        var cw = new Miner(tcpMiner, ListOfMiners.Count + 1, ip);
                        ListOfMiners.Add(cw);

                        await Task.Factory.StartNew(() => cw.HandleMinerAsync(), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);

                    }
                    catch
                    {
                    }
                }
                ProxyStarted = false;
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

    public class IncommingConnectionObjectPacket : IDisposable
    {
        public byte[] buffer;
        public string packet;
        private bool disposed;

        public IncommingConnectionObjectPacket()
        {
            buffer = new byte[8192];
            packet = string.Empty;
        }

        ~IncommingConnectionObjectPacket()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                buffer = null;
                packet = null;
            }
            disposed = true;
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
        public string MinerName;
        public string MinerVersion;
        public int MinerId;
        public string MinerIp;


        public Miner(TcpClient tcpClient, int id, string ip)
        {
            tcpMiner = tcpClient;
            MinerId = id;
            MinerIp = ip;
        }

        private async Task CheckMinerConnectionAsync()
        {
            while(MinerConnected)
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
            await Task.Factory.StartNew(CheckMinerConnectionAsync, CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);

            try
            {
                tcpMiner.SetSocketKeepAliveValues(20 * 60 * 1000, 30 * 1000);
            }
            catch
            {

            }
            while (MinerConnected && NetworkBlockchain.IsConnected)
            {
                if (!MinerConnected)
                {
                    break;
                }
                try
                {
                    using (var networkStream = new NetworkStream(tcpMiner.Client))
                    {
                        using (BufferedStream bufferedStreamNetwork = new BufferedStream(networkStream, ClassConnectorSetting.MaxNetworkPacketSize))
                        {
                            using (IncommingConnectionObjectPacket bufferPacket = new IncommingConnectionObjectPacket())
                            {
                                int received = 0;
                                while ((received = await bufferedStreamNetwork.ReadAsync(bufferPacket.buffer, 0, bufferPacket.buffer.Length)) > 0)
                                {
                                    if (received > 0)
                                    {
                                        bufferPacket.packet = Encoding.UTF8.GetString(bufferPacket.buffer, 0, received);
                                        await Task.Factory.StartNew(() => HandlePacketMinerAsync(bufferPacket.packet), CancellationToken.None, TaskCreationOptions.RunContinuationsAsynchronously, PriorityScheduler.Lowest).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
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

            if (NetworkBlockchain.ListMinerStats.ContainsKey(MinerName))
            {
                NetworkBlockchain.ListMinerStats[MinerName].MinerConnectionStatus = false;
                if (NetworkBlockchain.ListMinerStats[MinerName].MinerDifficultyEnd == 0 && NetworkBlockchain.ListMinerStats[MinerName].MinerDifficultyStart == 0)
                {
                    NetworkBlockchain.SpreadJobAsync();
                }
            }
        }

        /// <summary>
        /// Handle packet received from miner.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="tcpMiner"></param>
        private async void HandlePacketMinerAsync(string packet)
        {
            try
            {
                var splitPacket = packet.Split(new[] { "|" }, StringSplitOptions.None);
                switch (splitPacket[0])
                {
                    case "MINER": // For Login.
                        MinerName = splitPacket[1];
                        var MinerDifficulty = int.Parse(splitPacket[2]);
                        var MinerDifficultyPosition = int.Parse(splitPacket[3]);
                        if (MinerDifficulty > 100 || MinerDifficultyPosition > 100 || MinerDifficulty < 0 || MinerDifficultyPosition < 0)
                        {
                            MinerDifficulty = 0;
                            MinerDifficultyPosition = 0;
                        }
                        if (MinerDifficultyPosition > MinerDifficulty)
                        {
                            MinerDifficulty = 0;
                            MinerDifficultyPosition = 0;
                        }
                        if (splitPacket.Length > 4)
                        {
                            MinerVersion = splitPacket[4];
                        }
                        if (NetworkBlockchain.ListMinerStats.ContainsKey(MinerName))
                        {
                            NetworkBlockchain.ListMinerStats[MinerName].MinerConnectionStatus = true;
                            NetworkBlockchain.ListMinerStats[MinerName].MinerVersion = MinerVersion;
                            NetworkBlockchain.ListMinerStats[MinerName].MinerDifficultyStart = MinerDifficultyPosition;
                            NetworkBlockchain.ListMinerStats[MinerName].MinerDifficultyEnd = MinerDifficulty;
                            NetworkBlockchain.ListMinerStats[MinerName].MinerIp = MinerIp;
                        }
                        else
                        {
                            NetworkBlockchain.ListMinerStats.Add(MinerName, new ClassMinerStats() { MinerConnectionStatus = true, MinerTotalGoodShare = 0, MinerVersion = MinerVersion, MinerDifficultyStart = MinerDifficultyPosition, MinerDifficultyEnd = MinerDifficulty, MinerIp = MinerIp, MinerName = MinerName });
                        }
                        if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted + "|NO").ConfigureAwait(false))
                        {
                            DisconnectMiner();
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
                            if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendContentBlockMethod + "|" + dataMethod).ConfigureAwait(false))
                            {
                                DisconnectMiner();
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
                        if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendListBlockMethod + "|" + dateListMethod).ConfigureAwait(false))
                        {
                            DisconnectMiner();
                        }
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskCurrentBlockMining:
                        MinerInitialized = true;
                        NetworkBlockchain.SpreadJobAsync(MinerId);
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ShareHashrate:
                        if (NetworkBlockchain.ListMinerStats.ContainsKey(MinerName))
                        {
                            NetworkBlockchain.ListMinerStats[MinerName].MinerHashrateExpected = splitPacket[1];
                            var currentBlockId = 0;
                            if (int.TryParse(NetworkBlockchain.CurrentBlockId, out currentBlockId))
                            {
                                var differenceBlockId = (float)currentBlockId - NetworkBlockchain.FirstBlockId;
                                if (differenceBlockId > 0)
                                {
                                    var hashrateCalculated = Math.Round(((NetworkBlockchain.ListMinerStats[MinerName].MinerTotalGoodShare / differenceBlockId * 100) * 1024), 0);
                                    NetworkBlockchain.ListMinerStats[MinerName].MinerHashrateCalculated = "" + hashrateCalculated;
                                }
                                else
                                {
                                    NetworkBlockchain.ListMinerStats[MinerName].MinerHashrateCalculated = "0";
                                }
                            }
                        }
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveJob:
                        NetworkBlockchain.ListMinerStats[MinerName].MinerTotalShare++;

                        var encryptedShare = splitPacket[1];
                        if (NetworkBlockchain.CurrentBlockIndication == Utils.ConvertToSha512(encryptedShare))
                        {
                            NetworkBlockchain.ListMinerStats[MinerName].MinerTotalGoodShare++;
                            if (!await NetworkBlockchain.SendPacketAsync(packet, true).ConfigureAwait(false))
                            {
                                DisconnectMiner();
                            }
                        }
                        else
                        {
                            NetworkBlockchain.ListMinerStats[MinerName].MinerTotalInvalidShare++;
                            if (!await SendPacketAsync(ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus + "|" + ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad).ConfigureAwait(false))
                            {
                                DisconnectMiner();
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
                using (var networkStream = new NetworkStream(tcpMiner.Client))
                {
                    using (BufferedStream bufferedStreamNetwork = new BufferedStream(networkStream, ClassConnectorSetting.MaxNetworkPacketSize))
                    {
                        var bytePacket = Encoding.UTF8.GetBytes(packet);
                        await bufferedStreamNetwork.WriteAsync(bytePacket, 0, bytePacket.Length).ConfigureAwait(false);
                        await bufferedStreamNetwork.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
    
}
