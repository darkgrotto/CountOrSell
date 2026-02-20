namespace MtgHelper.Core.Entities;

public class UserSubmission
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public List<UserSubmissionItem> Items { get; set; } = new();
}

public class UserSubmissionItem
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public string ChangeType { get; set; } = string.Empty; // Add, Update, Delete
    public string EntityType { get; set; } = string.Empty; // Card, Set, etc.
    public string? EntityId { get; set; }
    public string? DataJson { get; set; }
    public string Status { get; set; } = "Pending";
}
