namespace CoreAr.Crm.Domain.Exceptions;

public class InvalidOrderTransitionException(string message)
    : DomainException(message);

public class DomainException(string message) : Exception(message);
