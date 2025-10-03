#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
using System.Reflection;

namespace Libplanet.Serialization;

public static class ModelScalarUtility
{
    private const string GetMethodName = "ToScalarValue";
    private const string SetMethodName = "FromScalarValue";
    private static readonly BindingFlags BindingFlags = BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly IServiceProvider _defaultServiceProvider = new ServiceProvider();

    public static ModelScalarKind GetScalarKind(Type type)
    {
        if (type.GetCustomAttribute<ModelScalarAttribute>() is not { } attribute)
        {
            throw new ArgumentException(
                $"Type '{type}' does not have the {nameof(ModelScalarAttribute)}", nameof(type));
        }

        return attribute.Kind;
    }

    public static object GetScalarValue(object obj)
    {
        var type = obj.GetType();
        if (type.GetCustomAttribute<ModelScalarAttribute>() is not { } attribute)
        {
            throw new ArgumentException($"Type '{type}' does not have the {nameof(ModelScalarAttribute)}", nameof(obj));
        }

        var kind = attribute.Kind;
        var method = GetToScalarValueMethod(type, kind);
        try
        {
            if (method.Invoke(obj, []) is not { } value)
            {
                throw new InvalidOperationException($"The '{GetMethodName}' method of '{type}' returned null.");
            }

            return value;
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException ?? e;
        }
    }

    public static object GetObjectFromScalarValue(Type type, object scalarValue)
        => GetObjectFromScalarValue(_defaultServiceProvider, type, scalarValue);

    public static object GetObjectFromScalarValue(IServiceProvider serviceProvider, Type type, object scalarValue)
    {
        if (type.GetCustomAttribute<ModelScalarAttribute>() is not { } attribute)
        {
            var message = $"Type '{type}' does not have the {nameof(ModelScalarAttribute)}";
            throw new ArgumentException(message, nameof(type));
        }

        var kind = attribute.Kind;
        var method = GetFromScalarValueMethod(type, kind);
        try
        {
            if (method.Invoke(null, [serviceProvider, scalarValue]) is not { } value)
            {
                throw new InvalidOperationException($"The '{SetMethodName}' method of '{type}' returned null.");
            }

            return value;
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException ?? e;
        }
    }

    private static MethodInfo GetToScalarValueMethod(Type type, ModelScalarKind kind) => kind switch
    {
        ModelScalarKind.String => GetToScalarValueMethod(type, typeof(string)),
        ModelScalarKind.Boolean => GetToScalarValueMethod(type, typeof(bool)),
        ModelScalarKind.Int32 => GetToScalarValueMethod(type, typeof(int)),
        ModelScalarKind.Int64 => GetToScalarValueMethod(type, typeof(long)),
        ModelScalarKind.Hex => GetToScalarValueMethod(type, typeof(byte[])),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static MethodInfo GetToScalarValueMethod(Type type, Type returnType)
    {
        var method = type.GetMethod(GetMethodName, BindingFlags | BindingFlags.Instance)
            ?? throw new ArgumentException($"Type '{type}' does not have the '{GetMethodName}' method.", nameof(type));
        if (method.ReturnType != returnType)
        {
            var message = $"The return type of '{GetMethodName}' method of '{type}' must be {returnType}.";
            throw new ArgumentException(message, nameof(type));
        }

        if (method.GetParameters().Length != 0)
        {
            var message = $"The '{GetMethodName}' method of '{type}' must not have parameters.";
            throw new ArgumentException(message, nameof(type));
        }

        return method;
    }

    private static MethodInfo GetFromScalarValueMethod(Type type, ModelScalarKind kind) => kind switch
    {
        ModelScalarKind.String => GetFromScalarValueMethod(type, typeof(string)),
        ModelScalarKind.Boolean => GetFromScalarValueMethod(type, typeof(bool)),
        ModelScalarKind.Int32 => GetFromScalarValueMethod(type, typeof(int)),
        ModelScalarKind.Int64 => GetFromScalarValueMethod(type, typeof(long)),
        ModelScalarKind.Hex => GetFromScalarValueMethod(type, typeof(byte[])),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static MethodInfo GetFromScalarValueMethod(Type type, Type parameterType)
    {
        var method = type.GetMethod(SetMethodName, BindingFlags | BindingFlags.Static)
            ?? throw new ArgumentException($"Type '{type}' does not have the '{SetMethodName}' method.", nameof(type));
        if (method.ReturnType != type)
        {
            var message = $"The return type of '{SetMethodName}' method of '{type}' must be {type}.";
            throw new ArgumentException(message, nameof(type));
        }

        var parameters = method.GetParameters();
        if (parameters.Length != 2)
        {
            var message = $"The '{SetMethodName}' method of '{type}' must have two parameters.";
            throw new ArgumentException(message, nameof(type));
        }

        if (parameters[0].ParameterType != typeof(IServiceProvider))
        {
            var message = $"The type of the first parameter of '{SetMethodName}' method of '{type}' " +
                         $"must be {typeof(IServiceProvider)}.";
            throw new ArgumentException(message, nameof(type));
        }

        if (parameters[1].ParameterType != parameterType)
        {
            var message = $"The type of the second parameter of '{SetMethodName}' method of '{type}' " +
                         $"must be {parameterType}.";
            throw new ArgumentException(message, nameof(type));
        }

        return method;
    }

    private sealed class ServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
