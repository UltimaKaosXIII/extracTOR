using System.IO;
using ZstdSharp.Unsafe;

namespace extracTOR
{
    public class FileEntry
    {
        public long DataOffset { get; set; }
        public int CompressedSize { get; set; }
        public int DecompressedSize { get; set; }
        public ulong Hash { get; set; }
        public uint CRC { get; set; }
        public short CompressionMethod { get; set; }
        public bool IsCompressed => CompressionMethod == 1;

        public static (FileEntry?, long) ReadEntry(BinaryReader br, string arc, long nextTableOffset)
        {
            bool verified = false;
            var offset = br.ReadInt64();
            var offsetAdjust = br.ReadInt32();
            long pos = br.BaseStream.Position;
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            byte[] checkBytes = br.ReadBytes(4);
            if (checkBytes[0] == 2 && checkBytes[1] == 0 && checkBytes[2] == 32 && checkBytes[3] == 0)
            {
                verified = true;
                br.BaseStream.Seek(pos, SeekOrigin.Begin);
            }
            if (!verified)
                return (null, 0);
            FileEntry entry = new FileEntry();
            entry.DataOffset = offset + offsetAdjust;
            entry.CompressedSize = br.ReadInt32();
            entry.DecompressedSize = br.ReadInt32();
            entry.Hash = br.ReadUInt64();
            entry.CRC = br.ReadUInt32();
            entry.CompressionMethod = br.ReadInt16();
            var nextEntry = br.BaseStream.Position;
            return (entry, nextEntry);
        }
    }
}
