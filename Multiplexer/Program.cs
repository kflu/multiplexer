namespace Multiplexer
{
    using System.Threading.Tasks;

    class Program
    {
        static void Main(string[] args)
        {
            var glob = new Global();
            var ctrl = new ControlChannel(glob);
            var clientServer = new ClientServer(3333, glob);

            Task.WaitAny(
                Task.Run(() => ctrl.Run(), glob.CancellationToken),
                Task.Run(() => clientServer.Run(), glob.CancellationToken));
        }
    }
}
