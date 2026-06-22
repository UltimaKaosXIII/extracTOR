using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using ZstdSharp;

namespace extracTOR
{
    public class TORArchive
    {
        public TORHeader Header { get; }
        
        private const int BufferSize = 81920;

        public TORArchive(BinaryReader reader, string archiveName)
        {
            Header = TORHeader.ReadHeader(reader, archiveName);
        }
        
        public async Task ExtractEntriesAsync(
        string archiveFilePath,
        bool isPts,
        bool isBeta,
        HashDictionary hashDictionary,
        string outputRootPath,
        Action progressCallback)
        {
            using (var archiveStream = new FileStream(archiveFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var reader = new BinaryReader(archiveStream))
            {
                long nextTableOffset = Header.IndexOffset;
                while (nextTableOffset != 0)
                {
                    archiveStream.Seek(nextTableOffset, SeekOrigin.Begin);
                    int tableCapacity = reader.ReadInt32();
                    nextTableOffset = reader.ReadInt64();
                    for (int i = 0; i < tableCapacity; i++)
                    {
                        FileEntry entry = new FileEntry();
                        long nextEntryOffset = 0;
                        try
                        {
                            bool validEntry = false;
                            var offset = reader.ReadInt64();
                            var offsetAdjust = reader.ReadInt32();
                            var entryOffset = reader.BaseStream.Position;
                            reader.BaseStream.Position = offset;
                            uint dataCheck = reader.ReadUInt32();
                            if (dataCheck == 0x00200002)
                            {
                                reader.BaseStream.Position = entryOffset;
                            }
                            else
                            {
                                entry = null;
                                break;
                            }
                            entry.DataOffset = offset + offsetAdjust;
                            entry.CompressedSize = reader.ReadInt32();
                            entry.DecompressedSize = reader.ReadInt32();
                            entry.Hash = reader.ReadUInt64();
                            entry.CRC = reader.ReadUInt32();
                            entry.CompressionMethod = reader.ReadInt16();
                            nextEntryOffset = reader.BaseStream.Position;
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception in FileEntry: {ex.Message}\n{ex.StackTrace}");
                            throw;
                        }
                        if (hashDictionary.TryGetPath(entry.Hash, out var relativePath) &&
                                ShouldProcessFile(relativePath, Path.GetFileName(archiveFilePath)))
                        {
                            await ProcessEntryAsync(
                                    archiveStream,
                                    entry,
                                    entry.DataOffset,
                                    ConstructOutputPath(outputRootPath, relativePath, isPts, isBeta),
                                    progressCallback);
                        }
                        reader.BaseStream.Seek(nextEntryOffset - reader.BaseStream.Position, SeekOrigin.Current);
                    }
                }
            }
        }

        private bool ShouldProcessFile(string relativePath, string archiveFileName)
        {
            if (string.Equals(relativePath, archiveFileName, StringComparison.OrdinalIgnoreCase))
                return false;

            return !relativePath.Contains("metadata.bin") &&
                   !relativePath.Contains("ft.sig") &&
                   !relativePath.Contains("groupmanifest.bin");
        }

        private string ConstructOutputPath(string rootPath, string relativePath, bool isPts, bool isBeta)
        {
            var basePath = isPts ? Path.Combine(rootPath, "pts") :
                          isBeta ? Path.Combine(rootPath, "beta") :
                                   rootPath;
            relativePath = relativePath.Replace('/', '\\');
            var fullPath = $"{basePath}{relativePath}";
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            return fullPath;
        }

        private async Task ProcessEntryAsync(
        FileStream archiveStream,
        FileEntry entry,
        long dataOffset,
        string outputPath,
        Action progressCallback)
        {
            using (var outputStream = new FileStream(outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                //long pos = archiveStream.Position;
                archiveStream.Seek(entry.DataOffset, SeekOrigin.Begin);
                if (entry.IsCompressed)
                {
                    await DecompressAsync(archiveStream, outputStream, entry);
                }
                else
                {
                    await CopyUncompressedAsync(archiveStream, outputStream, entry);
                }
                progressCallback();
            }
        }
        private async Task DecompressAsync(FileStream input, FileStream outputStream, FileEntry entry)
        {
            try
            {
                switch (Header.Version)
                {
                    case 5:
                        {
                            await using var decompressor = new ZLibStream(input, CompressionMode.Decompress, leaveOpen: true);
                            await decompressor.CopyToAsync(outputStream, BufferSize);
                        }
                        break;
                    case 6:
                        {
                            byte[] b = new byte[entry.CompressedSize];
                            await input.ReadAsync(b, 0, entry.CompressedSize);
                            await using var decompressor = new DecompressionStream(new MemoryStream(b), entry.CompressedSize, leaveOpen: true);
                            await decompressor.CopyToAsync(outputStream, BufferSize);
                        }
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception caught in DecompressAsync: {e.Message}\n\n{e.StackTrace}");
                Debugger.Break();
                throw;
            }
        }
        private async Task CopyUncompressedAsync(FileStream input, FileStream outputStream, FileEntry entry)
        {
            var bytesRemaining = entry.CompressedSize;
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                while (bytesRemaining > 0)
                {
                    var readSize = Math.Min(buffer.Length, bytesRemaining);
                    var bytesRead = await input.ReadAsync(buffer, 0, readSize);
                    if (bytesRead == 0)
                        throw new EndOfStreamException("Unexpected end of stream.");
                    await outputStream.WriteAsync(buffer, 0, bytesRead);
                    bytesRemaining -= bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public class TORHeader
    {
        public int MagicNumber { get; init; }
        public int Version { get; init; }
        public int Endianness { get; init; }
        public long IndexOffset { get; init; }
        public int Capacity { get; init; }
        public int FileCount { get; init; }

        public static TORHeader ReadHeader(BinaryReader reader, string archiveName)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            return new TORHeader
            {
                MagicNumber = reader.ReadInt32(),
                Version = reader.ReadInt32(),
                Endianness = reader.ReadInt32(),
                IndexOffset = reader.ReadInt64(),
                Capacity = reader.ReadInt32(),
                FileCount = reader.ReadInt32()
            };
        }
    }

    public class HashDictionary
    {
        private readonly ConcurrentDictionary<ulong, string> _dictionary = new();

        public void Add(ulong hash, string path) => _dictionary[hash] = path;
        public bool TryGetPath(ulong hash, out string path) =>
            _dictionary.TryGetValue(hash, out path);
    }
}