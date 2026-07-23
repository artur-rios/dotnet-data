using ArturRios.Data.Relational.Core.Entities;

namespace ArturRios.Data.Tests.TestSupport;

public class TestEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedTestEntity : VersionedEntity
{
    public string Name { get; set; } = string.Empty;
}
