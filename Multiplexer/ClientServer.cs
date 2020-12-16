namespace Multiplexer
{
    using System;
    using static Logger;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    /// <summary>
    /// Listens to connection requests and manages client connections
    /// </summary>
    class ClientServer
    {
        /// <summary>
        /// Port to listen on for clients connections
        /// </summary>
        readonly int port;
        Global glob;

        public ClientServer(
            int port, 
            Global glob)
        {
            this.port = port;
            this.glob = glob;
        }

        public event Action OnConnected;

        /// <summary>
        /// Continuously listening for client connection requests
        /// </summary>
        public async Task Run()
        {
            var localserver = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            localserver.Start();

            while (true)
            {
                Log($"Waiting for clients to connect at {localserver.LocalEndpoint}...");

                // AcceptTcpClientAsync() does not accept a cancellation token. But it's OK since
                // in no case would I want the client listener loop to stop running during the entire
                // multiplexer lifecycle. For the sake of completeness, if it is necessary to cancel
                // this operation, one could use CancellationToken.Register(localserver.Stop).
                // See: http://stackoverflow.com/a/30856169/695964
                var client = await localserver.AcceptTcpClientAsync().ConfigureAwait(false);

                var clientWrapper = new Client(client, glob.CancellationToken, Upload);
                Log($"Client connected: {clientWrapper}");

                // Register client
                glob.Clients[clientWrapper] = 0;

                // Unregister client when it is terminating
                clientWrapper.OnClose = () =>
                {
                    Log($"Removing client from clients list: {clientWrapper}");
                    byte c;
                    glob.Clients.TryRemove(clientWrapper, out c);
                };

                OnConnected?.Invoke();

                // Start the client. This is fire-and-forget. We don't want to await on it. I
                // t's OK because Start() has necessary logic to handle client termination and disposal.
                var tsk = clientWrapper.Start();
            }
        }

        /// <summary>
        /// Implementation of upload delegate to be called when there's data to upload to remote server
        /// </summary>
        /// <param name="data">the outbound data</param>
        void Upload(byte[] data)
        {
            if (!glob.UploadQueue.TryAdd(data))
            {
                Log($"Cannot add {data.Length}B to upload queue");
            }
        }
    }
}
