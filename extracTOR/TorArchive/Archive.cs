using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq.Expressions;
using ZstdSharp;

namespace extracTOR
{
    public class TORArchive
    {
        public TORHeader Header { get; private set; }
        public TORArchive(BinaryReader br, string arc)
        {
            Header = TORHeader.ReadHeader(br, arc);
        }
        public async Task ExtractEntriesAsync(
            BinaryReader br,
            string arc,
            string archiveFile,
            bool pts,
            bool beta,
            Dictionary<ulong, string> hashList,
            string outPath,
            Action reportProgress,
            List<string> hashes)
        {
            try
            {
                br.BaseStream.Seek(Header.IndexOffset, SeekOrigin.Begin);
                var nextTableOffset = Header.IndexOffset;
                while (nextTableOffset != 0 && br.BaseStream.Position < br.BaseStream.Length)
                {
                    // read table info
                    int tableCapacity = br.ReadInt32(); // read table capacity
                    nextTableOffset = br.ReadInt64(); // read next table offset
                    for (int i = 0; i < tableCapacity; ++i)
                    {
                        (FileEntry? entry, long nextPos) = FileEntry.ReadEntry(br, arc, nextTableOffset);
                        if (entry == null && nextTableOffset == 0)
                        {
                            break;
                        }
                        else
                        {

                            string output = @"C:\Users\Ben\Desktop\extracTOR_output.txt";
                            if (entry != null && entry.Hash != 0)
                            {
                                if (hashList.TryGetValue(entry.Hash, out var hashPath))
                                {
                                    if (hashPath.Contains("metadata.bin") || hashPath.Contains("ft.sig") || hashPath.Contains("groupmanifest.bin"))
                                    {
                                        hashPath = $"{arc}_{hashPath}";
                                    }
                                    hashes.Add($"{entry.Hash:X}#{hashPath}#DEADBEEF");
                                }
                                else
                                {
                                    hashes.Add($"{entry.Hash:X}##DEADBEEF");
                                }
                                Debug.WriteLine($"Closing sw");
                            }
                            if (nextPos != 0)
                                br.BaseStream.Seek(nextPos, SeekOrigin.Begin);
                            else continue;
                        }
                    }
                    if (nextTableOffset == 0)
                        break;
                    else
                        br.BaseStream.Seek(nextTableOffset, SeekOrigin.Begin);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception caught: {ex.Message}\n{ex.StackTrace}");
                Debugger.Break();
                //throw;
            }
        }
    }
    public class TORHeader
    {
        public int Magic { get; set; }
        public int Version { get; set; }
        public int Endian { get; set; }
        public long IndexOffset { get; set; }
        public int Capacity { get; set; }
        public int FileCount { get; set; }
        public static TORHeader ReadHeader(BinaryReader br, string arc)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin); //read from 0x0
            var header = new TORHeader()
            {
                Magic = br.ReadInt32(),
                Version = br.ReadInt32(),
                Endian = br.ReadInt32(),
                IndexOffset = br.ReadInt64(),
                Capacity = br.ReadInt32(),
                FileCount = br.ReadInt32()
            };
            return header;
        }
    }
}