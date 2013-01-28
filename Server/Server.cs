using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using SDOSerializer;
using Model;

namespace Server
{
    class Server
    {
        static void Main(string[] args)
        {
            // create the socket
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // bind the listening socket to the port
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5656);
            listenSocket.Bind(endPoint);

            // start listening
            listenSocket.Listen(100);

            while (true)
            {
                // accept client
                Socket clientSock = listenSocket.Accept();
                // handle client in new thread
                Task.Factory.StartNew(() => HandleClient(clientSock));
            }
        }

        /// <summary>
        /// Handles socket client request - encrypts or decrypts text
        /// </summary>
        /// <param name="client">client to handle</param>
        private static void HandleClient(Socket client)
        {
            Console.WriteLine(String.Format("Client connected. Task: {0}", Task.CurrentId.HasValue ? Task.CurrentId.Value.ToString() : "-"));
            //recieve data
            byte[] data = new byte[1024 * 5000];
            int receivedBytesLen = client.Receive(data);
            byte[] receivedData = new byte[receivedBytesLen];
            Array.Copy(data, receivedData, receivedBytesLen);

            //deserialize data
            SDO sdoEncoder = new SDO();
            Message receivedMessage = (Message)sdoEncoder.Deserialize(receivedData, typeof(Message));

            Console.WriteLine(String.Format("Original message text: {0}. Task: {1}", receivedMessage.Text, Task.CurrentId.HasValue ? Task.CurrentId.Value.ToString() : "-"));

            //select crypto algorithm
            SymmetricAlgorithm cryptAlgorithm;
            switch (receivedMessage.Alg)
            {
                case "Rijndael": 
                    cryptAlgorithm = Rijndael.Create();
                    break;
                case "TripleDES": 
                    cryptAlgorithm = TripleDES.Create();
                    break;
                default: goto case "Rijndael";
            }

            //parse key and initialization vecor
            byte[] key = new byte[16];
            byte[] iv = new byte[16];
            byte[] receivedKey = Encoding.UTF8.GetBytes(receivedMessage.Key);
            byte[] receivedIV = Encoding.UTF8.GetBytes(receivedMessage.IV);
            Array.Copy(receivedKey, key, receivedKey.Length);
            Array.Copy(receivedIV, iv, receivedIV.Length);

            //encrypt or decrypt text
            string proccessedText;
            if (receivedMessage.Encrypt)
                proccessedText = Encrypt(receivedMessage.Text, key, iv, cryptAlgorithm);
            else
                proccessedText = Decrypt(receivedMessage.Text, key, iv, cryptAlgorithm);

            Console.WriteLine(String.Format("Proccessed message text: {0}. Task: {1}", proccessedText, Task.CurrentId.HasValue ? Task.CurrentId.Value.ToString() : "-"));

            //serialize and send proccessed text back to client
            byte[] serializedText = sdoEncoder.Serialize(proccessedText);
            client.Send(serializedText);

            //close connection
            client.Close();
            Console.WriteLine(String.Format("Client disconnected. Task: {0}", Task.CurrentId.HasValue ? Task.CurrentId.Value.ToString() : "-"));
        }

        /// <summary>
        /// Encrypts given text
        /// </summary>
        /// <param name="text">Text to encrypt</param>
        /// <param name="key">Key, used for encryption</param>
        /// <param name="iv">Initialization Vector</param>
        /// <param name="cryptoProvider">Cryptography algorithm</param>
        /// <returns>Encrypted text</returns>
        public static string Encrypt(string text, byte[] key, byte[] iv, SymmetricAlgorithm cryptoProvider)
        {
            //using (var cryptoProvider = Rijndael.Create())
            using (cryptoProvider)
            using (var memoryStream = new MemoryStream())
            using (var cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateEncryptor(key, iv), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream))
            {
                writer.Write(text);
                writer.Flush();
                cryptoStream.FlushFinalBlock();
                writer.Flush();
                return Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
        }

        /// <summary>
        /// Decrypts given text
        /// </summary>
        /// <param name="encryptedText">Text to decrypt</param>
        /// <param name="key">Key, used for decryption</param>
        /// <param name="iv">Initialization Vector</param>
        /// <param name="cryptoProvider">Cryptography algorithm</param>
        /// <returns>Decrypted text</returns>
        public static string Decrypt(string encryptedText, byte[] key, byte[] iv, SymmetricAlgorithm cryptoProvider)
        {
            //using (var cryptoProvider = Rijndael.Create())
            using (cryptoProvider)
            using (var memoryStream = new MemoryStream(Convert.FromBase64String(encryptedText)))
            using (var cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateDecryptor(key, iv), CryptoStreamMode.Read))
            using (var reader = new StreamReader(cryptoStream))
            {
                return reader.ReadToEnd();
            }
        }

    }
}
