using CSCore.SoundIn;
using CSCore.Streams;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AudioStreamer
{
    class Program
    {
        static WasapiLoopbackCapture Capture;
        static SoundInSource Source;
        static NetworkStream Stream;
        static byte[] Bytes = new byte[1024 * 1024];
        static TcpClient Conn = new TcpClient()
        {
            NoDelay = true
        };

        static void Main(string[] args)
        {
            using (Capture = new WasapiLoopbackCapture(0))
            {
                Capture.Initialize();
                using (Source = new SoundInSource(Capture))
                {
                    Source.DataAvailable += DataAvailable;

                    Console.Write("IP? Empty for ADB: ");

                    IPAddress IPAddr;
                    var IP = Console.ReadLine();
                    if (IP == string.Empty)
                    {
                        IPAddr = IPAddress.Loopback;
                        Process.Start("adb", "forward tcp:1420 tcp:1420");
                    }
                    else
                        IPAddr = IPAddress.Parse(IP);

                    using (Conn)
                    {
                        Conn.Connect(new IPEndPoint(IPAddr, 1420));
                        Stream = Conn.GetStream();

                        Capture.Start();
                        Console.WriteLine("Started recording, press enter to exit");
                        Console.ReadLine();
                        Capture.Stop();
                    }
                }
            }
        }

        static async void DataAvailable(object s, DataAvailableEventArgs e)
        {
            if (e.ByteCount > 0)
            {
                Parallel.For(0, e.ByteCount / 4, j =>
                {
                    int i = j * 4;
                    Bytes[i + 3] = e.Data[i];
                    Bytes[i + 2] = e.Data[i + 1];
                    Bytes[i + 1] = e.Data[i + 2];
                    Bytes[i] = e.Data[i + 3];
                });
                
                await Stream.WriteAsync(Bytes, 0, e.ByteCount);
            }
        }
    }
}
