public static class Extensions
{
	extension(int rowCount)
	{
		public void EnsureSingle()
		{
			if (rowCount is not 1)
			{
				throw new Exception("Expected 1 row updated, actual = " + rowCount);
			}
		}
	}

	extension(SemaphoreSlim semaphore)
	{
		public async Task<IDisposable> LockAsync()
		{
			await semaphore.WaitAsync();
			return new Releaser(semaphore);
		}
	}

	private class Releaser(SemaphoreSlim _semaphore) : IDisposable
	{
		public void Dispose() => _semaphore.Release();
	}
}