using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Model;
using SDOSerializer;

namespace Client
{
    class Client
    {
        

        static void Main(string[] args)
        {
            bool correctUserInput = false;
            string text = "";
            string fileName = "";
            while (!correctUserInput)
            {
                //get file name
                Console.WriteLine("Įveskite duomenų failo pavadinimą:");
                fileName = Console.ReadLine();
                //read file
                try
                {
                    FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
              
                    byte[] bytesRead = new byte[fs.Length];
                    int numBytesToRead = (int)fs.Length;
                    int numBytesRead = 0;
                    while (numBytesToRead > 0)
                    {
                        // read may return anything from 0 to numBytesToRead. 
                        int n = fs.Read(bytesRead, numBytesRead, numBytesToRead);

                        // break when the end of the file is reached. 
                        if (n == 0)
                            break;

                        numBytesRead += n;
                        numBytesToRead -= n;
                    }
                    text = Encoding.UTF8.GetString(bytesRead);
                    correctUserInput = true;
                }
                catch (Exception e) { Console.WriteLine(String.Format("Nepavyko nuskaityti failo: {0}. Bandykite dar kartą.", e.Message)); correctUserInput = false; };
            }
            
            //encrypt or decrypt
            correctUserInput = false;
            bool encrypt = false;
            while (!correctUserInput)
            {
                Console.WriteLine("Tekstas turi būti užšifruotas ar iššifruotas? \n 1 - Užšifruotas \n 2 - Iššifruotas");
                try
                {
                    int chosenAlg = int.Parse(Console.ReadLine());
                    switch (chosenAlg)
                    {
                        case 1: encrypt = true;
                            correctUserInput = true;
                            break;
                        case 2: encrypt = false;
                            correctUserInput = true;
                            break;
                    }
                }
                catch (Exception e) { Console.WriteLine("Neteisingai pasirinktas variantas. Bandykite dar kartą."); }
            }

            //choose Crypto algorithm
            correctUserInput = false;
            string algorithmName = "";
            while (!correctUserInput)
            {
                Console.WriteLine("Pasirinkite šifravimo algoritmą: \n 1 - Rijndael \n 2 - TripleDES");
                try
                {
                    int chosenAlg = int.Parse(Console.ReadLine());
                    switch (chosenAlg)
                    {
                        case 1: algorithmName = "Rijndael";
                            correctUserInput = true;
                            break;
                        case 2: algorithmName = "TripleDES";
                            correctUserInput = true;
                            break;
                    }
                }
                catch (Exception e){ Console.WriteLine("Neteisingai pasirinktas šiframvimo algoritmas. Bandykite dar kartą."); }
            }


            //get Key
            correctUserInput = false;
            string key = "";
            while(!correctUserInput)
            {
                Console.WriteLine("Įveskite šifravmimo raktą:");
                key = Console.ReadLine();
                if (key.Length <= 16)
                    correctUserInput = true;
                else
                    Console.WriteLine("Raktas negali būti ilgesnis nei 16 simbolių. Bandykite dar kartą.");
            }

            //get initializacion vector
            string iv;
            if (encrypt)
            {
                iv = RandomString(5);
                Console.WriteLine(String.Format("Jums sugeneruotas inicializacijos vektorius:{0}", iv));
            }
            else
            {
                Console.WriteLine("Įveskite inicializacijos vektorių:");
                iv = Console.ReadLine();
            }

            Message msg = new Message() { Alg = algorithmName, IV = iv, Key = key, Text = text, Encrypt = encrypt };

            SDO sdoEncoder = new SDO();
            byte[] serializedMessage = sdoEncoder.Serialize(msg);
           
            //connect to server
            IPAddress ipAddress = IPAddress.Loopback;//Dns.GetHostAddresses("localhost")[0];
            IPEndPoint ipEnd = new IPEndPoint(ipAddress, 5656);
            Socket clientSock = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.IP);
            clientSock.Connect(ipEnd);

            //send serialized message
            clientSock.Send(serializedMessage);

            //get response
            byte[] data = new byte[1024 * 5000];
            int receivedBytesLen = clientSock.Receive(data);
            byte[] receivedData = new byte[receivedBytesLen];
            Array.Copy(data, receivedData, receivedBytesLen);
            //close connection
            clientSock.Close();
            
            string receivedText = (string)sdoEncoder.Deserialize(receivedData, typeof(string));

            //save encrypted/decrypted info to file
            fileName = fileName.Substring(0,fileName.IndexOf('.')) + "Response.txt";
            File.WriteAllText(fileName, receivedText);

            Console.WriteLine(String.Format("Užšifruotas/iššifruotas tekstas buvo išsaugotas į failą: {0}", fileName));
            Console.ReadKey();
        }


        /// <summary>
        /// Generate random string of given length
        /// </summary>
        /// <param name="length">length of generated string</param>
        /// <returns>Random string</returns>
        private static string RandomString(int length)
        {
            Random random = new Random((int)DateTime.Now.Ticks);
            StringBuilder builder = new StringBuilder();
            char ch;
            for (int i = 0; i < length; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
