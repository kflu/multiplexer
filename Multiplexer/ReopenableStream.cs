namespace Multiplexer
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class ReopenableStream : Stream
    {
        private readonly Func<Stream> getStream;

        private Stream stream;

        private bool reopening;

        private readonly ManualResetEvent reopeningRequested = new ManualResetEvent(false);

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position 
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public ReopenableStream(Func<Stream> getStream)
        {
            this.getStream = getStream;
            this.stream = getStream();
            this.reopening = false;
            this.reopeningRequested.Reset();
            new Thread(this.DoReopening);
        }

        public override void Flush()
        {
            try
            {
                stream.Flush();
            }
            catch (IOException e)
            {
                Console.Error.WriteLine($"Error encountered (ignored) during flush: {e}");
                Console.Error.WriteLine($"Reopening...");
                reopening = true;
                reopeningRequested.Set();
            }
        }

        private void DoReopening()
        {
            while (true)
            {
                reopeningRequested.WaitOne();
                Console.Error.WriteLine($"Reopening requested..");
                try
                {
                    stream.Dispose();
                }
                catch (IOException e)
                {
                    Console.Error.WriteLine($"Error ignored during disposing {e}");
                }

                stream = getStream();
                reopeningRequested.Reset();
                Console.Error.WriteLine($"Reopening done.");
                reopening = false;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (reopening)
            {
                Console.Error.WriteLine($"reopening stream, buffer dropped (len: {buffer.Length})");
                return;
            }

            stream.Write(buffer, offset, count);
        }
    }
}