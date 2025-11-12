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
}
