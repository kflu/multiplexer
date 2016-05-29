namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A class to handle local client connections (client - multiplexer)
    /// </summary>
    class Client : IDisposable
    {
        readonly TcpClient client;

        /// <summary>
        /// A delegate to invoke when receiving data from the client socket that should be uploaded.
        /// 
        /// Implementation should put this to the outbound queue. This should be non-blocking.
        /// </summary>
        readonly Action<byte[]> upload;
        readonly NetworkStream stream;

        /// <summary>
        /// A queue containing data from remote server that should be delivered to this client
        /// </summary>
        readonly BlockingCollection<byte[]> downlinkQueue = new BlockingCollection<byte[]>();

        /// <summary>
        /// A cancellation token source linked with an external token
        /// </summary>
        readonly CancellationTokenSource cts;

        public BlockingCollection<byte[]> DownlinkQueue
        {
            get
            {
                return this.downlinkQueue;
            }
        }

        /// <summary>
        /// A delegate to be called when the client is closed. The <see cref="ClientServer"/> uses this to 
        /// properly remove the client from the clients list.
        /// </summary>
        public Action OnClose { get; set; }

        public Client(TcpClient client, CancellationToken externalCancellationToken, Action<byte[]> upload)
        {
            this.client = client;
            this.upload = upload;
            this.cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            this.stream = client.GetStream();
        }

        /// <summary>
        /// Start the client traffic
        /// </summary>
        public async Task Start()
        {
            var uplinkTask = Task.Run(HandleUplink, cts.Token);
            var downlinkTask = Task.Run(HandleDownlink, cts.Token);

            try
            {
                // Await for either of the downlink or uplink task to finish
                await await Task.WhenAny(uplinkTask, downlinkTask).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                // Cancel the other task (uplink or downlink)
                cts.Cancel();
                Console.WriteLine("Client closing");
                Dispose();
            }
        }

        /// <summary>
        /// Handle uplink traffic (client -> multiplexer -> remote)
        /// </summary>
        async Task HandleUplink()
        {
            cts.Token.ThrowIfCancellationRequested();
            int c;
            byte[] buffer = new byte[256];
            while ((c = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
            {
                upload(buffer.Take(c).ToArray());
            }
        }

        /// <summary>
        /// Handle downlink traffic (remote -> multiplexer -> client)
        /// </summary>
        async Task HandleDownlink()
        {
            cts.Token.ThrowIfCancellationRequested();
            byte[] data;

            // This would block if the downlink queue is empty
            while (null != (data = downlinkQueue.Take(cts.Token)))
            {
                await stream.WriteAsync(data, 0, data.Length, cts.Token).ConfigureAwait(false);
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
