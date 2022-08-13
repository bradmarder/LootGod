using System.ComponentModel.DataAnnotations;

namespace LootGod;
public class LoginAttempt
{
	public LoginAttempt() { }
	public LoginAttempt(string name, string ip)
	{
		Name = name;
		IP = ip;
	}

	[Key]
	public int Id { get; set; }

	public DateTime CreatedDate { get; set; }

	[Required]
	[MaxLength(24)]
	public string Name { get; set; } = null!;

	[Required]
	public string IP { get; set; } = null!;
}
