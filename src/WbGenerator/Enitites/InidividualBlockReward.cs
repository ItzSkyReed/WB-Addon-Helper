namespace WbGenerator.Enitites;

public class IndividualBlockReward
{
    public int Id { get; set; }
    public required string Path { get; set; }
    public required string CommandType { get; set; }
    public double? BlocksBacap { get; set; }
    public double? BlocksBacaped { get; set; }
}