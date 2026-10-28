using System;
using System.ComponentModel.DataAnnotations;
using KamatekCrm.Shared.Helpers;

namespace KamatekCrm.Validation
{
    /// <summary>
    /// Türkiye telefon numarası formatı doğrulaması.
    /// Boş veya null ise geçerli kabul eder (Zorunluluk kontrolü [Required] ile yapılır).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TurkishPhoneAttribute : ValidationAttribute
    {
        public TurkishPhoneAttribute()
        {
            ErrorMessage = "Geçerli bir telefon numarası giriniz (Örn: 05XX XXX XX XX).";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
                return ValidationResult.Success;

            if (value is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return ValidationResult.Success;

                if (!CustomerValidationHelper.IsValidPhoneNumber(text))
                {
                    return new ValidationResult(ErrorMessage ?? "Geçerli bir telefon numarası giriniz (Örn: 05XX XXX XX XX).");
                }
            }

            return ValidationResult.Success;
        }
    }
}
