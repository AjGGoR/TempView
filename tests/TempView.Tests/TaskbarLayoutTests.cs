using TempView.Core;
using Xunit;

namespace TempView.Tests;

public sealed class TaskbarLayoutTests
{
    [Theory]
    [InlineData("left", 0)]
    [InlineData("right", 900)]
    public void EmptyBarUsesRequestedEdge(string side, int expected) =>
        Assert.Equal(expected, TaskbarLayout.TryFindGap(0, 1000, 100, side, []));

    [Theory]
    [InlineData("left", 100)]
    [InlineData("right", 650)]
    public void ReservesButtonsAndNotificationArea(string side, int expected) =>
        Assert.Equal(expected, TaskbarLayout.TryFindGap(0, 1000, 150, side, [(0,100),(300,600),(800,1000)]));

    [Theory]
    [InlineData("left", -1800)]
    [InlineData("right", -250)]
    public void NegativeCoordinatesArePreserved(string side, int expected) =>
        Assert.Equal(expected, TaskbarLayout.TryFindGap(-1920, 0, 100, side, [(-1920,-1800),(-150,0)]));

    [Fact]
    public void OverlappingNestedAndAdjacentIntervalsAreMerged() =>
        Assert.Equal(450, TaskbarLayout.TryFindGap(0, 1000, 100, "left", [(200,400),(0,200),(40,100),(350,450),(900,1000)]));

    [Fact]
    public void OccupiedAreaOutsideBoundsIsClipped() =>
        Assert.Equal(50, TaskbarLayout.TryFindGap(0, 500, 100, "left", [(-500,50),(400,900),(1000,2000)]));

    [Fact]
    public void ExactFitIsAccepted() =>
        Assert.Equal(100, TaskbarLayout.TryFindGap(0, 300, 100, "right", [(0,100),(200,300)]));

    [Fact]
    public void NoSpaceReturnsNull()
    {
        Assert.Null(TaskbarLayout.TryFindGap(0, 300, 101, "left", [(0,100),(200,300)]));
        Assert.Null(TaskbarLayout.TryFindGap(0, 300, 100, "right", [(-100,400)]));
        Assert.Null(TaskbarLayout.TryFindGap(0, 300, 301, "left", []));
        Assert.Null(TaskbarLayout.TryFindGap(0, 300, 0, "left", []));
    }

    [Fact]
    public void EmptyOrReversedOccupiedIntervalsHaveNoArea() =>
        Assert.Equal(0, TaskbarLayout.TryFindGap(0, 300, 100, "left", [(20,20),(200,100)]));

    [Fact]
    public void ExtremeCoordinatesDoNotOverflow() =>
        Assert.Equal(int.MaxValue - 100, TaskbarLayout.TryFindGap(int.MinValue, int.MaxValue, 100, "right", []));

    [Fact]
    public void UnknownSideIsRejected() =>
        Assert.Throws<ArgumentException>(() => TaskbarLayout.TryFindGap(0, 300, 100, "top", []));
}
