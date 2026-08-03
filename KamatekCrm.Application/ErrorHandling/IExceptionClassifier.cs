using System;

namespace KamatekCrm.ApplicationCore.ErrorHandling;

/// <summary>
/// Teknik istisnaları (Exception) güvenli, sınıflandırılmış ve kullanıcı dostu ApplicationError nesnesine dönüştürür.
/// </summary>
public interface IExceptionClassifier
{
    ApplicationError Classify(Exception exception, string? customUserMessage = null);
}
