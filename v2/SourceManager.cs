namespace Multiplexer
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class SourceManager
    {
        private readonly Func<Task<Stream>> getIncomingStream;
        private readonly Func<Task<Stream>> getOutgoingStream;
        private readonly AutoResetEvent EstablishOutgoingRequested = new AutoResetEvent(false);
        private readonly Thread establishOutgoingThread;
        private Stream incoming;
        private Stream outgoing;

        public event Func<byte[], Task> OnIncomingDataAvailable;

        public SourceManager(
            Func<Task<Stream>> getIncomingStream,
            Func<Task<Stream>> getOutgoingStream)
        {
            this.getIncomingStream = getIncomingStream;
            this.getOutgoingStream = getOutgoingStream;

            this.EstablishOutgoingRequested.Set();
            this.establishOutgoingThread = this.EstablishOutgoingStream();
        }

        public async Task HandleIncoming()
        {
            while (true)
            {
                // For named pipe, this should block until a writer is present.
                this.incoming = await this.getIncomingStream();
                Logger.Log("incoming stream established");

                int c = 0;
                byte[] buffer = new byte[256];
                while (true)
                {
                    try
                    {
                        c = await this.incoming.ReadAsync(buffer, 0, buffer.Length);
                    }
                    catch (IOException e)
                    {
                        Logger.Log($"Error reading incoming stream: {e}");
                        break;
                    }

                    if (c > 0)
                    {
                        if (this.OnIncomingDataAvailable != null)
                        {
                            await this.OnIncomingDataAvailable(buffer.Take(c).ToArray());
                        }
                    }
                    else
                    {
                        Logger.Log($"incoming EOF encountered.");
                        break;
                    }
                }
                Logger.Log("re-establishing incoming stream");
            }
        }

        public async Task SendOutgoingData(byte[] data)
        {
            if (this.outgoing == null)
            {
                // outgoing pipe not ready - drop the data
                Logger.Log($"Outgoing stream not ready, dropping the data of length {data.Length}");
            }
            else
            {
                try
                {
                    await this.outgoing.WriteAsync(data, 0, data.Length);
                    await this.outgoing.FlushAsync();
                }
                catch (IOException e)
                {
                    Logger.Log($"Error encountered when writing outgoing data: {e}");
                    try
                    {
                        this.outgoing.Dispose();
                    }
                    catch (IOException ed)
                    {
                        Logger.Log($"Error (ignored) disposing outgoing stream: {ed}");
                    }
                    this.outgoing = null;
                    EstablishOutgoingRequested.Set();
                }
           }
        }

        private Thread EstablishOutgoingStream()
        {                
            return new Thread(() =>
            {
                while (true)
                {
                    this.EstablishOutgoingRequested.WaitOne();

                    // blocks outgoing named pipe until reader present
                    try
                    {
                        this.outgoing = this.getOutgoingStream().Result;
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Error getting outgoing stream: {e}");
                        this.EstablishOutgoingRequested.Set();
                    }
                }
            });
        }
    }
}