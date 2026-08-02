using System.Data;
using System.Text.Json;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Quotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Infrastructure.Services;

public sealed class StandardQuoteCommandService : IStandardQuoteCommandService
{
    private const string AuditEntity = "Quote";
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;
    private readonly ICurrentUserContext _currentUser;

    public StandardQuoteCommandService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization,
        ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<Result<StandardQuoteSaveResult>> SaveAsync(
        SaveStandardQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure) return Result.Failure<StandardQuoteSaveResult>(authorization.Error);
        if (command.IdempotencyKey == Guid.Empty)
            return Result.Failure<StandardQuoteSaveResult>("İşlem anahtarı oluşturulamadı.");
        if (command.CustomerId <= 0)
            return Result.Failure<StandardQuoteSaveResult>("Müşteri seçilmelidir.");
        if (!string.Equals(command.Currency.Trim(), "TRY", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<StandardQuoteSaveResult>(
                "Kur bilgisi yapılandırılmadan yalnızca TRY teklif oluşturulabilir.");
        if (command.Status is not (QuoteStatus.Draft or QuoteStatus.Sent))
            return Result.Failure<StandardQuoteSaveResult>("Standart teklif yalnızca taslak veya gönderildi durumunda oluşturulabilir.");
        if (command.Lines.Count == 0)
            return Result.Failure<StandardQuoteSaveResult>("Teklifte en az bir kalem bulunmalıdır.");
        if (command.Lines.GroupBy(line => line.ProductId).Any(group => group.Count() > 1))
            return Result.Failure<StandardQuoteSaveResult>("Aynı ürün teklifte birden fazla satırda bulunamaz.");

        var quoteDate = NormalizeUtc(command.QuoteDate);
        var validUntil = NormalizeUtc(command.ValidUntil);
        if (validUntil <= quoteDate)
            return Result.Failure<StandardQuoteSaveResult>("Geçerlilik tarihi teklif tarihinden sonra olmalıdır.");
        if (validUntil > quoteDate.AddDays(365))
            return Result.Failure<StandardQuoteSaveResult>("Teklif geçerlilik süresi 365 günü aşamaz.");
        if ((command.TermsAndConditions?.Length ?? 0) > 4_000)
            return Result.Failure<StandardQuoteSaveResult>("Teklif şartları 4.000 karakteri aşamaz.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            var reference = $"STANDARD-QUOTE-SAVE:{command.IdempotencyKey:N}";
            var previous = await context.ActivityLogs.AsNoTracking()
                .SingleOrDefaultAsync(log => log.EntityName == AuditEntity && log.ReferenceId == reference,
                    cancellationToken);
            if (previous is not null && int.TryParse(previous.RecordId, out var previousId))
            {
                var replay = await BuildReplayAsync(context, previousId, cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            if (!await context.Customers.AnyAsync(customer => customer.Id == command.CustomerId, cancellationToken))
                return await FailAsync<StandardQuoteSaveResult>(transaction, "Seçilen müşteri artık mevcut değil.", cancellationToken);

            ServiceJob? sourceJob = null;
            if (command.SourceServiceJobId.HasValue)
            {
                sourceJob = await context.ServiceJobs.SingleOrDefaultAsync(
                    job => job.Id == command.SourceServiceJobId.Value && !job.IsDeleted,
                    cancellationToken);
                if (sourceJob is null)
                    return await FailAsync<StandardQuoteSaveResult>(transaction,
                        "Teklife kaynak olan servis işi bulunamadı.", cancellationToken);
                if (sourceJob.CustomerId != command.CustomerId)
                    return await FailAsync<StandardQuoteSaveResult>(transaction,
                        "Teklif müşterisi kaynak servis işiyle eşleşmiyor.", cancellationToken);
                if (sourceJob.Status != KamatekCrm.Shared.Enums.JobStatus.Quoting)
                    return await FailAsync<StandardQuoteSaveResult>(transaction,
                        "Kaynak servis işi teklif aşamasında değil.", cancellationToken);
                if (await context.ServiceJobHistories.AnyAsync(history =>
                        history.ServiceJobId == sourceJob.Id && history.Action == "StandardQuoteSaved",
                        cancellationToken))
                    return await FailAsync<StandardQuoteSaveResult>(transaction,
                        "Bu servis işi için standart teklif zaten kaydedilmiş.", cancellationToken);
            }

            var productIds = command.Lines.Select(line => line.ProductId).ToList();
            var products = await context.Products.AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);
            if (products.Count != productIds.Count)
                return await FailAsync<StandardQuoteSaveResult>(transaction,
                    "Teklif kalemlerinden en az biri artık ürün kataloğunda bulunmuyor.", cancellationToken);

            var pricingInput = command.Lines.Select(line =>
            {
                var product = products[line.ProductId];
                return (line.ProductId, line.Quantity, line.UnitPrice, product.PurchasePrice,
                    line.DiscountPercent, (decimal)product.VatRate);
            }).ToList();
            var pricingResult = StandardQuotePricingPolicy.Calculate(pricingInput);
            if (pricingResult.IsFailure || pricingResult.Value is null)
                return await FailAsync<StandardQuoteSaveResult>(transaction, pricingResult.Error, cancellationToken);
            var pricing = pricingResult.Value;

            var quote = new Quote
            {
                QuoteNumber = await NextQuoteNumberAsync(context, quoteDate.Year, cancellationToken),
                CustomerId = command.CustomerId,
                Date = quoteDate,
                ValidUntil = validUntil,
                Status = command.Status,
                Currency = "TRY",
                SubTotal = pricing.SubTotal,
                TotalDiscount = pricing.TotalDiscount,
                TotalTax = pricing.TotalTax,
                GrandTotal = pricing.GrandTotal,
                TermsAndConditions = command.TermsAndConditions?.Trim() ?? string.Empty
            };
            for (var index = 0; index < command.Lines.Count; index++)
            {
                var input = command.Lines[index];
                var product = products[input.ProductId];
                var linePricing = pricing.Lines[index];
                quote.Lines.Add(new QuoteLine
                {
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    ProductCode = product.SKU,
                    Quantity = input.Quantity,
                    Unit = product.Unit,
                    PurchasePrice = product.PurchasePrice,
                    UnitPrice = input.UnitPrice,
                    DiscountPercent = input.DiscountPercent,
                    TaxPercent = product.VatRate,
                    LineTotal = linePricing.LineTotal
                });
            }
            context.Quotes.Add(quote);
            await context.SaveChangesAsync(cancellationToken);
            if (sourceJob is not null)
            {
                sourceJob.ProposalSentDate = command.Status == QuoteStatus.Sent ? DateTime.UtcNow : null;
                sourceJob.ProposalNotes = $"Standart teklif: {quote.QuoteNumber} (#{quote.Id})";
                sourceJob.ModifiedDate = DateTime.UtcNow;
                sourceJob.ModifiedBy = _currentUser.Username;
                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = sourceJob.Id,
                    Date = DateTime.UtcNow,
                    JobStatusChange = sourceJob.Status,
                    TechnicianNote = $"{quote.QuoteNumber} numaralı standart teklif kaydedildi.",
                    Action = "StandardQuoteSaved",
                    Notes = $"QuoteId: {quote.Id}; Durum: {quote.Status}",
                    UserId = _currentUser.Username,
                    PerformedAt = DateTime.UtcNow
                });
            }
            context.ActivityLogs.Add(new ActivityLog
            {
                UserId = _currentUser.UserId,
                Username = _currentUser.Username,
                Action = command.Status == QuoteStatus.Sent ? "StandardQuoteSavedAsSent" : "StandardQuoteDraftSaved",
                ActionType = "Create",
                EntityName = AuditEntity,
                RecordId = quote.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ReferenceId = reference,
                Description = $"{quote.QuoteNumber} numaralı standart teklif kaydedildi.",
                AdditionalData = JsonSerializer.Serialize(new
                {
                    command.IdempotencyKey,
                    quote.Status,
                    pricing.GrandTotal,
                    LineCount = quote.Lines.Count,
                    command.SourceServiceJobId
                }),
                Timestamp = DateTime.UtcNow,
                UserAgent = "WPF Client"
            });
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result.Success(new StandardQuoteSaveResult(
                quote.Id, quote.QuoteNumber, quote.Status, pricing, false));
        }
        catch (Exception exception)
        {
            return await FailAsync<StandardQuoteSaveResult>(transaction,
                $"Standart teklif kaydedilemedi: {exception.GetBaseException().Message}", cancellationToken);
        }
    }

    private static async Task<Result<StandardQuoteSaveResult>> BuildReplayAsync(
        AppDbContext context,
        int quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await context.Quotes.AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == quoteId, cancellationToken);
        if (quote is null)
            return Result.Failure<StandardQuoteSaveResult>("Önceki standart teklif işleminin kaydı bulunamadı.");
        var pricing = StandardQuotePricingPolicy.Calculate(quote.Lines.Select(line =>
            (line.ProductId, line.Quantity, line.UnitPrice, line.PurchasePrice,
                line.DiscountPercent, line.TaxPercent)).ToList());
        return pricing.IsFailure || pricing.Value is null
            ? Result.Failure<StandardQuoteSaveResult>(pricing.Error)
            : Result.Success(new StandardQuoteSaveResult(
                quote.Id, quote.QuoteNumber, quote.Status, pricing.Value, true));
    }

    private static async Task<string> NextQuoteNumberAsync(
        AppDbContext context,
        int year,
        CancellationToken cancellationToken)
    {
        var prefix = $"TKLF-{year}-";
        var values = await context.Quotes.AsNoTracking()
            .Where(quote => quote.QuoteNumber.StartsWith(prefix))
            .Select(quote => quote.QuoteNumber)
            .ToListAsync(cancellationToken);
        var maximum = values.Select(value => value.Split('-').LastOrDefault())
            .Select(value => int.TryParse(value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{maximum + 1:D4}";
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
    };

    private static async Task<Result<T>> FailAsync<T>(
        IDbContextTransaction? transaction,
        string error,
        CancellationToken cancellationToken)
    {
        if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
        return Result.Failure<T>(error);
    }
}
