namespace Multiplexer
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Class to handle multiplexer management commands.
    /// </summary>
    class ControlChannel
    {
        Global glob;

        public ControlChannel(Global glob)
        {
            this.glob = glob;
        }

        /// <summary>
        /// Start the remote connection
        /// </summary>
        public async Task StartServer(string downlinkFile, string uplinkFile)
        {
            Remote server = null;
            try
            {
                System.Console.WriteLine("Open downlink..");
                var dn = File.Open(downlinkFile, FileMode.Open);
                System.Console.WriteLine("Open downlink done.");

                System.Console.WriteLine("Open uplink...");
                var uplink = new ReopenableStream(() => File.OpenWrite(uplinkFile));
                System.Console.WriteLine("Open uplink done.");

                server = new Remote(
                    // new ReopenableStream(() => File.OpenRead(downlinkFile)),
                    // File.OpenRead(downlinkFile),
                    dn,
                    uplink,
                    glob.UploadQueue, 
                    glob.CancellationToken, 
                    data /* receive: */ =>
                    {
                        // Implementation of receive() is to put inbound data to each of the client queues.
                        // Note that this is non-blocking. If any queue is full, the data is dropped from that
                        // queue.
                        foreach (var client in glob.Clients)
                        {
                            client.Key.DownlinkQueue.TryAdd(data);
                        }
                    });
            
                // Start and wait for the remote connection to terminate
                System.Console.WriteLine("Starting remote...");
                await server.Start().ConfigureAwait(false);
                System.Console.WriteLine("remote server finished");
            }
            catch (Exception e)
            {
                Console.WriteLine($"exception during remote serving: {e}");
            }
            finally
            {
                Console.WriteLine($"Disposing remote connection: {server}");
                server.Dispose();
                server = null;

                // When remote connection is terminated. Also disconnects all the clients.
                glob.Cancel();
            }
        }
    }
}
