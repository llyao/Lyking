#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lyking.UnityEcsDemos.Benchmark;
using Lyking.UnityEcsDemos.Scenes;

namespace Lyking.UnityEcsDemos.Editor
{
    /// <summary>
    /// 供无头 Unity 验证使用：逐个打开场景并进入播放，确认后端能完成初始化和至少一帧更新。
    /// </summary>
    public static class EcsDemoBatchSmoke
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/00_GameObjectBaseline.unity",
            "Assets/Scenes/01_DotsEntities.unity",
            "Assets/Scenes/02_DotsJobs.unity",
            "Assets/Scenes/03_EntitasStyle.unity",
            "Assets/Scenes/04_HydraVetBrain.unity",
            "Assets/Scenes/05_UnityMass.unity"
        };

        private static int _sceneIndex;
        private static int _frames;
        private static readonly List<string> Failures = new List<string>();

        public static void Run()
        {
            _sceneIndex = 0;
            _frames = 0;
            Failures.Clear();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            OpenNextScene();
        }

        public static void RunStructure()
        {
            Failures.Clear();
            foreach (string scenePath in ScenePaths)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (sceneAsset is null)
                {
                    Failures.Add(scenePath + " 不是可加载的场景资产。");
                    continue;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EcsDemoBootstrap bootstrap = UnityEngine.Object.FindObjectOfType<EcsDemoBootstrap>();
                if (bootstrap is null)
                {
                    Failures.Add(scenePath + " 缺少 EcsDemoBootstrap。");
                }
                else
                {
                    Debug.Log("ECS 场景结构通过：" + scenePath + "，后端=" + bootstrap.BackendKind);
                }
            }

            Finish();
        }

        private static void OpenNextScene()
        {
            if (_sceneIndex >= ScenePaths.Length)
            {
                Finish();
                return;
            }

            string scenePath = ScenePaths[_sceneIndex];
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            _frames = 0;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.update += CheckCurrentScene;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= CheckCurrentScene;
                _sceneIndex++;
                EditorApplication.delayCall += OpenNextScene;
            }
        }

        private static void CheckCurrentScene()
        {
            _frames++;
            if (_frames < 5)
            {
                return;
            }

            BenchmarkRunner runner = UnityEngine.Object.FindObjectOfType<BenchmarkRunner>();
            if (runner is null || runner.Backend is null || !runner.Backend.IsInitialized || runner.Metrics is null)
            {
                Failures.Add(ScenePaths[_sceneIndex] + " 未完成 BenchmarkRunner 初始化。");
            }
            else
            {
                Debug.Log("ECS 场景通过：" + ScenePaths[_sceneIndex] + "，后端=" + runner.Backend.BackendName);
            }

            EditorApplication.isPlaying = false;
        }

        private static void Finish()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (Failures.Count > 0)
            {
                foreach (string failure in Failures)
                {
                    Debug.LogError(failure);
                }

                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("六个 ECS 场景均完成无包兼容后端烟测。");
            EditorApplication.Exit(0);
        }
    }
}
#endif
