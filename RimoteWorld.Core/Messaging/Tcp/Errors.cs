using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace RimoteWorld.Core.Messaging.Tcp
{
    public class TcpWriteError : Exception
    {
        public TcpClient ClientWithError { get; private set; }

        public TcpWriteError(Exception innerException) : base("Tcp Write Error", innerException)
        {

        }
    }
    public class TcpReadError : Exception
    {
        public TcpClient ClientWithError { get; private set; }

        public TcpReadError(Exception innerException) : base("Tcp Read Error", innerException)
        {

        }
    }
}
