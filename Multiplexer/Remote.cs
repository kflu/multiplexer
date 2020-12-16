namespace Multiplexer
{
    using System;
    using static Logger;
    using System.IO;
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
    }

    interface IRemote : IRemoteInfo, IDisposable
    {
        Task Start();
    }

    /// <summary>
    /// Class to manage connection to the remote server
    /// </summary>
    class Remote : IRemote
    {
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

        readonly string uplinkFifo;
        readonly string downlinkFifo;
        readonly ManualResetEvent uplinkReopenRequested = new ManualResetEvent(false);
        readonly ManualResetEvent downlinkReopenRequested = new ManualResetEvent(false);
        readonly ManualResetEvent downlinkReady = new ManualResetEvent(false);

        Stream uplink;
        Stream downlink;

        public bool Connected => uplink != null;

        public Remote(
            string uplinkFifo,
            string downlinkFifo,
            BlockingCollection<byte[]> uplinkQueue,
            CancellationToken externalCancellationToken, 
            Action<byte[]> receive)
        {
            this.uplinkFifo = uplinkFifo;
            this.downlinkFifo = downlinkFifo;
            this.uplinkQueue = uplinkQueue;
            this.receive = receive;
            linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        }

        Task ReopenUplink()
        {
            while (true)
            {
                try
                {
                    Log($"DD waiting uplink reopen req");
                    this.uplinkReopenRequested.WaitOne();
                    Log($"DD reopening uplink");
                    this.uplink = File.OpenWrite(this.uplinkFifo);
                    Log($"DD uplink reopened");
                    this.uplinkReopenRequested.Reset();
                }
                catch (Exception e)
                {
                    Log($"Error reopening uplink: {e}");
                }
            }
        }

        Task ReopenDownlink()
        {
            while (true)
            {
                try
                {
                    this.downlinkReopenRequested.WaitOne();
                    this.downlink = File.OpenRead(this.downlinkFifo);
                    this.downlinkReopenRequested.Reset();
                    this.downlinkReady.Set();
                }
                catch (Exception e)
                {
                    Log($"Error reopening downlink: {e}");
                }
            }
        }

        /// <summary>
        /// Async task to handle downlink (remote -> multiplexer) traffic
        /// 
        /// This is to read from the socket and put data into the downlink queue (via receive())
        /// </summary>
        async Task HandleDownlink()
        {
            linkedCTS.Token.ThrowIfCancellationRequested();
            byte[] buffer = new byte[256];
            while (true)
            {
                this.downlinkReady.WaitOne();
                var cachedDownlink = this.downlink;
                int c = 0;
                try
                {
                    c = await cachedDownlink.ReadAsync(buffer, 0, buffer.Length, linkedCTS.Token).ConfigureAwait(false);
                }
                catch (IOException e)
                {
                    Log($"Error reading from downlink: {e}");
                    this.downlink = null;
                    this.downlinkReopenRequested.Set();
                    this.downlinkReady.Reset();

                    var t = Task.Run(() =>
                    {
                        try { cachedDownlink.Dispose(); }
                        catch (IOException ex) { Log($"Error disposing downlink: {ex}"); }
                    });
                }

                // Receive is non-blocking
                if (c != 0) 
                {
                    receive(buffer.Take(c).ToArray());
                }
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

            // Taking from the queue can be blocked if there's nothing in the queue for consumption
            while (true)
            {
                byte[] data = uplinkQueue.Take(linkedCTS.Token);
                Log($"Took {data.Length}B from uplink queue");
                var cachedUplink = this.uplink;
                if (cachedUplink == null)
                {
                    Log($"Uplink not ready, discarding {data.Length}B");
                }
                else
                {
                    try
                    {
                        await cachedUplink.WriteAsync(data, 0, data.Length, linkedCTS.Token).ConfigureAwait(false);
                        await cachedUplink.FlushAsync();
                    }
                    catch (IOException e)
                    {
                        Log($"Error writing to uplink: {e}");
                        this.uplink = null;
                        this.uplinkReopenRequested.Set();

                        var t = Task.Run(() => {
                            try
                            {
                                cachedUplink.Dispose();
                            }
                            catch (IOException ex)
                            {
                                Log($"Error disposing uplink: {ex}");
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Async task to start and wait for the uplink and downlink handlers
        /// </summary>
        public async Task Start()
        {
            try
            {
                var reopenDownlinkTask = Task.Run(this.ReopenDownlink, linkedCTS.Token);
                var reopenUplinkTask = Task.Run(this.ReopenUplink, linkedCTS.Token);
                var downlinkTask = Task.Run(HandleDownlink, linkedCTS.Token);
                var uplinkTask = Task.Run(HandleUplink, linkedCTS.Token);

                this.downlinkReopenRequested.Set();
                this.uplinkReopenRequested.Set();

                // If either task returns, the connection is considered to be terminated.
                await await Task
                    .WhenAny(reopenDownlinkTask, reopenUplinkTask, downlinkTask, uplinkTask)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            finally
            {
                // Cancel the other task (uplink or downlink)
                linkedCTS.Cancel();
                Log("Remote connection exited.");
                this.Dispose();
            }
        }

        public void Dispose()
        {
            Log("Disposing of remote connection");
            linkedCTS.Dispose();
        }
    }
}
