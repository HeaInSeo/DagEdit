using System;

namespace DagEdit
{
    internal sealed class ProjectionChangedSubscription
    {
        private DagViewerProjectionAdapter? _adapter;
        private EventHandler? _handler;

        public void Attach(DagViewerProjectionAdapter adapter, EventHandler handler)
        {
            if (ReferenceEquals(_adapter, adapter) && ReferenceEquals(_handler, handler))
            {
                return;
            }

            Detach();
            _adapter = adapter;
            _handler = handler;
            _adapter.ProjectionChanged += _handler;
        }

        public void Detach()
        {
            if (_adapter != null && _handler != null)
            {
                _adapter.ProjectionChanged -= _handler;
            }

            _adapter = null;
            _handler = null;
        }
    }
}
