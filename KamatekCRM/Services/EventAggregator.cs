using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Thread-safe Event Aggregator (Pub/Sub) - ViewModel'ler arası iletişim için.
    /// WeakReference kullanarak kapanan pencere ve ViewModel'lerin bellekte kalmasını (Memory Leak) önler.
    /// </summary>
    public class EventAggregator
    {
        private static readonly Lazy<EventAggregator> _instance = new(() => new EventAggregator());
        public static EventAggregator Instance => _instance.Value;

        private readonly ConcurrentDictionary<Type, List<IWeakSubscription>> _subscriptions = new();
        private readonly object _syncLock = new();

        public EventAggregator() { }

        /// <summary>
        /// Belirli bir event türüne zayıf referans (WeakReference) ile abone ol.
        /// Abonelik sonlandırma için IDisposable token döndürür; mevcut 'void' çağıran yerlerle tam geriye dönük uyumludur.
        /// </summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            var subscription = new WeakSubscription<TEvent>(this, handler);

            lock (_syncLock)
            {
                var list = _subscriptions.GetOrAdd(eventType, _ => new List<IWeakSubscription>());
                list.Add(subscription);
            }

            return subscription;
        }

        /// <summary>
        /// Event yayınla - tüm aktif abonelere bildirim gönderir, ölü referansları (!IsAlive) otomatik temizler.
        /// </summary>
        public void Publish<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (!_subscriptions.TryGetValue(eventType, out var list)) return;

            List<IWeakSubscription>? deadList = null;
            List<IWeakSubscription> currentList;

            lock (_syncLock)
            {
                currentList = new List<IWeakSubscription>(list);
            }

            foreach (var sub in currentList)
            {
                if (sub.IsAlive)
                {
                    try
                    {
                        sub.Invoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"EventAggregator handler error: {ex.Message}");
                    }
                }
                else
                {
                    deadList ??= new List<IWeakSubscription>();
                    deadList.Add(sub);
                }
            }

            if (deadList != null && deadList.Count > 0)
            {
                lock (_syncLock)
                {
                    foreach (var dead in deadList)
                    {
                        list.Remove(dead);
                    }
                }
            }
        }

        /// <summary>
        /// Belirli bir handler'ı abonelikten çıkar.
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) return;

            var eventType = typeof(TEvent);
            if (!_subscriptions.TryGetValue(eventType, out var list)) return;

            lock (_syncLock)
            {
                list.RemoveAll(s => !s.IsAlive || s.Matches(handler));
            }
        }

        internal void RemoveSubscription(Type eventType, IWeakSubscription subscription)
        {
            if (!_subscriptions.TryGetValue(eventType, out var list)) return;

            lock (_syncLock)
            {
                list.Remove(subscription);
            }
        }

        internal interface IWeakSubscription
        {
            bool IsAlive { get; }
            bool Matches(Delegate handler);
            void Invoke(object? eventData);
        }

        private sealed class WeakSubscription<TEvent> : IWeakSubscription, IDisposable
        {
            private readonly EventAggregator _aggregator;
            private readonly WeakReference? _targetRef;
            private readonly WeakReference? _enclosingTargetRef;
            private readonly MethodInfo _method;
            private readonly bool _isStatic;
            private readonly Action<TEvent>? _closureDelegate; // Sadece lambda enclosure canlı kaldığı sürece tutulur
            private bool _isDisposed;

            public WeakSubscription(EventAggregator aggregator, Action<TEvent> handler)
            {
                _aggregator = aggregator;
                _method = handler.Method;

                if (handler.Target == null)
                {
                    _isStatic = true;
                }
                else
                {
                    var target = handler.Target;
                    var targetType = target.GetType();

                    // Derleyici tarafından üretilen lambda / closure sınıfı kontrolü
                    if (targetType.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    {
                        // Closure içerisindeki 'this' (ViewModel vb.) nesnesini zayıf referansla koru
                        object? enclosingInstance = null;
                        foreach (var field in targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                            {
                                var val = field.GetValue(target);
                                if (val != null)
                                {
                                    enclosingInstance = val;
                                    break;
                                }
                            }
                        }

                        if (enclosingInstance != null)
                        {
                            _enclosingTargetRef = new WeakReference(enclosingInstance);
                            _closureDelegate = handler;
                        }
                        else
                        {
                            _targetRef = new WeakReference(target);
                            _closureDelegate = handler;
                        }
                    }
                    else
                    {
                        _targetRef = new WeakReference(target);
                    }
                }
            }

            public bool IsAlive
            {
                get
                {
                    if (_isDisposed) return false;
                    if (_isStatic) return true;
                    if (_enclosingTargetRef != null) return _enclosingTargetRef.IsAlive;
                    return _targetRef != null && _targetRef.IsAlive;
                }
            }

            public bool Matches(Delegate handler)
            {
                if (_isDisposed) return false;
                if (handler.Method != _method) return false;

                if (_isStatic) return handler.Target == null;

                if (_enclosingTargetRef != null)
                {
                    return _enclosingTargetRef.Target == handler.Target;
                }

                return _targetRef?.Target == handler.Target;
            }

            public void Invoke(object? eventData)
            {
                if (!IsAlive) return;

                if (_isStatic)
                {
                    _method.Invoke(null, new[] { eventData });
                    return;
                }

                if (_closureDelegate != null)
                {
                    if (_enclosingTargetRef == null || _enclosingTargetRef.IsAlive)
                    {
                        _closureDelegate((TEvent)eventData!);
                    }
                    return;
                }

                var target = _targetRef?.Target;
                if (target != null)
                {
                    _method.Invoke(target, new[] { eventData });
                }
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    _aggregator.RemoveSubscription(typeof(TEvent), this);
                }
            }
        }
    }

    /// <summary>
    /// Ağ bağlantısı veya veritabanı bağlantısı koptuğunda fırlatılır
    /// </summary>
    public class DatabaseConnectionLostEvent { }

    /// <summary>
    /// Ağ bağlantısı veya veritabanı bağlantısı tekrar sağlandığında fırlatılır
    /// </summary>
    public class DatabaseConnectionRestoredEvent { }

    /// <summary>
    /// İlk kurulumda veya yeniden denemede veritabanı bağlantısı kesinleştiğinde fırlatılır
    /// </summary>
    public class DatabaseConnectionEstablishedEvent { }

    /// <summary>
    /// Hiçbir sunucu bulunamadığında UI tarafında manuel bağlantı sihirbazını göstermek için fırlatılır
    /// </summary>
    public class ShowManualConnectionWizardEvent { }

    /// <summary>
    /// Ağ Yönetimi sayfasına navigasyon istendiğinde fırlatılır.
    /// SettingsViewModel → MainContentViewModel iletişimi için kullanılır.
    /// </summary>
    public class NavigateToNetworkSettingsEvent { }
}
