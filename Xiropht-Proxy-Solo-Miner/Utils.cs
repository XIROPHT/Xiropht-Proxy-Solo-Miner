using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Xiropht_Proxy_Solo_Miner
{
    public class Utils
    {
        private static readonly char[] HexArray = "0123456789ABCDEF".ToCharArray();

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


                if (stateOfConnection != TcpState.Closed && stateOfConnection != TcpState.CloseWait && stateOfConnection != TcpState.Closing && stateOfConnection != TcpState.Unknown)
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

        public static string GetStringBetween(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
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

        public static ClassMinerStats GetMinerStatsFromId(long id)
        {
            int counter = 0;
            foreach (var stats in NetworkBlockchain.ListMinerStats)
            {
                counter++;
                if (id == counter)
                {
                    return stats.Value;
                }
            }
            return null;
        }

        public static float GetMinerHashrateExpected()
        {
            float totalHashrateExpected = 0;
            foreach (var minerObjectHashrate in NetworkBlockchain.ListMinerStats)
            {
                if (minerObjectHashrate.Value.MinerConnectionStatus)
                {
                    totalHashrateExpected += float.Parse(minerObjectHashrate.Value.MinerHashrateExpected);
                }
            }
            return totalHashrateExpected;
        }

        public static float GetMinerHashrateCalculated()
        {
            float totalHashrateCalculated = 0;
            foreach (var minerObjectHashrate in NetworkBlockchain.ListMinerStats)
            {
                if (minerObjectHashrate.Value.MinerConnectionStatus)
                {
                    totalHashrateCalculated += float.Parse(minerObjectHashrate.Value.MinerHashrateCalculated);
                }
            }
            return totalHashrateCalculated;
        }

        public static int GetTotalBlockFound()
        {

            return NetworkBlockchain.TotalBlockUnlocked;
        }

        public static byte[] FromHexString(string @string)
        {
            if ((@string.Length & 1) != 0)
            {
                throw new Exception("Invalid hex string");
            }
            byte[] data = new byte[(@string.Length / 2)];
            for (int i = 0; i < @string.Length / 2; i++)
            {
                data[i] = (byte)((FromHexChar(@string[i * 2]) << 4) | FromHexChar(@string[(i * 2) + 1]));
            }
            return data;
        }

        public static string ToHexString(byte[] bytes)
        {
            char[] hexChars = new char[(bytes.Length * 2)];
            for (int j = 0; j < bytes.Length; j++)
            {
                int v = bytes[j] & 255;
                hexChars[j * 2] = HexArray[(int)((uint)v >> 4)];
                hexChars[(j * 2) + 1] = HexArray[v & 15];
            }
            return new string(hexChars);
        }

        public static int FromHexChar(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - 48;
            }
            if (c >= 'A' && c <= 'F')
            {
                return (c - 65) + 10;
            }
            if (c >= 'a' && c <= 'f')
            {
                return (c - 97) + 10;
            }
            throw new Exception("Invalid hex character");
        }
    }
}
