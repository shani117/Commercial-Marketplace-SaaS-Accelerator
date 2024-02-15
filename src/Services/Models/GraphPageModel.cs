using Microsoft.Graph.Models;
using Newtonsoft.Json;

namespace Marketplace.SaaS.Accelerator.Services.Models;
public class GraphPageModel
{
    public SpnAssignment[] RoleAssignments { get; set; }
    public bool HasClientConsented { get; set; }
}

public class SpnAssignment
{
    public AppRoleAssignment RoleAssignment { get; set; } 
    public string AppRoleDisplayName { get; set; }
    public string AppRoleDescription { get; set; }
}

public class AddSpnAssignmentPageModel
{    
    public string UserUpn { get; set; }
    public string AppRoleId { get; set; }
}

public class RemoveSpnAssignmentPageModel
{
    public string PrincipalId { get; set; }
    public string AssignmentId { get; set; }
}