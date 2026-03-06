using FluentValidation;

namespace SecureVault.Application.Secrets.CreateSecret;

public sealed class CreateSecretCommandValidator : AbstractValidator<CreateSecretCommand>
{
    public const int MaxPlaintextLength = 1000;

    /// <summary>Error code for RFC 7807 / client mapping.</summary>
    public const string CodeSecretEmpty = "SECRET_EMPTY";
    /// <summary>Error code for RFC 7807 / client mapping.</summary>
    public const string CodeSecretTooLong = "SECRET_TOO_LONG";
    /// <summary>Error code for RFC 7807 / client mapping.</summary>
    public const string CodeExpiryRequired = "EXPIRY_REQUIRED";
    /// <summary>Error code for RFC 7807 / client mapping.</summary>
    public const string CodeExpiryInvalid = "EXPIRY_INVALID";

    public CreateSecretCommandValidator()
    {
        RuleFor(x => x.Plaintext)
            .NotEmpty()
            .WithErrorCode(CodeSecretEmpty)
            .WithMessage("Secret cannot be empty.")
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithErrorCode(CodeSecretEmpty)
            .WithMessage("Secret cannot be empty.")
            .Must(s => s != null && s.Any(c => !char.IsControl(c) && !char.IsWhiteSpace(c)))
            .WithErrorCode(CodeSecretEmpty)
            .WithMessage("Secret cannot be empty.")
            .MaximumLength(MaxPlaintextLength)
            .WithErrorCode(CodeSecretTooLong)
            .WithMessage("Secret must be at most 1000 characters.")
            .Must(BeValidUtf8)
            .WithMessage("Secret contains invalid characters.");
        RuleFor(x => x.ExpiryType)
            .IsInEnum();
        RuleFor(x => x.Password)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Password));
    }

    private static bool BeValidUtf8(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        try
        {
            _ = System.Text.Encoding.UTF8.GetByteCount(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
