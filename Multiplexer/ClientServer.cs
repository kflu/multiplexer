namespace Multiplexer
{
    using System;
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

        /// <summary>
        /// Continuously listening for client connection requests
        /// </summary>
        public async Task Run()
        {
            var localserver = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            localserver.Start();

            while (true)
            {
                Console.WriteLine("Waiting for clients to connect...");
                var client = await localserver.AcceptTcpClientAsync();

                var clientWrapper = new Client(client, glob.CancellationToken, Upload);
                Console.WriteLine($"Client connected: {clientWrapper}");

                // Register client
                glob.Clients[clientWrapper] = 0;

                // Unregister client when it is terminating
                clientWrapper.OnClose = () =>
                {
                    Console.WriteLine($"Removing client from clients list: {clientWrapper}");
                    byte c;
                    glob.Clients.TryRemove(clientWrapper, out c);
                };

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
            // Do not enqueue data if remote is not connected (drop it)
            if (glob.Remote.Connected)
            {
                glob.UploadQueue.TryAdd(data);
            }
        }
    }
}
