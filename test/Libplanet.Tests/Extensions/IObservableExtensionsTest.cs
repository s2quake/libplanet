using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Extensions;

namespace Libplanet.Tests.Extensions;

public sealed class IObservableExtensionsTest
{
    [Fact]
    public async Task WaitAsync()
    {
        using var testClass = new TestClass();
        var task1 = testClass.Observable1.WaitAsync();
        var task2 = testClass.Observable2.WaitAsync();
        var task3 = testClass.Observable3.WaitAsync();
        testClass.Invoke1();
        testClass.Invoke2("test");
        testClass.Invoke3("test1", "test2");

        Assert.Equal(Unit.Default, await task1);
        Assert.Equal("test", await task2);
        Assert.Equal(("test1", "test2"), await task3);
    }

    [Fact]
    public async Task WaitAsync_Throw_WhenTokenCancelled()
    {
        using var testClass = new TestClass();
        using var cancellationTokenSource = new CancellationTokenSource(100);
        var task1 = testClass.Observable1.WaitAsync(cancellationTokenSource.Token);
        var task2 = testClass.Observable2.WaitAsync(cancellationTokenSource.Token);
        var task3 = testClass.Observable3.WaitAsync(cancellationTokenSource.Token);
        _ = testClass.Invoke1Async(TimeSpan.FromSeconds(5));
        _ = testClass.Invoke2Async("test", TimeSpan.FromSeconds(5));
        _ = testClass.Invoke3Async("test1", "test2", TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task1);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task2);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task3);
    }

    [Fact]
    public async Task WaitAsync_WithCancellationToken()
    {
        using var testClass = new TestClass();
        using var cancellationTokenSource = new CancellationTokenSource(3000);
        var task1 = testClass.Observable1.WaitAsync(cancellationTokenSource.Token);
        var task2 = testClass.Observable2.WaitAsync(cancellationTokenSource.Token);
        var task3 = testClass.Observable3.WaitAsync(cancellationTokenSource.Token);
        _ = testClass.Invoke1Async(TimeSpan.FromMilliseconds(100));
        _ = testClass.Invoke2Async("test", TimeSpan.FromMilliseconds(100));
        _ = testClass.Invoke3Async("test1", "test2", TimeSpan.FromMilliseconds(100));

        Assert.Equal(Unit.Default, await task1);
        Assert.Equal("test", await task2);
        Assert.Equal(("test1", "test2"), await task3);
    }

    [Fact]
    public async Task WaitAsync_WithPredicate()
    {
        using var testClass = new TestClass();
        using var cancellationTokenSource = new CancellationTokenSource(500);
        var cancellationToken = cancellationTokenSource.Token;
        var task1 = testClass.Observable1.WaitAsync(_ => true);
        var task2_1 = testClass.Observable2.WaitAsync(_ => true);
        var task2_2 = testClass.Observable2.WaitAsync(
            _ => _ is string s && s == "test", cancellationToken);
        var task2_3 = testClass.Observable2.WaitAsync(
            _ => _ is string s && s == "test1", cancellationToken);
        var task3_1 = testClass.Observable3.WaitAsync(_ => true);
        var task3_2 = testClass.Observable3.WaitAsync(
            _ => _ is (string s1, string s2) && s1 == "test1" && s2 == "test2", cancellationToken);
        var task3_3 = testClass.Observable3.WaitAsync(
            _ => _ is (string s1, string s2) && s1 == "test3" && s2 == "test4", cancellationToken);
        testClass.Invoke1();
        testClass.Invoke2("test");
        testClass.Invoke3("test1", "test2");
        Assert.Equal(Unit.Default, await task1);
        Assert.Equal("test", await task2_1);
        Assert.Equal("test", await task2_2);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task2_3);
        Assert.Equal(("test1", "test2"), await task3_1);
        Assert.Equal(("test1", "test2"), await task3_2);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task3_3);
    }

    [Fact]
    public async Task WaitAsync_WithPredicate_Throw_WhenTokenCancelled()
    {
        using var testClass = new TestClass();
        using var cancellationTokenSource = new CancellationTokenSource(100);
        var task1 = testClass.Observable1.WaitAsync(_ => true, cancellationTokenSource.Token);
        var task2 = testClass.Observable2.WaitAsync(_ => true, cancellationTokenSource.Token);
        var task3 = testClass.Observable3.WaitAsync(_ => true, cancellationTokenSource.Token);
        _ = testClass.Invoke1Async(TimeSpan.FromSeconds(5));
        _ = testClass.Invoke2Async("test", TimeSpan.FromSeconds(5));
        _ = testClass.Invoke3Async("test1", "test2", TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task1);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task2);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task3);
    }

    [Fact]
    public async Task WaitAsync_WithPredicate_WithCancellationToken()
    {
        using var testClass = new TestClass();
        using var cancellationTokenSource = new CancellationTokenSource(3000);
        var task1 = testClass.Observable1.WaitAsync(_ => true, cancellationTokenSource.Token);
        var task2 = testClass.Observable2.WaitAsync(_ => true, cancellationTokenSource.Token);
        var task3 = testClass.Observable3.WaitAsync(_ => true, cancellationTokenSource.Token);
        _ = testClass.Invoke1Async(TimeSpan.FromMilliseconds(100));
        _ = testClass.Invoke2Async("test", TimeSpan.FromMilliseconds(100));
        _ = testClass.Invoke3Async("test1", "test2", TimeSpan.FromMilliseconds(100));

        Assert.Equal(Unit.Default, await task1);
        Assert.Equal("test", await task2);
        Assert.Equal(("test1", "test2"), await task3);
    }

    private sealed class TestClass : IDisposable
    {
        private readonly Subject<Unit> _observable1Subject = new();
        private readonly Subject<object> _observable2Subject = new();
        private readonly Subject<(object, object)> _observable3Subject = new();

        public IObservable<Unit> Observable1 => _observable1Subject;

        public IObservable<object> Observable2 => _observable2Subject;

        public IObservable<(object, object)> Observable3 => _observable3Subject;

        public void Dispose()
        {
            _observable1Subject.Dispose();
            _observable2Subject.Dispose();
            _observable3Subject.Dispose();
        }

        public void Invoke1()
        {
            _observable1Subject.OnNext(Unit.Default);
        }

        public async Task Invoke1Async(TimeSpan delay)
        {
            await Task.Delay(delay);
            _observable1Subject.OnNext(Unit.Default);
        }

        public void Invoke2(object value)
        {
            _observable2Subject.OnNext(value);
        }

        public async Task Invoke2Async(object value, TimeSpan delay)
        {
            await Task.Delay(delay);
            _observable2Subject.OnNext(value);
        }

        public void Invoke3(object value1, object value2)
        {
            _observable3Subject.OnNext((value1, value2));
        }

        public async Task Invoke3Async(object value1, object value2, TimeSpan delay)
        {
            await Task.Delay(delay);
            _observable3Subject.OnNext((value1, value2));
        }
    }
}
