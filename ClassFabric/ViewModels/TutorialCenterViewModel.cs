using System.Collections.ObjectModel;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Models.Tutorial;
using ClassFabric.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace ClassFabric.ViewModels;

public partial class TutorialCenterViewModel(SettingsService settingsService, ITutorialService tutorialService) : ObservableObject
{
   public SettingsService SettingsService { get; } = settingsService;
   public ITutorialService TutorialService { get; } = tutorialService;

   [ObservableProperty] private TutorialGroup? _selectedTutorialGroup;
   [ObservableProperty] private Tutorial? _selectedTutorial;
   
   public ObservableCollection<object> NavigationViewItems { get; } = [];
}