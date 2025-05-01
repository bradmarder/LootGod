using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

public class PayloadDeliveryService(
	ILogger<PayloadDeliveryService> _logger,
	Channel<Payload> _payloadChannel,
	ConcurrentDictionary<string, DataSink> _dataSinks) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach (var payload in _payloadChannel.Reader.ReadAllAsync(stoppingToken))
		{
			var watch = Stopwatch.StartNew();

			foreach (var sink in _dataSinks)
			{
				if (payload.GuildId is not null && payload.GuildId != sink.Value.GuildId)
				{
					continue;
				}

				var text = new StringBuilder()
					.AppendLine("event: " + payload.Event)
					.AppendLine("data: " + payload.JsonData)
					.AppendLine("id: " + sink.Value.IncrementEventId())
					.AppendLine()
					.AppendLine()
					.ToString();
				try
				{
					using var failsafe = new CancellationTokenSource(1_000);
					using var link = CancellationTokenSource.CreateLinkedTokenSource(failsafe.Token, sink.Value.Token, stoppingToken);

					var res = sink.Value.Response;
					await res.WriteAsync(text, link.Token);
					await res.Body.FlushAsync(link.Token);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Orphan connection removed - {ConnectionId}", sink.Key);
					_dataSinks.Remove(sink.Key, out _);
				}
			}

			using var __ = _logger.BeginScope(new
			{
				GuildId = payload.GuildId,
				Event = payload.Event,
				JsonLength = payload.JsonData.Length,
				DataSinkCount = _dataSinks.Count,
				ElapsedMilliseconds = watch.ElapsedMilliseconds,
			});
			_logger.LogInformation("PayloadDeliveryService");
		}
	}
}
