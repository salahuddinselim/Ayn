using System.Windows.Media;

namespace WindowsBuddy.Tools.Sounds;

public class SoundChannel
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Icon { get; set; }

	public string FilePath { get; set; }

	public bool IsPlaying { get; set; }

	public double Volume { get; set; }

	public MediaPlayer Player { get; set; }

	public SoundChannel(string id, string name, string icon, string path)
	{
		Id = id;
		Name = name;
		Icon = icon;
		FilePath = path;
		Volume = 0.5;
		IsPlaying = false;
		Player = new MediaPlayer();
	}
}
