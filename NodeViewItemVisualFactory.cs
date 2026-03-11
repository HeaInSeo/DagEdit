using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using VirtualCanvas.Avalonia.Factories;
using VirtualCanvas.Core.Spatial;

namespace DagEdit
{
    /// <summary>
    /// G-0 Viewer wiring — NodeViewItem → Avalonia Border 를 실현하는 최소 IVisualFactory.
    ///
    /// ─── 역할 ──────────────────────────────────────────────────────────────────
    /// Phase 1 PoC: NodeViewItem을 VCA에서 시각화하기 위한 최소 구현.
    /// Border를 생성하고 NodeViewItem.Bounds에 따라 크기를 설정한다.
    ///
    /// ─── stable reference 계약 ───────────────────────────────────────────────
    /// ISpatialItem reference를 키로 Control을 캐시한다.
    /// VCA는 같은 ISpatialItem object가 들어오면 같은 Control을 재사용한다.
    /// NodeViewItem.Bounds가 in-place로 변경되어도 Control을 재생성하지 않는다.
    ///
    /// ─── 미구현 / 다음 spike ─────────────────────────────────────────────────
    /// - 노드 타입별 외관 (RunnerNode, StartNode, EndNode)
    /// - 선택 상태 시각 피드백
    /// - 커넥션 viewer item
    /// </summary>
    internal sealed class NodeViewItemVisualFactory : IVisualFactory
    {
        private readonly Dictionary<ISpatialItem, Control> _pool = new();

        // ─── H-0 Observability counters ──────────────────────────────────────

        /// <summary>
        /// VCA가 factory.Realize를 호출해 새 Border를 생성한 횟수.
        /// VCA._visualMap에 없는 item에 대해서만 호출되므로:
        ///   - add: 1 증가
        ///   - move: 0 증가 (VCA가 _visualMap에서 기존 Control 재사용)
        ///   - pool hit(기존에 virtualize된 item 재실현): 0 증가 (pool에서 반환)
        /// </summary>
        public int RealizeNewCount { get; private set; }

        /// <summary>
        /// factory._pool에서 기존 Control을 반환한 횟수 (VCA가 virtualize 후 재실현).
        /// IsVirtualizing=False + stable ref 패턴에서는 일반적으로 0.
        /// </summary>
        public int RealizeHitCount { get; private set; }

        /// <summary>
        /// VCA가 factory.Virtualize를 호출한 횟수 (item이 snapshot에서 제거됨).
        /// remove 1회당 1 증가.
        /// </summary>
        public int VirtualizeCount { get; private set; }

        public void BeginRealize()
        {
        }

        public Control? Realize(ISpatialItem item, bool force)
        {
            if (item is not NodeViewItem nodeItem)
            {
                return null;
            }

            if (_pool.TryGetValue(nodeItem, out Control? existing))
            {
                RealizeHitCount++;
                return existing;
            }

            RealizeNewCount++;
            var border = new Border
            {
                Width = nodeItem.Bounds.Width,
                Height = nodeItem.Bounds.Height,
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x3A, 0x3A, 0x5C)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x88, 0x88, 0xCC)),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
            };

            _pool[nodeItem] = border;
            return border;
        }

        public bool Virtualize(Control visual)
        {
            VirtualizeCount++;
            return true;
        }

        public void EndRealize()
        {
        }
    }
}
