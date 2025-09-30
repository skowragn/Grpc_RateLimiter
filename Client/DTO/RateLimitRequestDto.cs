namespace Client.DTO;

public class RateLimitRequestDto
{
   public required string ClientId { get; set; }
   public required string Resource { get; set; }
}
