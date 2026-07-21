using System;

namespace BetterExperience.CustomScene.Packaging;

public class VirtIOAccessor
{
	private VirtIO io;

	private string name;

	public Package Package => io.Package;

	public VirtIO IO => io;

	public VirtIOAccessor(VirtIO virtio, string name)
	{
		io = virtio;
		this.name = name;
	}

	public byte[] Read()
	{
		return io.Read(name);
	}

	public void Write(byte[] data)
	{
		io.Write(name, data);
	}

	public void Persist<T>(T value)
	{
		io.Persist(value, name);
	}

	public void PersistOrDelete<T>(T value)
	{
		io.PersistOrDelete(value, name);
	}

	public T Persisted<T>(Func<T> factory)
	{
		return io.Persisted(factory, name);
	}

	public override string ToString()
	{
		return io.ToString();
	}
}
