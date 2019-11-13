﻿using OsuHelper.Models;
using Tyrrrz.Settings;

namespace OsuHelper.Services
{
    public class SettingsService : SettingsManager
    {
        public string? UserId { get; set; }

        public string? ApiKey { get; set; }

        public GameMode GameMode { get; set; } = GameMode.Standard;

        public bool DownloadWithoutVideo { get; set; }

        public double PreviewVolume { get; set; } = 0.75;

        public bool IsAutoUpdateEnabled { get; set; } = true;

        public SettingsService()
        {
            Configuration.FileName = "Config.dat";
            Configuration.SubDirectoryPath = "";
            Configuration.StorageSpace = StorageSpace.Instance;
        }
    }
}