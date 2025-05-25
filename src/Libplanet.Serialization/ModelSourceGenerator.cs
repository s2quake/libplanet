using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Libplanet.Serialization.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Libplanet.Serialization;

[Generator]
public partial class ModelSourceGenerator : Microsoft.CodeAnalysis.ISourceGenerator
{
    public const string ModelAttributeFullName = "Libplanet.Serialization.ModelAttribute";
    
    public void Execute(GeneratorExecutionContext context)
    {
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        
    }
}
