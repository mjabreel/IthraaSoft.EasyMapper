using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace IthraaSoft.EasyMapper;

internal class ExtensionGenerationContext
{
    public string Key { get; set; }
    public INamedTypeSymbol Source { get; set; }
    public INamedTypeSymbol Target { get; set; }
    public bool IsBidirectional { get; set; }
}
