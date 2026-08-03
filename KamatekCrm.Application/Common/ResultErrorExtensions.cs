using System;
using KamatekCrm.ApplicationCore.ErrorHandling;

namespace KamatekCrm.ApplicationCore.Common;

public static class ResultErrorExtensions
{
    public static Result Failure(this ApplicationError error)
    {
        return Result.Failure(error.UserMessage);
    }

    public static Result<T> Failure<T>(this ApplicationError error)
    {
        return Result.Failure<T>(error.UserMessage);
    }

    public static Result ToResult(this Exception exception, IExceptionClassifier classifier)
    {
        var error = classifier.Classify(exception);
        return Result.Failure(error.UserMessage);
    }

    public static Result<T> ToResult<T>(this Exception exception, IExceptionClassifier classifier)
    {
        var error = classifier.Classify(exception);
        return Result.Failure<T>(error.UserMessage);
    }
}
