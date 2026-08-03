using System;
using System.Collections.Generic;

namespace KamatekCrm.ApplicationCore.ErrorHandling;

public class DomainException : Exception
{
    public ErrorCategory Category { get; }
    public string? Code { get; }

    public DomainException(string message, ErrorCategory category = ErrorCategory.Unexpected, string? code = null, Exception? inner = null)
        : base(message, inner)
    {
        Category = category;
        Code = code;
    }
}

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message, ErrorCategory.Validation, "VAL_001")
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(message, ErrorCategory.NotFound, "NOT_FOUND")
    {
    }
}

public class ExternalServiceException : DomainException
{
    public ExternalServiceException(string message, Exception? inner = null)
        : base(message, ErrorCategory.ExternalService, "EXT_SVC_ERR", inner)
    {
    }
}

public class PrintingException : DomainException
{
    public PrintingException(string message, Exception? inner = null)
        : base(message, ErrorCategory.Printing, "PRNT_ERR", inner)
    {
    }
}
