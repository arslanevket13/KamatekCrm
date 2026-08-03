using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using FluentAssertions;
using KamatekCrm.ApplicationCore.ErrorHandling;
using KamatekCrm.Infrastructure.ErrorHandling;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KamatekCrm.Tests.ErrorHandling;

public sealed class ExceptionMapperTests
{
    private readonly ExceptionMapper _mapper = new();

    [Fact]
    public void Classify_ValidationException_ReturnsValidationCategory()
    {
        var errors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Geçersiz e-posta adresi." } }
        };
        var ex = new ValidationException("Girdi verileri geçersiz.", errors);

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Validation);
        result.UserMessage.Should().Be("Girdi verileri geçersiz.");
        result.ValidationErrors.Should().ContainKey("Email");
        result.CorrelationId.Should().NotBeNullOrWhiteSpace();
        result.Code.Should().Be("VAL_001");
    }

    [Fact]
    public void Classify_AuthenticationException_ReturnsAuthenticationCategory()
    {
        var ex = new AuthenticationException("Parola hatalı.");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Authentication);
        result.UserMessage.Should().Contain("Kimlik doğrulama başarısız");
        result.Code.Should().Be("AUTH_FAILED");
    }

    [Fact]
    public void Classify_UnauthorizedAccessException_ReturnsAuthorizationCategory()
    {
        var ex = new UnauthorizedAccessException("Yetkisiz alan.");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Authorization);
        result.UserMessage.Should().Contain("yetkiniz yetersizdir");
        result.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public void Classify_KeyNotFoundException_ReturnsNotFoundCategory()
    {
        var ex = new KeyNotFoundException("Müşteri 999 bulunamadı.");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.NotFound);
        result.UserMessage.Should().Contain("bulunamadı");
        result.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Classify_PostgresUniqueViolation_ReturnsConflictCategory()
    {
        var pgEx = new PostgresException("Unique violation", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);

        var result = _mapper.Classify(pgEx);

        result.Category.Should().Be(ErrorCategory.Conflict);
        result.UserMessage.Should().Contain("Mükerrer kayıt");
        result.Code.Should().Be("UNIQUE_VIOLATION");
    }

    [Fact]
    public void Classify_DbUpdateConcurrencyException_ReturnsConcurrencyCategory()
    {
        var ex = new DbUpdateConcurrencyException("Optimistic concurrency error");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Concurrency);
        result.UserMessage.Should().Contain("değiştirilmiş");
        result.Code.Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public void Classify_NpgsqlException_ReturnsDatabaseConnectionCategory()
    {
        var ex = new NpgsqlException("Connection refused");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.DatabaseConnection);
        result.UserMessage.Should().ContainEquivalentOf("bağlantı hatası");
        result.Code.Should().Be("DB_CONN_ERR");
    }

    [Fact]
    public void Classify_PostgresForeignKeyViolation_ReturnsDatabaseConstraintCategory()
    {
        var pgEx = new PostgresException("FK violation", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation);

        var result = _mapper.Classify(pgEx);

        result.Category.Should().Be(ErrorCategory.DatabaseConstraint);
        result.UserMessage.Should().Contain("bağlı olduğu için silinemez");
        result.Code.Should().Be("FK_VIOLATION");
    }

    [Fact]
    public void Classify_SocketException_ReturnsNetworkCategory()
    {
        var ex = new SocketException((int)SocketError.HostNotFound);

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Network);
        result.UserMessage.Should().Contain("iletişim kurulamadı");
        result.Code.Should().Be("NET_ERR");
    }

    [Fact]
    public void Classify_FileNotFoundException_ReturnsFileSystemCategory()
    {
        var ex = new FileNotFoundException("Rapor şablonu bulunamadı.");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.FileSystem);
        result.UserMessage.Should().Contain("başarısız");
        result.Code.Should().Be("FILE_SYS_ERR");
    }

    [Fact]
    public void Classify_PrintingException_ReturnsPrintingCategory()
    {
        var ex = new PrintingException("Yazıcı kuyruğu kilitli.");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Printing);
        result.UserMessage.Should().Be("Yazıcı kuyruğu kilitli.");
        result.Code.Should().Be("PRNT_ERR");
    }

    [Fact]
    public void Classify_ExternalServiceException_ReturnsExternalServiceCategory()
    {
        var ex = new ExternalServiceException("E-Fatura entegrasyonu zaman aşımına uğradı.");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.ExternalService);
        result.UserMessage.Should().Be("E-Fatura entegrasyonu zaman aşımına uğradı.");
        result.Code.Should().Be("EXT_SVC_ERR");
    }

    [Fact]
    public void Classify_TaskCanceledException_ReturnsCancellationCategory()
    {
        var ex = new TaskCanceledException();

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Cancellation);
        result.IsCancellation.Should().BeTrue();
        result.UserMessage.Should().Be("İşlem iptal edildi.");
        result.Code.Should().Be("CANCELLED");
    }

    [Fact]
    public void Classify_UnhandledException_ReturnsUnexpectedCategoryWithoutLeakingDetails()
    {
        var ex = new InvalidOperationException("Internal secret connection string Password=12345;");

        var result = _mapper.Classify(ex);

        result.Category.Should().Be(ErrorCategory.Unexpected);
        result.UserMessage.Should().Be("Beklenmeyen bir sistem hatası oluştu. Lütfen sistem yöneticiniz ile iletişime geçiniz.");
        result.UserMessage.Should().NotContain("Password=12345");
        result.Code.Should().Be("UNEXPECTED");
        result.CorrelationId.Should().StartWith("ERR-");
    }
}
