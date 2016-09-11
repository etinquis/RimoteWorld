using Polenter.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RimoteWorld.Core
{
    internal static class MessageSerializer
    {
        private static SharpSerializer _serializer = new SharpSerializer();

        private enum Magic : ulong
        {
            Value = 0xFEED
        }
        
        private class SerializationHeader
        {
            private ulong _magic = (ulong)Magic.Value;
            private ulong _headerVersion = CurrentHeaderVersion;
            private ulong _headerLength;
            private ulong _bodyLength;

            private const ulong CurrentHeaderVersion
                = 1;

            private const ulong CurrentHeaderLength
                = sizeof(ulong)  // magic
                + sizeof(ulong)  // header version
                + sizeof(ulong)  // header length
                + sizeof(ulong); // body length 

            public ulong HeaderVersion
            {
                get { return _headerVersion; }
            }

            public ulong HeaderLength
            {
                get { return _headerLength; }
            }
            public ulong BodyLength
            {
                get { return _bodyLength; }
                set { _bodyLength = value; }
            }

            private SerializationHeader()
            {
                
            }

            private static ulong ReadMember(BinaryReader reader, ref ulong member)
            {
                member = reader.ReadUInt64();
                return sizeof(ulong);
            }

            public static SerializationHeader ReadFrom(Stream stream)
            {
                return ReadFrom(new BinaryReader(stream));
            }

            public static SerializationHeader ReadFrom(BinaryReader reader)
            {
                SerializationHeader header = new SerializationHeader();
                ulong consumedHeaderSize = 0;
                consumedHeaderSize += ReadMember(reader, ref header._magic);
                if (!Magic.Value.Equals((Magic) header._magic))
                {
                    throw new FormatException("Header magic value not correct");
                }

                consumedHeaderSize += ReadMember(reader, ref header._headerVersion);
                consumedHeaderSize += ReadMember(reader, ref header._headerLength);
                consumedHeaderSize += ReadMember(reader, ref header._bodyLength);

                if (header._bodyLength == 0)
                {
                    throw new FormatException("Header says body length is 0, which is invalid");
                } else if (header.HeaderLength < consumedHeaderSize)
                {
                    throw new FormatException(string.Format("Header says header length is {0}, but we've read {1}",
                        header._headerLength, consumedHeaderSize));
                }

                var headerRemaining = header._headerLength - consumedHeaderSize;
                ulong unknownRead = 0;
                while (unknownRead < headerRemaining)
                {
                    var toRead = (int)Math.Min(headerRemaining - unknownRead, (ulong)int.MaxValue);
                    byte[] unknownStuff = reader.ReadBytes(toRead);
                    unknownRead += (ulong)unknownStuff.Length;
                }

                return header;
            }

            public static void WriteTo(BinaryWriter writer, ulong bodyLength)
            {
                if (bodyLength == 0)
                {
                    throw new ArgumentException("bodyLength must be greater than 0");
                }

                writer.Write((ulong)Magic.Value);
                writer.Write(CurrentHeaderVersion);
                writer.Write(CurrentHeaderLength);
                writer.Write(bodyLength);
            }
        }

        private static MemoryStream SerializeObject(object obj)
        {
            MemoryStream buf = new MemoryStream();
            _serializer.Serialize(obj, buf);
            return buf;
        }

        public static void SerializeMessageAsync(Message message, Action<Result<MemoryStream>> callback)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                MemoryStream stream = new MemoryStream();
                try
                {
                    SerializeMessageToStream(message, stream);
                }
                catch (Exception ex)
                {
                    callback(ex);
                    return;
                }
                callback(stream);
            });
        }

        public static void SerializeMessageToStream(Message obj, Stream stream)
        {
            var buf = SerializeObject(obj);

            SerializationHeader.WriteTo(new BinaryWriter(stream), (ulong)buf.Length);
            buf.WriteTo(stream);
        }

        public static Message DeserializeFromStream(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            SerializationHeader header = SerializationHeader.ReadFrom(reader);
            byte[] body = reader.ReadBytes((int)header.BodyLength);
            MemoryStream memStream = new MemoryStream(body);
            return (Message)_serializer.Deserialize(memStream);
        }
    }
}
