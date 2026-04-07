using CodexECS;
using CodexFramework.CodexEcsUnityIntegration.Views;
using CodexFramework.Utils.Pools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System.Reflection;
#endif

namespace CodexFramework.Utils
{
    public static class UtilityMethods
    {
        public static int GetMask(params int[] layers)
        {
            int mask = 0;
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer != -1)
                    mask |= 1 << layer;
            }

            return mask;
        }
        
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAlpha(this Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAlpha(this SpriteRenderer spriteRenderer, float alpha)
        {
            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAlpha(this TextMeshProUGUI text, float alpha)
        {
            var color = text.color;
            color.a = alpha;
            text.color = color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public static Dictionary<T1, T2> TupleEnumerableToDict<T1, T2>(IEnumerable<Pair<T1, T2>> enumerable)
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

        private static readonly RaycastHit[] _castBuffer = new RaycastHit[16];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (RaycastHit[], int) SphereCastNonAlloc(
            Ray ray,
            float radius,
            float maxDistance,
            int layerMask) => SphereCastNonAlloc(ray.origin, radius, ray.direction, maxDistance, layerMask);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (RaycastHit[], int) SphereCastNonAlloc(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float maxDistance,
            int layerMask) =>
            (_castBuffer, Physics.SphereCastNonAlloc(origin, radius, direction, _castBuffer, maxDistance, layerMask));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (RaycastHit[], int) CapsuleCastNonAlloc(CapsuleCollider capsule, Vector3 direction,
            float maxDistance, int layerMask)
        {
            var (p0, p1, radius) = GetCapsuleDimensions(capsule);
            return CapsuleCastNonAlloc(p0, p1, radius, direction, maxDistance, layerMask);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (RaycastHit[], int) CapsuleCastNonAlloc(
            Vector3 point1,
            Vector3 point2,
            float radius,
            Vector3 direction,
            float maxDistance,
            int layerMask) =>
            (_castBuffer, Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, _castBuffer, maxDistance, layerMask));

        private static readonly Collider[] _overlapBuffer = new Collider[16];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Collider[], int) OverlapSphereNonAlloc(Vector3 position, float radius, int layerMask) =>
            (_overlapBuffer, Physics.OverlapSphereNonAlloc(position, radius, _overlapBuffer, layerMask));
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Collider[], int) OverlapCapsuleNonAlloc(CharacterController controller, int layerMask)
        {
            var (p0, p1, radius) = GetCapsuleDimensions(controller);
            return (_overlapBuffer, Physics.OverlapCapsuleNonAlloc(p0, p1, radius, _overlapBuffer, layerMask));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Collider[], int) OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, int layerMask) =>
            (_overlapBuffer, Physics.OverlapCapsuleNonAlloc(point0, point1, radius, _overlapBuffer, layerMask));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector3, Vector3, float) GetCapsuleDimensions(CharacterController controller) =>
            GetCapsuleDimensions(controller, Vector3.up);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector3, Vector3, float) GetCapsuleDimensions(CharacterController controller, Vector3 up)
        {
            var radius = controller.radius;
            var center = controller.transform.TransformPoint(controller.center);
            var height = Mathf.Max(controller.height, radius * 2f);
            var halfHeight = height * 0.5f - radius;
            var p0 = center + up * halfHeight;
            var p1 = center - up * halfHeight;

            return (p0, p1, radius);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector3, Vector3, float) GetCapsuleDimensions(CapsuleCollider collider, Vector3 up)
        {
            var radius = collider.radius;
            var center = collider.bounds.center;
            var height = Mathf.Max(collider.height, radius * 2f);
            var halfHeight = height * 0.5f - radius;
            var p0 = center + up * halfHeight;
            var p1 = center - up * halfHeight;

            return (p0, p1, radius);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Vector3, Vector3, float) GetCapsuleDimensions(CapsuleCollider collider) => GetCapsuleDimensions(collider, Vector3.up);

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
        
        public static EntityView GetPooledEntityView(PooledEntityView prototype, EcsWorld world)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var view = pool.Get().GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static EntityView GetPooledEntityView(PooledEntityView prototype, EcsWorld world, Vector3 position)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
            var view = pool.Get(position).GetComponentAndCache<EntityView>();
            view.InitAsEntity(world);
            return view;
        }

        public static EntityView GetPooledEntityView(PooledEntityView prototype, EcsWorld world, Vector3 position, Quaternion rotation)
        {
            var pool = PoolManager.Instance.GetByPrototype(prototype.Item);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 FindClosestPoint(this Collider a, Collider b) => a.ClosestPoint(b.ClosestPoint(a.transform.position));

#if DEBUG
        public static GameObject CreateDebugIndicatorObject(string name = "IndicatorObject", PrimitiveType primitive = PrimitiveType.Sphere, float scale = 0.3f)
            => CreateDebugIndicatorObject(Color.cyan, name, primitive, scale);
        public static GameObject CreateDebugIndicatorObject(Color color, string name = "IndicatorObject", PrimitiveType primitive = PrimitiveType.Sphere, float scale = 0.3f)
        {
            var indicator = GameObject.CreatePrimitive(primitive);
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.transform.localScale = new Vector3(scale, scale, scale);
            indicator.GetComponent<Renderer>().material.color = color;
            indicator.name = name;

            return indicator;
        }
#endif
    }

#if UNITY_EDITOR
    public static class EditorUtilityMethods
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

        public static void GenerateFolderPaths_AssetDatabase(string fullPath)
        {
            if (!fullPath.StartsWith("Assets/"))
            {
                Debug.LogError("Path must start with 'Assets/'");
                return;
            }
            
            var folders = fullPath.Split('/');
            var currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                var nextPath = $"{currentPath}/{folders[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                currentPath = nextPath;
            }
        }

        public static void CreateAssetAtPath(Object asset, string path, string assetName)
        {
            GenerateFolderPaths_AssetDatabase(path);
            AssetDatabase.CreateAsset(asset, path + '/' + assetName);
        }

        private static IEnumerable<T> GetObjectsFromProject<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>);
        }

        private static IEnumerable<GameObject> GetObjectsWithComponentInHierarchy<T>() where T : Component
        {
            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
            List<GameObject> components = new();
            foreach (string guid in prefabGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var comps = prefab.GetComponentsInChildren<T>();
                if (comps.Length > 0)
                    components.Add(prefab);
            }

            return components;
        }

        //[MenuItem("../../..")]
        public static void ChangeComponentsInProject<T>(Action<T> changer) where T : Component
        {
            var components = GetObjectsFromProject<T>();
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

        public static void ChangeObjectsWithComponentInHerarchyInProject<T>(Action<GameObject> changer) where T : Component
        {
            var objects = GetObjectsWithComponentInHierarchy<T>();
            foreach (var go in objects)
            {
                changer(go);
                EditorUtility.SetDirty(go);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Changing " + typeof(T).Name + " in project complete");
        }

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