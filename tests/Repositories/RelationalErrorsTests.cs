using System;
using System.Data.Common;
using ArturRios.Data.Relational.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Repositories;

/// <summary>
///     The provider-independent classification behind every relational error envelope, pinned against
///     provider-shaped exceptions: SQLSTATE where a provider reports one, provider wording where it does not.
/// </summary>
[Trait("Category", "Unit")]
public class RelationalErrorsTests
{
    [Fact]
    public void GivenAConcurrencyException_WhenDescribed_ThenTheConcurrencyMessageComesBack() =>
        Assert.Equal(RelationalErrors.ConcurrencyMessage,
            RelationalErrors.Describe(new DbUpdateConcurrencyException("stale")));

    [Theory]
    [InlineData("23505", "anything")] // PostgreSQL unique_violation
    [InlineData(null, "UNIQUE constraint failed: Items.Email")] // SQLite
    [InlineData(null, "Cannot insert duplicate key row in object 'dbo.Items' with unique index 'IX'")] // SQL Server
    [InlineData(null, "Violation of UNIQUE KEY constraint 'UQ_Items'")] // SQL Server
    [InlineData("23000", "Duplicate entry 'a@b.com' for key 'IX_Email'")] // MySQL: class-level SQLSTATE
    public void GivenAUniqueViolation_WhenDescribed_ThenTheUniqueMessageComesBack(string? sqlState, string message) =>
        Assert.Equal(RelationalErrors.UniqueViolationMessage,
            RelationalErrors.Describe(new FakeDbException(message, sqlState)));

    [Theory]
    [InlineData("23503", "anything")] // PostgreSQL foreign_key_violation
    [InlineData(null, "FOREIGN KEY constraint failed")]
    [InlineData(null, "The INSERT statement conflicted with the CHECK CONSTRAINT 'CK_Price'")]
    [InlineData(null, "Column 'Name' cannot be null")]
    [InlineData(null, "null value in column \"name\" violates not-null constraint")]
    public void GivenAnIntegrityViolation_WhenDescribed_ThenTheIntegrityMessageComesBack(string? sqlState,
        string message) =>
        Assert.Equal(RelationalErrors.IntegrityViolationMessage,
            RelationalErrors.Describe(new FakeDbException(message, sqlState)));

    [Fact]
    public void GivenATransientProviderFailure_WhenDescribed_ThenTheTransientMessageComesBack() =>
        Assert.Equal(RelationalErrors.TransientMessage,
            RelationalErrors.Describe(new FakeDbException("connection reset", transient: true)));

    [Fact]
    public void GivenATimeoutAnywhereInTheChain_WhenDescribed_ThenTheTransientMessageComesBack() =>
        Assert.Equal(RelationalErrors.TransientMessage,
            RelationalErrors.Describe(new InvalidOperationException("outer", new TimeoutException("slow"))));

    [Fact]
    public void GivenAProviderFailureWrappedByEntityFramework_WhenDescribed_ThenTheInnerFailureIsClassified() =>
        Assert.Equal(RelationalErrors.UniqueViolationMessage,
            RelationalErrors.Describe(new DbUpdateException("save failed",
                new FakeDbException("UNIQUE constraint failed: Items.Email"))));

    [Fact]
    public void GivenAUniqueViolationThatIsAlsoTransient_WhenDescribed_ThenTheUniqueClassificationWins() =>
        Assert.Equal(RelationalErrors.UniqueViolationMessage,
            RelationalErrors.Describe(new FakeDbException("duplicate key", "23505", transient: true)));

    [Theory]
    [InlineData("42P01", "relation \"items\" does not exist")]
    [InlineData(null, "syntax error near SELECT")]
    public void GivenAnUnclassifiedProviderFailure_WhenDescribed_ThenTheGenericMessageComesBack(string? sqlState,
        string message) =>
        Assert.Equal(RelationalErrors.GenericMessage,
            RelationalErrors.Describe(new FakeDbException(message, sqlState)));

    [Fact]
    public void GivenAFailureThatIsNotADatabaseFailure_WhenDescribed_ThenTheGenericMessageComesBack() =>
        Assert.Equal(RelationalErrors.GenericMessage,
            RelationalErrors.Describe(new InvalidOperationException("UNIQUE constraint failed")));

    [Fact]
    public void GivenAUniqueViolationNestedInTheChain_WhenCheckingForOne_ThenItIsFound() =>
        Assert.True(RelationalErrors.IsUniqueViolation(
            new InvalidOperationException("outer", new DbUpdateException("save", new FakeDbException("x", "23505")))));

    [Theory]
    [InlineData("23503", "FOREIGN KEY constraint failed")]
    [InlineData(null, "syntax error")]
    public void GivenAFailureThatIsNotAUniqueViolation_WhenCheckingForOne_ThenItIsNotFound(string? sqlState,
        string message) =>
        Assert.False(RelationalErrors.IsUniqueViolation(new FakeDbException(message, sqlState)));

    [Fact]
    public void GivenAFailureWithNoDatabaseFailureInItsChain_WhenCheckingForAUniqueViolation_ThenItIsNotFound() =>
        Assert.False(RelationalErrors.IsUniqueViolation(new InvalidOperationException("duplicate key")));

    private sealed class FakeDbException(string message, string? sqlState = null, bool transient = false)
        : DbException(message)
    {
        public override string? SqlState => sqlState;

        public override bool IsTransient => transient;
    }
}
