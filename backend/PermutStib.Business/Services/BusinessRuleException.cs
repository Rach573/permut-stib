namespace PermutStib.Business.Services;

public sealed class BusinessRuleException(string message) : InvalidOperationException(message);

