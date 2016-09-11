using NUnit.Framework;
using System.IO;
using System.Linq;

namespace RimoteWorld.Core.Tests
{
    [TestFixture]
    public class PartialReadStreamTests
    {
        [Test]
        public void MatchesStreamAPISubsetWhenLengthsMatch()
        {
            var streamBytes = Enumerable.Range(1, 255).Select(i => (byte) i).ToArray();

            MemoryStream memStream = new MemoryStream(streamBytes, false);

            PartialReadStream partialStream = new PartialReadStream(memStream, (ulong)streamBytes.Length);
            Assert.That(partialStream.CanRead, Is.EqualTo(memStream.CanRead), "CanRead should match");
            Assert.That(partialStream.CanWrite, Is.EqualTo(false), "CanWrite should be false");
            Assert.That(partialStream.CanSeek, Is.EqualTo(memStream.CanSeek), "CanSeek should match");

            if (partialStream.CanSeek)
            {
                if (partialStream.CanRead)
                {
                    Assert.That(partialStream.Position, Is.EqualTo(memStream.Position), "Positions should match");
                    byte[] buffer = new byte[100];
                    partialStream.Read(buffer, 0, buffer.Length);
                    Assert.That(partialStream.Position, Is.EqualTo(memStream.Position),
                        "Positions should match after read");

                    buffer = new byte[300];
                    partialStream.Read(buffer, 0, buffer.Length);
                    Assert.That(partialStream.Position, Is.EqualTo(memStream.Position),
                        "Positions should match after reading to the end");

                    partialStream.Seek(0, SeekOrigin.Begin);
                }

                partialStream.Seek(0, SeekOrigin.End);
                Assert.That(partialStream.Position, Is.EqualTo(memStream.Position), "Positions should match after seek end");

                Assert.That(() => partialStream.Seek(-1, SeekOrigin.Begin),
                    Throws.InstanceOf<IOException>()
                        .And.Message.EqualTo(
                            "An attempt was made to move the position before the beginning of the stream."));

                // seeking past the end is supported see https://msdn.microsoft.com/en-us/library/system.io.stream.seek(v=vs.110).aspx
                Assert.That(() => partialStream.Seek(1, SeekOrigin.End), Throws.Nothing);

                partialStream.Seek(0, SeekOrigin.Begin);
                Assert.That(partialStream.Position, Is.EqualTo(memStream.Position), "Positions should match after seek begin");
            }

            if (partialStream.CanRead)
            {
                byte[] buffer = new byte[100];
                int readCount = partialStream.Read(buffer, 0, buffer.Length);
                Assert.That(readCount, Is.EqualTo(buffer.Length));
                Assert.That(buffer, Is.EquivalentTo(streamBytes.Take(100)));

                buffer = new byte[300];
                readCount = partialStream.Read(buffer, 0, buffer.Length);
                Assert.That(readCount, Is.EqualTo(255 - 100));
                Assert.That(partialStream.ReadByte(), Is.EqualTo(-1));

                readCount = partialStream.Read(buffer, 0, buffer.Length);
                Assert.That(readCount, Is.EqualTo(0));
            }
        }
    }
}
