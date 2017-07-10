using CSCore.SoundIn;
using CSCore.Streams;
using System;
using System.Diagnostics;
using System.IO;
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
        static IPEndPoint End;
        static TaskCompletionSource<bool> DisconnectWaiter;

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

                    End = new IPEndPoint(IPAddr, 1420);
                    Capture.Start();

                    Console.WriteLine("Booted");
                    while (true)
                    {
                        using (var Conn = new TcpClient()
                        {
                            NoDelay = true
                        })
                        {
                            try
                            {
                                Conn.Connect(End);
                                Stream = Conn.GetStream();
                                (DisconnectWaiter = new TaskCompletionSource<bool>()).Task.GetAwaiter().GetResult();
                            }
                            catch (Exception Ex)
                            {
                                Console.WriteLine(Ex);
                            }

                            Stream = null;
                        }
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

                try
                {
                    await Stream?.WriteAsync(Bytes, 0, e.ByteCount);
                }
                catch (Exception)
                {
                    DisconnectWaiter?.TrySetResult(false);
                }
            }
        }
    }
}
