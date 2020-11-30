namespace Multiplexer
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;

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
