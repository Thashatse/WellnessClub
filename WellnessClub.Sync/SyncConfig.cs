namespace WellnessClub.Sync;

public class SyncConfig
{
    public string StorageConnection { get; set; } = default!;
    public string StravaClientId { get; set; } = default!;
    public string StravaClientSecret { get; set; } = default!;
    public string CompanyName { get; set; } = default!;
}
