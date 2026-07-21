using System;

namespace BetterExperience.GameScopes;

public interface PersistenceService
{
	string Dir { get; }

	string ExchangeDir { get; }

	T Persisted<T>(Func<T> factory, string customName = null, bool exchange = false);

	void Persist<T>(T value, string customName = null, bool exchange = false);
}
