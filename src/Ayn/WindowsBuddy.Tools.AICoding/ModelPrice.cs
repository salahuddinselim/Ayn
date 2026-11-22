namespace WindowsBuddy.Tools.AICoding;

public class ModelPrice
{
	public string Model { get; set; }

	public double Input { get; set; }

	public double Output { get; set; }

	public double CacheWrite5m { get; set; }

	public double CacheWrite1h { get; set; }

	public double CacheRead { get; set; }

	public ModelPrice()
	{
	}

	public ModelPrice(string model, double input, double output, double cacheRead, double write5m = -1.0, double write1h = -1.0)
	{
		Model = model;
		Input = input;
		Output = output;
		CacheRead = cacheRead;
		CacheWrite5m = ((write5m >= 0.0) ? write5m : (input * 1.25));
		CacheWrite1h = ((write1h >= 0.0) ? write1h : (input * 2.0));
	}

	public double Cost(long input, long output, long write5m, long write1h, long read)
	{
		return ((double)input * Input + (double)output * Output + (double)write5m * CacheWrite5m + (double)write1h * CacheWrite1h + (double)read * CacheRead) / 1000000.0;
	}
}
