using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using KamatekCrm.ApplicationCore.ErrorHandling;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KamatekCrm.Infrastructure.ErrorHandling;

public class ExceptionMapper : IExceptionClassifier
{
    public ApplicationError Classify(Exception exception, string? customUserMessage = null)
    {
        if (exception == null)
            return new ApplicationError(ErrorCategory.Unexpected, "Tanımlanamayan boş istisna.");

        // Unroll TargetInvocationException or AggregateException if needed
        var root = UnrollException(exception);

        // 1. Cancellation Exception (Should not be logged as error)
        if (root is OperationCanceledException or TaskCanceledException)
        {
            return new ApplicationError(
                ErrorCategory.Cancellation,
                customUserMessage ?? "İşlem iptal edildi.",
                code: "CANCELLED");
        }

        // 2. Domain Exceptions
        if (root is ValidationException valEx)
        {
            return new ApplicationError(
                ErrorCategory.Validation,
                customUserMessage ?? valEx.Message,
                code: valEx.Code ?? "VAL_001",
                validationErrors: valEx.Errors);
        }

        if (root is DomainException domEx)
        {
            return new ApplicationError(
                domEx.Category,
                customUserMessage ?? domEx.Message,
                code: domEx.Code);
        }

        // 3. Security & Access
        if (root is UnauthorizedAccessException)
        {
            return new ApplicationError(
                ErrorCategory.Authorization,
                customUserMessage ?? "Bu işlemi gerçekleştirmek için yetkiniz yetersizdir.",
                code: "AUTH_DENIED");
        }

        if (root is AuthenticationException)
        {
            return new ApplicationError(
                ErrorCategory.Authentication,
                customUserMessage ?? "Kimlik doğrulama başarısız oldu. Lütfen tekrar giriş yapınız.",
                code: "AUTH_FAILED");
        }

        // 4. Not Found
        if (root is KeyNotFoundException)
        {
            return new ApplicationError(
                ErrorCategory.NotFound,
                customUserMessage ?? "İstenen kayıt veya kaynak bulunamadı.",
                code: "NOT_FOUND");
        }

        // 5. Concurrency
        if (root is DbUpdateConcurrencyException)
        {
            return new ApplicationError(
                ErrorCategory.Concurrency,
                customUserMessage ?? "Bu kayıt başka bir kullanıcı veya işlem tarafından değiştirilmiş. Lütfen güncel veriyi tekrar yükleyin.",
                code: "CONCURRENCY_CONFLICT");
        }

        // 6. PostgreSQL & EF Core Database Exceptions
        if (root is PostgresException pgEx)
        {
            return MapPostgresException(pgEx, customUserMessage);
        }

        if (root is NpgsqlException)
        {
            return new ApplicationError(
                ErrorCategory.DatabaseConnection,
                customUserMessage ?? "Veritabanı sunucusuna erişim sağlanamadı. Bağlantı hatası.",
                code: "DB_CONN_ERR");
        }

        if (root is DbUpdateException dbUpEx)
        {
            if (dbUpEx.InnerException is PostgresException innerPgEx)
            {
                return MapPostgresException(innerPgEx, customUserMessage);
            }
            return new ApplicationError(
                ErrorCategory.DatabaseConstraint,
                customUserMessage ?? "Veritabanı güncelleme işlemi gerçekleştirilemedi.",
                code: "DB_UPDATE_ERR");
        }

        // 7. Network / Socket / HTTP
        if (root is SocketException or HttpRequestException or WebException)
        {
            return new ApplicationError(
                ErrorCategory.Network,
                customUserMessage ?? "Ağ veya uzaktaki sunucu ile iletişim kurulamadı.",
                code: "NET_ERR");
        }

        // 8. File System / IO
        if (root is FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return new ApplicationError(
                ErrorCategory.FileSystem,
                customUserMessage ?? "Dosya erişimi veya okuma/yazma işlemi başarısız.",
                code: "FILE_SYS_ERR");
        }

        // 9. Printing
        if (root is PrintingException prntEx)
        {
            return new ApplicationError(
                ErrorCategory.Printing,
                customUserMessage ?? prntEx.Message,
                code: prntEx.Code ?? "PRINT_ERR");
        }

        if (root.GetType().FullName?.Contains("Print", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ApplicationError(
                ErrorCategory.Printing,
                customUserMessage ?? "Yazıcı çıktısı alınırken donanım veya sürücü hatası oluştu.",
                code: "PRINT_ERR");
        }

        // 10. External Service
        if (root is ExternalServiceException extEx)
        {
            return new ApplicationError(
                ErrorCategory.ExternalService,
                customUserMessage ?? extEx.Message,
                code: extEx.Code ?? "EXT_SVC_ERR");
        }

        // 11. Unexpected / Fallback
        return new ApplicationError(
            ErrorCategory.Unexpected,
            customUserMessage ?? "Beklenmeyen bir sistem hatası oluştu. Lütfen sistem yöneticiniz ile iletişime geçiniz.",
            code: "UNEXPECTED");
    }

    private static ApplicationError MapPostgresException(PostgresException pgEx, string? customUserMessage)
    {
        return pgEx.SqlState switch
        {
            // Unique Violation (23505) -> Conflict
            PostgresErrorCodes.UniqueViolation => new ApplicationError(
                ErrorCategory.Conflict,
                customUserMessage ?? "Bu kayda ait benzersiz bilgiler veritabanında zaten mevcut (Mükerrer kayıt).",
                code: "UNIQUE_VIOLATION"),

            // Foreign Key Violation (23503) -> DatabaseConstraint
            PostgresErrorCodes.ForeignKeyViolation => new ApplicationError(
                ErrorCategory.DatabaseConstraint,
                customUserMessage ?? "Bu kayıt başka bir veriye bağlı olduğu için silinemez veya güncellenemez.",
                code: "FK_VIOLATION"),

            // Not Null Violation (23502) -> DatabaseConstraint
            PostgresErrorCodes.NotNullViolation => new ApplicationError(
                ErrorCategory.DatabaseConstraint,
                customUserMessage ?? "Zorunlu alan eksik bırakıldığı için veritabanına kaydedilemedi.",
                code: "NOT_NULL_VIOLATION"),

            // Check Violation (23514) -> DatabaseConstraint
            PostgresErrorCodes.CheckViolation => new ApplicationError(
                ErrorCategory.DatabaseConstraint,
                customUserMessage ?? "Veri kısıtları doğrulanamadı.",
                code: "CHECK_VIOLATION"),

            // Serialization Failure (40001) / Deadlock (40P01) -> Concurrency
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => new ApplicationError(
                ErrorCategory.Concurrency,
                customUserMessage ?? "Eşzamanlı çakışma oluştu. Lütfen işlemi tekrar deneyiniz.",
                code: "DB_DEADLOCK"),

            // Connection Class (08000, 08001, 08003, 08006) -> DatabaseConnection
            var state when state.StartsWith("08") => new ApplicationError(
                ErrorCategory.DatabaseConnection,
                customUserMessage ?? "Veritabanı bağlantısı koptu veya erişilemiyor.",
                code: "DB_CONN_LOST"),

            _ => new ApplicationError(
                ErrorCategory.DatabaseConstraint,
                customUserMessage ?? "Veritabanı işlemi gerçekleştirilirken hata oluştu.",
                code: $"PG_SQLSTATE_{pgEx.SqlState}")
        };
    }

    private static Exception UnrollException(Exception ex)
    {
        var current = ex;
        while (current.InnerException != null && (current is System.Reflection.TargetInvocationException || current is AggregateException))
        {
            current = current.InnerException;
        }
        return current;
    }
}
