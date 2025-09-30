namespace RateLimitingGrpcService.Model;
public class ClientStatistics
{
    public DateTime LastSuccessfulResponseTime { get; set; }
    public int NumberOfRequestsCompletedSuccessfully { get; set; }
}