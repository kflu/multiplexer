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
            var ctrl = new ControlChannel();
            var clientServer = new ClientServer(3333, Global.Instance.UploadQueue, Global.Instance.Clients);

            Task.WaitAny(
                Task.Run(() => ctrl.Run(), Global.Instance.CancellationToken),
                Task.Run(() => clientServer.Run(), Global.Instance.CancellationToken));
        }
    }

    class Global
    {
        static Global instance = new Global();
        public static Global Instance => instance;
        private Global() { }

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
