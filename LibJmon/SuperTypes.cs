using OneOf;
using OneOf.Types;

namespace LibJmon.SuperTypes;

public interface IImplicitConversion<TSelf, TOther>
    where TSelf : IImplicitConversion<TSelf, TOther>
{
    static abstract implicit operator TOther(TSelf from);
    static abstract implicit operator TSelf(TOther to);
}

public interface IUnion<TBase, TDer0, TDer1>
    where TBase : IUnion<TBase, TDer0, TDer1>
    where TDer0 : TBase
    where TDer1 : TBase { }

public interface IUnion<TBase, TDer0, TDer1, TDer2>
    where TBase : IUnion<TBase, TDer0, TDer1, TDer2>
    where TDer0 : TBase
    where TDer1 : TBase
    where TDer2 : TBase { }

public interface IUnion<TBase, TDer0, TDer1, TDer2, TDer3>
    where TBase : IUnion<TBase, TDer0, TDer1, TDer2, TDer3>
    where TDer0 : TBase
    where TDer1 : TBase
    where TDer2 : TBase
    where TDer3 : TBase { }

public interface IUnion<TBase, TDer0, TDer1, TDer2, TDer3, TDer4>
    where TBase : IUnion<TBase, TDer0, TDer1, TDer2, TDer3, TDer4>
    where TDer0 : TBase
    where TDer1 : TBase
    where TDer2 : TBase
    where TDer3 : TBase
    where TDer4 : TBase { }

internal static class UnionUtil
{
    public static OneOf<TDer0, TDer1>
        AsOneOf<TObj, TDer0, TDer1>(this IUnion<TObj, TDer0, TDer1> obj)
        where TObj : IUnion<TObj, TDer0, TDer1> 
        where TDer0 : TObj
        where TDer1 : TObj =>
        obj switch
        {
            TDer0 t0 => t0,
            TDer1 t1 => t1,
            _ => throw new Exception(),
        };
    
    public static OneOf<TDer0, TDer1, TDer2>
        AsOneOf<TObj, TDer0, TDer1, TDer2>(this IUnion<TObj, TDer0, TDer1, TDer2> obj)
        where TObj : IUnion<TObj, TDer0, TDer1, TDer2>
        where TDer0 : TObj
        where TDer1 : TObj
        where TDer2 : TObj =>
        obj switch
        {
            TDer0 t0 => t0,
            TDer1 t1 => t1,
            TDer2 t2 => t2,
            _ => throw new Exception(),
        };
    
    public static OneOf<TDer0, TDer1, TDer2, TDer3>
        AsOneOf<TObj, TDer0, TDer1, TDer2, TDer3>(this IUnion<TObj, TDer0, TDer1, TDer2, TDer3> obj)
        where TObj : IUnion<TObj, TDer0, TDer1, TDer2, TDer3>
        where TDer0 : TObj
        where TDer1 : TObj
        where TDer2 : TObj
        where TDer3 : TObj =>
        obj switch
        {
            TDer0 t0 => t0,
            TDer1 t1 => t1,
            TDer2 t2 => t2,
            TDer3 t3 => t3,
            _ => throw new Exception(),
        };

    public static OneOf<TDer0, TDer1, TDer2, TDer3, TDer4>
        AsOneOf<TObj, TDer0, TDer1, TDer2, TDer3, TDer4>(this IUnion<TObj, TDer0, TDer1, TDer2, TDer3, TDer4> obj)
        where TObj : IUnion<TObj, TDer0, TDer1, TDer2, TDer3, TDer4>
        where TDer0 : TObj
        where TDer1 : TObj
        where TDer2 : TObj
        where TDer3 : TObj
        where TDer4 : TObj =>
        obj switch
        {
            TDer0 t0 => t0,
            TDer1 t1 => t1,
            TDer2 t2 => t2,
            TDer3 t3 => t3,
            TDer4 t4 => t4,
            _ => throw new Exception(),
        };
}