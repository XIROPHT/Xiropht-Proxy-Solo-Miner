using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xiropht_Proxy_Solo_Miner.API
{
    public class ClassApiHttpRequestEnumeration
    {
        public const string GetTotalMiner = "get_total_miner";
        public const string GetMinerById = "get_miner_by_id";
        public const string MinerNotExist = "miner_not_exist";
        public const string PacketNotExist = "not_exist";
    }

    public class ClassApi
    {
        public static bool IsBehindProxy;
        private static Thread ThreadListenApiHttpConnection;
        private static TcpListener ListenerApiHttpConnection;
        private static bool ListenApiHttpConnectionStatus;

        /// <summary>
        /// Enable http/https api of the remote node, listen incoming connection throught web client.
        /// </summary>
        public static void StartApiHttpServer()
        {
            ListenApiHttpConnectionStatus = true;
            ListenerApiHttpConnection = new TcpListener(IPAddress.Any, Config.ProxyApiPort);
            ListenerApiHttpConnection.Start();
            ThreadListenApiHttpConnection = new Thread(async delegate ()
            {
                while (ListenApiHttpConnectionStatus)
                {
                    try
                    {
                        var client = await ListenerApiHttpConnection.AcceptTcpClientAsync();
                        var ip = ((IPEndPoint)(client.Client.RemoteEndPoint)).Address.ToString();



                        await Task.Factory.StartNew(async () => await new ClassClientApiHttpObject(client, ip).StartHandleClientHttpAsync().ConfigureAwait(false), CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).ConfigureAwait(false);

                    }
                    catch
                    {

                    }
                }
            });
            ThreadListenApiHttpConnection.Start();
        }

        /// <summary>
        /// Stop http server
        /// </summary>
        public static void StopApiHttpServer()
        {
            ListenApiHttpConnectionStatus = false;
            if (ThreadListenApiHttpConnection != null && (ThreadListenApiHttpConnection.IsAlive || ThreadListenApiHttpConnection != null))
            {
                ThreadListenApiHttpConnection.Abort();
                GC.SuppressFinalize(ThreadListenApiHttpConnection);
            }
            ListenerApiHttpConnection.Stop();
        }
    }


    public class ClassClientApiHttpObject
    {
        private bool _clientStatus;
        private TcpClient _client;
        private string _ip;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ip"></param>
        public ClassClientApiHttpObject(TcpClient client, string ip)
        {
            _clientStatus = true;
            _client = client;
            _ip = ip;
        }

        /// <summary>
        /// Start to listen incoming client.
        /// </summary>
        /// <returns></returns>
        public async Task StartHandleClientHttpAsync()
        {

            var checkBanResult = false;


            int totalWhile = 0;
            if (!checkBanResult)
            {
                try
                {

                    while (_clientStatus)
                    {
                        try
                        {
                            using (NetworkStream clientHttpReader = new NetworkStream(_client.Client))
                            {
                                byte[] buffer = new byte[8192];
                                int received = await clientHttpReader.ReadAsync(buffer, 0, buffer.Length);
                                if (received > 0)
                                {
                                    string packet = Encoding.UTF8.GetString(buffer, 0, received);

                                    packet = Utils.GetStringBetween(packet, "GET", "HTTP");

                                    packet = packet.Replace("/", "");
                                    packet = packet.Replace(" ", "");
                                    await HandlePacketHttpAsync(packet);
                                    break;
                                }
                                else
                                {
                                    totalWhile++;
                                }
                                if (totalWhile >= 8)
                                {
                                    break;
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            Console.WriteLine("HTTP API - exception error: " + error.Message);
                            break;
                        }
                    }
                }
                catch
                {
                }

                CloseClientConnection();
            }
        }


        /// <summary>
        /// Close connection incoming from the client.
        /// </summary>
        private void CloseClientConnection()
        {
            _client?.Close();
            _client?.Dispose();
        }

        /// <summary>
        /// Handle get request received from client.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task HandlePacketHttpAsync(string packet)
        {
            long selectedIndex = 0;

            if (packet.Contains("="))
            {
                var splitPacket = packet.Split(new[] { "=" }, StringSplitOptions.None);
                long.TryParse(splitPacket[1], out selectedIndex);
                packet = splitPacket[0];
            }
            switch (packet)
            {
                case ClassApiHttpRequestEnumeration.GetTotalMiner:
                    await BuildAndSendHttpPacketAsync(""+NetworkBlockchain.ListMinerStats.Count);
                    break;
                case ClassApiHttpRequestEnumeration.GetMinerById:
                    var minerObject = Utils.GetMinerStatsFromId(selectedIndex);
                    if (minerObject != null)
                    {
                        var status = "disconnected";
                        if (minerObject.MinerConnectionStatus)
                        {
                            status = "connected";
                        }
                        Dictionary<string, string> minerContent = new Dictionary<string, string>
                        {
                            { "miner_name", "" + minerObject.MinerName },
                            {"miner_status", status },
                            { "miner_total_share", "" + minerObject.MinerTotalShare },
                            { "miner_total_good_share", "" + minerObject.MinerTotalGoodShare },
                            { "miner_total_invalid_share", "" + minerObject.MinerTotalInvalidShare },
                            { "miner_hashrate", "" + minerObject.MinerHashrateExpected },
                            { "miner_calculated_hashrate", "" + minerObject.MinerHashrateCalculated },
                            { "miner_range", "" + minerObject.MinerDifficultyStart+"|"+minerObject.MinerDifficultyEnd },
                            { "miner_version", minerObject.MinerVersion }
                        };

                        await BuildAndSendHttpPacketAsync(null, true, minerContent);
                        minerContent.Clear();
                    }
                    else
                    {
                        await BuildAndSendHttpPacketAsync(ClassApiHttpRequestEnumeration.MinerNotExist);
                    }
                    break;
                default:
                    await BuildAndSendHttpPacketAsync(ClassApiHttpRequestEnumeration.PacketNotExist);
                    break;
            }
        }

        /// <summary>
        /// build and send http packet to client.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private async Task BuildAndSendHttpPacketAsync(string content, bool multiResult = false, Dictionary<string, string> dictionaryContent = null)
        {
            string contentToSend = string.Empty;
            if (!multiResult)
            {
                contentToSend = BuildJsonString(content);
            }
            else
            {
                contentToSend = BuildFullJsonString(dictionaryContent);
            }
            StringBuilder builder = new StringBuilder();

            builder.AppendLine(@"HTTP/1.1 200 OK");
            builder.AppendLine(@"Content-Type: text/plain");
            builder.AppendLine(@"Content-Length: " + contentToSend.Length);
            builder.AppendLine(@"Access-Control-Allow-Origin: *");
            builder.AppendLine(@"");
            builder.AppendLine(@"" + contentToSend);
            await SendPacketAsync(builder.ToString());
            builder.Clear();
            contentToSend = string.Empty;
        }

        /// <summary>
        /// Return content converted for json.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private string BuildJsonString(string content)
        {
            JObject jsonContent = new JObject
            {
                { "result", content },
                { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString() }
            };
            return JsonConvert.SerializeObject(jsonContent);
        }

        /// <summary>
        /// Return content converted for json.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private string BuildFullJsonString(Dictionary<string, string> dictionaryContent)
        {
            JObject jsonContent = new JObject();
            foreach (var content in dictionaryContent)
            {
                jsonContent.Add(content.Key, content.Value);
            }
            jsonContent.Add("version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            return JsonConvert.SerializeObject(jsonContent);
        }

        /// <summary>
        /// Send packet to client.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task SendPacketAsync(string packet)
        {
            try
            {

                using (var networkStream = new NetworkStream(_client.Client))
                {
                    var bytePacket = Encoding.UTF8.GetBytes(packet);
                    await networkStream.WriteAsync(bytePacket, 0, bytePacket.Length).ConfigureAwait(false);
                    await networkStream.FlushAsync().ConfigureAwait(false);
                }

            }
            catch
            {
            }
        }
    }
}
