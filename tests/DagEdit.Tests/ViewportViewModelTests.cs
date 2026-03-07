namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// DagEditorViewModel의 Viewport 상태 (Feature 3) 단위 테스트.
///
/// ViewportLocation/ViewportScale이 ViewModel의 반응형 프로퍼티로 관리됨을 검증한다.
/// </summary>
public class ViewportViewModelTests
{
    [Fact]
    public void ViewportLocation_DefaultIsZero()
    {
        using var vm = new DagEditorViewModel();

        Assert.Equal(Constants.ZeroPoint, vm.ViewportLocation);
    }

    [Fact]
    public void ViewportScale_DefaultIsOne()
    {
        using var vm = new DagEditorViewModel();

        Assert.Equal(1.0, vm.ViewportScale);
    }

    [Fact]
    public void ViewportLocation_CanBeUpdated()
    {
        using var vm = new DagEditorViewModel();

        vm.ViewportLocation = new Point(100, 50);

        Assert.Equal(new Point(100, 50), vm.ViewportLocation);
    }

    [Fact]
    public void ViewportScale_CanBeUpdated()
    {
        using var vm = new DagEditorViewModel();

        vm.ViewportScale = 2.5;

        Assert.Equal(2.5, vm.ViewportScale);
    }

    [Fact]
    public void ViewportLocation_RaisesPropertyChanged()
    {
        using var vm = new DagEditorViewModel();
        bool raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DagEditorViewModel.ViewportLocation))
                raised = true;
        };

        vm.ViewportLocation = new Point(10, 20);

        Assert.True(raised);
    }

    [Fact]
    public void ViewportScale_RaisesPropertyChanged()
    {
        using var vm = new DagEditorViewModel();
        bool raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DagEditorViewModel.ViewportScale))
                raised = true;
        };

        vm.ViewportScale = 1.5;

        Assert.True(raised);
    }

    [Fact]
    public void ViewportLocation_CanBeDecremented_SimulatingPanning()
    {
        using var vm = new DagEditorViewModel();
        vm.ViewportLocation = new Point(200, 100);

        // 패닝: ViewportLocation -= delta
        var delta = new Vector(50, 30);
        vm.ViewportLocation -= delta;

        Assert.Equal(new Point(150, 70), vm.ViewportLocation);
    }
}
