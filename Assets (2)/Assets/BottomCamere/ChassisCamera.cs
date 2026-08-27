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

        void Start()
        {
            SetupCamera();
        }

        public void SetupCamera()
        {
            if (bottomCam != null) return;

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
