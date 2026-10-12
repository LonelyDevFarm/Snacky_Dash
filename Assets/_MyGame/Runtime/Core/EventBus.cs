using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Bảng tin chung cho các hệ thống không quen nhau: một nơi đăng tin (<c>EventBus.Publish(new LevelWonEvent(5))</c>),
    /// nơi khác nghe (<c>EventBus.Subscribe&lt;LevelWonEvent&gt;(OnWon)</c>, nhớ <c>Unsubscribe</c> khi tắt). Tin là struct
    /// (khai báo trong <see cref="GameEvents"/>) nên không sinh rác bộ nhớ. Logic bên trong một hệ thống vẫn dùng event C# thường.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Handlers.TryGetValue(typeof(T), out var existing);
            Handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out var existing)) return;
            var rest = Delegate.Remove(existing, handler);
            if (rest == null) Handlers.Remove(typeof(T)); else Handlers[typeof(T)] = rest;
        }

        public static void Publish<T>(T message) where T : struct
        {
            if (Handlers.TryGetValue(typeof(T), out var handler)) ((Action<T>)handler)(message);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Handlers.Clear();
    }
}
