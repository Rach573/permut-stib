using Microsoft.AspNetCore.Identity;
using PermutStib.Business.Models;
using PermutStib.Data.Entities;

namespace PermutStib.Api;

public static class AdminBootstrapper
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var matricule = configuration["BootstrapAdmin:Matricule"];
        var phone = configuration["BootstrapAdmin:PhoneNumber"];
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(matricule) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
            return;

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AgentUser>>();

        if (await users.FindByNameAsync(matricule) is not null)
            return;

        var admin = new AgentUser
        {
            Id = Guid.NewGuid(),
            UserName = matricule,
            Matricule = matricule,
            PhoneNumber = phone,
            Status = AgentStatus.Active,
            AppRole = AgentRole.Admin
        };

        var result = await users.CreateAsync(admin, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Impossible d'initialiser le compte administrateur : " + string.Join(" ", result.Errors.Select(x => x.Description)));
    }
}

