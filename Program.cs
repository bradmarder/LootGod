using LootGod;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Security.Cryptography;

var host = new WebHostBuilder()
.UseKestrel()
.UseUrls("http://*:5000")
.UseContentRoot(Directory.GetCurrentDirectory())
.UseStartup<Startup>()
.Build();

host.Run();

//using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
//using var foo = scope.ServiceProvider.GetRequiredService<LootGodContext>();
//foo.Database.EnsureCreated();

public class CreateLoginAttempt
{
	public string MainName { get; set; } = null!;
}
public class CreatePlayer
{
	public string Name { get; set; } = null!;
}
public class CreateLoot
{
	public byte Quantity { get; set; }
	public string Name { get; set; } = null!;
}