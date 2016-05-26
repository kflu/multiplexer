using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplexer
{
    /// <summary>
    /// Listens to connection requests and manages client connections
    /// </summary>
    class ClientServer
    {
        readonly ConcurrentDictionary<Client, byte> clients;
        public ConcurrentDictionary<Client, byte> Clients => clients;
        readonly int port;
        readonly BlockingCollection<byte[]> uploadQueue;

        public ClientServer(
            int port, 
            BlockingCollection<byte[]> uploadQueue,
            ConcurrentDictionary<Client, byte> clients)
        {
            this.port = port;
            this.uploadQueue = uploadQueue;
            this.clients = clients;
        }

        public void Run()
        {
            var localserver = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            localserver.Start();

            while (true)
            {
                Console.WriteLine("Waiting for clients to connect...");
                var client = localserver.AcceptTcpClient();

                var clientWrapper = new Client(client, Global.Instance.CancellationToken, Upload);
                Console.WriteLine($"Client connected: {clientWrapper}");
                clients[clientWrapper] = 0;
                clientWrapper.OnClose = () =>
                {
                    Console.WriteLine($"Removing client from clients list: {clientWrapper}");
                    byte c;
                    clients.TryRemove(clientWrapper, out c);
                };

                var tsk = clientWrapper.Start();
            }
        }

        void Upload(byte[] data)
        {
            // Do not enqueue data if remote is not connected
            if (Global.Instance.Remote.Connected)
            {
                uploadQueue.TryAdd(data);
            }
        }
    }
}
