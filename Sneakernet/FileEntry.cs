using System;

namespace SneakerNetSync
{
    public class FileEntry
    {
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public DateTime LastWriteTime { get; set; }

        public override string ToString()
        {
            return RelativePath;
        }
    }
}