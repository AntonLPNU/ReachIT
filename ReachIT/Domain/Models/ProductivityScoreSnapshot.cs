namespace ReachIT.Domain.Models;

public sealed class ProductivityScoreSnapshot
{
    public int ProductivityScore { get; set; }
    public int FocusScore { get; set; }
    public int DistractionScore { get; set; }
    public int ProgressScore { get; set; }
    public int Interruptions { get; set; }
    public double FocusMinutes { get; set; }
    public double DistractingMinutes { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
