using System;
using Avalonia.Data.Converters;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.ComponentSettings;

namespace ClassFabric.Controls.Components;

/// <summary>
/// CountDownComponentSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class CountDownComponentSettingsControl : ComponentBase<CountDownComponentSettings>
{
    public CountDownComponentSettingsControl()
    {
        InitializeComponent();
    }
}

