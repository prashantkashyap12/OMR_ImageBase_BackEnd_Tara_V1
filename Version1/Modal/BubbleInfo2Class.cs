using OpenCvSharp;

namespace SQCScanner.Modal
{
    public class BubbleInfo2Class
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Point2f Center => new Point2f(X + Width / 2f, Y + Height / 2f);

    }
}
