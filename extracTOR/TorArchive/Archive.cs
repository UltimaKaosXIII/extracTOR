using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32.SafeHandles;
using ZstdSharp;

namespace extracTOR
{
    public class TORArchive
    {
        public TORHeader Header { get; }

        // Size of one entry in an index table: offset(8) headerSize(4) compressed(4) decompressed(4) hash(8) crc(4) method(2)
        private const int EntrySize = 34;
        // Size of an index table header: capacity(4) nextTableOffset(8)
        private const int TableHeaderSize = 12;

        public TORArchive(BinaryReader reader, string archiveName)
        {
            Header = TORHeader.ReadHeader(reader, archiveName);
        }

        public TORArchive(SafeFileHandle handle)
        {
            Header = TORHeader.ReadHeader(handle);
        }

        /// <summary>
        /// Reads every index table in the archive and returns the entries that should be extracted,
        /// sorted by their position in the archive so the data can then be read front-to-back
        /// (one long sequential read instead of a seek per file - this matters a lot on HDDs).
        /// </summary>
        public List<FileEntry> ReadEntries(
            SafeFileHandle handle,
            HashDictionary hashDictionary,
            string archiveFileName,
            Func<string, string, bool> shouldProcessFile)
        {
            var entries = new List<FileEntry>(Math.Max(0, Header.FileCount));
            var tableHeader = new byte[TableHeaderSize];
            byte[] table = Array.Empty<byte>();

            long nextTableOffset = Header.IndexOffset;
            while (nextTableOffset != 0)
            {
                ReadExactly(handle, tableHeader, nextTableOffset);
                int capacity = BinaryPrimitives.ReadInt32LittleEndian(tableHeader);
                long tableDataOffset = nextTableOffset + TableHeaderSize;
                nextTableOffset = BinaryPrimitives.ReadInt64LittleEndian(tableHeader.AsSpan(4));

                int tableBytes = capacity * EntrySize;
                if (table.Length < tableBytes) table = new byte[tableBytes];
                ReadExactly(handle, table.AsSpan(0, tableBytes), tableDataOffset);

                for (int i = 0; i < capacity; i++)
                {
                    var e = table.AsSpan(i * EntrySize, EntrySize);
                    long offset = BinaryPrimitives.ReadInt64LittleEndian(e);

                    // Offset 0 marks an unused slot. Skip it but keep reading the table:
                    // used entries can follow empty ones.
                    if (offset == 0) continue;

                    ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(e.Slice(20));
                    if (!hashDictionary.TryGetPath(hash, out var relativePath) ||
                        !shouldProcessFile(relativePath, archiveFileName))
                        continue;

                    entries.Add(new FileEntry
                    {
                        DataOffset = offset + BinaryPrimitives.ReadInt32LittleEndian(e.Slice(8)),
                        CompressedSize = BinaryPrimitives.ReadInt32LittleEndian(e.Slice(12)),
                        DecompressedSize = BinaryPrimitives.ReadInt32LittleEndian(e.Slice(16)),
                        Hash = hash,
                        CRC = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(28)),
                        CompressionMethod = BinaryPrimitives.ReadInt16LittleEndian(e.Slice(32)),
                        RelativePath = relativePath,
                    });
                }
            }

            entries.Sort((a, b) => a.DataOffset.CompareTo(b.DataOffset));
            return entries;
        }

        /// <summary>
        /// Decompresses (if needed) one entry's data and writes it to <paramref name="outputPath"/>.
        /// <paramref name="source"/> holds exactly the entry's stored (possibly compressed) bytes.
        /// </summary>
        public static void WriteEntry(
            FileEntry entry,
            int archiveVersion,
            ReadOnlySpan<byte> source,
            Span<byte> scratch,
            Decompressor zstd,
            string outputPath)
        {
            ReadOnlySpan<byte> data;
            if (!entry.IsCompressed || entry.DecompressedSize == 0)
            {
                data = source;
            }
            else
            {
                int written = archiveVersion switch
                {
                    6 => zstd.Unwrap(source, scratch),
                    5 => InflateZlib(source, scratch),
                    _ => throw new NotSupportedException($"Unsupported archive version {archiveVersion}")
                };
                if (written != entry.DecompressedSize)
                    throw new InvalidDataException($"Decompressed {written} bytes, expected {entry.DecompressedSize}: {entry.RelativePath}");
                data = scratch[..written];
            }

            // One open + one write per file; no async/stream overhead for the many tiny files.
            using var file = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            if (!data.IsEmpty)
                RandomAccess.Write(file, data, 0);
        }

        /// <summary>
        /// Extracts one entry by streaming it from the archive to <paramref name="outputPath"/> in 1MB chunks.
        /// Used for very large files so they never have to fit in memory.
        /// </summary>
        public static void StreamEntry(SafeFileHandle archive, FileEntry entry, int archiveVersion, string outputPath)
        {
            const int ChunkSize = 1024 * 1024;
            using var source = new HandleRangeStream(archive, entry.DataOffset, entry.CompressedSize);
            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize);

            if (!entry.IsCompressed || entry.DecompressedSize == 0)
            {
                source.CopyTo(output, ChunkSize);
            }
            else
            {
                using Stream decompressor = archiveVersion switch
                {
                    6 => new DecompressionStream(source, ChunkSize, leaveOpen: true),
                    5 => new ZLibStream(source, CompressionMode.Decompress, leaveOpen: true),
                    _ => throw new NotSupportedException($"Unsupported archive version {archiveVersion}")
                };
                decompressor.CopyTo(output, ChunkSize);
            }

            if (output.Length != entry.DecompressedSize)
                throw new InvalidDataException($"Wrote {output.Length} bytes, expected {entry.DecompressedSize}: {entry.RelativePath}");
        }

        // Version 5 (zlib) archives are legacy; a copy of the input keeps this simple.
        private static int InflateZlib(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            using var input = new MemoryStream(source.ToArray(), writable: false);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            int total = 0, read;
            while (total < destination.Length && (read = inflater.Read(destination[total..])) > 0)
                total += read;
            return total;
        }

        public static void ReadExactly(SafeFileHandle handle, Span<byte> buffer, long fileOffset)
        {
            while (!buffer.IsEmpty)
            {
                int read = RandomAccess.Read(handle, buffer, fileOffset);
                if (read <= 0) throw new EndOfStreamException("Unexpected end of archive.");
                buffer = buffer[read..];
                fileOffset += read;
            }
        }
    }

    /// <summary>Read-only stream over a byte range of a file handle (does not own or close the handle).</summary>
    internal sealed class HandleRangeStream : Stream
    {
        private readonly SafeFileHandle _handle;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public HandleRangeStream(SafeFileHandle handle, long start, long length)
        {
            _handle = handle;
            _start = start;
            _length = length;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            long remaining = _length - _position;
            if (remaining <= 0) return 0;
            if (buffer.Length > remaining) buffer = buffer[..(int)remaining];
            int read = RandomAccess.Read(_handle, buffer, _start + _position);
            _position += read;
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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

        public static TORHeader ReadHeader(SafeFileHandle handle)
        {
            Span<byte> h = stackalloc byte[28];
            TORArchive.ReadExactly(handle, h, 0);
            return new TORHeader
            {
                MagicNumber = BinaryPrimitives.ReadInt32LittleEndian(h),
                Version = BinaryPrimitives.ReadInt32LittleEndian(h[4..]),
                Endianness = BinaryPrimitives.ReadInt32LittleEndian(h[8..]),
                IndexOffset = BinaryPrimitives.ReadInt64LittleEndian(h[12..]),
                Capacity = BinaryPrimitives.ReadInt32LittleEndian(h[20..]),
                FileCount = BinaryPrimitives.ReadInt32LittleEndian(h[24..]),
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
