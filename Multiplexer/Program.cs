namespace Multiplexer
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
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

    class Global
    {
        public BlockingCollection<byte[]> UploadQueue => uploadQueue;
        public ConcurrentDictionary<Client, byte> Clients => clients;
        public CancellationToken CancellationToken => cts.Token;
        public IRemoteInfo Remote => remote ?? new DummyRemote();

        BlockingCollection<byte[]> uploadQueue = new BlockingCollection<byte[]>();
        ConcurrentDictionary<Client, byte> clients = new ConcurrentDictionary<Client, byte>();
        CancellationTokenSource cts = new CancellationTokenSource();
        IRemoteInfo remote;

        public IRemoteInfo RegisterRemote(IRemoteInfo remote)
        {
            return Interlocked.Exchange(ref this.remote, remote);
        }

        public void Cancel()
        {
            if (!cts.Token.IsCancellationRequested)
            {
                lock(cts)
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        cts.Cancel();
                        cts.Dispose();
                        cts = new CancellationTokenSource();
                    }
                }
            }
        }

        public void ResetUploadQueue()
        {
            Console.WriteLine($"Resetting upload queue ({uploadQueue.Count})");
            byte[] b;
            while (uploadQueue.TryTake(out b)) { }
        }

        class DummyRemote : IRemoteInfo
        {
            public bool Connected => false;
            public string RemoteAddress => "";
        }
    }
}
