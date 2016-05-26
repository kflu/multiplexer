namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    class Client : IDisposable
    {
        readonly TcpClient client;
        readonly Action<byte[]> upload;
        readonly NetworkStream stream;
        readonly BlockingCollection<byte[]> downlinkQueue = new BlockingCollection<byte[]>();
        readonly CancellationTokenSource cts;

        public BlockingCollection<byte[]> DownlinkQueue
        {
            get
            {
                return this.downlinkQueue;
            }
        }

        public Action OnClose { get; set; }

        public Client(TcpClient client, CancellationToken externalCancellationToken, Action<byte[]> upload)
        {
            this.client = client;
            this.upload = upload;
            this.cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            this.stream = client.GetStream();
        }

        public async Task Start()
        {
            var uplinkTask = Task.Run(HandleUplink, cts.Token);
            var downlinkTask = Task.Run(HandleDownlink, cts.Token);

            try
            {
                await await Task.WhenAny(uplinkTask, downlinkTask);
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
            cts.Dispose();
            stream.Dispose();
            client.Close();
        }
    }
}
