using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BitacoraEvidencias.Web.Validation;

public sealed class HttpUrlAttribute : ValidationAttribute, IClientModelValidator
{
    public HttpUrlAttribute() => ErrorMessage = "Captura una URL valida con http:// o https://.";
    public override bool IsValid(object? value) => string.IsNullOrWhiteSpace(value as string) ||
        (Uri.TryCreate((value as string)?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host));
    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-httpurl", ErrorMessage!);
    }
}
