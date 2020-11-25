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

            if (string.IsNullOrEmpty(config.ReadFile) || !File.Exists(config.ReadFile))
            {
                System.Console.WriteLine($"Invalid file to read: {config.ReadFile}");
                return -1;
            }

            Task.WaitAny(ctrl.Run(), clientServer.Run());

            return 0;
        }
    }

    class Configuration
    {
        [CommandLine.Value(0, MetaName = "read-file", HelpText = "file to read in")]
        public string ReadFile { get; set; }

        [CommandLine.Option('p', "port", Default = 3333, HelpText = "Local port to listen for client connections")]
        public int Port { get; set; }
    }
}
