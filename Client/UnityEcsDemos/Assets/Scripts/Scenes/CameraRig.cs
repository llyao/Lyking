using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lyking.UnityEcsDemos.Scenes
{
    /// <summary>
    /// 统一六个场景的相机位置和快捷切换。后端逻辑不依赖本组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Vector3 lookAt = Vector3.zero;
        [SerializeField] private float distance = 62f;
        [SerializeField] private float height = 2f;
        [SerializeField] private bool enableSceneShortcuts = true;

        private void Awake()
        {
            Camera camera = GetComponent<Camera>();
            if (camera is null)
            {
                return;
            }

            transform.position = lookAt + new Vector3(0f, height, -distance);
            transform.LookAt(lookAt);
            camera.orthographic = true;
            camera.orthographicSize = 32f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);
        }

        private void Update()
        {
            if (!enableSceneShortcuts)
            {
                return;
            }

            for (int index = 0; index < 6; index++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + index)))
                {
                    SceneManager.LoadScene(SceneBackendSelector.GetSceneName((DemoBackendKind)index));
                    return;
                }
            }
        }
    }
}
