using System.Collections.Generic;

namespace WindowsBuddy.BuddyCapsule.Core;

internal sealed class RestingFitItem
{
	public string Id;

	public double FullWidth;

	public int DropOrder = 99;

	public readonly List<RestingFitStep> Steps = new List<RestingFitStep>();

	public int StepsApplied;

	public bool Dropped;

	public double Width
	{
		get
		{
			if (Dropped)
			{
				return 0.0;
			}
			if (StepsApplied != 0)
			{
				return Steps[StepsApplied - 1].Width;
			}
			return FullWidth;
		}
	}

	public bool Protected
	{
		get
		{
			foreach (RestingFitStep step in Steps)
			{
				if (step.Drops)
				{
					return false;
				}
			}
			return true;
		}
	}

	public RestingFitItem Shrinks(int stage, double width)
	{
		Steps.Add(new RestingFitStep
		{
			Stage = stage,
			Width = width
		});
		return this;
	}

	public RestingFitItem Drops(int stage)
	{
		Steps.Add(new RestingFitStep
		{
			Stage = stage,
			Width = 0.0,
			Drops = true
		});
		return this;
	}
}
