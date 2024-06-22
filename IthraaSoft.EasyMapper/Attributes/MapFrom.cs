using System;
using System.Collections.Generic;
using System.Text;

namespace IthraaSoft.EasyMapper.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class MapFromAttribute(params Type[] sourceTypes) : Attribute
{
    public Type[] SourceTypes { get; } = sourceTypes;
}
