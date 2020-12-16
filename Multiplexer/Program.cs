namespace Multiplexer
{
    using System;
    using static Logger;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using System.Text;

    class Program
    {
        static int Main(string[] args)
        {
            // Parse command line
            Configuration config = Parser.Default.ParseArguments<Configuration>(args).MapResult(opts => opts, err => null);
            if (config == null)
            {
                return -1;
            }

            var glob = new Global(config);
            var clientServer = new ClientServer(config.Port, glob);
            var remote = new Remote(
                config.UplinkFifo, 
                config.DownlinkFifo, 
                glob.UploadQueue, 
                glob.CancellationToken,
                /* receive */ data =>
                {
                    // Implementation of receive() is to put inbound data to each of the client queues.
                    // Note that this is non-blocking. If any queue is full, the data is dropped from that
                    // queue.
                    foreach (var client in glob.Clients)
                    {
                        client.Key.DownlinkQueue.TryAdd(data);
                    }
                }
            );

            var clientServerTask = Task.Run(clientServer.Run);
            var remoteTask = Task.Run(remote.Start);

            Task.WaitAll(new [] {clientServerTask, remoteTask}, glob.CancellationToken);

            Log($"Program exiting");
            return 0;
        }
    }

    class Configuration
    {
        [CommandLine.Option('p', "port", Default = 3333, HelpText = "Local port to listen for client connections")]
        public int Port { get; set; }

        [CommandLine.Option('u', "uplink")]
        public string UplinkFifo {get;set;}

        [CommandLine.Option('d', "downlink")]
        public string DownlinkFifo {get;set;}
    }
}
