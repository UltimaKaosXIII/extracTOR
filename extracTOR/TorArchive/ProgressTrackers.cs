namespace extracTOR
{
    // Progress callbacks reported by FileExtractor. Shared by the WPF (Windows) and Avalonia (Linux) front-ends.
    public class ProgressTrackers
    {
        public IProgress<double> ExtractionProgress { get; set; }
        public IProgress<string> TimeProgress { get; set; }
        public IProgress<long> MemoryProgress { get; set; }
        public IProgress<string> TotalFilesProgress { get; set; }
        public IProgress<string> ExtractedFilesProgress { get; set; }
        public IProgress<int> ArcsProgress { get; set; }
    }
}
