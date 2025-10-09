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
			using var __ = _logger.BeginScope(new
			{
				GuildId = payload.GuildId,
				Event = payload.Event,
				JsonLength = payload.JsonData.Length,
				DataSinkCount = _dataSinks.Count,
			});

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
				using var failsafe = new CancellationTokenSource(1_000);
				using var link = CancellationTokenSource.CreateLinkedTokenSource(failsafe.Token, sink.Value.Token, stoppingToken);
				var response = sink.Value.Response;
				try
				{
					await response.WriteAsync(text, link.Token);
					await response.Body.FlushAsync(link.Token);
				}
				catch (Exception ex)
				{
					using var ____ = _logger.BeginScope(new
					{
						EventId = sink.Value.EventId,
						LinkCancel = link.IsCancellationRequested,
						FailSafeCancel = failsafe.IsCancellationRequested,
						SinkCancel = sink.Value.Token.IsCancellationRequested,
						StoppingCancel = stoppingToken.IsCancellationRequested,
					});
					_logger.LogError(ex, "Broken connection detected - {ConnectionId}", sink.Key);
					_dataSinks.Remove(sink.Key, out _);
				}
			}

			using var ___ = _logger.BeginScope(new
			{
				ElapsedMs = watch.ElapsedMilliseconds,
			});
			_logger.LogInformation("PayloadDeliveryService");
		}
	}
}
