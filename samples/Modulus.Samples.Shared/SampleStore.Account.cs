namespace Modulus.Samples.Shared;

// Account settings for the signed-in demo user: profile, notification preferences, two-step
// verification and connected apps. Values are copied in and out so callers never share state.
public sealed partial class SampleStore
{
    private readonly ProfileSettingsInput _account = new();
    private readonly HashSet<string> _notifications = new NotificationPrefs().Enabled;
    private readonly HashSet<string> _connectedApps = SampleContent.ConnectedApps.Where(a => a.Connected).Select(a => a.Name).ToHashSet();
    private bool _twoFactor;

    public ProfileSettingsInput Account()
    {
        lock (_gate) return Copy(_account, new ProfileSettingsInput());
    }

    public void SaveAccount(ProfileSettingsInput input)
    {
        lock (_gate) Copy(input, _account);
    }

    public NotificationPrefs Notifications()
    {
        lock (_gate) return new NotificationPrefs { Enabled = [.. _notifications] };
    }

    public void SaveNotifications(IEnumerable<string> enabled)
    {
        lock (_gate)
        {
            _notifications.Clear();
            _notifications.UnionWith(enabled);
        }
    }

    public bool TwoFactorEnabled
    {
        get { lock (_gate) return _twoFactor; }
        set { lock (_gate) _twoFactor = value; }
    }

    public bool IsAppConnected(string name)
    {
        lock (_gate) return _connectedApps.Contains(name);
    }

    public void SetAppConnected(string name, bool connected)
    {
        lock (_gate)
        {
            if (connected) _connectedApps.Add(name);
            else _connectedApps.Remove(name);
        }
    }

    private static ProfileSettingsInput Copy(ProfileSettingsInput from, ProfileSettingsInput to)
    {
        to.FirstName = from.FirstName; to.LastName = from.LastName; to.Email = from.Email;
        to.Organization = from.Organization; to.Phone = from.Phone; to.Address = from.Address;
        to.City = from.City; to.Zip = from.Zip; to.Country = from.Country; to.Language = from.Language;
        to.TimeZone = from.TimeZone; to.Currency = from.Currency;
        return to;
    }
}
