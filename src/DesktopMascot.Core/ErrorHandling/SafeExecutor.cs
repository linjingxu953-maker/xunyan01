namespace DesktopMascot.Core.ErrorHandling;

/// <summary>
/// 安全执行器
/// </summary>
public static class SafeExecutor
{
    /// <summary>安全执行（返回结果）</summary>
    public static async Task<OperationResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        RetryPolicy? retryPolicy = null,
        ErrorHandler? errorHandler = null,
        CancellationToken ct = default)
    {
        retryPolicy ??= RetryPolicy.Default;
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryPolicy.MaxRetries)
        {
            try
            {
                var result = await action(ct);
                return OperationResult<T>.Ok(result);
            }
            catch (OperationCanceledException)
            {
                return OperationResult<T>.Fail("操作已取消", ErrorType.OperationCancelled);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                // 记录错误
                errorHandler?.HandleException(ex);

                // 检查是否应该重试
                if (attempt > retryPolicy.MaxRetries)
                    break;

                if (retryPolicy.ShouldRetry != null && !retryPolicy.ShouldRetry(ex))
                    break;

                // 计算延迟
                var delay = CalculateDelay(retryPolicy, attempt);
                await Task.Delay(delay, ct);
            }
        }

        return OperationResult<T>.Fail(lastException!);
    }

    /// <summary>安全执行（无返回值）</summary>
    public static async Task<OperationResult> ExecuteAsync(
        Func<CancellationToken, Task> action,
        RetryPolicy? retryPolicy = null,
        ErrorHandler? errorHandler = null,
        CancellationToken ct = default)
    {
        retryPolicy ??= RetryPolicy.Default;
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryPolicy.MaxRetries)
        {
            try
            {
                await action(ct);
                return OperationResult.Ok();
            }
            catch (OperationCanceledException)
            {
                return OperationResult.Fail("操作已取消", ErrorType.OperationCancelled);
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                errorHandler?.HandleException(ex);

                if (attempt > retryPolicy.MaxRetries)
                    break;

                if (retryPolicy.ShouldRetry != null && !retryPolicy.ShouldRetry(ex))
                    break;

                var delay = CalculateDelay(retryPolicy, attempt);
                await Task.Delay(delay, ct);
            }
        }

        return OperationResult.Fail(lastException!);
    }

    /// <summary>同步安全执行</summary>
    public static OperationResult Execute(
        Action action,
        ErrorHandler? errorHandler = null)
    {
        try
        {
            action();
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            errorHandler?.HandleException(ex);
            return OperationResult.Fail(ex);
        }
    }

    private static TimeSpan CalculateDelay(RetryPolicy policy, int attempt)
    {
        var delay = policy.InitialDelay.TotalMilliseconds * Math.Pow(policy.BackoffMultiplier, attempt - 1);
        delay = Math.Min(delay, policy.MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(delay);
    }
}
