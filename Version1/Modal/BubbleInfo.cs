namespace SQCScanner.Modal
{
    public class BubbleInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public string? ReadingDirection { get; set; }
        public double? bubbleIntensity {  get; set; }
        public List<string>? Options { get; set; }
    }
}
