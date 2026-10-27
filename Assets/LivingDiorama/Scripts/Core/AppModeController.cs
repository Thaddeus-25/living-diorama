using System;
using UnityEngine;

namespace LivingDiorama.Core
{
    public enum AppMode
    {
        /// <summary>Editor UI visible. Creatures keep living unless paused (see <see cref="AppModeController.CreaturesPausedInEdit"/>).</summary>
        Edit,
        /// <summary>UI hidden, the diorama runs on its own.</summary>
        Preview
    }

    /// <summary>
    /// Holds the current mode and whether creatures are simulated. Other systems listen to
    /// <see cref="ModeChanged"/> and <see cref="SimulationChanged"/>.
    /// </summary>
    // Runs before other scripts so Instance exists when creatures in the scene enable.
    [DefaultExecutionOrder(-100)]
    public class AppModeController : MonoBehaviour
    {
        [SerializeField] private AppMode startMode = AppMode.Edit;

        private bool creaturesPausedInEdit;

        /// <summary>
        /// The scene's mode controller. Creatures are spawned from prefabs and can't hold scene
        /// references, so they look it up here. Null when there is none (e.g. isolated tests).
        /// </summary>
        public static AppModeController Instance { get; private set; }

        public AppMode Mode { get; private set; }
        public bool IsEditing => Mode == AppMode.Edit;

        /// <summary>The "Pause creatures" toggle: freezes creatures while editing. Preview always runs them.</summary>
        public bool CreaturesPausedInEdit => creaturesPausedInEdit;

        /// <summary>Creatures act while this is true (individual creatures may still be held, e.g. while selected).</summary>
        public bool IsSimulationRunning => Mode == AppMode.Preview || !creaturesPausedInEdit;

        public event Action<AppMode> ModeChanged;
        /// <summary>Raised when <see cref="IsSimulationRunning"/> changes; the argument is the new value.</summary>
        public event Action<bool> SimulationChanged;
        /// <summary>Raised when the "Pause creatures" toggle changes; the argument is the new value.</summary>
        public event Action<bool> CreaturePauseChanged;

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
            bool wasRunning = IsSimulationRunning;
            Mode = mode;
            ModeChanged?.Invoke(mode);
            NotifyIfSimulationChanged(wasRunning);
        }

        public void Toggle() => SetMode(Mode == AppMode.Edit ? AppMode.Preview : AppMode.Edit);

        public void SetCreaturesPausedInEdit(bool paused)
        {
            if (paused == creaturesPausedInEdit) return;
            bool wasRunning = IsSimulationRunning;
            creaturesPausedInEdit = paused;
            CreaturePauseChanged?.Invoke(paused);
            NotifyIfSimulationChanged(wasRunning);
        }

        private void NotifyIfSimulationChanged(bool wasRunning)
        {
            if (IsSimulationRunning != wasRunning) SimulationChanged?.Invoke(IsSimulationRunning);
        }
    }
}
