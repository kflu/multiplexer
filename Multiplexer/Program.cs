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
        [CommandLine.Option('a', "auto-connect", Default = false, HelpText = "Whether to auto connect to remote server when the first client connects")]
        public bool AutoConnect { get; set; }

        [CommandLine.Option('p', "port", Default = 3333, HelpText = "Local port to listen for client connections")]
        public int Port { get; set; }

        [CommandLine.Option('r', "remote-host", HelpText = "Remote server to connect")]
        public string RemoteHost { get; set; }

        [CommandLine.Option('t', "remote-port", HelpText = "Remote port to connect")]
        public int RemotePort { get; set; }
    }
}
