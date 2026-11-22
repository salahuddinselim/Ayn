using System;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.UI;

namespace WindowsBuddy.Tools.Notes;

public static class NotePalette
{
	public static readonly string[] Keys = new string[8] { null, "grey", "red", "orange", "yellow", "green", "blue", "purple" };

	public static readonly string[] Names = new string[8] { "White", "Grey", "Red", "Orange", "Yellow", "Green", "Blue", "Purple" };

	public static bool IsKnown(string key)
	{
		if (key != null)
		{
			return Array.IndexOf(Keys, key) > 0;
		}
		return false;
	}

	public static Brush BrushOf(string key)
	{
		return key switch
		{
			"grey" => BuddyTheme.NoteGrey, 
			"red" => BuddyTheme.NoteRed, 
			"orange" => BuddyTheme.NoteOrange, 
			"yellow" => BuddyTheme.NoteYellow, 
			"green" => BuddyTheme.NoteGreen, 
			"blue" => BuddyTheme.NoteBlue, 
			"purple" => BuddyTheme.NotePurple, 
			_ => null, 
		};
	}

	public static string KeyOf(Brush brush)
	{
		if (!(brush is SolidColorBrush solidColorBrush))
		{
			return null;
		}
		for (int i = 1; i < Keys.Length; i++)
		{
			if (BrushOf(Keys[i]) is SolidColorBrush solidColorBrush2 && (solidColorBrush2.Color == solidColorBrush.Color || BuddyTheme.NoteInkMatches(solidColorBrush2, solidColorBrush.Color)))
			{
				return Keys[i];
			}
		}
		return null;
	}
}
