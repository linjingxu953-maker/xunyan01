using DesktopMascot.Core.ErrorHandling;

namespace DesktopMascot.Core.Tests;

public class ErrorHandlerTests
{
    [Fact]
    public void HandleException_ShouldReturnErrorContext()
    {
        var handler = new ErrorHandler();
        var exception = new InvalidOperationException("测试错误");

        var context = handler.HandleException(exception, "TestSource");

        Assert.NotNull(context);
        Assert.Equal(exception, context.Exception);
        Assert.Equal("TestSource", context.Source);
        Assert.Equal(ErrorType.Unknown, context.ErrorType);
    }

    [Fact]
    public void ClassifyError_OperationCanceled_ShouldReturnOperationCancelled()
    {
        var exception = new OperationCanceledException();

        var errorType = ErrorHandler.ClassifyError(exception);

        Assert.Equal(ErrorType.OperationCancelled, errorType);
    }

    [Fact]
    public void ClassifyError_Timeout_ShouldReturnTimeout()
    {
        var exception = new TimeoutException();

        var errorType = ErrorHandler.ClassifyError(exception);

        Assert.Equal(ErrorType.Timeout, errorType);
    }

    [Fact]
    public void ClassifyError_FileNotFound_ShouldReturnFileOperation()
    {
        var exception = new System.IO.FileNotFoundException();

        var errorType = ErrorHandler.ClassifyError(exception);

        Assert.Equal(ErrorType.FileOperation, errorType);
    }

    [Fact]
    public void ClassifyError_AppException_ShouldReturnType()
    {
        var exception = new AppException("测试", ErrorType.Network);

        var errorType = ErrorHandler.ClassifyError(exception);

        Assert.Equal(ErrorType.Network, errorType);
    }

    [Fact]
    public void AddHandler_ShouldBeCalled()
    {
        var handler = new ErrorHandler();
        var called = false;
        handler.AddHandler(ctx => called = true);

        handler.HandleException(new Exception("test"));

        Assert.True(called);
    }
}

public class SafeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_Success_ShouldReturnOk()
    {
        var result = await SafeExecutor.ExecuteAsync<string>(async ct =>
        {
            await Task.Delay(10, ct);
            return "成功";
        });

        Assert.True(result.Success);
        Assert.Equal("成功", result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_ShouldReturnFail()
    {
        var result = await SafeExecutor.ExecuteAsync<string>(ct =>
        {
            throw new InvalidOperationException("失败");
        });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetry_ShouldRetry()
    {
        var attempt = 0;
        var result = await SafeExecutor.ExecuteAsync<string>(
            ct =>
            {
                attempt++;
                if (attempt < 3)
                    throw new TimeoutException();
                return Task.FromResult("成功");
            },
            new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromMilliseconds(10) });

        Assert.True(result.Success);
        Assert.Equal(3, attempt);
    }

    [Fact]
    public async Task ExecuteAsync_MaxRetries_ShouldFailAfterMax()
    {
        var result = await SafeExecutor.ExecuteAsync<string>(
            ct =>
            {
                throw new TimeoutException();
            },
            new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromMilliseconds(10) });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ShouldReturnCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await SafeExecutor.ExecuteAsync<string>(
            ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult("完成");
            },
            retryPolicy: null,
            ct: cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.OperationCancelled, result.ErrorType);
    }

    [Fact]
    public void Execute_Sync_ShouldReturnOk()
    {
        var result = SafeExecutor.Execute(() => { });

        Assert.True(result.Success);
    }

    [Fact]
    public void Execute_Sync_Failure_ShouldReturnFail()
    {
        var result = SafeExecutor.Execute(() =>
        {
            throw new Exception("错误");
        });

        Assert.False(result.Success);
    }
}

public class OperationResultTests
{
    [Fact]
    public void Ok_ShouldReturnSuccess()
    {
        var result = OperationResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Fail_ShouldReturnFailure()
    {
        var result = OperationResult.Fail("错误消息");

        Assert.False(result.Success);
        Assert.Equal("错误消息", result.ErrorMessage);
    }

    [Fact]
    public void Ok_WithValue_ShouldReturnValue()
    {
        var result = OperationResult<int>.Ok(42);

        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Fail_WithType_ShouldReturnType()
    {
        var result = OperationResult.Fail("网络错误", ErrorType.Network);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Network, result.ErrorType);
    }
}
