using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace KamatekCrm.Validation;

/// <summary>
/// Bir alanı, aynı ViewModel üzerindeki başka bir property belirli değerdeyken zorunlu yapar.
/// Koşullu formlarda görünmeyen alanların kullanıcıyı engellemesini önler.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class RequiredWhenAttribute : ValidationAttribute
{
    private readonly string _dependentProperty;
    private readonly object _expectedValue;

    public RequiredWhenAttribute(string dependentProperty, object expectedValue)
    {
        _dependentProperty = dependentProperty;
        _expectedValue = expectedValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        PropertyInfo? property = validationContext.ObjectType.GetProperty(_dependentProperty);
        if (property is null)
        {
            return new ValidationResult($"Doğrulama alanı bulunamadı: {_dependentProperty}");
        }

        object? actualValue = property.GetValue(validationContext.ObjectInstance);
        bool isRequired = Equals(actualValue, _expectedValue);
        bool isEmpty = value is null || value is string text && string.IsNullOrWhiteSpace(text);

        return isRequired && isEmpty
            ? new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} zorunludur.")
            : ValidationResult.Success;
    }
}
