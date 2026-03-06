namespace SecureVault.Domain.ValueObjects;

/// <summary>
/// How long the secret is available before it is considered expired.
/// BurnAfterRead: single reveal then expired.
/// </summary>
public enum ExpiryType
{
    BurnAfterRead = 0,
    OneHour = 1,
    TwentyFourHours = 2,
    SevenDays = 3
}
