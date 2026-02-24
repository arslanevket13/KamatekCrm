using System;
using System.Collections.Generic;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Basit Event Aggregator (Pub/Sub) - ViewModel'ler arası iletişim için
    /// WeakReference kullanarak memory leak önlenir
    /// </summary>
    public class EventAggregator
    {
        private static readonly Lazy<EventAggregator> _instance = new(() => new EventAggregator());
        public static EventAggregator Instance => _instance.Value;

        private readonly Dictionary<Type, List<WeakReference>> _subscribers = new();
        private readonly object _lock = new();

        private EventAggregator() { }

        /// <summary>
        /// Belirli bir event türüne abone ol
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                {
                    _subscribers[eventType] = new List<WeakReference>();
                }
                _subscribers[eventType].Add(new WeakReference(handler));
            }
        }

        /// <summary>
        /// Event yayınla - tüm abonelere bildirim gönder
        /// </summary>
        public void Publish<TEvent>(TEvent eventData)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType)) return;

                var deadReferences = new List<WeakReference>();

                foreach (var weakRef in _subscribers[eventType])
                {
                    if (weakRef.Target is Action<TEvent> handler)
                    {
                        try
                        {
                            handler(eventData);
                        }
                        catch (Exception ex)
                        {
                            // Hata olsa bile diğer subscriber'lara devam et
                            System.Diagnostics.Debug.WriteLine($"EventAggregator handler error: {ex.Message}");
                        }
                    }
                    else
                    {
                        deadReferences.Add(weakRef);
                    }
                }

                // Ölü referansları temizle
                foreach (var dead in deadReferences)
                {
                    _subscribers[eventType].Remove(dead);
                }
            }
        }

        /// <summary>
        /// Belirli bir handler'ı abonelikten çıkar
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType)) return;

                var toRemove = new List<WeakReference>();
                foreach (var weakRef in _subscribers[eventType])
                {
                    if (weakRef.Target == null || weakRef.Target.Equals(handler))
                    {
                        toRemove.Add(weakRef);
                    }
                }

                foreach (var item in toRemove)
                {
                    _subscribers[eventType].Remove(item);
                }
            }
        }
    }
}
