using System;

namespace MangaERP.Task.Domain.Entities;

public class TaskComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static TaskComment Create(Guid pageTaskId, Guid userId, string userFullName, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty.");

        return new TaskComment
        {
            PageTaskId = pageTaskId,
            UserId = userId,
            UserFullName = userFullName,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }
}
