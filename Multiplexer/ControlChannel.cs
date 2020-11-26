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
            var downlinkStream = File.OpenRead(downlinkFile);
            var uplinkStream = File.OpenWrite(uplinkFile);
            var server = new Remote(
                downlinkStream, uplinkStream, 
                glob.UploadQueue, glob.CancellationToken, 
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
                server.Dispose();
                server = null;

                // When remote connection is terminated. Also disconnects all the clients.
                glob.Cancel();
            }
        }
    }
}
