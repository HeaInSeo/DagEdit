using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using DynamicData;

namespace DagEdit
{
    public sealed class Dag : IDisposable
    {
        private readonly SourceList<DagItems> _dagItemsSource = new();
        private readonly ReadOnlyObservableCollection<DagItems> _readOnlyItems;
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Guid, DagNode> _nodeIndex = new();
        private bool _disposed;

        public Dag()
        {
            _dagItemsSource
                .Connect()
                .Bind(out _readOnlyItems)
                .Subscribe()
                .DisposeWith(_disposables);
        }

        public ReadOnlyObservableCollection<DagItems> DAGItemsSource => _readOnlyItems;

        public IObservable<IChangeSet<DagItems>> Connect()
        {
            ThrowIfDisposed();
            return _dagItemsSource.Connect();
        }

        // ─── Add ──────────────────────────────────────────────────────────────

        /// <summary>커넥션을 추가한다. 실패 시 null 반환.</summary>
        public DagItems? AddDagConnectionItem(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId)
        {
            ThrowIfDisposed();

            if (source is null || target is null)
            {
                return null;
            }

            var newItem = new DagItems();
            newItem.CreateDagConnection(source, sourceNodeId, target, targetNodeId);

            var connection = newItem.ConnectionItem!;
            var sourceNode = sourceNodeId.HasValue ? FindNode(sourceNodeId.Value) : null;
            var targetNode = targetNodeId.HasValue ? FindNode(targetNodeId.Value) : null;
            sourceNode?.SourceConnections.Add(connection);
            targetNode?.TargetConnections.Add(connection);

            _dagItemsSource.Add(newItem);
            return newItem;
        }

        /// <summary>노드를 추가한다. 실패 시 null 반환.</summary>
        public DagItems? AddDagNodeItem(Point? location)
        {
            ThrowIfDisposed();

            if (!location.HasValue)
            {
                return null;
            }

            var newItem = new DagItems();
            newItem.CreateDagNode(location);
            _nodeIndex[newItem.NodeItem!.NodeId!.Value] = newItem.NodeItem;
            _dagItemsSource.Add(newItem);
            return newItem;
        }

        // ─── Delete ───────────────────────────────────────────────────────────

        public bool DelDagConnectionItem(Guid? connectionId)
        {
            ThrowIfDisposed();

            var itemToDelete = _dagItemsSource.Items
                .FirstOrDefault(i => i.ConnectionItem?.ConnectionId == connectionId);
            if (itemToDelete == null)
            {
                return false;
            }

            // 노드의 연결 목록에서도 제거하여 일관성 유지
            var conn = itemToDelete.ConnectionItem!;
            var sourceNode = conn.SourceNodeId.HasValue ? FindNode(conn.SourceNodeId.Value) : null;
            var targetNode = conn.TargetNodeId.HasValue ? FindNode(conn.TargetNodeId.Value) : null;
            sourceNode?.SourceConnections.Remove(conn);
            targetNode?.TargetConnections.Remove(conn);

            _dagItemsSource.Remove(itemToDelete);
            return true;
        }

        public bool DelDagNodeItem(Guid? nodeId)
        {
            ThrowIfDisposed();

            // 일부러 여기서는 ?. 안씀. 명시적으로 null 체크 함.
            var itemToDelete = _dagItemsSource.Items.FirstOrDefault(i => i.NodeItem != null && i.NodeItem.NodeId == nodeId);
            if (itemToDelete != null)
            {
                if (itemToDelete.NodeItem!.NodeId.HasValue)
                {
                    // 연결된 모든 Connection 먼저 삭제
                    var connectionsToRemove = new List<DagConnection>();
                    connectionsToRemove.AddRange(itemToDelete.NodeItem.SourceConnections);
                    connectionsToRemove.AddRange(itemToDelete.NodeItem.TargetConnections);
                    foreach (var conn in connectionsToRemove)
                    {
                        DelDagConnectionItem(conn.ConnectionId);
                    }

                    // 모델에서 먼저 제거하고, UI 참조는 후처리로 끊는다.
                    _nodeIndex.Remove(nodeId!.Value);
                    _dagItemsSource.Remove(itemToDelete);
                    itemToDelete.NodeItem.NodeInstance = null; // GC 를 위한 참조 해제
                    return true; // 삭제 성공
                }
            }

            return false; // 매칭되는 아이템이 없어서 삭제 실패
        }

        // ─── Undo 보조 (Remove / Restore) ────────────────────────────────────

        /// <summary>
        /// Add 명령의 Undo. 캐스케이드 없이 해당 아이템만 SourceList에서 제거한다.
        /// 커넥션인 경우 노드의 연결 목록도 정리한다.
        /// </summary>
        public bool RemoveDagItem(DagItems item)
        {
            ThrowIfDisposed();

            if (item.ConnectionItem is { } conn)
            {
                var sourceNode = conn.SourceNodeId.HasValue ? FindNode(conn.SourceNodeId.Value) : null;
                var targetNode = conn.TargetNodeId.HasValue ? FindNode(conn.TargetNodeId.Value) : null;
                sourceNode?.SourceConnections.Remove(conn);
                targetNode?.TargetConnections.Remove(conn);
            }
            else if (item.NodeItem is { } node && node.NodeId.HasValue)
            {
                _nodeIndex.Remove(node.NodeId.Value);
            }

            _dagItemsSource.Remove(item);
            return true;
        }

        /// <summary>
        /// Del 명령의 Undo. 이전에 삭제된 노드 DagItems를 SourceList에 복원한다.
        /// NodeInstance = null 로 설정하여 CreateContainerForItemOverride 가 새 컨트롤을 생성하게 한다.
        /// </summary>
        public bool RestoreDagNodeItem(DagItems item)
        {
            ThrowIfDisposed();

            if (item.NodeItem?.NodeId == null)
            {
                return false;
            }

            item.NodeItem.NodeInstance = null; // 이전 컨트롤은 이미 제거됨. 재생성 강제.
            _nodeIndex[item.NodeItem.NodeId.Value] = item.NodeItem;
            _dagItemsSource.Add(item);
            return true;
        }

        /// <summary>
        /// Del 명령의 Undo. 이전에 삭제된 커넥션 DagItems를 복원한다.
        /// 노드의 SourceConnections / TargetConnections 목록에도 다시 등록한다.
        /// </summary>
        public bool RestoreDagConnectionItem(DagItems item)
        {
            ThrowIfDisposed();

            if (item.ConnectionItem == null)
            {
                return false;
            }

            var conn = item.ConnectionItem;
            conn.ConnectionInstance = null; // 재생성 강제

            var sourceNode = conn.SourceNodeId.HasValue ? FindNode(conn.SourceNodeId.Value) : null;
            var targetNode = conn.TargetNodeId.HasValue ? FindNode(conn.TargetNodeId.Value) : null;
            sourceNode?.SourceConnections.Add(conn);
            targetNode?.TargetConnections.Add(conn);

            _dagItemsSource.Add(item);
            return true;
        }

        // ─── Query ────────────────────────────────────────────────────────────

        public DagNode? FindNode(Guid nodeId) =>
            _nodeIndex.TryGetValue(nodeId, out var node) ? node : null;

        /// <summary>nodeId에 해당하는 DagItems를 반환한다.</summary>
        public DagItems? GetDagItemForNode(Guid nodeId) =>
            !_disposed
                ? _dagItemsSource.Items.FirstOrDefault(i => i.NodeItem?.NodeId == nodeId)
                : throw new ObjectDisposedException(nameof(Dag));

        /// <summary>nodeId와 연결된 모든 커넥션 DagItems를 반환한다.</summary>
        public List<DagItems> GetConnectionItemsForNode(Guid nodeId)
        {
            ThrowIfDisposed();
            var result = new List<DagItems>();
            foreach (var item in _dagItemsSource.Items)
            {
                if (item.ConnectionItem is { } conn &&
                    (conn.SourceNodeId == nodeId || conn.TargetNodeId == nodeId))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposables.Dispose();
            _dagItemsSource.Dispose();
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
