using NAudio.Wave;
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
        static TcpClient Conn;
        static NetworkStream Stream;

        static void Main(string[] args)
        {
            Capture = new WasapiLoopbackCapture();
            Capture.DataAvailable += DataAvailable;
            Capture.RecordingStopped += RecordingStopped;
            
            Conn = new TcpClient();
            Conn.NoDelay = true;
            Console.Write("IP? Empty for ADB: ");

            IPAddress IPAddr;
            var IP = Console.ReadLine();
            if (IP == string.Empty)
            {
                IPAddr = IPAddress.Loopback;
                Process.Start("adb", "forward tcp:1420 tcp:1420");
            }
            else
            {
                IPAddr = IPAddress.Parse(IP);
            }

            Conn.ConnectAsync(IPAddr, 1420).GetAwaiter().GetResult();
            Stream = Conn.GetStream();

            Capture.StartRecording();
            Console.WriteLine("Started recording, press enter to exit");
            Console.ReadLine();
            Capture.StopRecording();
        }

        static async void DataAvailable(object s, WaveInEventArgs e)
        {
            try
            {
                if (e.BytesRecorded > 0)
                {                   
                    Parallel.For(0, e.BytesRecorded / 4, i =>
                    {
                        i *= 4;
                        byte TempByte = e.Buffer[i];
                        e.Buffer[i] = e.Buffer[i + 3];
                        e.Buffer[i + 3] = TempByte;
                        TempByte = e.Buffer[i + 1];
                        e.Buffer[i + 1] = e.Buffer[i + 2];
                        e.Buffer[i + 2] = TempByte;
                    });

                    await Stream.WriteAsync(e.Buffer, 0, e.BytesRecorded);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex);
                Capture.StopRecording();
            }
        }

        static void RecordingStopped(object s, StoppedEventArgs e)
        {
            Dispose(ref Stream);
            Dispose(ref Conn);
            Dispose(ref Capture);
            Environment.Exit(0);
        }

        static void Dispose<T>(ref T Disposable) where T : IDisposable
        {
            if (Disposable != null)
            {
                Disposable.Dispose();
                Disposable = default(T);
            }
        }
    }
}
