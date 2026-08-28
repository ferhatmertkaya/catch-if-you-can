using UnityEngine;

namespace CatchIfYouCan.Utilities
{
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; protected set; }
        [SerializeField] protected bool persist = true;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this as T;
            if (Instance == null)
                Instance = GetComponent<T>();

            if (persist)
                DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
