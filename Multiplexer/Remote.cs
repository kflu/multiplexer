using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexer
{
    class Remote : IDisposable
    {
        TcpClient client;
        NetworkStream stream;
        Action<byte[]> receive;
        readonly BlockingCollection<byte[]> uplinkQueue;
        CancellationTokenSource cts = new CancellationTokenSource();

        public Remote(
            TcpClient client,
            BlockingCollection<byte[]> uplinkQueue, 
            Action<byte[]> receive)
        {
            this.client = client;
            this.uplinkQueue = uplinkQueue;
            this.receive = receive;
            stream = client.GetStream();
        }

        async Task HandleDownlink()
        {
            cts.Token.ThrowIfCancellationRequested();
            int c;
            byte[] buffer = new byte[256];
            while ((c = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
            {
                receive(buffer.Take(c).ToArray());
            }
        }

        async Task HandleUplink()
        {
            cts.Token.ThrowIfCancellationRequested();
            byte[] data;
            while (null != (data = uplinkQueue.Take(cts.Token)))
            {
                await stream.WriteAsync(data, 0, data.Length, cts.Token);
            }
        }

        public async Task Start()
        {
            try
            {
                var downlinkTask = Task.Run(HandleDownlink, cts.Token);
                var uplinkTask = Task.Run(HandleUplink, cts.Token);

                await Task.WhenAny(downlinkTask, uplinkTask);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                cts.Cancel();
                Console.WriteLine("Remote connection exited.");
                this.Dispose();
            }
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing of remote connection");
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            if (client != null)
            {
                client.Close();
                client = null;
            }
        }
    }
}
