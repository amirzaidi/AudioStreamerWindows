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
        static TaskCompletionSource<bool> DisconnectWaiter;
        const int ServerPort = 1420;

        static void Main(string[] args)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            bool UseAdb = false;

            try
            {
                var AdbDevices = Process.Start(new ProcessStartInfo()
                {
                    FileName = "adb",
                    Arguments = "devices",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });

                AdbDevices.StandardOutput.ReadLine();
                UseAdb = AdbDevices.StandardOutput.ReadLine().Trim() != string.Empty;
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

            IPAddress IPAddr;
            if (UseAdb)
                IPAddr = IPAddress.Loopback;
            else
            {
                Console.Write("IP: ");
                IPAddr = IPAddress.Parse(Console.ReadLine());
            }

            var Remote = new IPEndPoint(IPAddr, ServerPort);
            using (Capture = new WasapiLoopbackCapture(0))
            {
                Capture.Initialize();
                using (Source = new SoundInSource(Capture))
                {
                    Source.DataAvailable += DataAvailable;
                    Capture.Start();

                    Console.WriteLine("Started recording audio");
                    while (true)
                    {
                        var NoSpamDelay = Task.Delay(1000);
                        if (UseAdb)
                            Process.Start(new ProcessStartInfo()
                            {
                                FileName = "adb",
                                Arguments = "forward tcp:1420 tcp:1420",
                                UseShellExecute = false
                            });

                        using (var Conn = new TcpClient()
                        {
                            NoDelay = true
                        })
                        {
                            try
                            {
                                Conn.Connect(Remote);
                                Stream = Conn.GetStream();
                                (DisconnectWaiter = new TaskCompletionSource<bool>()).Task.GetAwaiter().GetResult();
                            }
                            catch
                            {
                            }

                            Stream = null;
                        }

                        NoSpamDelay.GetAwaiter().GetResult();
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
