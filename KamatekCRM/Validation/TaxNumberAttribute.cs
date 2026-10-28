using System;
using System.ComponentModel.DataAnnotations;
using KamatekCrm.Shared.Helpers;

namespace KamatekCrm.Validation
{
    /// <summary>
    /// Değer girilmişse Türkiye Vergi Kimlik Numarası (VKN - 10 Hane) Mod-9/Mod-10 Checksum algoritmasını doğrular.
    /// Boş veya null ise geçerli kabul eder (Zorunluluk kontrolü [Required] veya [RequiredWhen] ile yapılır).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TaxNumberAttribute : ValidationAttribute
    {
        public TaxNumberAttribute()
        {
            ErrorMessage = "Geçersiz Vergi Kimlik Numarası.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
                return ValidationResult.Success;

            if (value is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return ValidationResult.Success;

                if (!CustomerValidationHelper.IsValidTaxNumber(text))
                {
                    return new ValidationResult(ErrorMessage ?? "Geçersiz Vergi Kimlik Numarası.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
