using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Libplanet.Serialization.Descriptors;
using Microsoft.CodeAnalysis;

namespace Libplanet.Serialization;

[Generator]
public class ModelSourceGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {

        
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        throw new NotImplementedException();
    }
}
