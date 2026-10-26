using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Services;
using Xunit;

namespace KamatekCrm.Tests.Events
{
    public class EventAggregatorTests
    {
        public record SampleMessageEvent(string Message);

        private class SubscriberTarget
        {
            public List<string> ReceivedMessages { get; } = new();

            public void Handle(SampleMessageEvent e)
            {
                ReceivedMessages.Add(e.Message);
            }
        }

        [Fact]
        public void EventAggregator_Publish_ShouldDeliverToSubscriber()
        {
            // Arrange
            var aggregator = new EventAggregator();
            var target = new SubscriberTarget();
            aggregator.Subscribe<SampleMessageEvent>(target.Handle);

            // Act
            aggregator.Publish(new SampleMessageEvent("Hello Antigravity"));

            // Assert
            target.ReceivedMessages.Should().ContainSingle().Which.Should().Be("Hello Antigravity");
        }

        [Fact]
        public void EventAggregator_Unsubscribe_ShouldStopReceivingEvents()
        {
            // Arrange
            var aggregator = new EventAggregator();
            var target = new SubscriberTarget();
            aggregator.Subscribe<SampleMessageEvent>(target.Handle);

            aggregator.Publish(new SampleMessageEvent("Msg 1"));
            aggregator.Unsubscribe<SampleMessageEvent>(target.Handle);
            aggregator.Publish(new SampleMessageEvent("Msg 2"));

            // Assert
            target.ReceivedMessages.Should().ContainSingle().Which.Should().Be("Msg 1");
        }

        [Fact]
        public void EventAggregator_SubscriptionDispose_ShouldUnsubscribe()
        {
            // Arrange
            var aggregator = new EventAggregator();
            var target = new SubscriberTarget();
            var subscription = aggregator.Subscribe<SampleMessageEvent>(target.Handle);

            aggregator.Publish(new SampleMessageEvent("First"));
            subscription.Dispose();
            aggregator.Publish(new SampleMessageEvent("Second"));

            // Assert
            target.ReceivedMessages.Should().ContainSingle().Which.Should().Be("First");
        }

        [Fact]
        public void EventAggregator_WeakReference_ShouldCleanUpDeadSubscribersAfterGC()
        {
            // Arrange
            var aggregator = new EventAggregator();

            Action registerDeadSubscriber = () =>
            {
                var deadTarget = new SubscriberTarget();
                aggregator.Subscribe<SampleMessageEvent>(deadTarget.Handle);
                // deadTarget scope bitince GC tarafından toplanabilir hale gelir
            };

            registerDeadSubscriber();

            // GC çalıştır
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Act: Yayın yapıldığında ölü referans temizlenmeli ve hata fırlatmamalıdır
            Action publish = () => aggregator.Publish(new SampleMessageEvent("After GC"));

            // Assert
            publish.Should().NotThrow();
        }

        [Fact]
        public async Task EventAggregator_ShouldBeThreadSafe_UnderConcurrentPublish()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int counter = 0;
            var syncLock = new object();

            var sub = aggregator.Subscribe<SampleMessageEvent>(_ =>
            {
                lock (syncLock)
                {
                    counter++;
                }
            });

            // Act: 10 paralel task eşzamanlı publish yapıyor
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    aggregator.Publish(new SampleMessageEvent("Concurrent"));
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            counter.Should().Be(20);
            sub.Dispose();
        }
    }
}
