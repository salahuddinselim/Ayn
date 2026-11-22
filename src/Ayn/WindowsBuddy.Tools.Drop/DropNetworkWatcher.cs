using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.Drop;

public sealed class DropNetworkWatcher
{
	[ClassInterface(ClassInterfaceType.None)]
	[ComVisible(true)]
	public sealed class Sink : INetworkEvents
	{
		private readonly DropNetworkWatcher owner;

		internal Sink(DropNetworkWatcher owner)
		{
			this.owner = owner;
		}

		public void NetworkAdded(Guid networkId)
		{
			owner.Changed();
		}

		public void NetworkDeleted(Guid networkId)
		{
			owner.Changed();
		}

		public void NetworkConnectivityChanged(Guid networkId, int connectivity)
		{
			owner.Changed();
		}

		public void NetworkPropertyChanged(Guid networkId, int flags)
		{
			owner.Changed();
		}
	}

	private readonly Dispatcher dispatcher;

	private readonly Action refresh;

	private object manager;

	private IConnectionPoint point;

	private int cookie;

	private bool running;

	private bool queued;

	public bool EventsConnected => cookie != 0;

	internal DropNetworkWatcher(Dispatcher dispatcher, Action refresh)
	{
		this.dispatcher = dispatcher;
		this.refresh = refresh;
	}

	public void Start()
	{
		if (running)
		{
			return;
		}
		running = true;
		NetworkChange.NetworkAddressChanged += OnAddressChanged;
		NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
		try
		{
			manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")));
			Guid riid = typeof(INetworkEvents).GUID;
			((IConnectionPointContainer)manager).FindConnectionPoint(ref riid, out point);
			point.Advise(new Sink(this), out cookie);
		}
		catch
		{
			Release();
		}
	}

	public void Stop()
	{
		if (running)
		{
			running = false;
			NetworkChange.NetworkAddressChanged -= OnAddressChanged;
			NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
			Release();
		}
	}

	private void Release()
	{
		try
		{
			if (point != null && cookie != 0)
			{
				point.Unadvise(cookie);
			}
		}
		catch
		{
		}
		if (point != null)
		{
			try
			{
				Marshal.ReleaseComObject(point);
			}
			catch
			{
			}
		}
		if (manager != null)
		{
			try
			{
				Marshal.ReleaseComObject(manager);
			}
			catch
			{
			}
		}
		point = null;
		manager = null;
		cookie = 0;
	}

	private void OnAddressChanged(object sender, EventArgs e)
	{
		Changed();
	}

	private void OnAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
	{
		Changed();
	}

	private void Changed()
	{
		lock (this)
		{
			if (queued || !running)
			{
				return;
			}
			queued = true;
		}
		dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			lock (this)
			{
				queued = false;
			}
			if (running)
			{
				refresh();
			}
		});
	}
}
