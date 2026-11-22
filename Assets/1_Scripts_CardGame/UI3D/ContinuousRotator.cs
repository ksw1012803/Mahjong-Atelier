using UnityEngine;

namespace CardGame.UI3D
{
    /// <summary>
    /// 지속 회전 — 3D Scene의 바닥에 배치된 Quad/Plane을 축 기준으로 지속 회전시킴.
    /// 바닥 장식(심볼, 룬, 로고 등)에 사용.
    /// </summary>
    public class ContinuousRotator : MonoBehaviour
    {
        [Header("Rotation")]
        [Tooltip("회전 축. 바닥에 수평으로 눕힌 경우 Y축(up) 권장.")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        [Tooltip("초당 회전 각도 (deg/sec). 양수=반시계, 음수=시계.")]
        [SerializeField] private float rotationSpeed = 20f;

        [Tooltip("회전 기준 공간. Self = 로컬 축, World = 월드 축.")]
        [SerializeField] private Space rotationSpace = Space.Self;

        [Header("Pulse (옵션)")]
        [Tooltip("활성화 시 회전 속도가 사인파로 변동 (느려졌다 빨라졌다).")]
        [SerializeField] private bool pulsing = false;
        [Tooltip("Pulse 활성 시 최소 속도 배율 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float minSpeedMultiplier = 0.5f;
        [Tooltip("Pulse 주기 (초).")]
        [SerializeField] private float pulseDuration = 2f;

        private void Update()
        {
            float speed = rotationSpeed;

            if (pulsing && pulseDuration > 0f)
            {
                float t = (Time.time % pulseDuration) / pulseDuration;
                float wave = 0.5f + Mathf.Sin(t * Mathf.PI * 2f) * 0.5f;  // 0~1
                float mult = Mathf.Lerp(minSpeedMultiplier, 1f, wave);
                speed *= mult;
            }

            transform.Rotate(rotationAxis, speed * Time.deltaTime, rotationSpace);
        }
    }
}
