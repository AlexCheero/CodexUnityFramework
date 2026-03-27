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
                Debug.LogWarning(typeof(T).Name + " instance not found, creating new one!");
                var instance = FindFirstObjectByType<T>();
                if (instance == null)
                    instance = new GameObject(typeof(T).Name).AddComponent<T>();
                InitInstance(instance);
                return _instance;
            }
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

        void OnDestroy()
        {
            if (this == _instance)
                _instance = null;
        }
    }
}