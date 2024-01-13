namespace CompilerRealTime.Models
{
    public class CompilationUpdate
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }
}
