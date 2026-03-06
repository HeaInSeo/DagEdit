using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using DynamicData;
using DynamicData.Binding;

namespace DagEdit
{
    public sealed class Dag : IDisposable
    {
        private readonly SourceList<DagItems> _dagItemsSource = new();
        private readonly ReadOnlyObservableCollection<DagItems> _readOnlyItems;
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Guid, DagNode> _nodeIndex = new();

        public ReadOnlyObservableCollection<DagItems> DAGItemsSource => _readOnlyItems;

        public IObservable<IChangeSet<DagItems>> Connect() => _dagItemsSource.Connect();

        public Dag()
        {
            _dagItemsSource
                .Connect()
                .Bind(out _readOnlyItems)
                .Subscribe()
                .DisposeWith(_disposables);
        }

        public bool AddDagConnectionItem(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId)
        {
            if (source is null || target is null)
            {
                return false;
            }

            var newItem = new DagItems();
            newItem.CreateDagConnection(source, sourceNodeId, target, targetNodeId);

            var connection = newItem.ConnectionItem!;
            var sourceNode = sourceNodeId.HasValue ? FindNode(sourceNodeId.Value) : null;
            var targetNode = targetNodeId.HasValue ? FindNode(targetNodeId.Value) : null;
            sourceNode?.SourceConnections.Add(connection);
            targetNode?.TargetConnections.Add(connection);

            _dagItemsSource.Add(newItem);
            return true;
        }

        public DagNode? FindNode(Guid nodeId) =>
            _nodeIndex.TryGetValue(nodeId, out var node) ? node : null;

        public bool DelDagConnectionItem(Guid? connectionId)
        {
            var itemToDelete = _dagItemsSource.Items
                .FirstOrDefault(i => i.ConnectionItem?.ConnectionId == connectionId);
            if (itemToDelete == null)
            {
                return false;
            }

            _dagItemsSource.Remove(itemToDelete);
            return true;
        }

        public bool AddDagNodeItem(Point? location)
        {
            if (!location.HasValue)
            {
                return false;
            }

            var newItem = new DagItems();
            newItem.CreateDagNode(location);
            _nodeIndex[newItem.NodeItem!.NodeId!.Value] = newItem.NodeItem;
            _dagItemsSource.Add(newItem);
            return true;
        }

        public bool DelDagNodeItem(Guid? NodeId)
        {
            // 일부러 여기서는 ?. 안씀. 명시적으로 null 체크 함.
            var itemToDelete = _dagItemsSource.Items.FirstOrDefault(i => i.NodeItem != null && i.NodeItem.NodeId == NodeId);
            if (itemToDelete != null)
            {
                // NodeInstance 가 null 인 상태에서 삭제하는 것은 위험하다.
                if (itemToDelete.NodeItem!.NodeInstance != null)
                {
                    // 연결된 모든 Connection 먼저 삭제
                    var connectionsToRemove = new List<DagConnection>();
                    connectionsToRemove.AddRange(itemToDelete.NodeItem.SourceConnections);
                    connectionsToRemove.AddRange(itemToDelete.NodeItem.TargetConnections);
                    foreach (var conn in connectionsToRemove)
                    {
                        DelDagConnectionItem(conn.ConnectionId);
                    }

                    // SourceList에서 제거 → Avalonia가 비주얼 트리에서 컨테이너를 제거
                    // → BaseNode.Unloaded 핸들러가 Node.Dispose()를 직접 호출
                    // 모델 계층이 UI lifecycle을 직접 건드리지 않는다.
                    _nodeIndex.Remove(NodeId!.Value);
                    _dagItemsSource.Remove(itemToDelete);
                    itemToDelete.NodeItem.NodeInstance = null; // GC 를 위한 참조 해제
                    return true; // 삭제 성공
                }
            }

            return false; // 매칭되는 아이템이 없어서 삭제 실패
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
