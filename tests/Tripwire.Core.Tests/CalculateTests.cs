using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>Calculate: A op B into a variable, for numbers, positions, colors and text.</summary>
public class CalculateTests
{
    static ActionCall Calc(string variable, ArgValue a, ActionCatalog.CalcOp op, ArgValue b) =>
        new ActionCall { ActionId = ActionCatalog.CalculateId, Args = { ArgValue.Const(variable), a, ArgValue.Const((int)op), b } };

    static TriggerProgram Program(ActionCall call, params VariableDecl[] vars)
    {
        var p = new TriggerProgram();
        p.Variables.AddRange(vars);
        p.Events.Add(new EventBlock { EventId = "Interact", Actions = { call } });
        return p;
    }

    static VariableDecl Var(string name, ValueKind kind, object initial, bool synced = false) => new VariableDecl { Name = name, Kind = kind, Initial = initial, Synced = synced };

    [Fact]
    public void NumbersUseTheOperator()
    {
        var src = Flat(Program(Calc("score", ArgValue.Var("score"), ActionCatalog.CalcOp.Multiply, ArgValue.Const(2)), Var("score", ValueKind.Int, 0)));
        Assert.Contains("v_score = (v_score) * (2);", src);
    }

    [Fact]
    public void WholeNumberDivisionByZeroGivesZero()
    {
        var src = Flat(Program(Calc("n", ArgValue.Var("n"), ActionCatalog.CalcOp.Divide, ArgValue.Var("d")), Var("n", ValueKind.Int, 0), Var("d", ValueKind.Int, 0)));
        Assert.Contains("v_n = ((v_d) == 0 ? 0 : (v_n) / (v_d));", src);
        var constant = CodeGenerator.Generate(Program(Calc("n", ArgValue.Var("n"), ActionCatalog.CalcOp.Remainder, ArgValue.Const(0)), Var("n", ValueKind.Int, 0)));
        Assert.Contains(constant.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 3);
    }

    [Fact]
    public void APositionIsScaledByANumber()
    {
        Assert.Equal(ValueKind.Float, CodeGenerator.CalculateOperandB(ParamType.Of(ValueKind.Vector3), ActionCatalog.CalcOp.Multiply).Kind);
        Assert.Equal(ValueKind.Vector3, CodeGenerator.CalculateOperandB(ParamType.Of(ValueKind.Vector3), ActionCatalog.CalcOp.Add).Kind);
        var src = Flat(Program(Calc("pos", ArgValue.Var("pos"), ActionCatalog.CalcOp.Multiply, ArgValue.Const(2f)), Var("pos", ValueKind.Vector3, new float[3], synced: true)));
        Assert.Contains("Tw_Set_pos((v_pos) * (2f));", src);
    }

    [Fact]
    public void TextOnlyJoins()
    {
        var src = Flat(Program(Calc("s", ArgValue.Const("Score: "), ActionCatalog.CalcOp.Add, ArgValue.Var("s")), Var("s", ValueKind.String, "")));
        Assert.Contains("v_s = (\"Score: \") + (v_s);", src);
        var minus = CodeGenerator.Generate(Program(Calc("s", ArgValue.Var("s"), ActionCatalog.CalcOp.Subtract, ArgValue.Var("s")), Var("s", ValueKind.String, "")));
        Assert.Contains(minus.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 2);
    }

    [Fact]
    public void ABoolCantBeCalculated()
    {
        var g = CodeGenerator.Generate(Program(Calc("b", ArgValue.Const(true), ActionCatalog.CalcOp.Add, ArgValue.Const(true)), Var("b", ValueKind.Bool, false)));
        Assert.Contains(g.Diagnostics, d => d.Severity == Severity.Error && d.Arg == 0);
    }
}
