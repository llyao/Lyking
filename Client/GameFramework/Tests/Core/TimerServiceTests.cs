using Lyking.GameFramework.Client.Core.Scheduling;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Core;

public sealed class TimerServiceTests
{
    private static TimerService CreateService() => new();

    [Fact]
    public void 一次性计时到期触发一次()
    {
        var timer = CreateService();
        int fired = 0;
        timer.Schedule(TimeSpan.FromSeconds(1), () => fired++);

        timer.Update(0.5);
        Assert.Equal(0, fired);
        timer.Update(0.5);
        Assert.Equal(1, fired);
        timer.Update(0.5);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void 重复计时按间隔触发()
    {
        var timer = CreateService();
        int fired = 0;
        timer.ScheduleRepeating(TimeSpan.FromSeconds(1), () => fired++);

        timer.Update(1.0);
        Assert.Equal(1, fired);
        timer.Update(1.0);
        Assert.Equal(2, fired);
        timer.Update(0.99);
        Assert.Equal(2, fired);
    }

    [Fact]
    public void 取消后不再触发()
    {
        var timer = CreateService();
        int fired = 0;
        var handle = timer.Schedule(TimeSpan.FromSeconds(1), () => fired++);

        handle.Cancel();
        timer.Update(2.0);

        Assert.Equal(0, fired);
        Assert.True(handle.IsCancelled);
    }

    [Fact]
    public void 回调内取消自身与新增计时器都安全()
    {
        var timer = CreateService();
        int innerFired = 0;
        ITimerHandle? self = null;

        self = timer.Schedule(TimeSpan.FromSeconds(0.5), () =>
        {
            self!.Cancel();
            timer.Schedule(TimeSpan.FromSeconds(0.25), () => innerFired++);
        });

        timer.Update(0.5);
        Assert.True(self.IsCancelled);

        // 新增的 0.25s 计时器在下一帧（0.5s 后）触发。
        timer.Update(0.5);
        Assert.Equal(1, innerFired);
    }

    [Fact]
    public void 重复计时器可被取消()
    {
        var timer = CreateService();
        int fired = 0;
        var handle = timer.ScheduleRepeating(TimeSpan.FromSeconds(0.5), () => fired++);

        timer.Update(0.5);
        handle.Cancel();
        timer.Update(0.5);
        timer.Update(0.5);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void 重复间隔必须为正()
    {
        var timer = CreateService();
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.ScheduleRepeating(TimeSpan.Zero, () => { }));
    }
}
