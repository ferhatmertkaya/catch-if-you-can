using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            Services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class
        {
            Services.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var s))
                return s as T;
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        public static void Clear() => Services.Clear();
    }
}
