// Avalonia Headless 테스트 애플리케이션 부트스트랩
//
// [현재 상태]
// - Avalonia.Headless.XUnit 11.0.0은 assembly-level 어트리뷰트를 지원하지 않는다.
//   (AvaloniaTestApplicationAttribute는 이후 버전에서 추가됨)
// - 현재 모든 테스트는 순수 모델 레이어 테스트([Fact])이므로 Avalonia 초기화 불필요.
//
// [향후 UI 테스트 추가 시]
// Avalonia.Headless.XUnit를 최신 버전으로 업그레이드 후 아래 주석을 해제:
//   [assembly: Avalonia.Headless.XUnit.AvaloniaTestApplication(typeof(DagEdit.Tests.TestApp))]
//
// [AvaloniaFact] 어트리뷰트를 사용하면 Avalonia dispatcher 스레드에서 테스트가 실행된다.

namespace DagEdit.Tests;

using Avalonia;
using Avalonia.Headless;

/// <summary>
/// Avalonia 헤드리스 테스트 애플리케이션.
/// UI 컨트롤을 직접 테스트하는 [AvaloniaFact] 테스트에서 사용한다.
/// </summary>
public class TestApp : Application
{
    /// <summary>
    /// Avalonia 애플리케이션을 헤드리스 모드로 구성한다.
    /// UseHeadlessDrawing = true: 실제 렌더링 없이 레이아웃/이벤트만 처리.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true,
            });
}
