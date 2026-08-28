using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Utilities
{
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        private readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Register(string key, GameObject prefab, int preload = 4)
        {
            if (prefab == null || string.IsNullOrEmpty(key)) return;
            _prefabs[key] = prefab;
            if (!_pools.ContainsKey(key))
                _pools[key] = new Queue<GameObject>();
            for (int i = 0; i < preload; i++)
            {
                var go = Instantiate(prefab, transform);
                go.SetActive(false);
                _pools[key].Enqueue(go);
            }
        }

        public GameObject Get(string key, Vector3 pos, Quaternion rot)
        {
            if (!_pools.TryGetValue(key, out var q) || q.Count == 0)
            {
                if (!_prefabs.TryGetValue(key, out var prefab) || prefab == null)
                    return null;
                var created = Instantiate(prefab, pos, rot);
                created.SetActive(true);
                return created;
            }
            var go = q.Dequeue();
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            return go;
        }

        public void Release(string key, GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(transform);
            if (!_pools.ContainsKey(key))
                _pools[key] = new Queue<GameObject>();
            _pools[key].Enqueue(go);
        }
    }
}
