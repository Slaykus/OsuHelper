﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Data;
using Gress;
using MaterialDesignThemes.Wpf;
using OsuHelper.Exceptions;
using OsuHelper.Internal;
using OsuHelper.Models;
using OsuHelper.Services;
using OsuHelper.ViewModels.Framework;
using Stylet;
using Tyrrrz.Extensions;

namespace OsuHelper.ViewModels
{
    public class RootViewModel : Screen
    {
        private readonly IViewModelFactory _viewModelFactory;
        private readonly DialogManager _dialogManager;
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;
        private readonly CacheService _cacheService;
        private readonly RecommendationService _recommendationService;

        public ISnackbarMessageQueue Notifications { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(5));

        public IProgressManager ProgressManager { get; } = new ProgressManager();

        public bool IsBusy { get; private set; }

        public IReadOnlyList<Recommendation>? Recommendations { get; private set; }

        public Recommendation? SelectedRecommendation { get; set; }

        public bool IsNomodFilterEnabled { get; set; } = true;

        public bool IsHiddenFilterEnabled { get; set; } = true;

        public bool IsHardRockFilterEnabled { get; set; } = true;

        public bool IsDoubleTimeFilterEnabled { get; set; } = true;

        public bool IsOtherFilterEnabled { get; set; } = true;

        public RootViewModel(IViewModelFactory viewModelFactory, DialogManager dialogManager,
            SettingsService settingsService, UpdateService updateService, CacheService cacheService,
            RecommendationService recommendationService)
        {
            _viewModelFactory = viewModelFactory;
            _dialogManager = dialogManager;
            _settingsService = settingsService;
            _updateService = updateService;
            _cacheService = cacheService;
            _recommendationService = recommendationService;

            // Title
            DisplayName = $"{App.Name} v{App.VersionString}";

            // Update busy state when progress manager changes
            ProgressManager.Bind(o => o.IsActive, (sender, args) => IsBusy = ProgressManager.IsActive);

            // Update recommendations view filter when recommendations change
            this.Bind(o => o.Recommendations, (sender, args) => UpdateRecommendationsViewFilter());

            // Update recommendations view filter when filters change
            this.Bind(o => o.IsNomodFilterEnabled, (sender, args) => UpdateRecommendationsViewFilter());
            this.Bind(o => o.IsHiddenFilterEnabled, (sender, args) => UpdateRecommendationsViewFilter());
            this.Bind(o => o.IsHardRockFilterEnabled, (sender, args) => UpdateRecommendationsViewFilter());
            this.Bind(o => o.IsDoubleTimeFilterEnabled, (sender, args) => UpdateRecommendationsViewFilter());
            this.Bind(o => o.IsOtherFilterEnabled, (sender, args) => UpdateRecommendationsViewFilter());
        }

        private async Task HandleAutoUpdateAsync()
        {
            try
            {
                // Don't check for updates if auto-update is disabled
                if (!_settingsService.IsAutoUpdateEnabled)
                    return;

                // Check for updates
                var updateVersion = await _updateService.CheckForUpdatesAsync();
                if (updateVersion == null)
                    return;

                // Notify user of an update and prepare it
                Notifications.Enqueue($"Downloading update to {App.Name} v{updateVersion}...");
                await _updateService.PrepareUpdateAsync(updateVersion);

                // Prompt user to install update (otherwise install it when application exits)
                Notifications.Enqueue(
                    "Update has been downloaded and will be installed when you exit",
                    "INSTALL NOW", () =>
                    {
                        _updateService.FinalizeUpdate(true);
                        RequestClose();
                    });
            }
            catch
            {
                // Failure to update shouldn't crash the application
                Notifications.Enqueue("Failed to perform application update");
            }
        }

        protected override async void OnViewLoaded()
        {
            base.OnViewLoaded();

            // Load settings
            _settingsService.Load();

            // Load last recommendations
            Recommendations = _cacheService.RetrieveOrDefault<IReadOnlyList<Recommendation>>("LastRecommendations");

            // Check and prepare update
            await HandleAutoUpdateAsync();
        }

        protected override void OnClose()
        {
            base.OnClose();

            // Save settings
            _settingsService.Save();

            // Finalize updates if necessary
            _updateService.FinalizeUpdate(false);
        }

        private void UpdateRecommendationsViewFilter()
        {
            var view = CollectionViewSource.GetDefaultView(Recommendations);
            if (view == null)
                return;

            view.Filter = o =>
            {
                var recommendation = (Recommendation) o;

                var accepted = true;

                if (recommendation.Mods == Mods.None)
                    accepted &= IsNomodFilterEnabled;

                if (recommendation.Mods.HasFlag(Mods.Hidden))
                    accepted &= IsHiddenFilterEnabled;

                if (recommendation.Mods.HasFlag(Mods.HardRock))
                    accepted &= IsHardRockFilterEnabled;

                if (recommendation.Mods.HasFlag(Mods.DoubleTime))
                    accepted &= IsDoubleTimeFilterEnabled;

                var modsOther = recommendation.Mods & ~Mods.Hidden & ~Mods.HardRock & ~Mods.DoubleTime;
                if (modsOther != Mods.None)
                    accepted &= IsOtherFilterEnabled;

                return accepted;
            };
        }

        public bool CanShowSettings => !IsBusy;

        public async void ShowSettings()
        {
            // Create dialog
            var dialog = _viewModelFactory.CreateSettingsViewModel();

            // Show dialog
            await _dialogManager.ShowDialogAsync(dialog);
        }

        public bool CanShowBeatmapDetails => SelectedRecommendation != null;

        public async void ShowBeatmapDetails()
        {
            // HACK: Stylet's event actions don't respect guard properties
            if (!CanShowBeatmapDetails)
                return;

            // Create dialog
            var dialog = _viewModelFactory.CreateBeatmapDetailsViewModel(SelectedRecommendation!.Beatmap);

            // Show dialog
            await _dialogManager.ShowDialogAsync(dialog);
        }

        public void ShowAbout() => App.GitHubProjectUrl.ToUri().OpenInBrowser();

        public bool CanPopulateRecommendations => !IsBusy;

        public async void PopulateRecommendations()
        {
            // Create progress operation
            var operation = ProgressManager.CreateOperation();

            try
            {
                // Validate settings
                if (_settingsService.UserId.IsNullOrWhiteSpace() || _settingsService.ApiKey.IsNullOrWhiteSpace())
                {
                    Notifications.Enqueue("Not configured – set username and API key in settings",
                        "OPEN", ShowSettings);
                    return;
                }

                // Get recommendations
                Recommendations = await _recommendationService.GetRecommendationsAsync(operation);

                // Persist recommendations in cache
                _cacheService.Store("LastRecommendations", Recommendations);

                // Notify completion
                Notifications.Enqueue("Recommendations updated");
            }
            catch (RecommendationsUnavailableException)
            {
                Notifications.Enqueue("Recommendations unavailable – no top plays set in selected game mode");
            }
            catch (HttpErrorStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                Notifications.Enqueue("Unauthorized – make sure API key is valid");
            }
            finally
            {
                // Dispose progress operation
                operation.Dispose();
            }
        }
    }
}