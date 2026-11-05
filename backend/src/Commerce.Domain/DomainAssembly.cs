using System.Reflection;

namespace Commerce.Domain;

/// <summary>Anchor for the domain assembly. Entities and business rules are added from Phase 2.</summary>
public static class DomainAssembly
{
    public static readonly Assembly Reference = typeof(DomainAssembly).Assembly;
}
