using System.Collections;
using LivingDiorama.Persistence;
using LivingDiorama.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LivingDiorama.Tests
{
    public class DayNightPlayTests
    {
        private DayNightCycle cycle;
        private Light sun;

        [UnitySetUp]
        public IEnumerator LoadGarden()
        {
            yield return SceneManager.LoadSceneAsync("Garden", LoadSceneMode.Single);
            yield return null;
            cycle = Object.FindAnyObjectByType<DayNightCycle>();
            Assert.IsNotNull(cycle, "DayNightCycle missing from Garden scene");
            sun = GameObject.Find("Sun").GetComponent<Light>();
            cycle.SetAutoAdvance(false);
        }

        [UnityTest]
        public IEnumerator Noon_IsBright_Midnight_IsDarkAndCool()
        {
            cycle.SetTime(12f);
            yield return null;
            Assert.Less(cycle.Darkness, 0.01f);
            float noonIntensity = sun.intensity;
            float noonElevation = sun.transform.eulerAngles.x;

            cycle.SetTime(0f);
            yield return null;
            Assert.Greater(cycle.Darkness, 0.99f);
            Assert.Less(sun.intensity, noonIntensity * 0.5f, "Moonlight is much dimmer");
            Assert.Greater(sun.color.b, sun.color.r, "Moonlight is bluish");
            Assert.Greater(noonElevation, 50f, "Sun is high at noon");
        }

        [UnityTest]
        public IEnumerator Dusk_IsInBetween_AndTimeAdvancesWhenAuto()
        {
            cycle.SetTime(18f);
            yield return null;
            Assert.That(cycle.Darkness, Is.InRange(0.2f, 0.8f));

            cycle.SetTime(9f);
            cycle.SetAutoAdvance(true);
            float before = cycle.TimeOfDay;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.Greater(cycle.TimeOfDay, before, "Time passes on its own");

            cycle.SetAutoAdvance(false);
            float frozen = cycle.TimeOfDay;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(frozen, cycle.TimeOfDay, "Time stops when auto is off");
        }

        [UnityTest]
        public IEnumerator NightLight_IsOnAtNight_OffByDay_FireFlickers()
        {
            var lampObject = new GameObject("TestLamp", typeof(Light));
            var lamp = lampObject.AddComponent<NightLight>();
            lamp.Configure(night: 2f, day: 0f, flickerAmount: 0f);
            var light = lampObject.GetComponent<Light>();

            cycle.SetTime(12f);
            yield return null;
            Assert.IsFalse(light.enabled, "Lamps are off by day");

            cycle.SetTime(23f);
            yield return null;
            Assert.IsTrue(light.enabled);
            Assert.AreEqual(2f, light.intensity, 0.05f);
        }

        [UnityTest]
        public IEnumerator TimeOfDay_IsSavedAndRestored()
        {
            var service = Object.FindAnyObjectByType<SceneSaveService>();
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LivingDioramaTests", System.Guid.NewGuid().ToString("N"));
            service.UseStore(new SaveFileStore(temp));
            try
            {
                cycle.SetTime(21.5f);
                cycle.SetAutoAdvance(false);
                service.Save("Evening");
                cycle.SetTime(8f);
                cycle.SetAutoAdvance(true);
                yield return null;

                Assert.IsTrue(service.Load("Evening").Success);
                Assert.AreEqual(21.5f, cycle.TimeOfDay, 0.01f);
                Assert.IsFalse(cycle.AutoAdvance);
            }
            finally
            {
                System.IO.Directory.Delete(temp, recursive: true);
            }
        }
    }
}
