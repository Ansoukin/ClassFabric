using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Behaviors;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.Views;

public sealed class ScheduleItemDataGridDropHandler : BaseDataGridDropHandler<KeyValuePair<Guid, ScheduleItem>>
{
    public const string DataFormat = "ClassIsland.ProfileScheduleItem";

    protected override KeyValuePair<Guid, ScheduleItem> MakeCopy(
        ObservableCollection<KeyValuePair<Guid, ScheduleItem>> parentCollection,
        KeyValuePair<Guid, ScheduleItem> item) =>
        throw new NotSupportedException("Schedule item sorting only supports move operations.");

    protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext,
        bool execute)
    {
        if (!IsDragAllowed(dg, e, sourceContext, targetContext, out var sourceItem, out var items, out var targetControl))
        {
            return false;
        }

        var targetRow = targetControl as DataGridRow
            ?? targetControl.FindAncestorOfType<DataGridRow>()
            ?? targetControl.FindLogicalAncestorOfType<DataGridRow>();
        var targetItemContext = targetRow?.DataContext ?? targetControl.DataContext;
        if (targetItemContext is not KeyValuePair<Guid, ScheduleItem> targetItem
            || sourceItem.Key == targetItem.Key)
        {
            return false;
        }

        return RunDropAction(dg, e, execute, sourceItem, targetItem, items);
    }

    /// <summary>
    /// 拖拽前置校验。集控策略命中任何一条禁改开关都拒绝拖拽，
    /// 后续的模式/上下文/命中控件检查也都在这里短路完成，Validate 只留两段式主流程。
    /// </summary>
    private static bool IsDragAllowed(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext,
        out KeyValuePair<Guid, ScheduleItem> sourceItem,
        out ObservableCollection<KeyValuePair<Guid, ScheduleItem>> items,
        out Control targetControl)
    {
        var policy = dg.FindAncestorOfType<ProfileSettingsWindow>()?.ViewModel.ManagementService.Policy;
        if (policy == null
            || policy.DisableProfileEditing
            || policy.DisableProfileClassPlanEditing
            || policy.DisableProfileTimeLayoutEditing
            || policy.DisableProfileSubjectsEditing
            || e.DragEffects != DragDropEffects.Move
            || sourceContext is not KeyValuePair<Guid, ScheduleItem> source
            || targetContext is not ObservableCollection<KeyValuePair<Guid, ScheduleItem>> collection
            || dg.GetVisualAt(e.GetPosition(dg)) is not Control control)
        {
            sourceItem = default;
            items = [];
            targetControl = null!;
            return false;
        }

        sourceItem = source;
        items = collection;
        targetControl = control;
        return true;
    }
}
