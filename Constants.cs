using Avalonia;

namespace DagEdit
{
    public static class Constants
    {
        public const double AppliedThreshold = 12d * 12d;
        public const double NodeWidth = 200d;
        public const double NodeHeight = 124d;

        public static readonly Point ZeroPoint = new(0, 0);
        public static readonly Vector ZeroVector = new(0d, 0d);
        public static readonly Size DefaultArrowSize = new(7, 6);
    }
}
