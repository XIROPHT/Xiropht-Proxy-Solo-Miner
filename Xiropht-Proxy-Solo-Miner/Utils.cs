using System;
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
                        return !(socket.Client.Poll(100, SelectMode.SelectRead) && socket.Available == 0);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return false;
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
