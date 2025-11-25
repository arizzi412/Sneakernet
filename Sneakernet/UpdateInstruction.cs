using System;

namespace SneakerNetSync;


public class UpdateInstruction
{
    // Properties used by DataGridView
    public string Action { get; set; } // COPY, MOVE, DELETE
    public string Source { get; set; }
    public string Destination { get; set; }
    public string SizeInfo { get; set; } // e.g. "50 MB"

    // Internal data
    public long RawSizeBytes { get; set; }
}