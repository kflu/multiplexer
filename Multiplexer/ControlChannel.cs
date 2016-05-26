namespace Multiplexer
{
    using Newtonsoft.Json;
    using System;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    class ControlChannel
    {
        Global glob;
        public ControlChannel(Global glob)
        {
            this.glob = glob;
        }

        public void Run()
        {
            while (true)
            {
                var line = Console.ReadLine();

                var toks = line.Split();
                switch (toks[0])
                {
                    case "connect":
                        if (glob.Remote.Connected)
                        {
                            Console.WriteLine("Error: Remote is connected. Try disconnect first");
                        }

                        Console.WriteLine($"Resetting upload queue (count={glob.UploadQueue.Count})");
                        glob.ResetUploadQueue();
                        // This is a fire-and-forget task. It is the responsibility of
                        // the task to properly handle resource clean up.
                        Task.Run(() => StartServer(toks[1], int.Parse(toks[2])));
                        break;
                    case "disconnect":
                        Console.WriteLine("Disconnecting");
                        glob.Cancel();
                        break;
                    case "stats":
                        DumpStats();
                        break;
                    case "quit":
                        Console.WriteLine("Exiting...");
                        glob.Cancel();
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
                Clients = glob.Clients.Select(c => c.ToString()).ToArray(),
                UploadQueue = glob.UploadQueue.Select(msg => System.Text.Encoding.UTF8.GetString(msg)).ToArray(),
            };
            Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        async Task StartServer(string hostname, int port)
        {
            Console.WriteLine($"Connecting to {hostname}:{port}");
            var remote = new TcpClient(hostname, port);
            var server = new Remote(remote, glob.UploadQueue, glob.CancellationToken, data =>
            {
                foreach (var client in glob.Clients)
                {
                    client.Key.DownlinkQueue.TryAdd(data);
                }
            });

            glob.RegisterRemote(server);

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
