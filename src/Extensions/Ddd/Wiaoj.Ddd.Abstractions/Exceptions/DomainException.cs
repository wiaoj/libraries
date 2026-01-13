namespace Wiaoj.Ddd.Exceptions;
public class DomainException(string message) : Exception(message);
public sealed class CreatedAtAlreadySetException() : DomainException("CreatedAt can only be set once.");
public sealed class EntityAlreadyDeletedException() : DomainException("Entity is already deleted.");
public sealed class EntityNotDeletedException() : DomainException("Entity is not deleted.");