﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenComponent<global::Test.MyComponent>(0);
            __builder.AddAttribute(1, "ParamBefore", "before");
            __builder.AddAttribute(2, "ParamAfter", "after");
            __builder.SetKey(
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                        someDate.Day

#line default
#line hidden
#nullable disable
            );
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
#nullable restore
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
       
    private DateTime someDate = DateTime.Now;

#line default
#line hidden
#nullable disable
    }
}
#pragma warning restore 1591
