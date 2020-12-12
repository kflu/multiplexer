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
        public event Func<byte[], Task> OnOutgoingDataAvailable;

        readonly TcpClient client;

        readonly NetworkStream stream;

        /// <summary>
        /// A cancellation token source linked with an external token
        /// </summary>
        readonly CancellationTokenSource cts;

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
                throw;
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

            System.Console.WriteLine("reading for uplink data..");
            while ((c = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
            {
                upload(buffer.Take(c).ToArray());
                System.Console.WriteLine("reading for uplink data..");
            }

            System.Console.WriteLine("no more uplink data");
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
                await stream.FlushAsync();
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
