namespace PermutStib.Business.Abstractions;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
}
