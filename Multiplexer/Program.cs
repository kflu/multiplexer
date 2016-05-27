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

            if (config.AutoConnect)
            {
                if (string.IsNullOrEmpty(config.RemoteHost) || config.RemotePort <= 0)
                {
                    Console.WriteLine($"Invalid remote host or port: {config.RemoteHost}:{config.RemotePort}");
                    return -1;
                }

                clientServer.OnConnected += () =>
                {
                    try
                    {
                        if (glob.Remote.Connected) return;
                        Console.WriteLine($"Client connected. Auto-connecting to remote server: {config.RemoteHost} {config.RemotePort}");
                        ctrl.SubmitCommand($"connect {config.RemoteHost} {config.RemotePort}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                };
            }

            Task.WaitAny(ctrl.Run(), clientServer.Run());

            return 0;
        }
    }

    class Configuration
    {
        [CommandLine.Value(0, MetaName = "remote-host", HelpText = "Remote server to connect (for auto-connect). If not specified, auto-connect is disabled.")]
        public string RemoteHost { get; set; }

        [CommandLine.Value(1, MetaName = "remote-port", HelpText = "Remote port to connect (for auto-connect). If not specified, auto-connect is disabled.")]
        public int RemotePort { get; set; }

        [CommandLine.Option('p', "port", Default = 3333, HelpText = "Local port to listen for client connections")]
        public int Port { get; set; }

        public bool AutoConnect => !string.IsNullOrEmpty(RemoteHost) && RemotePort > 0;
    }
}
