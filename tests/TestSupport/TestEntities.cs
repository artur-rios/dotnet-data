using ArturRios.Data;

namespace ArturRios.Data.Tests.TestSupport;

public class TestEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedTestEntity : VersionedEntity
{
    public string Name { get; set; } = string.Empty;
}
