namespace KeyboardLauncher.Core;

public sealed record AppLicense(bool IsActive, bool IsTrial, DateTimeOffset Expiration)
{
    public bool CanUse(DateTimeOffset now) => IsActive && (!IsTrial || now < Expiration);
}
