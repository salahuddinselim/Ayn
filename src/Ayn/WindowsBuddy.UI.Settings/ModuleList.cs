using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Tools;

namespace WindowsBuddy.UI.Settings;

internal sealed class ModuleList : StackPanel
{
	private const double RowH = 52.0;

	private readonly PageContext _ctx;

	private static readonly string[] Categories = new string[3] { "Essentials", "Productivity", "Insights" };

	private readonly Dictionary<string, StackPanel> _categoryStacks = new Dictionary<string, StackPanel>(StringComparer.OrdinalIgnoreCase);

	private FrameworkElement _dragRow;

	private StackPanel _dragStack;

	private string _dragCategory;

	private int _dragFrom;

	private int _dragTo;

	private double _dragStartY;

	public ModuleList(PageContext ctx)
	{
		_ctx = ctx;
		Build();
	}

	public void Build()
	{
		base.Children.Clear();
		_categoryStacks.Clear();
		List<IBuddyTool> source = ModuleCatalog.DockOrder();
		string[] categories = Categories;
		foreach (string cat in categories)
		{
			base.Children.Add(SUI.SectionLabel(cat));
			StackPanel stackPanel = new StackPanel();
			_categoryStacks[cat] = stackPanel;
			Func<IBuddyTool, bool> predicate = (IBuddyTool t) => string.Equals(ModuleCatalog.For(t).Category, cat, StringComparison.OrdinalIgnoreCase);
			List<IBuddyTool> list = source.Where(predicate).ToList();
			for (int j = 0; j < list.Count; j++)
			{
				stackPanel.Children.Add(BuildRow(list[j], j, cat, stackPanel));
			}
			base.Children.Add(stackPanel);
		}
	}

	private FrameworkElement BuildRow(IBuddyTool tool, int index, string category, StackPanel parentStack)
	{
		ModuleInfo info = ModuleCatalog.For(tool);
		string id = tool.Id;
		SClickable row = new SClickable(0.0, info.Name)
		{
			VisualRole = SVisualRole.Navigation,
			Height = 56.0,
			MinHeight = 56.0,
			RenderTransform = new TranslateTransform(),
			Tag = "setting-module-" + tool.Id
		};
		row.FocusVisualStyle = SettingsTheme.FocusStyle(4.0);
		AutomationProperties.SetHelpText(row, "Enter opens settings. Alt+Up or Alt+Down moves it in the dock.");
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(32.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Border handle = new Border
		{
			Width = 32.0,
			Height = 56.0,
			Background = Brushes.Transparent,
			Cursor = Cursors.SizeNS,
			ToolTip = "Drag to reorder",
			Opacity = 0.0,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		FrameworkElement frameworkElement = Glyphs.Create("drag", 14.0, SettingsTheme.TextLabel);
		frameworkElement.HorizontalAlignment = HorizontalAlignment.Center;
		frameworkElement.VerticalAlignment = VerticalAlignment.Center;
		handle.Child = frameworkElement;
		handle.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
		{
			BeginDrag(row, parentStack, category, handle, e);
		};
		handle.MouseMove += delegate(object s, MouseEventArgs e)
		{
			if (_dragRow == row)
			{
				DragMove(e);
			}
		};
		handle.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
		{
			if (_dragRow == row)
			{
				EndDrag();
				e.Handled = true;
			}
		};
		handle.LostMouseCapture += delegate
		{
			if (_dragRow == row)
			{
				EndDrag();
			}
		};
		grid.Children.Add(handle);
		FrameworkElement frameworkElement2 = IconTile.Create(info.Hue, info.Glyph, 28.0);
		frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement2.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		Grid.SetColumn(frameworkElement2, 1);
		grid.Children.Add(frameworkElement2);
		StackPanel stackPanel = new StackPanel();
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		stackPanel.Margin = new Thickness(0.0, 0.0, 12.0, 0.0);
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(SettingsTheme.Text(info.Name, 15.0, SettingsTheme.TextPrimary, AynType.Semibold));
		TextBlock d = SettingsTheme.Text(info.Description, 13.5, SettingsTheme.TextSecondary, FontWeights.Normal);
		d.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		d.TextWrapping = TextWrapping.NoWrap;
		d.TextTrimming = TextTrimming.CharacterEllipsis;
		stackPanel2.Children.Add(d);
		Grid.SetColumn(stackPanel2, 2);
		grid.Children.Add(stackPanel2);
		SToggle toggle = new SToggle(tool.IsEnabled, "Show " + info.Name + " in the dock")
		{
			Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
		};
		if (id == "home")
		{
			toggle.IsEnabled = false;
			toggle.ToolTip = "Home is always in your dock";
		}
		toggle.VerticalAlignment = VerticalAlignment.Center;
		SSettingBinding.Attach(toggle, delegate
		{
			toggle.IsOn = tool.IsEnabled;
		});
		toggle.Toggled += delegate(bool v)
		{
			if (v && !ToolRegistry.Instance.CanAddToDock(id))
			{
				toggle.IsOn = false;
				d.Text = "Your dock is full. Remove a tool to add another.";
				d.Foreground = SettingsTheme.Danger;
				d.Tag = "setting-dock-full-note";
				DispatcherTimer restore = new DispatcherTimer
				{
					Interval = TimeSpan.FromSeconds(5.0)
				};
				restore.Tick += delegate
				{
					restore.Stop();
					d.Text = info.Description;
					d.Foreground = SettingsTheme.TextSecondary;
					d.Tag = null;
				};
				restore.Start();
				return;
			}
			_ctx.Window.SuppressToolsRefresh(on: true);
			try
			{
				ToolRegistry.Instance.SetToolEnabled(id, v);
			}
			finally
			{
				_ctx.Window.SuppressToolsRefresh(on: false);
			}
		};
		Grid.SetColumn(toggle, 3);
		grid.Children.Add(toggle);
		FrameworkElement frameworkElement3 = Glyphs.Create("chevron_right", 12.0, SettingsTheme.TextSecondary);
		frameworkElement3.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		frameworkElement3.VerticalAlignment = VerticalAlignment.Center;
		Grid.SetColumn(frameworkElement3, 4);
		grid.Children.Add(frameworkElement3);
		row.Child = grid;
		row.MouseEnter += delegate
		{
			if (_dragRow == null)
			{
				handle.Opacity = 1.0;
			}
		};
		row.MouseLeave += delegate
		{
			if (_dragRow != row)
			{
				handle.Opacity = 0.0;
			}
		};
		row.Click += delegate
		{
			_ctx.Navigate("module:" + id);
		};
		row.PreviewKeyDown += delegate(object s, KeyEventArgs e)
		{
			Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
			if (Keyboard.Modifiers == ModifierKeys.Alt && (key == Key.Up || key == Key.Down))
			{
				int num = parentStack.Children.IndexOf(row);
				int num2 = num + ((key != Key.Up) ? 1 : (-1));
				if (num2 >= 0 && num2 < parentStack.Children.Count)
				{
					MoveInCategory(category, num, num2);
					parentStack.Children[num2].Focus();
				}
				e.Handled = true;
			}
		};
		return row;
	}

	private void BeginDrag(FrameworkElement row, StackPanel stack, string category, Border handle, MouseButtonEventArgs e)
	{
		_dragRow = row;
		_dragStack = stack;
		_dragCategory = category;
		_dragFrom = (_dragTo = stack.Children.IndexOf(row));
		_dragStartY = e.GetPosition(stack).Y;
		Panel.SetZIndex(row, 10);
		((SClickable)row).IsSelected = true;
		row.Effect = null;
		handle.CaptureMouse();
		e.Handled = true;
	}

	private void DragMove(MouseEventArgs e)
	{
		if (_dragStack == null || _dragRow == null)
		{
			return;
		}
		double val = e.GetPosition(_dragStack).Y - _dragStartY;
		double val2 = (double)(-_dragFrom) * 52.0;
		double val3 = (double)(_dragStack.Children.Count - 1 - _dragFrom) * 52.0;
		val = Math.Max(val2, Math.Min(val3, val));
		((TranslateTransform)_dragRow.RenderTransform).Y = val;
		int num = Math.Max(0, Math.Min(_dragStack.Children.Count - 1, (int)Math.Round((double)_dragFrom + val / 52.0)));
		if (num == _dragTo)
		{
			return;
		}
		_dragTo = num;
		for (int i = 0; i < _dragStack.Children.Count; i++)
		{
			if (i != _dragFrom)
			{
				double to = 0.0;
				if (_dragFrom < _dragTo && i > _dragFrom && i <= _dragTo)
				{
					to = -52.0;
				}
				else if (_dragFrom > _dragTo && i >= _dragTo && i < _dragFrom)
				{
					to = 52.0;
				}
				if (((FrameworkElement)_dragStack.Children[i]).RenderTransform is TranslateTransform target)
				{
					SettingsTheme.Animate(target, TranslateTransform.YProperty, to, 140.0);
				}
			}
		}
	}

	private void EndDrag()
	{
		FrameworkElement dragRow = _dragRow;
		StackPanel dragStack = _dragStack;
		string dragCategory = _dragCategory;
		if (dragRow == null || dragStack == null)
		{
			return;
		}
		_dragRow = null;
		_dragStack = null;
		_dragCategory = null;
		dragRow.Effect = null;
		((SClickable)dragRow).IsSelected = false;
		Panel.SetZIndex(dragRow, 0);
		foreach (FrameworkElement child in dragStack.Children)
		{
			if (child.RenderTransform is TranslateTransform translateTransform)
			{
				translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
				translateTransform.Y = 0.0;
			}
		}
		if (_dragTo != _dragFrom)
		{
			MoveInCategory(dragCategory, _dragFrom, _dragTo);
		}
	}

	private void MoveInCategory(string category, int from, int to)
	{
		if (!_categoryStacks.ContainsKey(category))
		{
			return;
		}
		StackPanel stackPanel = _categoryStacks[category];
		UIElement element = stackPanel.Children[from];
		stackPanel.Children.RemoveAt(from);
		stackPanel.Children.Insert(to, element);
		for (int i = 0; i < stackPanel.Children.Count; i++)
		{
			if (stackPanel.Children[i] is SClickable sClickable && sClickable.Child is Grid)
			{
				Grid grid = (Grid)sClickable.Child;
				Border border = grid.Children.OfType<Border>().FirstOrDefault((Border b) => b.Height == 1.0 && b.VerticalAlignment == VerticalAlignment.Top);
				if (border != null)
				{
					border.Visibility = ((i == 0) ? Visibility.Collapsed : Visibility.Visible);
				}
			}
		}
		List<IBuddyTool> source = ModuleCatalog.DockOrder();
		List<string> list = source.Select((IBuddyTool t) => t.Id).ToList();
		List<string> list2 = (from t in source
			where string.Equals(ModuleCatalog.For(t).Category, category, StringComparison.OrdinalIgnoreCase)
			select t.Id).ToList();
		string item = list2[from];
		list2.RemoveAt(from);
		list2.Insert(to, item);
		List<int> list3 = new List<int>();
		for (int j = 0; j < list.Count; j++)
		{
			ModuleInfo moduleInfo = ModuleCatalog.Get(list[j]);
			if (moduleInfo != null && string.Equals(moduleInfo.Category, category, StringComparison.OrdinalIgnoreCase))
			{
				list3.Add(j);
			}
		}
		for (int k = 0; k < list3.Count && k < list2.Count; k++)
		{
			list[list3[k]] = list2[k];
		}
		_ctx.Window.SuppressToolsRefresh(on: true);
		try
		{
			ToolRegistry.Instance.ReorderTools(list);
		}
		finally
		{
			_ctx.Window.SuppressToolsRefresh(on: false);
		}
	}
}
