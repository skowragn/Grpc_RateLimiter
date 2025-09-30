namespace RateLimitingGrpcService.Model;
public class LimitRequests
{
    public int TimeWindow { get; set; }
    public int MaxRequests { get; set; }
}