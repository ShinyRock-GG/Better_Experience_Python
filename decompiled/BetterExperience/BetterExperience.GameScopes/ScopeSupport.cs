using System;
using System.Collections.Generic;

namespace BetterExperience.GameScopes;

public class ScopeSupport : IDisposable
{
	public class ScopeExceptionEvent
	{
		public ScopeSupport Sender { get; }

		public Exception Exception { get; }

		public bool Handled { get; set; }

		public ScopeExceptionEvent(ScopeSupport sender, Exception exception)
		{
			Sender = sender;
			Exception = exception;
		}
	}

	private List<IDisposable> children = new List<IDisposable>();

	private List<object> localObjects = new List<object>();

	private ScopeSupport _parent;

	private bool disposingNow;

	public bool Autostart { get; set; } = true;

	public string Name { get; set; } = "Nameless";

	public bool Silent { get; set; }

	public ScopeSupport Parent
	{
		get
		{
			return _parent;
		}
		private set
		{
			if (_parent != null && value != null)
			{
				throw new Exception("Multiple parents");
			}
			_parent = value;
			if (_parent == null)
			{
				return;
			}
			try
			{
				this.OnInit();
			}
			catch (Exception exception)
			{
				PropagateCrash(new ScopeExceptionEvent(this, exception));
				return;
			}
			if (Autostart)
			{
				if (_parent.Started)
				{
					Start();
				}
				else
				{
					_parent.OnStart += Start;
				}
			}
		}
	}

	public bool Started { get; private set; }

	public event Action OnInit = delegate
	{
	};

	public event Action OnDispose = delegate
	{
	};

	public event Action OnStart = delegate
	{
	};

	public event Action<ScopeExceptionEvent> OnException = delegate
	{
	};

	public void Start()
	{
		try
		{
			Started = true;
			this.OnStart();
		}
		catch (Exception exception)
		{
			PropagateCrash(new ScopeExceptionEvent(this, exception));
		}
	}

	public void AddChild(IDisposable child)
	{
		children.Add(child);
		if (!(child is ScopeSupport))
		{
			return;
		}
		ScopeSupport t = (ScopeSupport)child;
		if (t.Parent != null)
		{
			throw new Exception("Multiparent child");
		}
		t.Parent = this;
		t.OnDispose += delegate
		{
			if (!disposingNow)
			{
				if (!Silent)
				{
					new Logger().Debug("Child {0} of {1} Disposed", t.Name, Name);
				}
				children.Remove(t);
			}
		};
	}

	public void Dispose()
	{
		if (!Silent)
		{
			new Logger().Debug("Disposing scope {0}", Name);
		}
		disposingNow = true;
		foreach (IDisposable child in children)
		{
			child.Dispose();
		}
		this.OnDispose();
		Started = false;
		children.Clear();
	}

	public bool IsDescendant(IDisposable obj)
	{
		if (children.Contains(obj))
		{
			return true;
		}
		foreach (IDisposable child in children)
		{
			if (child is ScopeSupport && ((ScopeSupport)child).IsDescendant(obj))
			{
				return true;
			}
		}
		return false;
	}

	public T Provide<T>(T obj, ScopeSupport scope = null)
	{
		localObjects.Add(obj);
		if (scope != null)
		{
			if (!IsDescendant(scope))
			{
				AddChild(scope);
			}
			scope.OnDispose += delegate
			{
				new Logger().Debug("Unregister service {0} at {1}", obj.GetType().Name, Name);
				localObjects.Remove(obj);
			};
		}
		new Logger().Debug("Register service {0} at {1}", obj.GetType().Name, Name);
		return obj;
	}

	public T AddService<T>(T service) where T : PluginService
	{
		return Provide(service, service.Scope);
	}

	public virtual T Find<T>() where T : class
	{
		foreach (object p in localObjects)
		{
			if (p is T)
			{
				return (T)p;
			}
		}
		if (Parent != null)
		{
			return Parent.Find<T>();
		}
		return null;
	}

	public T Lookup<T>() where T : class
	{
		T result = Find<T>();
		if (result == null)
		{
			List<string> path = new List<string>();
			for (ScopeSupport scope = this; scope != null; scope = scope.Parent)
			{
				path.Add(scope.Name);
			}
			path.Reverse();
			throw new Exception("Unresolved service " + typeof(T).Name + " at " + string.Join("/", path));
		}
		return result;
	}

	public void NotifyCrash(Exception e)
	{
		PropagateCrash(new ScopeExceptionEvent(this, e));
	}

	protected void PropagateCrash(ScopeExceptionEvent e)
	{
		this.OnException(e);
		if (!e.Handled)
		{
			if (Parent != null)
			{
				Parent.PropagateCrash(e);
				return;
			}
			new Logger().Error(e.Exception, "Unhandled exception at scope {0}", Name);
		}
	}

	public T EventHandler<T>(Action<T> subscriber, Action<T> unsubscriber, T handler)
	{
		OnDispose += delegate
		{
			unsubscriber(handler);
		};
		subscriber(handler);
		return handler;
	}
}
