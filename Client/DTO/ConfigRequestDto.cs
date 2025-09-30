namespace Client.DTO;

public class ConfigRequestDto
{
    public required string ResourceId { get; set; }
    public int MaxRequests { get; set; }
    public int WindowSeconds { get; set; }
}
