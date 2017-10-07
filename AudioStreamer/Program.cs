using CSCore.SoundIn;
using CSCore.Streams;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
            Task.Run(MainAsync);
            Window.Init();
        }

        static async Task MainAsync()
        {
            Console.Title = "Audio Streamer - PC to Android";

            IPAddress IPAddr;
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

                await AdbDevices.StandardOutput.ReadLineAsync();
                UseAdb = !string.IsNullOrWhiteSpace(await AdbDevices.StandardOutput.ReadLineAsync());
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

            if (UseAdb)
                IPAddr = IPAddress.Loopback;
            else
            {
                Console.Write("IP: ");
                IPAddr = IPAddress.Parse(Console.ReadLine());
            }

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            using (Capture = new WasapiLoopbackCapture(0, new CSCore.WaveFormat(), ThreadPriority.Highest))
            {
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
                        NoDelay = true,
                        ReceiveBufferSize = 64,
                        SendBufferSize = 1 << 12    //2^12 = ~4000 so 1000 floats
                    })
                    {
                        try
                        {
                            await Conn.ConnectAsync(IPAddr, ServerPort);
                            Stream = Conn.GetStream();
                            if (Stream.ReadByte() == 1)
                            {
                                Console.WriteLine("Connected to " + IPAddr.ToString());
                                Capture.Initialize();
                                using (Source = new SoundInSource(Capture))
                                {
                                    int SampleRateServer = Source.WaveFormat.SampleRate;
                                    int SampleRateClient = Stream.ReadByte() | Stream.ReadByte() << 8 | Stream.ReadByte() << 16;
                                    if (SampleRateClient != SampleRateServer)
                                    {
                                        Console.WriteLine($"Sample rate mismatch, PC was {SampleRateServer} Hz but client was {SampleRateClient} Hz");
                                        Console.WriteLine("Adjust your PC's sample rate then press any key to try again");
                                        Console.ReadKey();
                                        Console.Clear();
                                    }
                                    else
                                    {
                                        // Start Capturing
                                        Source.DataAvailable += DataAvailable;
                                        Capture.Start();

                                        Console.WriteLine($"Started recording audio at {SampleRateServer} Hz");
                                        Window.SetWindowShown(false);

                                        // Stop Capturing
                                        await (DisconnectWaiter = new TaskCompletionSource<bool>()).Task;
                                        await Task.Run(() => Capture.Stop());

                                        Window.SetWindowShown(true);
                                        Console.WriteLine("Disconnected, stopped recording audio");
                                    }
                                }
                            }
                        }
                        catch { }
                        await NoSpamDelay;
                    }
                }
            }
        }

        static async void DataAvailable(object s, DataAvailableEventArgs e)
        {
            // Big endian to little endian
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
                await Stream.WriteAsync(Bytes, 0, e.ByteCount);
            }
            catch (Exception)
            {
                DisconnectWaiter?.TrySetResult(false);
            }
        }
    }
}
