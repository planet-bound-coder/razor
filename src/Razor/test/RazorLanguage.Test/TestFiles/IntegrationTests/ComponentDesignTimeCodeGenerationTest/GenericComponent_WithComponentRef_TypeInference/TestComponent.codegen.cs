// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 219
        private void __RazorDirectiveTokenHelpers__() {
        }
        #pragma warning restore 219
        #pragma warning disable 0414
        private static System.Object __o = null;
        #pragma warning restore 0414
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder)
        {
            __Blazor.Test.TestComponent.TypeInference.CreateMyComponent_0(builder, -1, -1, 
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                   3

#line default
#line hidden
#nullable disable
            , -1, (__value) => {
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                            _my = __value;

#line default
#line hidden
#nullable disable
            }
            );
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
__o = typeof(MyComponent<>);

#line default
#line hidden
#nullable disable
        }
        #pragma warning restore 1998
#nullable restore
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
       
    private MyComponent<int> _my;
    public void Foo() { System.GC.KeepAlive(_my); }

#line default
#line hidden
#nullable disable
    }
}
namespace __Blazor.Test.TestComponent
{
    #line hidden
    internal static class TypeInference
    {
        public static void CreateMyComponent_0<TItem>(global::Microsoft.AspNetCore.Components.RenderTree.RenderTreeBuilder builder, int seq, int __seq0, TItem __arg0, int __seq1, global::System.Action<global::Test.MyComponent<TItem>> __arg1)
        {
        builder.OpenComponent<global::Test.MyComponent<TItem>>(seq);
        builder.AddAttribute(__seq0, "Item", __arg0);
        builder.AddComponentReferenceCapture(__seq1, (__value) => { __arg1((global::Test.MyComponent<TItem>)__value); });
        builder.CloseComponent();
        }
    }
}
#pragma warning restore 1591
