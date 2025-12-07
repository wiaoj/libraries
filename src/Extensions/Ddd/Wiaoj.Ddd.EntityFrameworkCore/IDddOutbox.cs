using Microsoft.EntityFrameworkCore;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore;
/// <summary>
/// Indicates that the DbContext implements the Outbox Pattern and has the necessary DbSet.
/// </summary>
public interface IDddOutbox {
    DbSet<OutboxMessage> OutboxMessages { get; set; }
}