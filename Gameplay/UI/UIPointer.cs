using UnityEngine;

namespace CodexFramework.Gameplay.UI
{
    public class UIPointer : MonoBehaviour
    {
        public Transform Target;
        public RectTransform UITarget;

        [SerializeField]
        private float _minDistance = 150.0f;
        [SerializeField]
        private float _minUIDistance = 40.0f;
        [SerializeField]
        private float _wiggleScale = 20.0f;
        [SerializeField]
        private float _wiggleSpeed = 1.0f;

        private RectTransform rectTransform;

        private float MinDistance => Target != null ? _minDistance : _minUIDistance;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private Vector3 GetTargetScreenPoint()
        {
            if (Target != null)
                return Camera.main.WorldToScreenPoint(Target.position);

            if (UITarget != null)
                return UITarget.position;

            return Vector3.zero;
        }

        void Update()
        {
            if (Target == null && UITarget == null)
                return;

            var targetScreenPoint = GetTargetScreenPoint();

            var direction = targetScreenPoint - rectTransform.position;
            var angle = Vector3.Angle(direction, Vector3.right);
            if (Vector3.Dot(direction, Vector3.up) < 0)
                angle *= -1;
            rectTransform.eulerAngles = new Vector3(0, 0, angle);

            var pointerScreenPoint = targetScreenPoint - rectTransform.right * MinDistance;
            const float minCoordinate = 0.1f;
            const float maxCoordinate = 0.9f;
            var pointerViewPoint = Camera.main.ScreenToViewportPoint(pointerScreenPoint);
            pointerViewPoint.x = Mathf.Clamp(pointerViewPoint.x, minCoordinate, maxCoordinate);
            pointerViewPoint.y = Mathf.Clamp(pointerViewPoint.y, minCoordinate, maxCoordinate);
            pointerViewPoint.z = Mathf.Clamp(pointerViewPoint.z, minCoordinate, maxCoordinate);
            pointerScreenPoint = Camera.main.ViewportToScreenPoint(pointerViewPoint);

            var distanceDelta = (1 - Mathf.Sin(Time.time * _wiggleSpeed)) * _wiggleScale;
            pointerScreenPoint -= rectTransform.right * distanceDelta;

            rectTransform.position = pointerScreenPoint;
        }
    }
}