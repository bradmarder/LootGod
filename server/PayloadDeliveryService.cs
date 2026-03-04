using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

public class PayloadDeliveryService(
	ILogger<PayloadDeliveryService> _logger,
	ChannelReader<Payload> _channel,
	ConcurrentDictionary<string, DataSink> _dataSinks) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach (var payload in _channel.ReadAllAsync(stoppingToken))
		{
			var watch = Stopwatch.StartNew();
			using var _ = _logger.BeginScope(new LogState
			{
				["GuildId"] = payload.GuildId,
				["Event"] = payload.Event,
				["JsonLength"] = payload.JsonData.Length,
				["DataSinkCount"] = _dataSinks.Count,
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
					.AppendLine("id: " + sink.Value.EventId)
					.AppendLine()
					.AppendLine()
					.ToString();
				using var failsafe = new CancellationTokenSource(1_000);
				using var link = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, failsafe.Token, sink.Value.Context.RequestAborted);
				var response = sink.Value.Context.Response;
				try
				{
					await response.WriteAsync(text, link.Token);
					await response.Body.FlushAsync(link.Token);

					sink.Value.EventId++;
				}
				catch (Exception ex)
				{
					using var __ = _logger.BeginScope(new LogState
					{
						["EventId"] = sink.Value.EventId,
						["LinkCancel"] = link.IsCancellationRequested,
						["FailSafeCancel"] = failsafe.IsCancellationRequested,
						["StoppingCancel"] = stoppingToken.IsCancellationRequested,
						["RequestCancel"] = sink.Value.Context.RequestAborted.IsCancellationRequested,
					});
					_logger.BrokenConnection(ex, sink.Key);

					// abort the broken connection (the datasink will be removed by the CancellationTokenRegistration on the SSE endpoint)
					sink.Value.Context.Abort();
				}
			}

			_logger.PayloadDeliveryComplete(watch.ElapsedMilliseconds);
		}
	}
}