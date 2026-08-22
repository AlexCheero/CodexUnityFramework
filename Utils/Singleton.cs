using System.Runtime.CompilerServices;
using UnityEngine;

namespace CodexFramework.Utils
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        [SerializeField]
        private bool _dontDestroyOnLoad;

        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                var instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                if (instance == null)
                    instance = new GameObject(typeof(T).Name).AddComponent<T>();
                InitInstance(instance);
                return _instance;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static DT InstanceAs<DT>() where DT : T
        {
#if DEBUG
            if (Instance is not DT)
                Debug.LogError("WrongType");
#endif
            return (DT)Instance;
        }

        private static void InitInstance(T instance)
        {
            var singleton = instance as Singleton<T>;
            if (singleton._dontDestroyOnLoad)
                DontDestroyOnLoad(singleton.gameObject);
            _instance = instance;
            singleton.Init();
        }

        public static bool IsCreated => _instance != null;

        void Awake()
        {
            if (_instance == this)
                return;
            if (_instance != null)
            {
                if (_instance.gameObject.scene.buildIndex != -1)
                    Debug.LogWarning(GetType().FullName + " instance already created!");
                Destroy(this);
                return;
            }
            
            InitInstance(this as T);
        }

        protected virtual void Init() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearSingletonInstance()
        {
            if (this == _instance)
                _instance = null;
        }

        void OnDestroy() => ClearSingletonInstance();
    }
}