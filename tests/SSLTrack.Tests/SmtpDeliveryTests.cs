namespace SSLTrack.Tests;

public class SmtpDeliveryTests
{
    [Fact]
    public async Task Delivery_uses_latest_database_settings_and_preserves_message_content()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ssltrack-delivery-{Guid.NewGuid()}.db");
        try
        {
            var factory = new TestDbFactory(file);
            using (var db = factory.CreateDbContext())
                await db.Database.MigrateAsync();
            var settings = new SmtpSettingsService(factory, new EphemeralDataProtectionProvider());
            var mail = new MailService(NullLogger<MailService>.Instance, settings)
            {
                Subject = "Certificate expiry",
                Body = "Renew example.com"
            };
            await using var first = new TestSmtpServer();
            await settings.SaveAsync(new SmtpSettingsEdit { MailFrom = "from@example.com", MailTo = "alerts@example.com", SmtpHost = "127.0.0.1", Port = first.Port, TlsMode = SecureSocketOptions.None });
            await mail.SendMailAsync();
            var firstMessage = await first.Message.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains("Certificate expiry", firstMessage);
            Assert.Contains("Renew example.com", firstMessage);
            await using var next = new TestSmtpServer();
            var edit = await settings.GetForEditAsync();
            edit.Port = next.Port;
            edit.MailTo = "next@example.com";
            await settings.SaveAsync(edit);
            await mail.SendMailAsync();
            var nextMessage = await next.Message.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains("next@example.com", nextMessage);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }

    private sealed class TestSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(20));
        public int Port
        {
            get;
        }
        public Task<string> Message
        {
            get;
        }

        public TestSmtpServer()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Message = Receive();
        }

        private async Task<string> Receive()
        {
            using var client = await listener.AcceptTcpClientAsync(stop.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false))
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            await writer.WriteLineAsync("220 localhost SMTP test");
            var message = new StringBuilder();
            bool data = false;
            while (await reader.ReadLineAsync(stop.Token) is { } line)
            {
                if (data)
                {
                    if (line != ".")
                    {
                        message.AppendLine(line);
                        continue;
                    }

                    data = false;
                    await writer.WriteLineAsync("250 queued");
                    continue;
                }

                if (line.StartsWith("EHLO") || line.StartsWith("HELO"))
                {
                    await writer.WriteLineAsync("250 localhost");
                }
                else if (line.StartsWith("MAIL FROM") || line.StartsWith("RCPT TO"))
                {
                    await writer.WriteLineAsync("250 OK");
                }
                else if (line == "DATA")
                {
                    data = true;
                    await writer.WriteLineAsync("354 end with dot");
                }
                else if (line == "QUIT")
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }

            return message.ToString();
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            listener.Stop();
            try
            {
                await Message;
            }
            catch (OperationCanceledException)
            {
            }

            stop.Dispose();
        }
    }
}
