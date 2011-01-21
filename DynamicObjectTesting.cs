using System;
using System.Linq;
using System.Linq.Expressions;
using System.Dynamic;
using System.Reflection;
using System.Collections.Generic;

public class Program {
	public static void Main(string[] args) {
		Console.WriteLine("Via Inheritance: ");
		dynamic first = new DynamicViaInheritance();
		Console.WriteLine("first.Foo = {0}", first.Foo);
		first.Foo = 5;
		first.Bar();
		first.Bar(5);
		first.Bar("hi", 123, "there");

		Console.WriteLine("\n\nVia Interface: ");
		dynamic second = new DynamicViaInterface();
		Console.WriteLine("second.Foo = {0}", second.Foo);
		//second.Foo = 5;
		//second.Bar();
		//second.Bar(5);
		//second.Bar("hi", 123, "there");
	}
}

public class DynamicViaInheritance : DynamicObject {

	public override bool TryGetMember(GetMemberBinder binder, out object result) {
		Console.WriteLine("TryGetMember({0})", binder.Name);
		result = 5;
		return true;
	}

	public override bool TrySetMember(SetMemberBinder binder, object value) {
		Console.WriteLine("TrySetMember({0}, {1})", binder.Name, value);
		return true;
	}

	public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
		Console.WriteLine("TryInvokeMember({0}, {1})", binder.Name, string.Join(", ", new List<object>(args).Select(a => a.ToString()).ToArray()));
		result = null;
		return true;
	}
}

public class DynamicObjectProxy : DynamicObject, IDynamicMetaObjectProvider {

	public object Target { get; set; }

	public Type TargetType { get { return Target.GetType(); } }

	public MethodInfo TargetMethod(string name) {
		return TargetType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
	}

	public DynamicObjectProxy(object target) {
		Target = target;
	}

	public override bool TryGetMember(GetMemberBinder binder, out object result) {
		Console.WriteLine("Proxy.TryGetMember({0})", binder.Name);
		var method = TargetMethod("TryGetMember");
		if (method == null) {
			result = null;
			return false;
		} else {
			var args     = new object[] { binder, null };
			var response = method.Invoke(Target, args);
			result = args[1];
			return (bool) response;
		}
	}

	public override bool TrySetMember(SetMemberBinder binder, object value) {
		Console.WriteLine("FIXME TrySetMember({0}, {1})", binder.Name, value);
		return true;
	}

	public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
		Console.WriteLine("FIXME TryInvokeMember({0}, {1})", binder.Name, string.Join(", ", new List<object>(args).Select(a => a.ToString()).ToArray()));
		result = null;
		return true;
	}
}

public class DynamicViaInterface : IDynamicMetaObjectProvider {

	public DynamicObjectProxy DynamicObjectProxy { get { return new DynamicObjectProxy(this); } }
	public DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression parameter) {
		return new ForwardingMetaObject(parameter, BindingRestrictions.Empty, this, new DynamicObjectProxy(this), expr => Expression.Property(expr, "DynamicObjectProxy") );
	}

	public bool TryGetMember(GetMemberBinder binder, out object result) {
		Console.WriteLine("TryGetMember({0})", binder.Name);
		result = 5;
		return true;
	}

	public bool TrySetMember(SetMemberBinder binder, object value) {
		Console.WriteLine("TrySetMember({0}, {1})", binder.Name, value);
		return true;
	}

	public bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
		Console.WriteLine("TryInvokeMember({0}, {1})", binder.Name, string.Join(", ", new List<object>(args).Select(a => a.ToString()).ToArray()));
		result = null;
		return true;
	}
}

// DynamicMetaObject orwarding class based on: http://matousek.wordpress.com/2009/11/07/forwarding-meta-object/
public class ForwardingMetaObject : DynamicMetaObject {
    private readonly DynamicMetaObject _metaForwardee;

    public ForwardingMetaObject(Expression expression, BindingRestrictions restrictions, object forwarder,
        IDynamicMetaObjectProvider forwardee, Func<Expression, Expression> forwardeeGetter)
        : base(expression, restrictions, forwarder) { 

        // We'll use forwardee's meta-object to bind dynamic operations.
        _metaForwardee = forwardee.GetMetaObject(
            forwardeeGetter(
                Expression.Convert(expression, forwarder.GetType())   // [1]
            )
        );
    }

    // Restricts the target object's type to TForwarder. 
    // The meta-object we are forwarding to assumes that it gets an instance of TForwarder (see [1]).
    // We need to ensure that the assumption holds.
    private DynamicMetaObject AddRestrictions(DynamicMetaObject result) {
        return new DynamicMetaObject(
           result.Expression,
           BindingRestrictions.GetTypeRestriction(Expression, Value.GetType()).Merge(result.Restrictions),
           _metaForwardee.Value
       );
    }

	public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg) {
		return AddRestrictions(_metaForwardee.BindBinaryOperation(binder, arg));
	}
	public override DynamicMetaObject BindConvert(ConvertBinder binder) {
		return AddRestrictions(_metaForwardee.BindConvert(binder));
	}
	public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args) {
		return AddRestrictions(_metaForwardee.BindCreateInstance(binder, args));
	}
	public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes) {
		return AddRestrictions(_metaForwardee.BindDeleteIndex(binder, indexes));
	}
	public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) {
		return AddRestrictions(_metaForwardee.BindDeleteMember(binder));
	}
	public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes) {
		return AddRestrictions(_metaForwardee.BindGetIndex(binder, indexes));
	}
	public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
		return AddRestrictions(_metaForwardee.BindGetMember(binder));
	}
	public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args) {
		return AddRestrictions(_metaForwardee.BindInvoke(binder, args));
	}
	public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args) {
		return AddRestrictions(_metaForwardee.BindInvokeMember(binder, args));
	}
	public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value) {
		return AddRestrictions(_metaForwardee.BindSetIndex(binder, indexes, value));
	}
	public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
		return AddRestrictions(_metaForwardee.BindSetMember(binder, value));
	}
	public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder) {
		return AddRestrictions(_metaForwardee.BindUnaryOperation(binder));
	}
}