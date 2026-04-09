using System;
using Avalonia;

namespace DagEdit
{
    public enum DagItemsType
    {
        // Connection 도 일단 1개 이상일지 생각해야함.
        // Node 역시 3개 이상일지 생각해야함.
        StartNode,
        EndNode,
        RunnerNode,
        Connection,
    }

    public class DagItems
    {
        #region Fields

        private DagNode? _nodeItem;

        private DagConnection? _connectionItem;

        #endregion

        #region Constructor

        public DagItems()
        {
        }

        #endregion

        #region Properties

        public DagNode? NodeItem
        {
            get => _nodeItem;
            set => _nodeItem = value;
        }

        public DagConnection? ConnectionItem
        {
            get => _connectionItem;
            set => _connectionItem = value;
        }

        #endregion

        #region Methods

        public void CreateDagConnection(
            Point? sourceAnchor,
            Guid? sourceNodeId,
            Point? targetAnchor,
            Guid? targetNodeId)
        {
            _connectionItem = new DagConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceAnchor = sourceAnchor,
                TargetAnchor = targetAnchor,
                SourceNodeId = sourceNodeId,
                TargetNodeId = targetNodeId,
                DAGItemType = DagItemsType.Connection,
            };
        }

        public void CreateDagNode(Point? location)
        {
            _nodeItem = new DagNode
            {
                NodeId = Guid.NewGuid(),
                Location = location,
                DAGItemType = DagItemsType.RunnerNode,
            };
        }

        #endregion
    }

}
