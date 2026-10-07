using System;
using System.ComponentModel.DataAnnotations;
using ArturRios.Data.Relational.Core.Entities;

namespace ArturRios.Data.Tests.Entities;

[Trait("Category", "Unit")]
public class VersionedEntityTests
{
    [Fact]
    public void GivenTheVersionedEntityType_WhenInspected_ThenItDerivesFromTheEntityWithTheSameKey() =>
        Assert.True(typeof(Entity<Guid>).IsAssignableFrom(typeof(VersionedEntity<Guid>)));

    [Fact]
    public void GivenTheVersionedEntityType_WhenInspected_ThenItImplementsTheVersionedContract() =>
        Assert.True(typeof(IVersionedEntity).IsAssignableFrom(typeof(VersionedEntity<long>)));

    [Fact]
    public void GivenANewVersionedEntity_WhenInspected_ThenTheConcurrencyStampIsANonEmptyGuid()
    {
        var sample = new Sample();
        Assert.NotEqual(Guid.Empty, sample.ConcurrencyStamp);
    }

    [Fact]
    public void GivenTheConcurrencyStamp_WhenInspected_ThenItCarriesTheConcurrencyCheckAttribute()
    {
        var prop = typeof(VersionedEntity<long>).GetProperty(nameof(IVersionedEntity.ConcurrencyStamp))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false));
    }

    private sealed class Sample : VersionedEntity<long>;
}
