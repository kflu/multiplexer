namespace Multiplexer
{
    using System.Collections.Concurrent;
    using System;
    using System.Linq;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using System.Net.Sockets;
    using System.Collections.Generic;
    using System.Threading;

    class Client : TcpClient
    {
        public async Task Receive(byte[] data) 
        {

        }
    }

    class Program
    {

        List<Client> clients;

        string downlinkFifo;

        string uplinkFifo;

        Stream uplink;

        Stream downlink;

        BlockingCollection<byte[]> uplinkQueue = new BlockingCollection<byte[]>();

        ManualResetEvent uplinkReopenRequested = new ManualResetEvent(false);
        
        Task AcceptClients()
        {
        }

        Task OpenUplink()
        {
        }

        Task OpenDownlink()
        {
        }

        Task HandleUplink()
        {
        }

        async Task WriteToUplink()
        {
            while (true)
            {
                try
                {
                    var data = await Task.Run(() => uplinkQueue.Take());
                    var cachedUplink = this.uplink;
                    if (cachedUplink == null)
                    {
                        Logger.Log($"Uplink not ready, discard {data.Length}B data");
                    }
                    else
                    {
                        await cachedUplink.WriteAsync(data, 0, data.Length);
                        await cachedUplink.FlushAsync();
                    }
                }
                catch (IOException e)
                {
                    Logger.Log($"Exception in writing to uplink stream: {e}");
                    this.uplinkReopenRequested.Set();
                }
            }
        }

        void ReopenUplink()
        {
            while (true)
            {
                this.uplinkReopenRequested.WaitOne();
                this.uplink = null;


            }

        }


        // This is a fire-and-forget task
        async Task ReadFromClient(Client client)
        {
            var buffer = new byte[256];
            int count = 0;
            try
            {
                using (var stream = client.GetStream())
                {
                    while (true)
                    {
                        count = await stream.ReadAsync(buffer, 0, buffer.Length, new System.Threading.CancellationToken());
                        if (count == 0) // EOF?
                        {
                            break;
                        }

                        await this.uplinkQueue.Put(buffer.Take(count).ToArray());
                    }
                }
            }
            catch (IOException e)
            {
                Logger.Log($"Exception in client uplink: {e}");
            }
        }

        Task Run()
        {
            var acceptClients = AcceptClients();
            var openUplink = OpenUplink();
            var openDownlink = OpenDownlink();
            var handleUplink = HandleUplink();
            var handleDownlink = HandleDownlink();

            Task.WaitAll(
                acceptClients,
                openUplink,
                openDownlink,
                handleUplink,
                handleDownlink
            );
        }

        static int Main(string[] args)
        {
            // Parse command line
            Configuration config = Parser.Default.ParseArguments<Configuration>(args).MapResult(opts => opts, err => null);
            if (config == null)
            {
                return -1;
            }

            var glob = new Global(config);
            var ctrl = new ControlChannel(glob);
            var clientServer = new ClientServer(config.Port, glob);

            Task.WaitAny(
                clientServer.Run(),
                ctrl.StartServer(config.DownLinkFile, config.UpLinkFile)
            );
            System.Console.WriteLine("exiting program");

            return 0;
        }
    }

    class Configuration
    {
        [CommandLine.Value(0, MetaName = "downlink-file", HelpText = "downlink")]
        public string DownLinkFile { get; set; }

        [CommandLine.Value(0, MetaName = "uplink-file", HelpText = "uplink")]
        public string UpLinkFile { get; set; }

        [CommandLine.Option('p', "port", Default = 3333, HelpText = "Local port to listen for client connections")]
        public int Port { get; set; }
    }
}
