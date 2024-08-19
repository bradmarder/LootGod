using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public record SsePayload<T>
{
	public required string Evt { get; init; }
	public required T Json { get; init; }
	public required int Id { get; init; }
}

public static class HttpExtensions
{
	private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

	public static async Task EnsurePostAsJsonAsync(this HttpClient client, string requestUri)
	{
		await client.EnsurePostAsJsonAsync<string?>(requestUri, null);
	}

	public static async Task<string> EnsurePostAsJsonAsync<T>(this HttpClient client, string requestUri, T? value = default)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = value is null
				? await client.PostAsJsonAsync(requestUri, new { })
				: await client.PostAsJsonAsync(requestUri, value);

			return await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
		}
		catch
		{
			Assert.NotNull(response);
			Assert.Fail(response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync());
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}

	public static async Task<T> EnsureGetJsonAsync<T>(this HttpClient client, string requestUri)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = await client.GetAsync(requestUri);

			return (await response.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<T>())!;
		}
		catch
		{
			Assert.NotNull(response);
			Assert.Fail(response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync());
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}

	public static async Task<SsePayload<T>> GetSsePayload<T>(this HttpClient client)
	{
		var payload = await client.GetStringSsePayload();
		var data = JsonSerializer.Deserialize<T[]>(payload.Json, _options)![0];

		return new SsePayload<T>
		{
			Evt = payload.Evt,
			Id = payload.Id,
			Json = data,
		};
	}

	public static async Task<SsePayload<string>> GetStringSsePayload(this HttpClient client)
	{
		var key = client.DefaultRequestHeaders.SingleOrDefault(x => x.Key == "Player-Key");
		Assert.NotEqual(default, key);

		// get the stream synchronously
		await using var stream = client
			.GetStreamAsync("/SSE?playerKey=" + key.Key)
			.GetAwaiter()
			.GetResult();

		using var sr = new StreamReader(stream);

		Assert.Equal("data: empty", await sr.ReadLineAsync());
		Assert.Equal("", await sr.ReadLineAsync());
		Assert.Equal("", await sr.ReadLineAsync());

		using var cts = new CancellationTokenSource(5_000);
		try
		{
			var e = await sr.ReadLineAsync(cts.Token);
			var json = await sr.ReadLineAsync(cts.Token);
			var id = await sr.ReadLineAsync(cts.Token);

			return new SsePayload<string>
			{
				Evt = e![7..], // event:
				Json = json![6..], // data:
				Id = int.Parse(id![4..]), // id:
			};
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
			Assert.Fail("Never received SSE payload");
			throw;
		}
	}

	public static async Task<HttpContentHeaders> EnsureGetHeadersAsync(this HttpClient client, string requestUri)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = await client.GetAsync(requestUri);

			return response.EnsureSuccessStatusCode().Content.Headers;
		}
		catch
		{
			Assert.NotNull(response);
			Assert.Fail(response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync());
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}
}
