using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

/// <summary>
///     Minimal interface test double built on <see cref="DispatchProxy" />: each call is routed to a handler
///     that may answer it; unanswered calls return a completed task or the return type's default value.
/// </summary>
public class InterfaceStub : DispatchProxy
{
    private Func<MethodInfo, object?[]?, (bool Handled, object? Result)> _handler = (_, _) => (false, null);

    /// <summary>Creates a stub of <typeparamref name="T" /> whose calls go to <paramref name="handler" />.</summary>
    public static T For<T>(Func<MethodInfo, object?[]?, (bool Handled, object? Result)> handler) where T : class
    {
        var proxy = Create<T, InterfaceStub>();
        ((InterfaceStub)(object)proxy)._handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var (handled, result) = _handler(targetMethod!, args);
        if (handled)
        {
            return result;
        }

        var returnType = targetMethod!.ReturnType;

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
    }
}
