namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRemoteInfo
    {
        bool Connected { get; }
        string RemoteAddress { get; }
    }

    class Remote : IDisposable, IRemoteInfo
    {
        readonly TcpClient client;
        readonly NetworkStream stream;
        readonly Action<byte[]> receive;
        readonly BlockingCollection<byte[]> uplinkQueue;
        readonly CancellationToken ct;
        readonly CancellationTokenSource linkedCTS;

        public bool Connected => client.Connected;
        public string RemoteAddress => $"{client.Client.RemoteEndPoint}";

        public Remote(
            TcpClient client,
            BlockingCollection<byte[]> uplinkQueue,
            CancellationToken externalCancellationToken, 
            Action<byte[]> receive)
        {
            this.client = client;
            this.uplinkQueue = uplinkQueue;
            this.receive = receive;

            /*
             * Create a linked cancellation token that is cancelled when:
             * - external cancellation is requested, or
             * - the token of the linked CTS is cancelled
             *
             * Note that the cancellation of the linked source won't propagate to the external token
             */
            linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            ct = linkedCTS.Token;

            stream = client.GetStream();
        }

        async Task HandleDownlink()
        {
            ct.ThrowIfCancellationRequested();
            int c;
            byte[] buffer = new byte[256];
            while ((c = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                receive(buffer.Take(c).ToArray());
            }
        }

        async Task HandleUplink()
        {
            ct.ThrowIfCancellationRequested();
            byte[] data;
            while (null != (data = uplinkQueue.Take(ct)))
            {
                await stream.WriteAsync(data, 0, data.Length, ct);
            }
        }

        public async Task Start()
        {
            try
            {
                var downlinkTask = Task.Run(HandleDownlink, ct);
                var uplinkTask = Task.Run(HandleUplink, ct);

                await await Task.WhenAny(downlinkTask, uplinkTask);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                linkedCTS.Cancel();
                Console.WriteLine("Remote connection exited.");
                this.Dispose();
            }
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing of remote connection");
            linkedCTS.Dispose();
            stream.Dispose();
            client.Close();
        }
    }
}
