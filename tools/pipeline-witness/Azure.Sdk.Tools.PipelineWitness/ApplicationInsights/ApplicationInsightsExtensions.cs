using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Azure.Sdk.Tools.PipelineWitness.ApplicationInsights;

public static class ApplicationInsightsExtensions
{
    public static async Task<T> TraceAsync<T>(this TelemetryClient telemetryClient, string operationName, string operationType, Func<Task<T>> action)
    {
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>(operationName);
        operation.Telemetry.Type = operationType;

        try
        {
            var result = await action.Invoke();
            operation.Telemetry.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            telemetryClient.TrackException(ex);
            operation.Telemetry.Success = false;
            throw;
        }
    }

    public static async Task TraceAsync(this TelemetryClient telemetryClient, string operationName, string operationType, Func<Task> action)
    {
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>(operationName);
        operation.Telemetry.Type = operationType;

        try
        {
            await action.Invoke();
            operation.Telemetry.Success = true;
        }
        catch (Exception ex)
        {
            telemetryClient.TrackException(ex);
            operation.Telemetry.Success = false;
            throw;
        }
    }

    public static Task TraceAsync(this TelemetryClient telemetryClient, Expression<Func<Task>> action)
    {
        var activityName = GetActivityName(action);

        return TraceAsync(telemetryClient, activityName, "Internal", action.Compile());
    }

    public static async Task TraceRequestAsync(this TelemetryClient telemetryClient, string operationName, Func<RequestTelemetry, Task> action)
    {
        using var operation = telemetryClient.StartOperation<RequestTelemetry>(operationName);

        try
        {
            await action.Invoke(operation.Telemetry);
            operation.Telemetry.Success = true;
        }
        catch (Exception ex)
        {
            telemetryClient.TrackException(ex);
            operation.Telemetry.Success = false;
            throw;
        }
    }

    private static string GetActivityName(LambdaExpression expression)
    {
        if (expression.Body is MethodCallExpression methodCall)
        {
            var method = methodCall.Method;
            var type = methodCall.Method.ReflectedType;
            return $"{type.Name}.{method.Name}";
        }

        return expression.Body.ToString();
    }
}
