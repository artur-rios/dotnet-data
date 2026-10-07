using ArturRios.Data.Relational.Core.Entities;

namespace ArturRios.Data.Tests.TestSupport;

public class TestEntity : Entity<long>
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedTestEntity : VersionedEntity<long>
{
    public string Name { get; set; } = string.Empty;
}

public class UniqueTestEntity : Entity<long>
{
    public string Email { get; set; } = string.Empty;
}

public class GuidKeyedTestEntity : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
}

public class StringKeyedTestEntity : Entity<string>
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedGuidKeyedTestEntity : VersionedEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
}
