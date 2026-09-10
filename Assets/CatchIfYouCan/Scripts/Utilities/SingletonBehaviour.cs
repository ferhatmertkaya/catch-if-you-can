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

            // Only a ROOT object can be kept across scenes. Unity logs "DontDestroyOnLoad
            // only works for root GameObjects" and does nothing otherwise - so a subsystem
            // parented under its manager was never actually persisting, and said so three
            // times on every boot. Detached first, which is what the call needs and what the
            // singleton contract has always assumed.
            if (persist)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
