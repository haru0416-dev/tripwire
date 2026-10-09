using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Get Component: the variable's component type, from one object, its children or its parents.</summary>
public class GetComponentTests
{
    static TriggerProgram Program(ActionCall call, params VariableDecl[] vars)
    {
        var p = new TriggerProgram();
        p.Variables.AddRange(vars);
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { call } });
        return p;
    }

    static ActionCall Get(string variable, ArgValue source, int where = 0) =>
        new ActionCall { ActionId = ActionCatalog.GetComponentId, Args = { ArgValue.Const(variable), source, ArgValue.Const(where) } };

    static VariableDecl Body() { var t = ParamType.Object("UnityEngine.Rigidbody"); t.IsComponent = true; return new VariableDecl { Name = "body", Type = t }; }

    [Fact]
    public void TheVariablesTypeIsTheComponent()
    {
        Assert.Contains("v_body = gameObject.GetComponent<UnityEngine.Rigidbody>();", Flat(Program(Get("body", ArgValue.SelfObject()), Body())));
        Assert.Contains("GetComponentInChildren<UnityEngine.Rigidbody>()", Flat(Program(Get("body", ArgValue.SelfObject(), 1), Body())));
        Assert.Contains("GetComponentInParent<UnityEngine.Rigidbody>()", Flat(Program(Get("body", ArgValue.SelfObject(), 2), Body())));
    }

    [Fact]
    public void AnObjectFromAVariableIsCheckedFirst()
    {
        var found = new VariableDecl { Name = "found", Type = ParamType.Object("UnityEngine.GameObject") };
        Assert.Contains("if (Utilities.IsValid(v_found)) v_body = v_found.GetComponent<UnityEngine.Rigidbody>();", Flat(Program(Get("body", ArgValue.Var("found")), Body(), found)));
    }

    [Fact]
    public void ANonComponentVariableIsRefused()
    {
        var g = CodeGenerator.Generate(Program(Get("go", ArgValue.SelfObject()), new VariableDecl { Name = "go", Type = ParamType.Object("UnityEngine.GameObject") }));
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 0);
    }
}
