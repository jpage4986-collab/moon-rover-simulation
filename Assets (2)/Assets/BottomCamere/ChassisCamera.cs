using UnityEngine;
using UnityEngine.UI;

namespace MoonRover.Vision
{
    public class ChassisCamera : MonoBehaviour
    {
        [Header("底盘透视设置")]
        public float cameraHeight = 1.5f;
        public float cameraForwardOffset = 1f;
        public int resolution = 1024;

        [Header("UGUI 引用")]
        public RawImage camImage;

        private Camera bottomCam;
        private RenderTexture rt;

        void Awake()
        {
            SetupCamera();
        }

        void Start()
        {
            SetupCamera();
        }

        void OnEnable()
        {
            if (Application.isPlaying) SetupCamera();
        }

        void LateUpdate()
        {
            // Keep the preview bound even if the HUD is regenerated or a
            // scene reload clears the RawImage reference after Start().
            if (!Application.isPlaying) return;
            if (bottomCam == null || rt == null)
            {
                SetupCamera();
                return;
            }

            if (bottomCam.targetTexture != rt) bottomCam.targetTexture = rt;
            if (camImage != null && camImage.texture != rt)
            {
                camImage.texture = rt;
                camImage.color = Color.white;
            }
        }

        public void SetupCamera()
        {
            if (bottomCam != null)
            {
                bottomCam.enabled = true;
                if (rt != null) bottomCam.targetTexture = rt;
                if (camImage != null && rt != null)
                {
                    camImage.texture = rt;
                    camImage.color = Color.white;
                }
                return;
            }

            GameObject camObj = new GameObject("HazCam_Bottom");
            camObj.transform.SetParent(this.transform);

            camObj.transform.localPosition = new Vector3(0, cameraHeight, cameraForwardOffset);
            camObj.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            bottomCam = camObj.AddComponent<Camera>();
            bottomCam.fieldOfView = 100f;
            bottomCam.nearClipPlane = 0.01f;
            bottomCam.cullingMask = ~LayerMask.GetMask("UI");

            rt = new RenderTexture(resolution, resolution, 16);
            rt.filterMode = FilterMode.Bilinear;
            bottomCam.targetTexture = rt;

            if (camImage != null)
            {
                camImage.texture = rt;
                camImage.color = Color.white;
                Debug.Log("[ChassisCamera] 摄像头已绑定到 RawImage");
            }
            else
            {
                Debug.LogWarning("[ChassisCamera] camImage 未绑定！");
            }
        }

        void OnDestroy()
        {
            if (bottomCam != null) bottomCam.targetTexture = null;
            if (rt != null) rt.Release();
        }
    }
}
