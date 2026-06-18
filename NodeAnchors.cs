using Avalonia;

namespace DagEdit
{
    internal static class NodeAnchors
    {
        internal static (Point SourceAnchor, Point TargetAnchor) FindAnchors(Point location)
        {
            var sourceAnchor = new Point(location.X + Constants.NodeWidth, location.Y + (Constants.NodeHeight / 2));
            var targetAnchor = new Point(location.X, location.Y + (Constants.NodeHeight / 2));
            return (sourceAnchor, targetAnchor);
        }
    }
}
