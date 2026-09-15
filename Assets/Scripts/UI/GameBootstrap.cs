using UnityEngine;
using UnityEngine.EventSystems;

namespace CodeCubesPC
{
    /// <summary>
    /// 零配置启动：不依赖任何场景内容与预制体。
    ///
    /// 用 [RuntimeInitializeOnLoadMethod] 在任意场景加载后自动创建 EventSystem 与
    /// GameController，随后所有 UI 都由代码生成。因此：
    ///   * 直接用模板自带的 SampleScene，按 Play 就能玩
    ///   * 打包时也不需要手工往场景里拖任何东西
    ///
    /// 如果更希望场景里有可见的入口对象，也可以自己在场景里建一个空物体挂
    /// GameController —— 此时本类检测到已存在实例就会跳过，不会产生两份 UI。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            EnsureEventSystem();

            if (UnityEngine.Object.FindObjectOfType<GameController>() != null)
                return;

            var root = new GameObject("CodeCubesPC");
            root.AddComponent<GameController>();
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
