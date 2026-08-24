using System.Collections.Generic;
using System.Collections.ObjectModel;
using ClassFabric.Controls.EditMode;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Controls;
using ClassFabric.Core.Models.Components;
using ClassFabric.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.EditMode;

public partial class EditModeViewModel(
    MainWindow mainWindow,
    IComponentsService componentsService,
    SettingsService settingsService,
    IUriNavigationService uriNavigationService,
    ITutorialService tutorialService) : ObservableObject
{
    public MainWindow MainWindow { get; } = mainWindow;
    public IComponentsService ComponentsService { get; } = componentsService;
    public SettingsService SettingsService { get; } = settingsService;
    public IUriNavigationService UriNavigationService { get; } = uriNavigationService;
    public ITutorialService TutorialService { get; } = tutorialService;

    public MainViewModel MainViewModel => MainWindow.ViewModel;

    [ObservableProperty] private object? _mainDrawerContent;
    [ObservableProperty] private object? _mainDrawerTitle;
    [ObservableProperty] private VerticalDrawerOpenState _mainDrawerState;
    [ObservableProperty] private bool _isDrawerTempCollapsed;
    [ObservableProperty] private IReadOnlyList<ComponentInfo> _componentInfos = [];
    [ObservableProperty] private IReadOnlyList<ComponentGroup> _componentGroups = [];
    [ObservableProperty] private int _componentSettingsTabIndex = 0;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _createProfileName = "";
    [ObservableProperty] private IList<ComponentSettings>? _targetComponentsList;
    [ObservableProperty] private ComponentInfo? _selectedComponentInfo;
    
    [ObservableProperty] private object? _secondaryDrawerContent;
    [ObservableProperty] private object? _secondaryDrawerTitle;
    [ObservableProperty] private VerticalDrawerOpenState _secondaryDrawerState;
    [ObservableProperty] private MainWindowLineSettings? _selectedMainWindowLineSettings;

    [ObservableProperty]
    private Dictionary<ComponentSettings, EditModeContainerComponentInfo> _containerComponentCache = [];
}