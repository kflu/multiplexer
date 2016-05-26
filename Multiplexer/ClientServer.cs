namespace Multiplexer
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Listens to connection requests and manages client connections
    /// </summary>
    class ClientServer
    {
        readonly int port;
        Global glob;

        public ClientServer(
            int port, 
            Global glob)
        {
            this.port = port;
            this.glob = glob;
        }

        public void Run()
        {
            var localserver = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            localserver.Start();

            while (true)
            {
                Console.WriteLine("Waiting for clients to connect...");
                var client = localserver.AcceptTcpClient();

                var clientWrapper = new Client(client, glob.CancellationToken, Upload);
                Console.WriteLine($"Client connected: {clientWrapper}");
                glob.Clients[clientWrapper] = 0;
                clientWrapper.OnClose = () =>
                {
                    Console.WriteLine($"Removing client from clients list: {clientWrapper}");
                    byte c;
                    glob.Clients.TryRemove(clientWrapper, out c);
                };

                var tsk = clientWrapper.Start();
            }
        }

        void Upload(byte[] data)
        {
            // Do not enqueue data if remote is not connected
            if (glob.Remote.Connected)
            {
                glob.UploadQueue.TryAdd(data);
            }
        }
    }
}
