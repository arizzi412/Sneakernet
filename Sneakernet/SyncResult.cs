using System;

namespace SneakerNetSync
{
    // NEW: For returning detailed results to the UI
    public class SyncResult
    {
        public int FilesCopied { get; set; }
        public int FilesMoved { get; set; }
        public int FilesDeleted { get; set; }
        public long BytesTransferred { get; set; }
        public int Errors { get; set; }
    }
}