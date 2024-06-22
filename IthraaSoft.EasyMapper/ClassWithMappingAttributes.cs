using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace IthraaSoft.EasyMapper;

internal class ClassWithMappingAttributes(
    INamedTypeSymbol classSymbol, 
    List<AttributeData> mapFromAttributes, 
    List<AttributeData> mapToAttributes)
{
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;
    public List<AttributeData> MapFromAttributes { get; } = mapFromAttributes;
    public List<AttributeData> MapToAttributes { get; } = mapToAttributes;
}
