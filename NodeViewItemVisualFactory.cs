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
                return existing;
            }

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
            return true;
        }

        public void EndRealize()
        {
        }
    }
}
