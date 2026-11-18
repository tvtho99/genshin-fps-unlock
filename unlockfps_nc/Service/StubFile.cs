using System.Reflection;
using System.Security.Cryptography;

namespace unlockfps_nc.Service
{
    /// <summary>
    /// The embedded UnlockerStub.dll extracted to disk. While this object is alive the file is held
    /// open with FileShare.Read only, so no other process can overwrite, rename or delete it before
    /// the elevated unlocker and the game load it.
    /// </summary>
    internal sealed class StubFile : IDisposable
    {
        internal const string FileName = "UnlockerStub.dll";
        private const string ResourceName = "unlockfps_nc.Resources.UnlockerStub.dll";

        private readonly FileStream _lock;

        public string Path { get; }

        private StubFile(string path, FileStream lockStream)
        {
            Path = path;
            _lock = lockStream;
        }

        public static StubFile ExtractEmbedded(string directory)
        {
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                ?? throw new InvalidDataException($"Embedded resource {ResourceName} is missing.");
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            return Extract(directory, buffer.ToArray());
        }

        /// <exception cref="IOException">The file could not be written or locked (e.g. held by another process).</exception>
        /// <exception cref="InvalidDataException">The file on disk does not match <paramref name="content"/>.</exception>
        internal static StubFile Extract(string directory, byte[] content)
        {
            var path = System.IO.Path.Combine(directory, FileName);

            using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                writer.Write(content);

            // Re-open read-only and verify: anything swapped in between the two opens is rejected here.
            var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try {
                if (!SHA256.HashData(lockStream).AsSpan().SequenceEqual(SHA256.HashData(content)))
                    throw new InvalidDataException($"{path} does not match the embedded stub.");

                return new StubFile(path, lockStream);
            }
            catch {
                lockStream.Dispose();
                throw;
            }
        }

        public void Dispose() => _lock.Dispose();
    }
}
