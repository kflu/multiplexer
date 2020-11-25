namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    /// A class to hold common dependencies to other classes
    /// </summary>
    /// <remarks>
    /// This used to be a singleton and referenced directly by other code, hence the name "Global".
    /// I changed it to be dependencies passed as constructor parameters of other classes so it's 
    /// easier to write tests.
    /// </remarks>
    class Global
    {
        /// <summary>
        /// Queue for data to be uploaded to remote server (client -> remote)
        /// </summary>
        public BlockingCollection<byte[]> UploadQueue => uploadQueue;

        /// <summary>
        /// Set of connected clients. This is used as a set (only keys are used), but there's no ConcurrentSet.
        /// </summary>
        public ConcurrentDictionary<Client, byte> Clients => clients;

        /// <summary>
        /// A cancellation token for disconnection. This is used to cancel the remote connection and client connections.
        /// </summary>
        public CancellationToken CancellationToken => cts.Token;

        private BlockingCollection<byte[]> uploadQueue = new BlockingCollection<byte[]>();
        private ConcurrentDictionary<Client, byte> clients = new ConcurrentDictionary<Client, byte>();
        private CancellationTokenSource cts = new CancellationTokenSource();

        public Global(Configuration config)
        {
        }

        /// <summary>
        /// Cancel the global cancellation token. Used for disconnecting remote and clients
        /// </summary>
        public void Cancel()
        {
            if (!cts.Token.IsCancellationRequested)
            {
                lock (cts)
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        cts.Cancel();
                        cts.Dispose();
                        cts = new CancellationTokenSource();
                    }
                }
            }
        }
    }
}
