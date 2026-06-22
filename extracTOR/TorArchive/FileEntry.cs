using System.IO;

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

        public static FileEntry/*(FileEntry, long)*/ ReadEntry(BinaryReader br)
        {
            bool validEntry = false;
            var offset = br.ReadInt64();
            var offsetAdjust = br.ReadInt32();

            long pos = br.BaseStream.Position;
            br.BaseStream.Position = offset;
            byte[] check = br.ReadBytes(4);

            if (check[0] == 2 && check[1] == 0 && check[2] == 0x20 && check[3] == 0)
            {
                validEntry = true;
                br.BaseStream.Position = pos;
            }
            else return null;

            FileEntry entry = new FileEntry();
            entry.DataOffset = offset + offsetAdjust;
            entry.CompressedSize = br.ReadInt32();
            entry.DecompressedSize = br.ReadInt32();
            entry.Hash = br.ReadUInt64();
            entry.CRC = br.ReadUInt32();
            entry.CompressionMethod = br.ReadInt16();
            var nextEntry = br.BaseStream.Position;
            return entry;
        }
    }
}
