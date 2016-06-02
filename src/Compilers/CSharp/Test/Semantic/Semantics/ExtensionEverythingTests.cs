﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class ExtensionEverythingTests : CompilingTestBase
    {
        private static readonly CSharpParseOptions parseOptions = TestOptions.Regular.WithExtensionEverythingFeature();
        private static readonly MetadataReference[] additionalRefs = new[] { SystemCoreRef };

        // PROTOTYPE: Overloaded (non-)ambiguous methods, properties, etc.
        // PROTOTYPE: Expression lambdas
        // PROTOTYPE: Entry point extension method
        // PROTOTYPE: Generics

        [Fact]
        public void BasicFunctionality()
        {
            var text = @"
#define __DEMO__

class BaseClass
{
}

extension class ExtClass : BaseClass
{
    public void Ext()
    {
        System.Console.WriteLine(""Hello, world!"");
    }
}

class Program
{
    static void Main(string[] args)
    {
        new BaseClass().Ext();
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "Hello, world!",
                parseOptions: parseOptions);
        }

        [Fact]
        public void VariousMemberKinds()
        {
            var text = @"
#define __DEMO__

using System;

class BaseClass
{
}

extension class ExtClass : BaseClass
{
    public int ExtMethod()
    {
        return 2;
    }
    public int ExtProp
    {
        get { return 2; }
        set { Console.Write(value); }
    }
    public static int ExtStaticMethod()
    {
        return 2;
    }
    public static int ExtStaticProp
    {
        get { return 2; }
        set { Console.Write(value); }
    }
}

class Program
{
    static void Main(string[] args)
    {
        var obj = new BaseClass();
        Console.Write(obj.ExtMethod());
        Console.Write(obj.ExtProp);
        obj.ExtProp = 2;
        Console.Write(BaseClass.ExtStaticMethod());
        Console.Write(BaseClass.ExtStaticProp);
        BaseClass.ExtStaticProp = 2;
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "222222",
                parseOptions: parseOptions)
                .VerifyIL("Program.Main", @"{
  // Code size       60 (0x3c)
  .maxstack  2
  IL_0000:  newobj     ""BaseClass..ctor()""
  IL_0005:  dup
  IL_0006:  call       ""int ExtClass.ExtMethod(BaseClass)""
  IL_000b:  call       ""void System.Console.Write(int)""
  IL_0010:  dup
  IL_0011:  call       ""int ExtClass.get_ExtProp(BaseClass)""
  IL_0016:  call       ""void System.Console.Write(int)""
  IL_001b:  ldc.i4.2
  IL_001c:  call       ""void ExtClass.set_ExtProp(BaseClass, int)""
  IL_0021:  call       ""int ExtClass.ExtStaticMethod()""
  IL_0026:  call       ""void System.Console.Write(int)""
  IL_002b:  call       ""int ExtClass.ExtStaticProp.get""
  IL_0030:  call       ""void System.Console.Write(int)""
  IL_0035:  ldc.i4.2
  IL_0036:  call       ""void ExtClass.ExtStaticProp.set""
  IL_003b:  ret
}");
        }

        [Fact]
        public void VariousExtendedKinds()
        {
            var text = @"
//#define __DEMO__

using System;

class BaseClass
{
}

static class BaseStaticClass
{
}

class BaseStruct
{
}

class IBaseInterface
{
}

enum BaseEnum
{
}

extension class ExtClass : BaseClass
{
    public void Member() { Console.Write(1); }
    public static void StaticMember() { Console.Write(5); }
    public static void DirectCall() { Console.Write('a'); }
}

extension class ExtStaticClass : BaseStaticClass
{
    public static void StaticMember() { Console.Write(6); }
    public static void DirectCall() { Console.Write('b'); }
}

extension class ExtStruct : BaseStruct
{
    public void Member() { Console.Write(2); }
    public static void StaticMember() { Console.Write(7); }
    public static void DirectCall() { Console.Write('c'); }
}

extension class ExtInterface : IBaseInterface
{
    public void Member() { Console.Write(3); }
    public static void StaticMember() { Console.Write(8); }
    public static void DirectCall() { Console.Write('d'); }
}

extension class ExtEnum : BaseEnum
{
    public void Member() { Console.Write(4); }
    public static void StaticMember() { Console.Write(9); }
    public static void DirectCall() { Console.Write('e'); }
}

class Program
{
    static void Main(string[] args)
    {
        BaseClass obj1 = default(BaseClass);
        BaseStruct obj2 = default(BaseStruct);
        IBaseInterface obj3 = default(IBaseInterface);
        BaseEnum obj4 = default(BaseEnum);
        obj1.Member();
        obj2.Member();
        obj3.Member();
        obj4.Member();
        BaseClass.StaticMember();
        BaseStaticClass.StaticMember();
        BaseStruct.StaticMember();
        IBaseInterface.StaticMember();
        BaseEnum.StaticMember();
        ExtClass.DirectCall();
        ExtStaticClass.DirectCall();
        ExtStruct.DirectCall();
        ExtInterface.DirectCall();
        ExtEnum.DirectCall();
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "123456789abcde",
                parseOptions: parseOptions)
                .VerifyIL("Program.Main", @"{
}");
        }

        [Fact]
        public void DuckDiscovery()
        {
            var text = @"
//#define __DEMO__

using System;
using System.Threading.Tasks;

// Add() invocation requires that the class implements IEnumerable (even though it doesn't use it)
class BaseEnumerable : System.Collections.IEnumerable
{
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
}

class BaseClass
{
}

class BaseEnumerator
{
    public bool accessed;
}

class BaseAwaiter : System.Runtime.CompilerServices.INotifyCompletion
{
    void System.Runtime.CompilerServices.INotifyCompletion.OnCompleted(Action action) => action();
}

extension class ExtEnumerable : BaseEnumerable
{
    public void Add(int x) => Console.Write(x);
}

extension class ExtClass : BaseClass
{
    public void Add(int x) => Console.Write(x);
    public new string ToString() => ""3""; // need new to hide *ExtClass*'s ToString method. Probably wrong?
    public BaseEnumerator GetEnumerator() => new BaseEnumerator();
    public BaseAwaiter GetAwaiter() => new BaseAwaiter();
}

extension class ExtEnumerator : BaseEnumerator
{
    public int Current => 4;
    public bool MoveNext()
    {
        var temp = accessed;
        accessed = true;
        return !temp;
    }
    public void Dispose() { }
    public void Reset() { }
}

extension class ExtAwaiter : BaseAwaiter
{
    public bool IsCompleted => true;
    public int GetResult() => 5;
    public void OnCompleted(Action action) => action();
}

class Program
{
    static async Task<int> Async(BaseClass obj)
    {
        return await obj;
    }
    static void Main(string[] args)
    {
        new BaseEnumerable { 1, 2 };
        var obj = new BaseClass();
        Console.Write(obj.ToString());
        // PROTOTYPE: Decide if we want extension foreach. (Old extension methods do not work)
        //foreach (var item in obj)
        //{
        //    Console.Write(item);
        //}
        Console.Write(Async(obj).Result);
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs.Concat(new[] { MscorlibRef_v46 }),
                expectedOutput: "12345",
                parseOptions: parseOptions)
                .VerifyIL("Program.Main", @"{
}");
        }

        [Fact]
        public void Params()
        {
            var text = @"
using System;

class BaseClass
{
}

extension class ExtClass : BaseClass
{
    public void Method(params int[] arr) => Console.Write(string.Join("""", arr));
    public static void StaticMethod(params int[] arr) => Console.Write(string.Join("""", arr));
    public string this[params int[] arr] => string.Join("""", arr);
}

class Program
{
    static void Main(string[] args)
    {
        var obj = default(BaseClass);
        obj.Method(1, 2);
        BaseClass.StaticMethod(3, 4);
        Console.Write(obj[5, 6]);
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs.Concat(new[] { MscorlibRef_v46 }),
                expectedOutput: "12345",
                parseOptions: parseOptions)
                .VerifyIL("Program.Main", @"{
}");
        }

        [Fact]
        public void Delegate()
        {
            var text = @"
using System;

class BaseClass
{
}

extension class ExtClass : BaseClass
{
    public void Method(int x) => Console.Write(x);
    public static void StaticMethod(int x) => Console.Write(x);
}

class Program
{
    static void Main(string[] args)
    {
        BaseClass obj = new BaseClass();
        Action<int> method = obj.Method;
        method(1);
        Action<int> staticMethod = BaseClass.StaticMethod;
        staticMethod(2);
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "12",
                parseOptions: parseOptions)
                .VerifyIL("Program.Main", @"{
  // Code size       41 (0x29)
  .maxstack  2
  IL_0000:  newobj     ""BaseClass..ctor()""
  IL_0005:  ldftn      ""void ExtClass.Method(BaseClass, int)""
  IL_000b:  newobj     ""System.Action<int>..ctor(object, System.IntPtr)""
  IL_0010:  ldc.i4.1
  IL_0011:  callvirt   ""void System.Action<int>.Invoke(int)""
  IL_0016:  ldnull
  IL_0017:  ldftn      ""void ExtClass.StaticMethod(int)""
  IL_001d:  newobj     ""System.Action<int>..ctor(object, System.IntPtr)""
  IL_0022:  ldc.i4.2
  IL_0023:  callvirt   ""void System.Action<int>.Invoke(int)""
  IL_0028:  ret
}");
        }

        // PROTOTYPE: `using` all the things

        [Fact]
        public void Using()
        {
            var text = @"
using System;

namespace One
{
    class BaseClass
    {
    }
}

extension class ExtGlobal : One.BaseClass
{
    public int Global => 3;
}

namespace Two
{
    using One;

    extension class ExtClass : BaseClass
    {
        public void Method() => Console.Write(1);
    }
}

namespace Prog
{
    using Two;
    class Program
    {
        static void Main()
        {
            var thing = new One.BaseClass();
            thing.Method();
            Four.FourClass.Test();
            Console.Write(thing.Global + 1);
        }
    }
}
";
            var text2 = @"
using System;

namespace Three
{
    using One;

    extension class ExtClass : BaseClass
    {
        public void Method() => Console.Write(2);
    }
}

namespace Four
{
    using Three;
    public class FourClass
    {
        public static void Test()
        {
            var thing = new One.BaseClass();
            thing.Method();
            Console.Write(thing.Global);
        }
    }
}
";

            CompileAndVerify(
                sources: new[] { text, text2 },
                additionalRefs: additionalRefs,
                expectedOutput: "1234",
                parseOptions: parseOptions);
        }

        [Fact]
        public void AmbiguityPriority()
        {
            var text = @"
using System;

class BaseClass
{
    public int Property { get { return 1; } set { Console.Write(value); } }
    public static int StaticProperty { get { return 5; } set { Console.Write(value); } }
}

extension class ExtClass : BaseClass
{
    public int Property { get { return 2; } set { Console.Write(value + 1); } }
    public static int StaticProperty { get { return 6; } set { Console.Write(value + 1); } }
}

class Program
{
    static void Main()
    {
        BaseClass obj = new BaseClass();
        Console.Write(obj.Property);
        Console.Write(2); // PROTOTYPE: figure out syntax to get ExtClass.Property
        obj.Property = 3;
        Console.Write(4); // PROTOTYPE: figure out syntax to set ExtClass.Property
        Console.Write(BaseClass.StaticProperty);
        Console.Write(ExtClass.StaticProperty);
        BaseClass.StaticProperty = 7;
        ExtClass.StaticProperty = 7;
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "12345678",
                parseOptions: parseOptions);
        }

        // PROTOTYPE: Extension methods/indexers (static/instance) with argument order oddness and default params

        [Fact]
        public void ArgumentOrdering()
        {
            var text = @"
using System;
using System.Linq;
using static EffectLogger;

class EffectLogger
{
    public static T Log<T>(T value, string log)
    {
        Console.Write(log);
        return value;
    }
}

class BaseClass
{
}

extension class ExtClass : BaseClass
{
    public int this[int a, int b = 4, params int[] c] =>
        Log(a + b + c.Sum(), ""["" + a + b + string.Join("""", c) + ""]"");
    public int Func(int a, int b = 4, params int[] c) =>
        Log(a + b + c.Sum(), ""("" + a + b + string.Join("","", c) + "")"");
    public static int StaticFunc(int a, int b = 4, params int[] c) =>
        Log(a + b + c.Sum(), ""{"" + a + b + string.Join("","", c) + ""}"");
}

class Program
{
    static void Main()
    {
        Console.Write(Log(new BaseClass(), ""1"")[c: new[] { Log(1, ""2""), Log(2, ""3"") }, a: Log(3, ""4"")]);
        Console.Write(Log(new BaseClass(), ""1"").Func(c: new[] { Log(1, ""2""), Log(2, ""3"") }, a: Log(3, ""4"")));
        Console.Write(BaseClass.StaticFunc(c: new[] { Log(1, ""1""), Log(2, ""2"") }, a: Log(3, ""3"")));
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "1234[3412]1234(3412)1234{3412}",
                parseOptions: parseOptions);
        }

        [Fact]
        public void DllImportExtern()
        {
            var text = @"
using System;
using System.Runtime.InteropServices;

class BaseClass
{
}

extension class ExtClass : BaseClass
{
    [DllImport(""bogusAssembly"")]
    public static extern void ExternMethod();
}

class Program
{
    static void Main()
    {
        try
        {
            BaseClass.ExternMethod();
        }
        catch (System.DllNotFoundException)
        {
            Console.Write(1);
        }
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "1",
                parseOptions: parseOptions);
        }

        [Fact]
        public void COMClass()
        {
            var text = @"
using System;
using System.Runtime.InteropServices;

[ComImport, Guid(""bb29cd77-7fc8-42c3-94ed-be985450be09"")]
public class BaseClassCOM
{
}

extension class ExtClass : BaseClassCOM
{
    public void Member() { Console.Write(1); }
    public int Property => 2; // there was strange things with properties in particular
    public static void StaticMember() { Console.Write(3); }
}

class Program
{
    static void Main(string[] args)
    {
        BaseClassCOM obj = default(BaseClassCOM);
        obj.Member();
        Console.Write(obj.Property);
        BaseClassCOM.StaticMember();
    }
}
";

            CompileAndVerify(
                source: text,
                additionalRefs: additionalRefs,
                expectedOutput: "123",
                parseOptions: parseOptions)
                .VerifyIL("Program.Main", @"{
  // Code size       23 (0x17)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  dup
  IL_0002:  call       ""void ExtClass.Member(BaseClassCOM)""
  IL_0007:  call       ""int ExtClass.get_Property(BaseClassCOM)""
  IL_000c:  call       ""void System.Console.Write(int)""
  IL_0011:  call       ""void ExtClass.StaticMember()""
  IL_0016:  ret
}");
        }

        [Fact]
        public void ExtensionMethodInExtensionClass()
        {
            var text = @"
class Base
{
}

extension class Ext : Base {
    public static void ExtMethod(this Base param)
    {
    }
}
";

            CreateCompilationWithMscorlibAndSystemCore(text, parseOptions: parseOptions).VerifyDiagnostics(
                // (7,24): error CS8207: An extension method cannot be defined in an extension class.
                //     public static void ExtMethod(this Base param)
                Diagnostic(ErrorCode.ERR_ExtensionMethodInExtensionClass, "ExtMethod").WithLocation(7, 24)
            );
        }

        [Fact]
        public void InstanceInStaticExtension()
        {
            var text = @"
static class Base
{
}

extension class Ext : Base {
    public void ExtMethod() { }
}
";

            // PROTOTYPE: Wrong error, but there's no error message yet so this is just to keep the test failing
            CreateCompilationWithMscorlibAndSystemCore(text, parseOptions: parseOptions).VerifyDiagnostics(
                // (7,24): error CS8207: An extension method cannot be defined in an extension class.
                //     public static void ExtMethod(this Base param)
                Diagnostic(ErrorCode.ERR_ExtensionMethodInExtensionClass, "ExtMethod").WithLocation(7, 24)
            );
        }

        [Fact]
        public void IncorrectExtendedType()
        {
            var text = @"
struct Base { }

extension class ExtNothing { }

unsafe extension class ExtPointer : Base* { }

extension class ExtArray : Base[] { }

extension class ExtTypeParam<T> : T { }

extension class ExtDynamic : dynamic { }

extension class ExtBase : Base { } // this is valid

extension class ExtExt : ExtBase { }

delegate void Del();

extension class ExtDelegate : Del { }
";

            // PROTOTYPE: Fix these error messages. Also make ExtNothing and ExtExt and ExtDelegate produce errors (they are not in the below list)
            CreateCompilationWithMscorlibAndSystemCore(text, options: TestOptions.UnsafeReleaseDll, parseOptions: parseOptions).VerifyDiagnostics(
                // (6,37): error CS1521: Invalid base type
                // unsafe extension class ExtPointer : Base* { }
                Diagnostic(ErrorCode.ERR_BadBaseType, "Base*").WithLocation(6, 37),
                // (8,28): error CS1521: Invalid base type
                // extension class ExtArray : Base[] { }
                Diagnostic(ErrorCode.ERR_BadBaseType, "Base[]").WithLocation(8, 28),
                // (12,30): error CS1965: 'ExtDynamic': cannot derive from the dynamic type
                // extension class ExtDynamic : dynamic { }
                Diagnostic(ErrorCode.ERR_DeriveFromDynamic, "dynamic").WithArguments("ExtDynamic").WithLocation(12, 30),
                // (10,35): error CS0689: Cannot derive from 'T' because it is a type parameter
                // extension class ExtTypeParam<T> : T { }
                Diagnostic(ErrorCode.ERR_DerivingFromATyVar, "T").WithArguments("T").WithLocation(10, 35),
                // (8,28): error CS0527: Type 'Base[]' in interface list is not an interface
                // extension class ExtArray : Base[] { }
                Diagnostic(ErrorCode.ERR_NonInterfaceInInterfaceList, "Base[]").WithArguments("Base[]").WithLocation(8, 28),
                // (6,37): error CS0527: Type 'Base*' in interface list is not an interface
                // unsafe extension class ExtPointer : Base* { }
                Diagnostic(ErrorCode.ERR_NonInterfaceInInterfaceList, "Base*").WithArguments("Base*").WithLocation(6, 37)
            );
        }
    }
}
