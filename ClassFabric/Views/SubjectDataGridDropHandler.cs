using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ClassFabric.Core.Abstractions.Behaviors;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Views;

public sealed class SubjectDataGridDropHandler : BaseDataGridDropHandler<Subject>
{
    public const string DataFormat = "ClassFabric.ProfileSubject";

    protected override Subject MakeCopy(ObservableCollection<Subject> parentCollection, Subject item) =>
        throw new NotSupportedException("Subject sorting only supports move operations.");

    protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext,
        bool execute)
    {
        if (e.DragEffects != DragDropEffects.Move
            || sourceContext is not Subject sourceItem
            || targetContext is not ObservableCollection<Subject> items
            || dg.GetVisualAt(e.GetPosition(dg)) is not Control targetControl
            || targetControl.DataContext is not Subject targetItem)
        {
            return false;
        }

        return RunDropAction(dg, e, execute, sourceItem, targetItem, items);
    }
}
