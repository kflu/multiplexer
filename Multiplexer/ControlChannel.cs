namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Sockets;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    class ControlChannel
    {
        public void Run()
        {
            while (true)
            {
                var line = Console.ReadLine();

                var toks = line.Split();
                switch (toks[0])
                {
                    case "connect":
                        if (Global.Instance.Remote.Connected)
                        {
                            Console.WriteLine("Error: Remote is connected. Try disconnect first");
                        }

                        Console.WriteLine($"Resetting upload queue (count={Global.Instance.UploadQueue.Count})");
                        Global.Instance.ResetUploadQueue();
                        // This is a fire-and-forget task. It is the responsibility of
                        // the task to properly handle resource clean up.
                        Task.Run(() => StartServer(toks[1], int.Parse(toks[2])));
                        break;
                    case "disconnect":
                        Console.WriteLine("Disconnecting");
                        Global.Instance.Cancel();
                        break;
                    case "stats":
                        DumpStats();
                        break;
                    case "quit":
                        Console.WriteLine("Exiting...");
                        Global.Instance.Cancel();
                        return;
                    default:
                        Console.WriteLine("Unknown command: " + line);
                        break;
                }
            }
        }

        private void DumpStats()
        {
            var info = new
            {
                Clients = Global.Instance.Clients.Select(c => c.ToString()).ToArray(),
                UploadQueue = Global.Instance.UploadQueue.Select(msg => System.Text.Encoding.UTF8.GetString(msg)).ToArray(),
            };
            Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        async Task StartServer(string hostname, int port)
        {
            Console.WriteLine($"Connecting to {hostname}:{port}");
            var remote = new TcpClient(hostname, port);
            var server = new Remote(remote, Global.Instance.UploadQueue, Global.Instance.CancellationToken, data =>
            {
                foreach (var client in Global.Instance.Clients)
                {
                    client.Key.DownlinkQueue.TryAdd(data);
                }
            });

            Global.Instance.RegisterRemote(server);

            try
            {
                await server.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Console.WriteLine($"Disposing remote connection: {server}");
                server.Dispose();
                server = null;
            }
        }
    }
}
