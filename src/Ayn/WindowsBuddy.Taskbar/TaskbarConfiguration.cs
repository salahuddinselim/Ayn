namespace WindowsBuddy.Taskbar;

public class TaskbarConfiguration
{
	public TaskbarAlignment Alignment { get; set; }

	public bool AutoHide { get; set; }

	public TaskbarSearchMode SearchMode { get; set; }

	public bool ShowWidgets { get; set; }

	public bool ShowTaskView { get; set; }

	public bool SystemTransparency { get; set; }

	public double Opacity { get; set; }

	public TaskbarStyle Style { get; set; }

	public TaskbarBlurStyle Blur { get; set; }

	public int CornerRadius { get; set; }

	public int Height { get; set; }

	public TaskbarConfiguration()
	{
		Alignment = TaskbarAlignment.Center;
		AutoHide = false;
		SearchMode = TaskbarSearchMode.SearchBox;
		ShowWidgets = true;
		ShowTaskView = true;
		SystemTransparency = true;
		Opacity = 0.85;
		Style = TaskbarStyle.Default;
		Blur = TaskbarBlurStyle.Medium;
		CornerRadius = 0;
		Height = 48;
	}

	public TaskbarConfiguration Clone()
	{
		TaskbarConfiguration taskbarConfiguration = new TaskbarConfiguration();
		taskbarConfiguration.Alignment = Alignment;
		taskbarConfiguration.AutoHide = AutoHide;
		taskbarConfiguration.SearchMode = SearchMode;
		taskbarConfiguration.ShowWidgets = ShowWidgets;
		taskbarConfiguration.ShowTaskView = ShowTaskView;
		taskbarConfiguration.SystemTransparency = SystemTransparency;
		taskbarConfiguration.Opacity = Opacity;
		taskbarConfiguration.Style = Style;
		taskbarConfiguration.Blur = Blur;
		taskbarConfiguration.CornerRadius = CornerRadius;
		taskbarConfiguration.Height = Height;
		return taskbarConfiguration;
	}
}
