using System.Security.Cryptography;

namespace MovieWatch {
    public static class VideoVerifier {
        private const long ChunkSize = 10 * 1024 * 1024; // 10MB

        public static async Task<(string hash, long fileSize)> ComputeAsync(
            string filePath,
            IProgress<string>? progress = null) {
            return await Task.Run(() => {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var fileSize = fs.Length;

                progress?.Report("Reading file chunks...");

                var first = ReadChunk(fs, 0, (int)Math.Min(ChunkSize, fileSize));
                var midStart = Math.Max(0L, (fileSize / 2) - (ChunkSize / 2));
                var middle = ReadChunk(fs, midStart, (int)Math.Min(ChunkSize, fileSize - midStart));
                var lastStart = Math.Max(0L, fileSize - ChunkSize);
                var last = ReadChunk(fs, lastStart, (int)(fileSize - lastStart));

                progress?.Report("Computing hash...");

                var combined = new byte[first.Length + middle.Length + last.Length + 8];
                var offset = 0;
                Buffer.BlockCopy(first, 0, combined, offset, first.Length); offset += first.Length;
                Buffer.BlockCopy(middle, 0, combined, offset, middle.Length); offset += middle.Length;
                Buffer.BlockCopy(last, 0, combined, offset, last.Length); offset += last.Length;
                Buffer.BlockCopy(BitConverter.GetBytes(fileSize), 0, combined, offset, 8);

                var hash = Convert.ToHexString(MD5.HashData(combined)).ToLower();
                return (hash, fileSize);
            });
        }

        private static byte[] ReadChunk(FileStream fs, long position, int length) {
            fs.Seek(position, SeekOrigin.Begin);
            var buffer = new byte[length];
            var totalRead = 0;
            while (totalRead < length) {
                var read = fs.Read(buffer, totalRead, length - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            return buffer;
        }
    }
}