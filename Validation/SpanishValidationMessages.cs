using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace BitacoraEvidencias.Web.Validation;

public sealed class SpanishValidationMessages : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.Attributes.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessage is not null || attribute.ErrorMessageResourceType is not null) continue;
            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute => "{0} es obligatorio.",
                StringLengthAttribute { MinimumLength: > 0 } => "{0} debe tener entre {2} y {1} caracteres.",
                StringLengthAttribute => "{0} admite hasta {1} caracteres.",
                RangeAttribute => "{0} debe estar entre {1} y {2}.",
                CompareAttribute => "{0} y {1} deben coincidir.",
                _ => attribute.ErrorMessage
            };
        }
    }

    public static void Configure(MvcOptions options)
    {
        options.ModelMetadataDetailsProviders.Add(new SpanishValidationMessages());
        var messages = options.ModelBindingMessageProvider;
        messages.SetAttemptedValueIsInvalidAccessor((_, field) => $"Captura un valor valido para {field}.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"Captura un valor valido para {field}.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "Captura un valor valido.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Captura un valor valido.");
        messages.SetValueIsInvalidAccessor(_ => "Captura un valor valido.");
        messages.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
        messages.SetValueMustBeANumberAccessor(field => $"Captura un numero valido para {field}.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Captura un numero valido.");
    }
}
