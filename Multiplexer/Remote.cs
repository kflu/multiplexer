namespace Multiplexer
{
    using System;
    using System.IO;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Class to manage connection to the remote server
    /// </summary>
    class Remote : IDisposable
    {
        readonly Stream downlinkStream;
        readonly Stream uplinkStream;

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

        public Remote(
            Stream downlinkStream,
            Stream uplinkStream,
            BlockingCollection<byte[]> uplinkQueue,
            CancellationToken externalCancellationToken, 
            Action<byte[]> receive)
        {
            this.uplinkQueue = uplinkQueue;
            this.receive = receive;
            linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
            this.downlinkStream = downlinkStream;
            this.uplinkStream = uplinkStream;
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
            while (true)
            {
                if ((c = await downlinkStream.ReadAsync(buffer, 0, buffer.Length, linkedCTS.Token).ConfigureAwait(false)) > 0)
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
            System.Console.WriteLine("taking data from uplink queue");
            while (null != (data = uplinkQueue.Take(linkedCTS.Token)))
            {
                try
                {
                    Console.Error.WriteLine($"start writing data (len: {data.Length})");
                    await uplinkStream.WriteAsync(data, 0, data.Length, linkedCTS.Token).ConfigureAwait(false);
                    Console.Error.WriteLine($"Finished writing data (len: {data.Length})");
                    await uplinkStream.FlushAsync();  // flush is important to make user input work
                }
                catch (IOException e)
                {
                    Console.Error.WriteLine($"IO Exception ignored: {e}");
                }
                System.Console.WriteLine("taking data from uplink queue");
            }
            System.Console.WriteLine("uplink finished");
        }

        /// <summary>
        /// Async task to start and wait for the uplink and downlink handlers
        /// </summary>
        public async Task Start()
        {
            System.Console.WriteLine("Starting remote");
            try
            {
                var downlinkTask = Task.Run(HandleDownlink, linkedCTS.Token);
                var uplinkTask = Task.Run(HandleUplink, linkedCTS.Token);

                // If either task returns, the connection is considered to be terminated.
                await await Task.WhenAny(downlinkTask, uplinkTask).ConfigureAwait(false);
                System.Console.WriteLine("down/up link task returned");
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
            this.downlinkStream.Dispose();
            this.uplinkStream.Dispose();
        }
    }
}
