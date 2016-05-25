using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexer
{
    class Client : IDisposable
    {
        TcpClient client;
        Action<byte[]> upload;
        NetworkStream stream;
        BlockingCollection<byte[]> downlinkQueue = new BlockingCollection<byte[]>();

        public BlockingCollection<byte[]> DownlinkQueue
        {
            get
            {
                return this.downlinkQueue;
            }
        }

        readonly CancellationTokenSource cts = new CancellationTokenSource();
        public Action OnClose { get; set; }

        public Client(TcpClient client, Action<byte[]> upload)
        {
            this.client = client;
            this.upload = upload;
            this.stream = client.GetStream();
        }

        public async Task Start()
        {
            var uplinkTask = Task.Run(HandleUplink, cts.Token);
            var downlinkTask = Task.Run(HandleDownlink, cts.Token);

            try
            {
                await Task.WhenAny(uplinkTask, downlinkTask);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                cts.Cancel();
                Console.WriteLine("Client closing");
                Dispose();
            }
        }

        async Task HandleUplink()
        {
            cts.Token.ThrowIfCancellationRequested();
            int c;
            byte[] buffer = new byte[256];
            while ((c = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
            {
                upload(buffer.Take(c).ToArray());
            }
        }

        async Task HandleDownlink()
        {
            cts.Token.ThrowIfCancellationRequested();
            byte[] data;

            // TODO: BlockingCollection.Take blocks and there need a way 
            // to cancel it.
            while (null != (data = downlinkQueue.Take(cts.Token)))
            {
                await stream.WriteAsync(data, 0, data.Length, cts.Token);
            }
        }

        public override string ToString()
        {
            return client.Client.RemoteEndPoint.ToString();
        }

        public void Dispose()
        {
            Console.WriteLine($"Disposing of client: {this}");
            OnClose();
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
