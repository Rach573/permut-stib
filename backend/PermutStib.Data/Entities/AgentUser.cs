using Microsoft.AspNetCore.Identity;
using PermutStib.Business.Models;

namespace PermutStib.Data.Entities;

public sealed class AgentUser : IdentityUser<Guid>
{
    public required string Matricule { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Pending;
    public AgentRole AppRole { get; set; } = AgentRole.Agent;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

