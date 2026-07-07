using System;
using System.ComponentModel.DataAnnotations;
using ArturRios.Data.Relational.Core;

namespace ArturRios.Data.Tests.Entities;

public class VersionedEntityTests
{
    [Fact]
    public void VersionedEntity_DerivesFromEntity() =>
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(VersionedEntity)));

    [Fact]
    public void ConcurrencyStamp_DefaultsToNonEmptyGuid()
    {
        var sample = new Sample();
        Assert.NotEqual(Guid.Empty, sample.ConcurrencyStamp);
    }

    [Fact]
    public void ConcurrencyStamp_HasConcurrencyCheckAttribute()
    {
        var prop = typeof(VersionedEntity).GetProperty(nameof(VersionedEntity.ConcurrencyStamp))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false));
    }

    private sealed class Sample : VersionedEntity;
}
