namespace WindowsBuddy.Tools;

public interface IFileDropTarget
{
	void OnFileDragOver(bool over);

	void OnFilesDropped(string[] paths);
}
