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

        public static void Clear() => Map.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
