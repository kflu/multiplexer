namespace Multiplexer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class ClientsManager
    {
        private readonly List<Client> clients = new List<Client>();

        /// Called by others to broadcast data to clients when available.
        public Task Broadcast(byte[] data)
        {
            throw new NotImplementedException();
        }

        /// listen and accepts client connection
        public Task Listen()
        {
            throw new NotImplementedException();
        }
    }
}