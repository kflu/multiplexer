namespace Multiplexer
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    /// <summary>
    /// Class to handle multiplexer management commands.
    /// </summary>
    class ControlChannel
    {
        BlockingCollection<string> cmdQueue = new BlockingCollection<string>();
        Global glob;

        public ControlChannel(Global glob)
        {
            this.glob = glob;
        }

        public bool SubmitCommand(string line)
        {
            return cmdQueue.TryAdd(line);
        }

        public async Task Run()
        {
            try
            {
                await await Task.WhenAny(
                    Task.Run(() => GetCommandsFromStdin()),
                    Task.Run(() => HandleCommands())).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                // Ideally, when one of the tasks is terminated, we should cancel the other.
                // But in case of control channel, when it's terminated, the whole app is going
                // down. So don't bother.
                Console.WriteLine("Control channel terminated");
            }
        }

        void GetCommandsFromStdin()
        {
            string line;
            while (true)
            //while (null != (line = Console.ReadLine()))
            {
                line = Console.ReadLine();
                cmdQueue.TryAdd(line);
            }
        }

        void HandleCommands()
        {
            bool quit = false;
            while (!quit)
            {
                var line = cmdQueue.Take();

                var toks = line.Split();
                switch (toks[0])
                {
                    // Connecting to remote server
                    // connect <remote_server> <port>
                    case "connect":
                        if (glob.Remote.Connected)
                        {
                            Console.WriteLine("Error: Remote is connected. Try disconnect first");
                        }
                        else
                        {
                            // Reset the upload queue so stale outbound data is not uploaded to the new
                            // connection
                            glob.ResetUploadQueue();

                            // This is a fire-and-forget task. It is the responsibility of
                            // the task to properly handle resource clean up.
                            Task.Run(() => StartServer(toks[1], int.Parse(toks[2])));
                        }
                        break;
                    
                    // Disconnecting:
                    // disconnect
                    case "disconnect":
                        if (glob.Remote.Connected)
                        {
                            Console.WriteLine("Disconnecting");

                            // cancel the global cancellation token. This would disconnect the server and all 
                            // clients. It is desirable to disconnect the clients to maintain the equivelency 
                            // when a client is directly connected to the server.
                            glob.Cancel();
                        }
                        else
                        {
                            Console.WriteLine("Not connected");
                        }
                        break;
                    
                    // Dump multiplexer status
                    // info|stats
                    case "info":
                    case "stats":
                        DumpStats();
                        break;
                    
                    // Quit the multiplexer application
                    case "quit":
                        Console.WriteLine("Exiting...");
                        glob.Cancel();
                        quit = true; // terminate the control channel loop
                        break;
                    
                    default:
                        Console.WriteLine("Unknown command: " + line);
                        break;
                }
            }
        }

        /// <summary>
        /// Dump multiplxer info
        /// </summary>
        private void DumpStats()
        {
            var info = new
            {
                Remote = glob.Remote,
                Clients = glob.Clients.Select(c => c.ToString()).ToArray(),
                UploadQueue = glob.UploadQueue.Select(msg => System.Text.Encoding.UTF8.GetString(msg)).ToArray(),
            };
            Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));
        }

        /// <summary>
        /// Start the remote connection
        /// </summary>
        /// <param name="hostname">remote hostname</param>
        /// <param name="port">remote port</param>
        async Task StartServer(string hostname, int port)
        {
            Console.WriteLine($"Connecting to {hostname}:{port}");
            var remote = new TcpClient(hostname, port);
            var server = new Remote(remote, glob.UploadQueue, glob.CancellationToken, /* receive: */ data =>
            {
                // Implementation of receive() is to put inbound data to each of the client queues.
                // Note that this is non-blocking. If any queue is full, the data is dropped from that
                // queue.
                foreach (var client in glob.Clients)
                {
                    client.Key.DownlinkQueue.TryAdd(data);
                }
            });

            // Register the remote connection globally so everyone is aware of the connection status. This is 
            // essential for several requirements, e.g., client data should not be added to outbound queue if
            // there's no connection.
            glob.RegisterRemote(server);

            try
            {
                // Start and wait for the remote connection to terminate
                await server.Start().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Console.WriteLine($"Disposing remote connection: {server}");
                glob.RegisterRemote(null); // unregister remote connection
                server.Dispose();
                server = null;

                // When remote connection is terminated. Also disconnects all the clients.
                glob.Cancel();
            }
        }
    }
}
