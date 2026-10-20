using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Sổ tra cứu các Manager: mỗi Manager tự đăng ký khi khởi động (<see cref="ManagersRoot"/>), script khác lấy bằng
    /// <c>Services.Get&lt;AudioManager&gt;()</c>. Chỉ gọi trong Awake / Start / Init rồi giữ lại, không gọi trong Update.
    /// Logic thuần (Model) không dùng Services: nó nhận thứ cần qua constructor.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class => Map[typeof(T)] = service;

        public static T Get<T>() where T : class => Map.TryGetValue(typeof(T), out var service) ? (T)service : null;

        public static bool Has<T>() where T : class => Map.ContainsKey(typeof(T));

        /// <summary>Gỡ <paramref name="service"/> khi nó bị huỷ (chỉ gỡ nếu đúng là cái đang đăng ký).</summary>
        public static void Unregister<T>(T service) where T : class
        {
            if (Map.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service)) Map.Remove(typeof(T));
        }

        public static void Clear() => Map.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
