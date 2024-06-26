﻿using Colossal.IO.AssetDatabase;
using Colossal.PSI.Environment;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using System.IO;
using UnityEngine;

namespace TripsData
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(TripsData)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public static Setting m_Setting;

        public static string tripsoutput = "Trips";
        public static string cimpurposeoutput = "CitizenPurpose";
        public static string smoothspeed = "SmoothSpeed";
        public static string transit = "transit";
        public static string cars = "cars";
        public static string trucks = "trucks";
        public static string garbage_trucks = "garbage_trucks";
        public static string commute_data = "commute_data";

        public static string outputPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(TripsData));          

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(TripsData), m_Setting, new Setting(this));

            log.Info($"Output Path: {outputPath}");
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            updateSystem.UpdateAt<CitizenStatistics>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<SimulationStatistics>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<TransitStatistics>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CarStatistics>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<TruckStatistics>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<GarbageTruckStatistics>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<CommuteStatistics>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
