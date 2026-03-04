using Avalonia;
using ReactiveUI;

namespace DagEdit
{
    /// <summary>
    /// PendingConnection의 드래그 미리보기 상태를 보관하는 ReactiveObject.
    ///
    /// ─── ReactiveUI WhenAnyValue 패턴 (PendingConnection 좌표 업데이트) ──────────
    ///
    /// [What it does]
    /// DagEditor가 SourceAnchor/TargetAnchor AvaloniaProperty를 설정하면
    /// PendingConnection이 이 상태를 거쳐 PART_Connection의 Source/Target을
    /// 반응형으로 갱신한다.
    ///
    /// 입력(AvaloniaProperty 변경)과 출력(PART_Connection 갱신)을 이 클래스로
    /// 분리함으로써 단위 테스트 시 UI 없이 상태 전이만 검증할 수 있다.
    ///
    /// [Go Analogy]
    ///   PendingConnectionState.SourceAnchor = 채널(chan Point)
    ///   AvaloniaProperty.Changed 핸들러   = 채널에 값을 보내는 고루틴 (ch &lt;- newPt)
    ///   WhenAnyValue 구독                 = 채널에서 값을 읽는 고루틴 (for pt := range ch)
    ///
    /// [확장 포인트]
    /// 향후 스냅-투-커넥터(EnableSnapping) 로직, 연결 가능 여부 검사,
    /// DistinctUntilChanged 튜닝 등이 이 클래스에 추가된다.
    /// </summary>
    internal sealed class PendingConnectionState : ReactiveObject
    {
        private Point _sourceAnchor;
        private Point _targetAnchor;

        /// <summary>
        /// 미리보기 연결선의 시작점(SourceConnector 앵커).
        /// 값이 변경될 때마다 WhenAnyValue 구독자에게 새 값이 전파된다.
        /// </summary>
        public Point SourceAnchor
        {
            get => _sourceAnchor;
            set => this.RaiseAndSetIfChanged(ref _sourceAnchor, value);
        }

        /// <summary>
        /// 미리보기 연결선의 끝점(포인터 위치 또는 TargetConnector 앵커).
        /// 값이 변경될 때마다 WhenAnyValue 구독자에게 새 값이 전파된다.
        /// </summary>
        public Point TargetAnchor
        {
            get => _targetAnchor;
            set => this.RaiseAndSetIfChanged(ref _targetAnchor, value);
        }
    }
}
