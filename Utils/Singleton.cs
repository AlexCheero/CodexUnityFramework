using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.Utils
{
    public abstract class Singleton : MonoBehaviour
    {
        public bool IsInited { get; }
    }
    
    public class Singleton<T> : Singleton where T : MonoBehaviour
    {
        [SerializeField]
        private bool _dontDestroyOnLoad;

        public static void ForceInit()
        {
            if (_instance != null)
                return;
            
            var singleton = FindFirstObjectByType<Singleton<T>>(FindObjectsInactive.Include);
            if (singleton != null)
                singleton.Awake();
        }

        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogWarning(typeof(T).Name + " instance not found, creating new one!");
                    _instance = new GameObject(typeof(T).Name).AddComponent<T>();
                }
                return _instance;
            }
        }

        public static bool IsCreated => _instance != null;
        
        public bool IsInited
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _instance == this;
        }

        void Awake()
        {
            if (_instance != null)
            {
                if (_instance.gameObject.scene.buildIndex != -1)
                    Debug.LogWarning(GetType().FullName + " instance already created!");
                Destroy(this);
                return;
            }

            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _instance = this as T;

            Init();
        }

        protected virtual void Init() { }

        void OnDestroy()
        {
            if (this == _instance)
                _instance = null;
        }
    }
}