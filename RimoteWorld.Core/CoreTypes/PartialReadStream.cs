using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core
{
    internal class PartialReadStream : Stream
    {
        private Stream _internalStream = null;
        private uint _startOffset = 0;
        private uint _readCount = 0;
        private uint _readLength = 0;

        public PartialReadStream(Stream internalStream, ulong readLength)
        {
            _internalStream = internalStream;
        }

        public override bool CanRead
        {
            get { return _internalStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _internalStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return _internalStream.Length; }
        }

        public override long Position
        {
            get { return _internalStream.Position - _startOffset; }

            set { _internalStream.Position = _startOffset + Math.Min(_startOffset + _readLength, Math.Max(0, value)); }
        }

        public override void Flush()
        {
            _internalStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _internalStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (CanSeek)
            {
                long newOffset = 0;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                    {
                        newOffset = _startOffset + offset;
                        break;
                    }
                    case SeekOrigin.Current:
                    {
                        newOffset = Position + offset;
                        break;
                    }
                    case SeekOrigin.End:
                    {
                        newOffset = _startOffset + _readLength + offset;
                        break;
                    }
                }

                if (newOffset < _startOffset)
                {
                    throw new IOException("An attempt was made to move the position before the beginning of the stream.");
                }
                return (Position = newOffset);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
