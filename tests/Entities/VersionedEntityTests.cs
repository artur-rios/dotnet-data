using System;
using System.ComponentModel.DataAnnotations;
using ArturRios.Data.Relational.Core.Entities;

namespace ArturRios.Data.Tests.Entities;

[Trait("Category", "Unit")]
public class VersionedEntityTests
{
    [Fact]
    public void GivenTheVersionedEntityType_WhenInspected_ThenItDerivesFromEntity() =>
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(VersionedEntity)));

    [Fact]
    public void GivenANewVersionedEntity_WhenInspected_ThenTheConcurrencyStampIsANonEmptyGuid()
    {
        var sample = new Sample();
        Assert.NotEqual(Guid.Empty, sample.ConcurrencyStamp);
    }

    [Fact]
    public void GivenTheConcurrencyStamp_WhenInspected_ThenItCarriesTheConcurrencyCheckAttribute()
    {
        var prop = typeof(VersionedEntity).GetProperty(nameof(VersionedEntity.ConcurrencyStamp))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false));
    }

    private sealed class Sample : VersionedEntity;
}
