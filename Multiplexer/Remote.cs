namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An interface to expose read-only remote connection information
    /// </summary>
    public interface IRemoteInfo
    {
        /// <summary>
        /// Whether connected to the remote service
        /// </summary>
        bool Connected { get; }

        /// <summary>
        /// Remote address
        /// </summary>
        string RemoteAddress { get; }
    }

    /// <summary>
    /// Class to manage connection to the remote server
    /// </summary>
    class Remote : IDisposable, IRemoteInfo
    {
        readonly TcpClient client;
        readonly NetworkStream stream;

        /// <summary>
        /// A delegate called on receiving a package. Implementation could be submitting the package to the queue.
        /// </summary>
        readonly Action<byte[]> receive;

        /// <summary>
        /// Queue for data to be uploaded to remote server
        /// </summary>
        readonly BlockingCollection<byte[]> uplinkQueue;

        /// <summary>
        /// a linked cancellation token that is cancelled when:
        ///   - external cancellation is requested, or
        ///   - the token of the linked CTS is cancelled
        /// Note that the cancellation of the linked source won't propagate to the external token
        /// </summary>
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
            linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            stream = client.GetStream();
        }

        /// <summary>
        /// Async task to handle downlink (remote -> multiplexer) traffic
        /// 
        /// This is to read from the socket and put data into the downlink queue (via receive())
        /// </summary>
        async Task HandleDownlink()
        {
            linkedCTS.Token.ThrowIfCancellationRequested();
            int c;
            byte[] buffer = new byte[256];
            while ((c = await stream.ReadAsync(buffer, 0, buffer.Length, linkedCTS.Token)) > 0)
            {
                // Receive is non-blocking
                receive(buffer.Take(c).ToArray());
            }
        }

        /// <summary>
        /// Async task to handle uplink (multiplexer -> remote) traffic
        /// 
        /// This is to take data from the uplink queue and write into the socket.
        /// </summary>
        async Task HandleUplink()
        {
            linkedCTS.Token.ThrowIfCancellationRequested();
            byte[] data;

            // Taking from the queue can be blocked if there's nothing in the queue for consumption
            while (null != (data = uplinkQueue.Take(linkedCTS.Token)))
            {
                await stream.WriteAsync(data, 0, data.Length, linkedCTS.Token);
            }
        }

        /// <summary>
        /// Async task to start and wait for the uplink and downlink handlers
        /// </summary>
        public async Task Start()
        {
            try
            {
                var downlinkTask = Task.Run(HandleDownlink, linkedCTS.Token);
                var uplinkTask = Task.Run(HandleUplink, linkedCTS.Token);

                // If either task returns, the connection is considered to be terminated.
                await await Task.WhenAny(downlinkTask, uplinkTask);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                // Cancel the other task (uplink or downlink)
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
