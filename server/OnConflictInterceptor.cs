using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

public class OnConflictInterceptor : DbCommandInterceptor
{
	public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
	{
		if (command.Parameters.Count > 0) { return result; }

		if (command.CommandText.StartsWith("CREATE TABLE \"Items\"")
			|| command.CommandText.StartsWith("CREATE TABLE \"Spells\""))
		{
			const string key = "PRIMARY KEY";
			command.CommandText = command.CommandText.Replace(key, key + " ON CONFLICT REPLACE");
		}

		if (command.CommandText.StartsWith("CREATE TABLE \"RaidDumps\""))
		{
			const string key = """PRIMARY KEY ("Timestamp", "PlayerId")""";
			command.CommandText = command.CommandText.Replace(key, key + " ON CONFLICT IGNORE");
		}

		return result;
	}
}