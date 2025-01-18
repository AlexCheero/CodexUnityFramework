using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using CodexFramework.Utils.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System.Reflection;
#endif

namespace CodexFramework.Utils
{
    [Serializable]
    public class SerializableDict<K, V>
    {
        [SerializeField]
        private MyTuple<K, V>[] _pairs;
        private Dictionary<K, V> _dict;

        public Dictionary<K, V> Dict
        {
            get
            {
                _dict ??= ConvertToDict(_pairs);
                return _dict;
            }
        }

        private Dictionary<K, V> ConvertToDict(MyTuple<K, V>[] pairs)
        {
            var set = new Dictionary<K, V>(pairs.Length);
            for (int i = 0; i < pairs.Length; i++)
                set.Add(pairs[i].Item1, pairs[i].Item2);
            pairs = null;

            return set;
        }
        
        public V this[K key]
        {
            get
            {
                _dict ??= ConvertToDict(_pairs);
#if DEBUG
                if (!_dict.ContainsKey(key))
                    throw new IndexOutOfRangeException("Set have no such entry!");
#endif
                return _dict[key];
            }
        }

        public bool Have(K key)
        {
            _dict ??= ConvertToDict(_pairs);
            return _dict.ContainsKey(key);
        }
    }
    
    [Serializable]
    public struct Trigger<T>
    {
        [SerializeField]
        private T _t;
        public T Check()
        {
            var result = _t;
            _t = default;
            return result;
        }

        public void Set(T t) => _t = t;
    }

    static class IListExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = UnityEngine.Random.Range(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public static T GetRandomItem<T>(this IList<T> list)
        {
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public delegate bool MergeSortComparator<T>(T a, T b);
        public static void InPlaceMergeSort<T>(this IList<T> list, MergeSortComparator<T> comparator, int low = 0) =>
            list.InPlaceMergeSort(comparator, low, list.Count - 1);
        public static void InPlaceMergeSort<T>(this IList<T> list, MergeSortComparator<T> comparator, int low, int high)
        {
            if (low < high)
            {
                int middle = low + (high - low) / 2;

                list.InPlaceMergeSort(comparator, low, middle);
                list.InPlaceMergeSort(comparator, middle + 1, high);

                list.Merge(comparator, low, middle, high);
            }
        }

        public static void Merge<T>(this IList<T> list, MergeSortComparator<T> comparator, int low, int middle, int high)
        {
            int i = low;
            int j = middle + 1;

            while (i <= middle && j <= high)
            {
                if (comparator(list[j], list[i]))
                {
                    i++;
                }
                else
                {
                    var value = list[j];
                    int index = j;

                    // Shift all the elements between element i and j to the right by one.
                    while (index != i)
                    {
                        list[index] = list[index - 1];
                        index--;
                    }
                    list[i] = value;

                    // Adjust the pointers
                    i++;
                    middle++;
                    j++;
                }
            }
        }
    }

    [Serializable]
    public struct MyTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
    }

    [Serializable]
    public struct WeightedValue<T>
    {
        public T Value;
        public float Weight;
    }

    [Serializable]
    public struct CountedValue<T>
    {
        public T Value;
        public int Count;
    }

    [Serializable]
    public struct DefaultableValue<T> where T : struct
    {
        private T? _initialValue;
        [SerializeField]
        private T _value;

        public T InitialValue { get { _initialValue ??= _value; return _initialValue.Value; } }
        public T Value
        {
            get => _value;
            set { _initialValue ??= _value; _value = value; }
        }

        public DefaultableValue(T value) => _initialValue = _value = value;

        public void Reset() => _value = InitialValue;
    }

    public static class Utils
    {
        public static void SwitchRBPhysics(this Rigidbody rb, bool on)
        {
            rb.useGravity = on;
            rb.isKinematic = !on;
        }

        public static Vector3 GetRandomVector3(float minValue, float maxValue)
        {
            float x = UnityEngine.Random.Range(minValue, maxValue);
            float y = UnityEngine.Random.Range(minValue, maxValue);
            float z = UnityEngine.Random.Range(minValue, maxValue);
            return new Vector3(x, y, z);
        }

        public static bool GetTouchDownPosition(ref Vector3 position)
        {
            if (Input.mousePresent)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    position = Input.mousePosition;
                    return true;
                }
            }
            else
            {
                if (Input.touchCount > 0)
                {
                    var touch = Input.GetTouch(0);
                    if (touch.phase != TouchPhase.Began)
                        return false;
                    position = Input.GetTouch(0).position;
                    return true;
                }
            }

            return false;
        }

        public static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
                SetLayerRecursively(child.gameObject, newLayer);
        }

        private static NavMeshPath _path;
        public static bool CheckReachability(Vector3 from, Vector3 to)
        {
            _path ??= new();
            _path.ClearCorners();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, _path);
        }

        public static T GetRandomItem<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.ElementAt(UnityEngine.Random.Range(0, enumerable.Count()));
        }

        public static void SetAlpha(this Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        public static void SetAlpha(this SpriteRenderer spriteRenderer, float alpha)
        {
            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        public static void SetAlpha(this TextMeshProUGUI text, float alpha)
        {
            var color = text.color;
            color.a = alpha;
            text.color = color;
        }

        public static void SetAlpha(this Material material, float alpha)
        {
            var color = material.color;
            color.a = alpha;
            material.color = color;
        }

        public static int CycleAdvance(int value, int step, int max)
        {
            var result = value + step;
            while (result < 0)
                result = max + result;
            result %= max;
            return result;
        }

        public static bool IsRectOnTheLeft(RectTransform rect, RectTransform otherRect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3[] otherCorners = new Vector3[4];
            otherRect.GetWorldCorners(otherCorners);

            return corners[2].x < otherCorners[0].x;
        }

        public static bool RectIntersects(RectTransform rectTransformA, RectTransform rectTransformB)
        {
            Vector3[] cornersA = new Vector3[4];
            Vector3[] cornersB = new Vector3[4];
            rectTransformA.GetWorldCorners(cornersA);
            rectTransformB.GetWorldCorners(cornersB);

            // Check for intersection
            if (cornersA[2].x < cornersB[0].x || cornersA[0].x > cornersB[2].x)
                return false;
            if (cornersA[2].y < cornersB[0].y || cornersA[0].y > cornersB[2].y)
                return false;

            return true;
        }

        public static void SetScaleForMatrix(this ref Matrix4x4 matrix, Vector3 desiredScale)
        {
            // Normalize rotation columns
            Vector3 right = matrix.GetColumn(0).normalized;
            Vector3 up = matrix.GetColumn(1).normalized;
            Vector3 forward = matrix.GetColumn(2).normalized;

            // Scale them
            matrix.SetColumn(0, right * desiredScale.x);
            matrix.SetColumn(1, up * desiredScale.y);
            matrix.SetColumn(2, forward * desiredScale.z);
        }

        public static Vector3 GetScaleFromMatrix(this Matrix4x4 matrix)
        {
            Vector3 scale;
            scale.x = matrix.GetColumn(0).magnitude;
            scale.y = matrix.GetColumn(1).magnitude;
            scale.z = matrix.GetColumn(2).magnitude;

            return scale;
        }

        public static Color LerpColors(Color color1, Color color2, float t) => new(
                Mathf.Lerp(color1.r, color2.r, t),
                Mathf.Lerp(color1.g, color2.g, t),
                Mathf.Lerp(color1.b, color2.b, t),
                Mathf.Lerp(color1.a, color2.a, t));

        public static bool IsAbove(Transform viewerTransform, Vector3 targetPosition)
        {
            var vectorToTarget = targetPosition - viewerTransform.position;
            return Vector3.Dot(vectorToTarget, viewerTransform.up) > 0;
        }

        public static bool IsOnTheRight(Transform viewerTransform, Vector3 targetPosition)
        {
            var vectorToTarget = targetPosition - viewerTransform.position;
            var cross = Vector3.Cross(viewerTransform.forward, vectorToTarget);
            return Vector3.Dot(cross, viewerTransform.up) > 0;
        }

        public static Dictionary<T1, T2> TupleEnumerableToDict<T1, T2>(IEnumerable<MyTuple<T1, T2>> enumerable)
        {
            var dict = new Dictionary<T1, T2>();
            foreach (var item in enumerable)
                dict[item.Item1] = item.Item2;
            return dict;
        }

        public static float GetAngleFrom0(float angle)
        {
            angle %= 360;
            if (Mathf.Abs(angle) < 180)
                return angle;

            return angle - Mathf.Sign(angle) * 360;
        }

        private static RaycastHit[] _castBuffer = new RaycastHit[32];
        public static (RaycastHit[], int) RayCastNonAlloc(
            Ray ray,
            float maxDistance,
            int layerMask,
            bool sort = false)
        {
            var num = Physics.RaycastNonAlloc(ray, _castBuffer, maxDistance, layerMask);
            if (sort)
                _castBuffer.InPlaceMergeSort((r1, r2) => r1.distance > r2.distance, 0, num - 1);
            return (_castBuffer, num);
        }

        public static (RaycastHit[], int) RayCastNonAlloc(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            int layerMask,
            bool sort = false)
        {
            var num = Physics.RaycastNonAlloc(origin, direction, _castBuffer, maxDistance, layerMask);
            if (sort)
                _castBuffer.InPlaceMergeSort((r1, r2) => r1.distance > r2.distance, 0, num - 1);
            return (_castBuffer, num);
        }

        public static (RaycastHit[], int) SphereCastNonAlloc(
            Ray ray,
            float radius,
            float maxDistance,
            int layerMask) => SphereCastNonAlloc(ray.origin, radius, ray.direction, maxDistance, layerMask);
        public static (RaycastHit[], int) SphereCastNonAlloc(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float maxDistance,
            int layerMask) =>
            (_castBuffer, Physics.SphereCastNonAlloc(origin, radius, direction, _castBuffer, maxDistance, layerMask));

        private static readonly Collider[] _overlapBuffer = new Collider[32];
        public static (Collider[], int) OverlapSphereNonAlloc(Vector3 position, float radius, int layerMask) =>
            (_overlapBuffer, Physics.OverlapSphereNonAlloc(position, radius, _overlapBuffer, layerMask));

        public static bool GetTouchPosition(out Vector3 position)
        {
            position = Vector3.zero;
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
#else
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
                return false;
            position = Input.GetTouch(0).position;
            return true;
        }
#endif

            return false;
        }

        public static int FindClosestBiggerPowerOfTwo(int n)
        {
            int power = 1;
            while (power < n)
                power <<= 1;
            return power;
        }

        public static int GetRandomIndexByWeight(IList<float> weights)
        {
            var totalSum = 0f;
            foreach (var weight in weights)
                totalSum += weight;
            var random = UnityEngine.Random.Range(0f, totalSum);
            for (int i = 0; i < weights.Count; i++)
            {
                if (random < weights[i])
                    return i;
                random -= weights[i];
            }

            Debug.LogError("This should never happen");
            return 0;
        }

        public static T GetRandomObjectByWeight<T>(IEnumerable<WeightedValue<T>> weightedObjects)
        {
            var totalSum = 0f;
            foreach (var x in weightedObjects)
                totalSum += x.Weight;
            var random = UnityEngine.Random.Range(0f, totalSum);
            foreach (var variant in weightedObjects)
            {
                if (random < variant.Weight)
                    return variant.Value;
                random -= variant.Weight;
            }

            Debug.LogError("This should never happen");
            return weightedObjects.First().Value;
        }

        public static EntityView GetPooledEntityView(
            PooledEntityView prototype,
            int initialCapacity,
            EcsWorld world,
            Action<PoolItem> onGet = null,
            Action<PoolItem> onReturn = null)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype, initialCapacity, onGet, onReturn);
            var view = pool.Get().GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static EntityView GetPooledEntityView(
            PooledEntityView prototype,
            int initialCapacity,
            EcsWorld world,
            Vector3 position,
            Action<PoolItem> onGet = null,
            Action<PoolItem> onReturn = null)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype, initialCapacity, onGet, onReturn);
            var view = pool.Get(position).GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static EntityView GetPooledEntityView(
            PooledEntityView prototype,
            int initialCapacity,
            EcsWorld world,
            Vector3 position,
            Quaternion rotation,
            Action<PoolItem> onGet = null,
            Action<PoolItem> onReturn = null)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype, initialCapacity, onGet, onReturn);
            var view = pool.Get(position, rotation).GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static Mesh BakeMesh(
            SkinnedMeshRenderer[] skins,
            MeshFilter[] meshes,
            Vector3 scale,
            bool mergeMeshes,
            bool optimize)
        {
            var invertedScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            Matrix4x4 scaleMatrix = Matrix4x4.Scale(invertedScale);
            var skinsLength = skins != null ? skins.Length : 0;
            var meshesLength = meshes != null ? meshes.Length : 0;
            var combine = new CombineInstance[skinsLength + meshesLength];
            for (int i = 0; i < skinsLength; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = skins[i];
                var mesh = new Mesh();
                skinnedMeshRenderer.BakeMesh(mesh, true);
                if (optimize)
                    mesh.Optimize();

                combine[i].mesh = mesh;
                combine[i].transform = scaleMatrix * skinnedMeshRenderer.transform.localToWorldMatrix;
            }
            for (int i = 0; i < meshesLength; i++)
            {
                Mesh mesh = meshes[i].sharedMesh != null ? meshes[i].sharedMesh : meshes[i].mesh;
                if (optimize)
                    mesh.Optimize();

                var combineIdx = i + skinsLength;
                combine[combineIdx].mesh = mesh;
                combine[combineIdx].transform = scaleMatrix * meshes[i].transform.localToWorldMatrix;
            }
            var combinedMesh = new Mesh();
            combinedMesh.CombineMeshes(combine, mergeMeshes);

            return combinedMesh;
        }

#if DEBUG
        public static GameObject CreateDebugIndicatorObject(string name = "IndicatorObject", PrimitiveType primitive = PrimitiveType.Sphere, float scale = 0.3f)
            => CreateDebugIndicatorObject(Color.cyan, name, primitive, scale);
        public static GameObject CreateDebugIndicatorObject(Color color, string name = "IndicatorObject", PrimitiveType primitive = PrimitiveType.Sphere, float scale = 0.3f)
        {
            var indicator = GameObject.CreatePrimitive(primitive);
            UnityEngine.Object.Destroy(indicator.GetComponent<Collider>());
            indicator.transform.localScale = new Vector3(scale, scale, scale);
            indicator.GetComponent<Renderer>().material.color = color;
            indicator.name = name;

            return indicator;
        }
#endif
    }

#if UNITY_EDITOR
    public static class EditorUtils
    {
        [MenuItem("Utils/Clear PlayerPrefs", false, -1)]
        private static void ClearPlayerPrefs() => PlayerPrefs.DeleteAll();

        public static void GenerateFolderPaths(string FullPath)
        {
            string[] requiredFolders = FullPath.Split("/");
            string path = string.Empty;
            for (int i = 0; i < requiredFolders.Length; i++)
            {
                path += requiredFolders[i];
                if (!AssetDatabase.IsValidFolder(path))
                    System.IO.Directory.CreateDirectory(path);
            }
        }

        public static void GenerateFolderPaths_AssetDatabase(string FullPath)
        {
            string[] requiredFolders = FullPath.Split("/");
            string path = requiredFolders[0];
            for (int i = 1; i < requiredFolders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(path + requiredFolders[i]))
                    AssetDatabase.CreateFolder(path, requiredFolders[i]);
                path += requiredFolders[i];
            }
        }

        // Sphere with radius of 1
        private static readonly Vector4[] s_UnitSphere = MakeUnitSphere(16);

        private static Vector4[] MakeUnitSphere(int len)
        {
            Debug.Assert(len > 2);
            var v = new Vector4[len * 3];
            for (int i = 0; i < len; i++)
            {
                var f = i / (float)len;
                float c = Mathf.Cos(f * (float)(Mathf.PI * 2.0));
                float s = Mathf.Sin(f * (float)(Mathf.PI * 2.0));
                v[0 * len + i] = new Vector4(c, s, 0, 1);
                v[1 * len + i] = new Vector4(0, c, s, 1);
                v[2 * len + i] = new Vector4(s, 0, c, 1);
            }
            return v;
        }

        public static void DrawDebugSphere(Vector3 pos, float radius, Color color, float duration = 0.0f)
            => DrawDebugSphere(new Vector4(pos.x, pos.y, pos.z), radius, color, duration);
        public static void DrawDebugSphere(Vector4 pos, float radius, Color color, float duration = 0.0f)
        {
            Vector4[] v = s_UnitSphere;
            int len = s_UnitSphere.Length / 3;
            for (int i = 0; i < len; i++)
            {
                var sX = pos + radius * v[0 * len + i];
                var eX = pos + radius * v[0 * len + (i + 1) % len];
                var sY = pos + radius * v[1 * len + i];
                var eY = pos + radius * v[1 * len + (i + 1) % len];
                var sZ = pos + radius * v[2 * len + i];
                var eZ = pos + radius * v[2 * len + (i + 1) % len];
                Debug.DrawLine(sX, eX, color, duration);
                Debug.DrawLine(sY, eY, color, duration);
                Debug.DrawLine(sZ, eZ, color, duration);
            }
        }

        private static IEnumerable<T> GetComponentsFromProject<T>() where T : Component
        {
            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
            List<T> components = new();
            foreach (string guid in prefabGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var comp = prefab.GetComponent<T>();
                if (comp != null)
                    components.Add(comp);
            }

            return components;
        }

        //[MenuItem("../../..")]
        public static void ChangeComponentsInProject<T>(Action<T> changer) where T : Component
        {
            var components = GetComponentsFromProject<T>();
            foreach (var component in components)
            {
                var go = component.gameObject;
                changer(component);
                EditorUtility.SetDirty(go);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Changing " + typeof(T).Name + " in project complete");
        }

        [MenuItem("Utils/" + nameof(CacheMeatParts))]
        public static void CacheMeatParts() => ChangeComponentsInProject<MeatPartsView>(view =>
        {
            ref var component = ref view.Component;
            var rigidBodies = view.GetComponentsInChildren<Rigidbody>();
            component.parts = new(rigidBodies.Length);
            foreach (var rb in rigidBodies)
            {
                if (rb.gameObject != view.gameObject)
                    component.parts.Add(new MeatParts.RbPositionPair { Rb = rb, LocalPosition = rb.transform.localPosition });
            }
        });

        public static void ClearLog()
        {
            var assembly = Assembly.GetAssembly(typeof(Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
        }
    }
#endif
}