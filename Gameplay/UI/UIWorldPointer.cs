using UnityEngine;

namespace CodexFramework.Gameplay.UI
{
    public class UIWorldPointer : MonoBehaviour
    {
        public Transform Target;
        public float Offset = 32f;

        private Camera _playerCamera;
        private Rect _rect;

        void Start()
        {
            _playerCamera = Camera.main;
            _rect = new Rect(-Screen.width * 0.5f + Offset, -Screen.height * 0.5f + Offset, Screen.width - Offset * 2f, Screen.height - Offset * 2f);
        }

        public Vector3 targetScreenPoint;

        void Update()
        {
            if (Target == null)
                return;

            targetScreenPoint = _playerCamera.WorldToScreenPoint(Target.position);
            var screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            var centeredScreenPoint = targetScreenPoint - screenCenter;

            // remove this line when finished fine tuning, since we already calculate Rect in Start()
            _rect = new Rect(-Screen.width * 0.5f + Offset, -Screen.height * 0.5f + Offset, Screen.width - Offset * 2f, Screen.height - Offset * 2f);
            // --------------------------------------------------------------------------------------

            var isInFrontOfCamera = _playerCamera.transform.InverseTransformPoint(Target.position).z > 0;

            var clampedScreenPoint = isInFrontOfCamera
                ? ClampScreenPoint(centeredScreenPoint, _rect)
                : ClampBackScreenPoint(-centeredScreenPoint, _rect);

            var clampedScreenPoint3 = new Vector3(clampedScreenPoint.x, clampedScreenPoint.y, 0f);
            transform.position = clampedScreenPoint3 + screenCenter;
        }

        public static Vector2 ClampScreenPoint(Vector2 point, Rect rect)
        {
            var right = rect.xMax;
            var left = rect.xMin;
            var top = rect.yMax;
            var bottom = rect.yMin;
            var width = rect.width;
            var height = rect.height;

            if (point.x > right)
            {
                if (point.y > top)
                {
                    if (point.y * width > point.x * height)
                    {
                        return new Vector2(top * point.x / point.y, top);
                    }
                    else
                    {
                        return new Vector2(right, right * point.y / point.x);
                    }
                }
                else if (point.y < bottom)
                {
                    if (-point.y * width > point.x * height)
                    {
                        return new Vector2(bottom * point.x / point.y, bottom);
                    }
                    else
                    {
                        return new Vector2(right, right * point.y / point.x);
                    }
                }
                else
                {
                    return new Vector2(right, right * point.y / point.x);
                }
            }
            else if (point.x < left)
            {
                if (point.y > top)
                {
                    if (point.y * width > -point.x * height)
                    {
                        return new Vector2(top * point.x / point.y, top);
                    }
                    else
                    {
                        return new Vector2(left, left * point.y / point.x);
                    }
                }
                else if (point.y < bottom)
                {
                    if (-point.y * width > -point.x * height)
                    {
                        return new Vector2(bottom * point.x / point.y, bottom);
                    }
                    else
                    {
                        return new Vector2(left, left * point.y / point.x);
                    }
                }
                else
                {
                    return new Vector2(left, left * point.y / point.x);
                }
            }
            else
            {
                if (point.y > top)
                {
                    return new Vector2(top * point.x / point.y, top);
                }
                else if (point.y < bottom)
                {
                    return new Vector2(bottom * point.x / point.y, bottom);
                }
                else
                {
                    return new Vector2(point.x, point.y);
                }
            }
        }

        public static Vector2 ClampBackScreenPoint(Vector2 point, Rect rect)
        {
            var right = rect.xMax;
            var left = rect.xMin;
            var top = rect.yMax;
            var bottom = rect.yMin;
            var width = rect.width;
            var height = rect.height;

            if (point.x > 0)
            {
                if (point.y > 0)
                {
                    if (point.y * width > point.x * height)
                    {
                        return new Vector2(top * point.x / point.y, top);
                    }
                    else
                    {
                        return new Vector2(right, right * point.y / point.x);
                    }
                }
                else if (point.y < 0)
                {
                    if (-point.y * width > point.x * height)
                    {
                        return new Vector2(bottom * point.x / point.y, bottom);
                    }
                    else
                    {
                        return new Vector2(right, right * point.y / point.x);
                    }
                }
                else
                {
                    return new Vector2(right, right * point.y / point.x);
                }
            }
            else if (point.x < 0)
            {
                if (point.y > 0)
                {
                    if (point.y * width > -point.x * height)
                    {
                        return new Vector2(top * point.x / point.y, top);
                    }
                    else
                    {
                        return new Vector2(left, left * point.y / point.x);
                    }
                }
                else if (point.y < 0)
                {
                    if (-point.y * width > -point.x * height)
                    {
                        return new Vector2(bottom * point.x / point.y, bottom);
                    }
                    else
                    {
                        return new Vector2(left, left * point.y / point.x);
                    }
                }
                else
                {
                    return new Vector2(left, left * point.y / point.x);
                }
            }
            else
            {
                if (point.y > 0)
                {
                    return new Vector2(top * point.x / point.y, top);
                }
                else if (point.y < bottom)
                {
                    return new Vector2(bottom * point.x / point.y, bottom);
                }
                else
                {
                    return new Vector2(0, bottom);
                }
            }
        }
    }
}