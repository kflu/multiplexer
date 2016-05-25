using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Multiplexer
{
    class Program
    {
        static BlockingCollection<byte[]> UploadQueue = new BlockingCollection<byte[]>();
        static ConcurrentDictionary<Client, byte> clients = new ConcurrentDictionary<Client, byte>();

        static void Main(string[] args)
        {
            Task.Run(() => HandleControlChannel((host, port) =>
            {
                StartServer(host, port);
            }));

            var localserver = new TcpListener(IPAddress.Parse("127.0.0.1"), 3333);
            localserver.Start();

            while (true)
            {
                Console.WriteLine("Waiting for clients to connect...");
                var client = localserver.AcceptTcpClient();

                var clientWrapper = new Client(client, data => UploadQueue.TryAdd(data));
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

        static void HandleControlChannel(Action<string, int> handleConnect)
        {
            while (true)
            {
                var line = Console.ReadLine();

                var toks = line.Split();
                switch (toks[0])
                {
                    case "connect":
                        try
                        {
                            handleConnect(
                                toks[1],
                                int.Parse(toks[2]));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }

                        break;
                    default:
                        Console.WriteLine("Unknown command: " + line);
                        break;
                }
            }
        }

        static Task StartServer(string hostname, int port)
        {
            Console.WriteLine($"Connecting to {hostname}:{port}");
            var remote = new TcpClient(hostname, port);
            var server = new Remote(remote, UploadQueue, data =>
            {
                foreach (var client in clients)
                {
                    client.Key.DownlinkQueue.TryAdd(data);
                }
            });

            return server.Start();
        }
    }
}
