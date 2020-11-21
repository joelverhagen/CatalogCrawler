using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Knapcode.CatalogCrawler
{
    public static class DirectoryBins
    {
        /// <summary>
        /// Hashes the provided key to determine a subdirectory for it. The result directory path is based off of
        /// multiple levels of subdirectories where each level is the hexadecimal representation of a byte from the
        /// hash. For example, the SHA-256 hash of "newtonsoft.json" starts with "2c4cf1fb". If the depth provided is
        /// 3, the output will be like "{baseDir}/2c/4c/f1". This is useful to limit the number of directories in any
        /// given directory to 256.
        /// </summary>
        public static string GetPath(string baseDir, int depth, string key)
        {
            if (depth < 1 || depth > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }

            using (var algorithm = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(key);
                var hashBytes = algorithm.ComputeHash(bytes);
                var hashHex = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
                var pieces = new List<string> { baseDir };
                for (var i = 0; i < depth; i++)
                {
                    pieces.Add(hashHex.Substring(2 * i, 2));
                }

                return Path.Combine(pieces.ToArray());
            }
        }
    }
}
