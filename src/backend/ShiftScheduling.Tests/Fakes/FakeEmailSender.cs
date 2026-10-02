using ShiftScheduling.Common.Email;

namespace ShiftScheduling.Tests.Fakes;

// Fake IEmailSender ghi nhận email đã gửi để kiểm thử notification mà không gọi SMTP thật.
public sealed record SentEmail(string ToEmail, string Subject, string Body);

public sealed class FakeEmailSender : IEmailSender
{
    public List<SentEmail> SentEmails { get; } = [];
    public bool ThrowOnSend { get; set; }
    public int SendCallCount { get; private set; }

    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        SendCallCount++;

        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Simulated SMTP failure.");
        }

        SentEmails.Add(new SentEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }
}
