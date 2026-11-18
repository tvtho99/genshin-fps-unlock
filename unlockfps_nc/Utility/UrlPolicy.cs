namespace unlockfps_nc.Utility
{
    internal static class UrlPolicy
    {
        private const string TrustedHost = "github.com";

        /// <summary>
        /// True only for absolute https URLs on github.com. The update URL comes from a remote server
        /// and is opened by an elevated process, so anything else (local paths, UNC, file:, other hosts)
        /// is refused.
        /// </summary>
        internal static bool IsTrustedReleaseUrl(string? url)
            => Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && string.IsNullOrEmpty(uri.UserInfo)
               && string.Equals(uri.Host, TrustedHost, StringComparison.OrdinalIgnoreCase);
    }
}
