using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Xiropht_Proxy_Solo_Miner
{
    public class ClassAlgoErrorEnumeration
    {
        public const string AlgoError = "WRONG";
    }

    public class ClassAlgoEnumeration
    {
        public const string Rijndael = "RIJNDAEL"; // 0
        public const string Xor = "XOR"; // 1
        public const string Aes = "AES"; // 2

    }

    public class ClassAlgo
    {
        /// <summary>
        /// Decrypt the result received and retrieve it.
        /// </summary>
        /// <param name="idAlgo"></param>
        /// <param name="result"></param>
        /// <param name="key"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string GetDecryptedResult(string idAlgo, string result, string key, int size)
        {
            try
            {
                switch (idAlgo)
                {
                    case ClassAlgoEnumeration.Rijndael:
                        return Rijndael.DecryptString(result, key, size);
                    case ClassAlgoEnumeration.Xor:
                        return Xor.EncryptString(result, key);
                    case ClassAlgoEnumeration.Aes:
                        return AesCrypt.DecryptString(result, key);

                }
            }
            catch (Exception erreur)
            {
                Console.WriteLine("Error decrypt: " + erreur.Message);
            }
            return "WRONG";
        }

        /// <summary>
        /// Encrypt the result received and retrieve it.
        /// </summary>
        /// <param name="idAlgo"></param>
        /// <param name="result"></param>
        /// <param name="key"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string GetEncryptedResult(string idAlgo, string result, string key, int size, byte[] keyByte)
        {
            try
            {
                switch (idAlgo)
                {
                    case ClassAlgoEnumeration.Rijndael:
                        return Rijndael.EncryptString(result, key, size);

                    case ClassAlgoEnumeration.Xor:
                        return Xor.EncryptString(result, key);

                    case ClassAlgoEnumeration.Aes:
                        return AesCrypt.EncryptString(result, key, keyByte);
                }
            }
            catch (Exception)
            {
            }
            return "WRONG";
        }

        /// <summary>
        /// Return an algo name from id.
        /// </summary>
        /// <param name="idAlgo"></param>
        /// <returns></returns>
        public static string GetNameAlgoFromId(int idAlgo)
        {
            switch (idAlgo)
            {
                case 0:
                    return ClassAlgoEnumeration.Rijndael;
                case 1:
                    return ClassAlgoEnumeration.Xor;
                case 2:
                    return ClassAlgoEnumeration.Aes;
            }

            return "NONE";
        }
    }

    public static class AesCrypt
    {
        public static string EncryptString(string text, string keyCrypt, byte[] keyByte)
        {
            var textByte = Encoding.ASCII.GetBytes(text);
            PasswordDeriveBytes pdb =
              new PasswordDeriveBytes(keyCrypt, keyByte); // Change this
            MemoryStream ms = new MemoryStream();
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.Key = pdb.GetBytes(aes.KeySize / 8);
            aes.IV = pdb.GetBytes(aes.BlockSize / 8);
            CryptoStream cs = new CryptoStream(ms,
              aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(textByte, 0, textByte.Length);
            cs.Close();
            return BitConverter.ToString(ms.ToArray());
        }

        public static string DecryptString(string text, string key)
        {
            var textByte = Encoding.ASCII.GetBytes(text);
            PasswordDeriveBytes pdb =
                new PasswordDeriveBytes(key, // Change this
                new byte[] { 0x43, 0x87, 0x23, 0x72 }); // Change this
            MemoryStream ms = new MemoryStream();
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.Key = pdb.GetBytes(aes.KeySize / 8);
            aes.IV = pdb.GetBytes(aes.BlockSize / 8);
            CryptoStream cs = new CryptoStream(ms,
              aes.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(textByte, 0, textByte.Length);
            cs.Close();
            return BitConverter.ToString(ms.ToArray());
        }



    }

    public static class Xor
    {
        public static string EncryptString(string text, string key)
        {
            var result = new StringBuilder();

            for (int c = 0; c < text.Length; c++)
                result.Append((char)((uint)text[c] ^ (uint)key[c % key.Length]));
            return result.ToString();
        }
    }

    public static class Rijndael
    {

        private const string InitVector = "HR$2pIjHR$2pIj12";

        /// <summary>
        /// Encrypt string from Rijndael.
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="keysize"></param>
        /// <returns></returns>
        public static string EncryptString(string plainText, string passPhrase, int keysize)
        {
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(InitVector);
            byte[] plainTextBytes = Encoding.ASCII.GetBytes(plainText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }

        /// <summary>
        /// Decrypt string with Rijndael.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="passPhrase"></param>
        /// <param name="keysize"></param>
        /// <returns></returns>
        public static string DecryptString(string cipherText, string passPhrase, int keysize)
        {
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(InitVector);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream(cipherTextBytes);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];
            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();
            return Encoding.ASCII.GetString(plainTextBytes, 0, decryptedByteCount);
        }
    }
}
