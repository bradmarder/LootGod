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

	public static async Task EnsurePostAsJsonAsync(this HttpClient client, string requestUri, CancellationToken token)
	{
		await client.EnsurePostAsJsonAsync<string?>(requestUri, null, token);
	}

	public static async Task EnsureDeleteAsync(this HttpClient client, string requestUri, CancellationToken token)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = await client.DeleteAsync(requestUri, token);
			response.EnsureSuccessStatusCode();
		}
		catch (Exception ex)
		{
			Assert.NotNull(response);
			Assert.Fail(ex.Message + Environment.NewLine + response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync(token));
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}

	public static async Task<string> EnsurePostAsJsonAsync<T>(this HttpClient client, string requestUri, T? value, CancellationToken token)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = value is null
				? await client.PostAsJsonAsync(requestUri, new { }, token)
				: await client.PostAsJsonAsync(requestUri, value, token);

			return await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync(token);
		}
		catch
		{
			Assert.NotNull(response);
			Assert.Fail(response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync(token));
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}

	public static async Task<T> EnsureGetJsonAsync<T>(this HttpClient client, string requestUri, CancellationToken token)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = await client.GetAsync(requestUri, token);

			return (await response.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<T>(token))!;
		}
		catch (Exception ex)
		{
			Assert.NotNull(response);
			Assert.Fail(ex.Message + Environment.NewLine + response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync(token));
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}

	public static async Task<SsePayload<T>> GetSsePayload<T>(this HttpClient client, CancellationToken token)
	{
		var payload = await client.GetStringSsePayload(token);
		var data = JsonSerializer.Deserialize<T[]>(payload.Json, _options)![0];

		return new()
		{
			Evt = payload.Evt,
			Id = payload.Id,
			Json = data,
		};
	}

	public static async Task<SsePayload<string>> GetStringSsePayload(this HttpClient client, CancellationToken token)
	{
		var key = client.DefaultRequestHeaders.SingleOrDefault(x => x.Key is "Player-Key");
		Assert.NotEqual(default, key);

		// get the stream synchronously
		await using var stream = client
			.GetStreamAsync("/SSE?playerKey=" + key.Key, token)
			.GetAwaiter()
			.GetResult();

		using var sr = new StreamReader(stream);

		Assert.Equal("data: empty", await sr.ReadLineAsync(token));
		Assert.Equal("", await sr.ReadLineAsync(token));
		Assert.Equal("", await sr.ReadLineAsync(token));

		using var cts = new CancellationTokenSource(5_000);
		using var link = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);
		try
		{
			var e = await sr.ReadLineAsync(link.Token);
			var json = await sr.ReadLineAsync(link.Token);
			var id = await sr.ReadLineAsync(link.Token);

			Assert.StartsWith("event: ", e);
			Assert.StartsWith("data: ", json);
			Assert.StartsWith("id: ", id);

			return new()
			{
				Evt = e![7..],
				Json = json![6..],
				Id = int.Parse(id![4..]),
			};
		}
		catch (OperationCanceledException) when (cts.IsCancellationRequested)
		{
			Assert.Fail("Never received SSE payload");
			throw;
		}
	}

	public static async Task<HttpContentHeaders> EnsureGetHeadersAsync(this HttpClient client, string requestUri, CancellationToken token)
	{
		HttpResponseMessage? response = null;
		try
		{
			response = await client.GetAsync(requestUri, token);

			return response.EnsureSuccessStatusCode().Content.Headers;
		}
		catch (Exception ex)
		{
			Assert.NotNull(response);
			Assert.Fail(ex.Message + Environment.NewLine + response.ToString() + Environment.NewLine + await response.Content.ReadAsStringAsync(token));
			throw;
		}
		finally
		{
			response?.Dispose();
		}
	}
}
