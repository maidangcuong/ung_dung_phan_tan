namespace DocumentApi.Models;

public enum DocumentStatus { Draft, Submitted, Approved, Rejected }

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public string CreatedBy { get; set; } = string.Empty;
    public string? Approver { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentHistory> Histories { get; set; } = new();
}

public class DocumentHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string Action { get; set; } = string.Empty; 
    public string PerformedBy { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}