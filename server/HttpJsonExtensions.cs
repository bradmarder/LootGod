using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DiscordWebhookContent))]
[JsonSerializable(typeof(ItemSearch[]))]
[JsonSerializable(typeof(ItemDto[]))]
[JsonSerializable(typeof(LootDto[]))]
[JsonSerializable(typeof(LootRequestDto[]))]
[JsonSerializable(typeof(RaidAttendanceDto[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
