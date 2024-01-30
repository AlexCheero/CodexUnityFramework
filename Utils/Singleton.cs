using UnityEngine;

namespace CodexFramework.Utils
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        [SerializeField]
        private bool _dontDestroyOnLoad;

        public static void ForceInit()
        {
            if (_instance != null)
                return;
            
            var singleton = FindObjectOfType<Singleton<T>>(true);
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
    }
}