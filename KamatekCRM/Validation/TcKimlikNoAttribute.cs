using System;
using System.ComponentModel.DataAnnotations;
using KamatekCrm.Shared.Helpers;

namespace KamatekCrm.Validation
{
    /// <summary>
    /// Değer girilmişse Türkiye Cumhuriyeti Kimlik Numarası (11 Hane) Mod-10 Checksum algoritmasını doğrular.
    /// Boş veya null ise geçerli kabul eder (Zorunluluk kontrolü [Required] veya [RequiredWhen] ile yapılır).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TcKimlikNoAttribute : ValidationAttribute
    {
        public TcKimlikNoAttribute()
        {
            ErrorMessage = "Geçersiz T.C. Kimlik Numarası.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
                return ValidationResult.Success;

            if (value is string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return ValidationResult.Success;

                if (!CustomerValidationHelper.IsValidTcKimlikNo(text))
                {
                    return new ValidationResult(ErrorMessage ?? "Geçersiz T.C. Kimlik Numarası.");
                }
            }

            return ValidationResult.Success;
        }
    }
}
