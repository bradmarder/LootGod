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
			command.CommandText = command.CommandText.Replace("PRIMARY KEY", "PRIMARY KEY ON CONFLICT REPLACE");
		}

		if (command.CommandText.StartsWith("CREATE TABLE \"RaidDumps\""))
		{
			command.CommandText = command.CommandText.Replace("PRIMARY KEY (\"Timestamp\", \"PlayerId\")", "PRIMARY KEY (\"Timestamp\", \"PlayerId\") ON CONFLICT IGNORE");
		}

		return result;
	}
}