using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class VerificationCodeDispatcherTests
    {
        private const string Phone = "+5511987654312";
        private const string OtherPhone = "+5511987654313";
        private const string Email = "fulano@exemplo.com";
        private const string Code = "654321";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly VerificationCodeQueue _queue = new(NullLogger<VerificationCodeQueue>.Instance);
        private readonly Mock<IVerificationCodeSender> _smsMock = new();
        private readonly Mock<IVerificationCodeSender> _emailMock = new();
        private readonly RecordingLogger _logger = new();

        private VerificationCodeDispatcher CreateDispatcher() => new(_queue, _smsMock.Object, _emailMock.Object, _logger);

        private static VerificationCodeMessage Sms(string destination = Phone) =>
            new(VerificationChannel.Sms, destination, Code, VerificationPurpose.PasswordReset);

        private static VerificationCodeMessage EmailMessage() =>
            new(VerificationChannel.Email, Email, Code, VerificationPurpose.Registration);

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        [Fact]
        public async Task QueueSendAsync_ShouldReturnWithoutCallingAnyDelivery()
        {
            await _queue.SendAsync(Sms());

            _smsMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
            _emailMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task QueueSendAsync_WhenQueueIsFull_ShouldDiscardWithoutThrowing()
        {
            for (var i = 0; i < 100; i++)
                await _queue.SendAsync(Sms());

            var act = () => _queue.SendAsync(Sms(OtherPhone));

            await act.Should().NotThrowAsync();
            _queue.Reader.Count.Should().Be(100);
        }

        [Fact]
        public async Task ExecuteAsync_WhenSmsIsQueued_ShouldDeliverOnlyThroughSms()
        {
            var delivered = NewSignal();
            var message = Sms();
            _smsMock.Setup(x => x.SendAsync(message)).Callback(delivered.SetResult).Returns(Task.CompletedTask);
            var dispatcher = CreateDispatcher();

            await dispatcher.StartAsync(CancellationToken.None);
            await _queue.SendAsync(message);
            await delivered.Task.WaitAsync(Timeout);
            await dispatcher.StopAsync(CancellationToken.None);

            _smsMock.Verify(x => x.SendAsync(message), Times.Once);
            _emailMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenEmailIsQueued_ShouldDeliverOnlyThroughEmail()
        {
            var delivered = NewSignal();
            var message = EmailMessage();
            _emailMock.Setup(x => x.SendAsync(message)).Callback(delivered.SetResult).Returns(Task.CompletedTask);
            var dispatcher = CreateDispatcher();

            await dispatcher.StartAsync(CancellationToken.None);
            await _queue.SendAsync(message);
            await delivered.Task.WaitAsync(Timeout);
            await dispatcher.StopAsync(CancellationToken.None);

            _emailMock.Verify(x => x.SendAsync(message), Times.Once);
            _smsMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDeliveryFails_ShouldLogMaskedAndKeepDelivering()
        {
            var nextDelivered = NewSignal();
            var failing = Sms();
            var next = Sms(OtherPhone);
            _smsMock.Setup(x => x.SendAsync(failing)).ThrowsAsync(new HttpRequestException("falhou"));
            _smsMock.Setup(x => x.SendAsync(next)).Callback(nextDelivered.SetResult).Returns(Task.CompletedTask);
            var dispatcher = CreateDispatcher();

            await dispatcher.StartAsync(CancellationToken.None);
            await _queue.SendAsync(failing);
            await _queue.SendAsync(next);
            await nextDelivered.Task.WaitAsync(Timeout);
            await dispatcher.StopAsync(CancellationToken.None);

            _logger.Messages.Should().ContainSingle().Which.Should().Contain("4312").And.NotContain(Phone).And.NotContain(Code);
        }

        [Fact]
        public async Task ExecuteAsync_WhenEmailDeliveryFails_ShouldLogMaskedEmail()
        {
            var nextDelivered = NewSignal();
            var next = Sms();
            _emailMock.Setup(x => x.SendAsync(It.IsAny<VerificationCodeMessage>())).ThrowsAsync(new HttpRequestException("falhou"));
            _smsMock.Setup(x => x.SendAsync(next)).Callback(nextDelivered.SetResult).Returns(Task.CompletedTask);
            var dispatcher = CreateDispatcher();

            await dispatcher.StartAsync(CancellationToken.None);
            await _queue.SendAsync(EmailMessage());
            await _queue.SendAsync(next);
            await nextDelivered.Task.WaitAsync(Timeout);
            await dispatcher.StopAsync(CancellationToken.None);

            _logger.Messages.Should().ContainSingle().Which.Should().Contain("f***@exemplo.com").And.NotContain(Email).And.NotContain(Code);
        }

        private sealed class RecordingLogger : ILogger<VerificationCodeDispatcher>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                Messages.Add(formatter(state, exception));
        }
    }
}
