namespace DirectoryMonitor.Business.Data
{
    public class DirectoryIterationData
    {
        // public int Level { get; set; }

        public string Path { get; set; }

        // public DirectoryIterationData? Parent { get; set; }
    }

    public class DirectoryIterationData<T> where T : class
    {
        // public int Level { get; set; }

        public string Path { get; set; }

        // public DirectoryIterationData? Parent { get; set; }

        public T? Data { get; set; }
    }
}