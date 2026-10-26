using System;
using UnityEngine;

namespace LivingDiorama.Core
{
    public enum AppMode
    {
        /// <summary>Editor UI visible, living things paused so they can be selected and moved.</summary>
        Edit,
        /// <summary>UI hidden, the diorama runs on its own.</summary>
        Preview
    }

    /// <summary>Holds the current mode. Other systems listen to <see cref="ModeChanged"/>.</summary>
    // Runs before other scripts so Instance exists when creatures in the scene enable.
    [DefaultExecutionOrder(-100)]
    public class AppModeController : MonoBehaviour
    {
        [SerializeField] private AppMode startMode = AppMode.Edit;

        /// <summary>
        /// The scene's mode controller. Creatures are spawned from prefabs and can't hold scene
        /// references, so they look it up here. Null when there is none (e.g. isolated tests).
        /// </summary>
        public static AppModeController Instance { get; private set; }

        public AppMode Mode { get; private set; }
        public bool IsEditing => Mode == AppMode.Edit;

        /// <summary>Living entities only act while this is true.</summary>
        public bool IsSimulationRunning => Mode == AppMode.Preview;

        public event Action<AppMode> ModeChanged;

        private void Awake()
        {
            Mode = startMode;
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => ModeChanged?.Invoke(Mode);

        public void SetMode(AppMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        public void Toggle() => SetMode(Mode == AppMode.Edit ? AppMode.Preview : AppMode.Edit);
    }
}
