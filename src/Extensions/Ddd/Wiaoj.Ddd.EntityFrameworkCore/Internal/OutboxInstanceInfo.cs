namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;
public sealed record OutboxInstanceInfo {
    public string InstanceId { get; } = Guid.NewGuid().ToString();
}