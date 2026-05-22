namespace RaidOps.Application.Contracts.Common
{
    public class CommandResponse(string message, object? body = null, string status = "ok")
    {
        public string Message { get; set; } = message;
        public object? Body { get; set; } = body;
        public string Status { get; set; } = status;
    }
}