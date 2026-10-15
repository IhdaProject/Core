namespace Core.Domain.Common;

public interface IAuditable
{
    DateTime? CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

public interface IAuditable<TUserId> : IAuditable
{
    TUserId CreatedBy { get; set; }
    TUserId UpdatedBy { get; set; }
}
