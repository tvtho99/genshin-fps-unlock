using System.Reflection.PortableExecutable;

namespace unlockfps_nc.Utility
{
    internal static class DllValidator
    {
        internal static bool IsNativeX64Dll(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new PEReader(stream);

                var headers = reader.PEHeaders;
                return headers.PEHeader != null && headers.IsDll && headers.CorHeader == null &&
                    headers.CoffHeader.Machine == Machine.Amd64;
            }
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
