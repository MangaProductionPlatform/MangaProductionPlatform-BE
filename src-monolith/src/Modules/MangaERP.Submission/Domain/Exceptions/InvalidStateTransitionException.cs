namespace MangaERP.Submission.Domain.Exceptions;

public class InvalidStateTransitionException : System.Exception
{
    public InvalidStateTransitionException(string message) : base(message) { }
}
