using System;
using System.Collections.ObjectModel;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Models.Profile;

public class ClassPlansTreeNode
{
    public Guid Guid { get; set; }
    public bool IsGroup { get; set; }
    
    public ReadOnlyObservableCollection<ClassPlansTreeNode>? SubPlans { get; set; }
    public ClassPlan? ClassPlan { get; set; }
}