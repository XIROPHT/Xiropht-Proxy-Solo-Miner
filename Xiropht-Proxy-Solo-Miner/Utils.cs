using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Xiropht_Proxy_Solo_Miner
{
    public class Utils
    {
        public static bool SocketIsConnected(TcpClient socket)
        {
            if (socket != null)
            {
                if (socket.Client != null)
                {
                    try
                    {
                        if (isClientConnected(socket))
                        {
                            return true;
                        }

                        return !(socket.Client.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return false;
        }


        public static bool isClientConnected(TcpClient ClientSocket)
        {
            try
            {
                var stateOfConnection = GetState(ClientSocket);


                if (stateOfConnection != TcpState.Closed && stateOfConnection != TcpState.CloseWait && stateOfConnection != TcpState.Closing)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

        }

        public static TcpState GetState(TcpClient tcpClient)
        {
            var foo = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint)
                                 && x.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint)
              );

            return foo != null ? foo.State : TcpState.Unknown;
        }

        public static string ConvertToSha512(string str)
        {
            var bytes = Encoding.ASCII.GetBytes(str);
            using (var hash = System.Security.Cryptography.SHA512.Create())
            {
                var hashedInputBytes = hash.ComputeHash(bytes);
                var hashedInputStringBuilder = new StringBuilder(128);
                foreach (var b in hashedInputBytes)
                    hashedInputStringBuilder.Append(b.ToString("X2"));
                return hashedInputStringBuilder.ToString();
            }
        }


        public static string FromHex(string hex)
        {
            byte[] ba = Encoding.ASCII.GetBytes(hex);

            return BitConverter.ToString(ba).Replace("-", "");
        }

        public static string SHA512(string input)
        {
            var bytes = Encoding.ASCII.GetBytes(input);
            using (var hash = System.Security.Cryptography.SHA512.Create())
            {
                var hashedInputBytes = hash.ComputeHash(bytes);

                var hashedInputStringBuilder = new StringBuilder(128);
                foreach (var b in hashedInputBytes)
                    hashedInputStringBuilder.Append(b.ToString("X2"));
                return hashedInputStringBuilder.ToString();
            }
        }
    }
}
