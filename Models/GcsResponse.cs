namespace TodoProject.Models
{
    public class GcsResponse
    {
        public string Content { get; set; } = string.Empty;
        public string ETag { get; set; } = string.Empty;
    }
    public class ProjectList
    {
        public string Owner { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Values { get; set; } = string.Empty;
    }
    public class TodoSettings { public string FilePath { get; set; } = string.Empty; }

    public class TaskError
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
