// BuilderPattern.cs
//
// Padrao Builder (Head First / GoF) em C#, modelado no builder do NXOpen:
//
//   var b = CreateMillingOperationBuilder(...);   // builder com defaults
//   b.Feeds.SpindleRpm = 6000;                     // configura, inclusive sub-builders
//   b.CutParameters.PartStock = 0.5;
//   MillingOperation op = b.Commit();              // valida e produz o objeto (imutavel)
//   b.Destroy();                                   // builder descartado
//
// Tres pecas:
//   1) Produtos imutaveis: Tool, MillingOperation (+ Feeds, CutParameters, NonCutting)
//   2) Builders: ToolBuilder, MillingOperationBuilder com sub-builders
//   3) Director: OperationTemplates — receitas prontas (rough, wall finish),
//      o equivalente dos FBM_*_ROUGH / *_FINISH do PATHNC
//
// Console App .NET Framework: cole este arquivo e chame BuilderDemo.Main().

using System;
using System.Collections.Generic;
using System.Text;

namespace BuilderDemoNamespace
{
    // ======================================================================
    //  1) PRODUTOS — imutaveis. So o builder cria; depois ninguem altera.
    // ======================================================================

    public enum ToolType { EndMill, BallNose, BullNose, Drill, Tap, CenterDrill }

    public sealed class Tool
    {
        public string Name { get; private set; }
        public ToolType Type { get; private set; }
        public double Diameter { get; private set; }
        public double CornerRadius { get; private set; }
        public int Flutes { get; private set; }
        public double FluteLength { get; private set; }
        public int Number { get; private set; }

        internal Tool(string name, ToolType type, double diameter, double cornerRadius,
                      int flutes, double fluteLength, int number)
        {
            Name = name; Type = type; Diameter = diameter; CornerRadius = cornerRadius;
            Flutes = flutes; FluteLength = fluteLength; Number = number;
        }

        public override string ToString()
        {
            return string.Format("T{0} {1} ({2} Ø{3:F1}{4}, {5} flutes)",
                Number, Name, Type, Diameter,
                CornerRadius > 0 ? " R" + CornerRadius.ToString("F1") : "", Flutes);
        }
    }

    public sealed class Feeds
    {
        public double SpindleRpm { get; private set; }
        public double FeedCut { get; private set; }        // mm/min
        public double FeedEngage { get; private set; }
        public double FeedRetract { get; private set; }
        internal Feeds(double rpm, double cut, double engage, double retract)
        { SpindleRpm = rpm; FeedCut = cut; FeedEngage = engage; FeedRetract = retract; }
    }

    public enum CutPattern { FollowPart, FollowPeriphery, ZigZag, Profile, Trochoidal }

    public sealed class CutParameters
    {
        public CutPattern Pattern { get; private set; }
        public double StepoverPercent { get; private set; }
        public double DepthPerCut { get; private set; }
        public double PartStock { get; private set; }
        public double FloorStock { get; private set; }
        public bool ClimbMilling { get; private set; }
        internal CutParameters(CutPattern p, double stepover, double depth, double stock, double floor, bool climb)
        { Pattern = p; StepoverPercent = stepover; DepthPerCut = depth; PartStock = stock; FloorStock = floor; ClimbMilling = climb; }
    }

    public enum EngageType { Plunge, Ramp, Helical, Arc }

    public sealed class NonCutting
    {
        public EngageType Engage { get; private set; }
        public double RampAngle { get; private set; }
        public double ClearanceHeight { get; private set; }
        public double SafeDistance { get; private set; }
        internal NonCutting(EngageType e, double ramp, double clearance, double safe)
        { Engage = e; RampAngle = ramp; ClearanceHeight = clearance; SafeDistance = safe; }
    }

    public enum OperationType { CavityMill, AdaptiveMill, ZLevelProfile, FloorWall, PlanarMill }

    public sealed class MillingOperation
    {
        public string Name { get; private set; }
        public OperationType Type { get; private set; }
        public Tool Tool { get; private set; }
        public string Method { get; private set; }
        public Feeds Feeds { get; private set; }
        public CutParameters Cut { get; private set; }
        public NonCutting NonCutting { get; private set; }

        internal MillingOperation(string name, OperationType type, Tool tool, string method,
                                  Feeds feeds, CutParameters cut, NonCutting ncm)
        { Name = name; Type = type; Tool = tool; Method = method; Feeds = feeds; Cut = cut; NonCutting = ncm; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0} [{1}] method={2}\n", Name, Type, Method);
            sb.AppendFormat("  tool     : {0}\n", Tool);
            sb.AppendFormat("  feeds    : S{0:F0} F{1:F0} (engage {2:F0}, retract {3:F0})\n",
                Feeds.SpindleRpm, Feeds.FeedCut, Feeds.FeedEngage, Feeds.FeedRetract);
            sb.AppendFormat("  cut      : {0}, stepover {1:F0}%, ap {2:F2}, stock {3:F2}/{4:F2}, {5}\n",
                Cut.Pattern, Cut.StepoverPercent, Cut.DepthPerCut, Cut.PartStock, Cut.FloorStock,
                Cut.ClimbMilling ? "climb" : "conventional");
            sb.AppendFormat("  non-cut  : {0}{1}, clearance {2:F0}, safe {3:F1}",
                NonCutting.Engage, NonCutting.Engage == EngageType.Ramp || NonCutting.Engage == EngageType.Helical
                    ? " " + NonCutting.RampAngle.ToString("F1") + "°" : "",
                NonCutting.ClearanceHeight, NonCutting.SafeDistance);
            return sb.ToString();
        }
    }

    // ======================================================================
    //  2) BUILDERS — mutaveis, com defaults, validam no Commit
    // ======================================================================

    public sealed class BuilderException : Exception
    {
        public BuilderException(string msg) : base(msg) { }
    }

    // Base com o ciclo de vida do NX: Commit / Destroy, e protecao contra
    // uso depois de destruido (o NX lanca NXException nesse caso).
    public abstract class BuilderBase<T>
    {
        private bool _destroyed;

        protected void EnsureAlive()
        {
            if (_destroyed) throw new BuilderException(GetType().Name + " was destroyed.");
        }

        public T Commit()
        {
            EnsureAlive();
            List<string> errors = new List<string>();
            Validate(errors);
            if (errors.Count > 0)
                throw new BuilderException(GetType().Name + " cannot commit:\n  - " + string.Join("\n  - ", errors));
            return Build();
        }

        public void Destroy() { _destroyed = true; }

        protected abstract void Validate(List<string> errors);
        protected abstract T Build();
    }

    // ---------- ToolBuilder ----------
    public sealed class ToolBuilder : BuilderBase<Tool>
    {
        public string Name { get; set; }
        public ToolType Type { get; set; } = ToolType.EndMill;
        public double Diameter { get; set; }
        public double CornerRadius { get; set; }
        public int Flutes { get; set; } = 4;
        public double FluteLength { get; set; }
        public int Number { get; set; }

        protected override void Validate(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("tool name is required");
            if (Diameter <= 0) errors.Add("diameter must be > 0");
            if (CornerRadius < 0 || CornerRadius > Diameter / 2) errors.Add("corner radius must be between 0 and D/2");
            if (Type == ToolType.BallNose && Math.Abs(CornerRadius - Diameter / 2) > 1e-6)
                errors.Add("ball nose corner radius must be D/2");
            if (Flutes < 1) errors.Add("flutes must be >= 1");
            if (Number <= 0) errors.Add("tool number must be > 0");
        }

        protected override Tool Build()
        {
            double fl = FluteLength > 0 ? FluteLength : Diameter * 2.5;    // default derivado
            return new Tool(Name, Type, Diameter, CornerRadius, Flutes, fl, Number);
        }
    }

    // ---------- sub-builders da operacao ----------
    public sealed class FeedsBuilder
    {
        public double SpindleRpm { get; set; }
        public double FeedCut { get; set; }
        public double FeedEngage { get; set; }      // 0 = derivado (60% do corte)
        public double FeedRetract { get; set; }     // 0 = rapido (usa 5000)

        internal void Validate(List<string> e)
        {
            if (SpindleRpm <= 0) e.Add("spindle RPM must be > 0");
            if (FeedCut <= 0) e.Add("cutting feed must be > 0");
        }
        internal Feeds Build()
        {
            return new Feeds(SpindleRpm, FeedCut,
                FeedEngage > 0 ? FeedEngage : FeedCut * 0.6,
                FeedRetract > 0 ? FeedRetract : 5000);
        }
    }

    public sealed class CutParametersBuilder
    {
        public CutPattern Pattern { get; set; } = CutPattern.FollowPart;
        public double StepoverPercent { get; set; } = 50;
        public double DepthPerCut { get; set; }
        public double PartStock { get; set; }
        public double FloorStock { get; set; } = -1;   // -1 = igual ao PartStock
        public bool ClimbMilling { get; set; } = true;

        internal void Validate(List<string> e, Tool tool)
        {
            if (StepoverPercent <= 0 || StepoverPercent > 100) e.Add("stepover must be in (0, 100]%");
            if (DepthPerCut <= 0) e.Add("depth per cut must be > 0");
            if (tool != null && DepthPerCut > tool.FluteLength) e.Add("depth per cut exceeds the tool flute length");
            if (PartStock < 0) e.Add("part stock must be >= 0");
        }
        internal CutParameters Build()
        {
            return new CutParameters(Pattern, StepoverPercent, DepthPerCut, PartStock,
                FloorStock >= 0 ? FloorStock : PartStock, ClimbMilling);
        }
    }

    public sealed class NonCuttingBuilder
    {
        public EngageType Engage { get; set; } = EngageType.Ramp;
        public double RampAngle { get; set; } = 3.0;
        public double ClearanceHeight { get; set; } = 50;
        public double SafeDistance { get; set; } = 3;

        internal void Validate(List<string> e, Tool tool)
        {
            if ((Engage == EngageType.Ramp || Engage == EngageType.Helical) && (RampAngle <= 0 || RampAngle > 45))
                e.Add("ramp angle must be in (0, 45]°");
            if (Engage == EngageType.Plunge && tool != null && tool.Type == ToolType.BullNose)
                e.Add("bull nose cutters should not plunge — use ramp or helical");
            if (SafeDistance < 0) e.Add("safe distance must be >= 0");
        }
        internal NonCutting Build() { return new NonCutting(Engage, RampAngle, ClearanceHeight, SafeDistance); }
    }

    // ---------- MillingOperationBuilder ----------
    public sealed class MillingOperationBuilder : BuilderBase<MillingOperation>
    {
        public string Name { get; set; }
        public OperationType Type { get; set; }
        public Tool Tool { get; set; }
        public string Method { get; set; } = "METHOD";

        // Sub-builders expostos como propriedades, igual ao NX
        // (builder.CutParameters.PartStock.Value = ...)
        public FeedsBuilder Feeds { get; private set; } = new FeedsBuilder();
        public CutParametersBuilder CutParameters { get; private set; } = new CutParametersBuilder();
        public NonCuttingBuilder NonCutting { get; private set; } = new NonCuttingBuilder();

        internal MillingOperationBuilder(OperationType type) { Type = type; }

        protected override void Validate(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(Name)) errors.Add("operation name is required");
            if (Tool == null) errors.Add("tool is required");
            Feeds.Validate(errors);
            CutParameters.Validate(errors, Tool);
            NonCutting.Validate(errors, Tool);

            // validacoes CRUZADAS: e para isso que o Commit existe
            if (Tool != null && Tool.Type == ToolType.Drill && Type != OperationType.PlanarMill)
                errors.Add("a drill cannot be used in a milling operation");
            if (Type == OperationType.ZLevelProfile && CutParameters.Pattern != CutPattern.Profile)
                errors.Add("Z-level profile requires the Profile cut pattern");
        }

        protected override MillingOperation Build()
        {
            return new MillingOperation(Name, Type, Tool, Method, Feeds.Build(), CutParameters.Build(), NonCutting.Build());
        }
    }

    // A "collection" que cria builders — o papel de
    // workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(...)
    public sealed class CamSetup
    {
        private readonly List<Tool> _tools = new List<Tool>();
        private readonly List<MillingOperation> _operations = new List<MillingOperation>();

        public IReadOnlyList<Tool> Tools { get { return _tools; } }
        public IReadOnlyList<MillingOperation> Operations { get { return _operations; } }

        public ToolBuilder CreateToolBuilder()
        {
            return new ToolBuilder { Number = _tools.Count + 1 };     // default: proximo numero
        }

        public MillingOperationBuilder CreateMillingOperationBuilder(OperationType type)
        {
            return new MillingOperationBuilder(type);
        }

        public Tool AddTool(ToolBuilder b) { Tool t = b.Commit(); b.Destroy(); _tools.Add(t); return t; }
        public MillingOperation AddOperation(MillingOperationBuilder b) { MillingOperation op = b.Commit(); b.Destroy(); _operations.Add(op); return op; }
    }

    // ======================================================================
    //  3) DIRECTOR — receitas prontas. E o que os FBM_*_ROUGH / *_FINISH
    //     do PATHNC fazem: um metodo que configura um builder de um jeito
    //     conhecido. O builder sabe COMO construir; o director sabe O QUE.
    // ======================================================================

    public static class OperationTemplates
    {
        // Desbaste padrao de ferramentaria: follow part, 50%, rampa 3°, sobremetal 0,5
        public static MillingOperationBuilder Roughing(CamSetup setup, string name, Tool tool, double depthPerCut)
        {
            MillingOperationBuilder b = setup.CreateMillingOperationBuilder(OperationType.CavityMill);
            b.Name = name; b.Tool = tool; b.Method = "MILL_ROUGH";
            b.CutParameters.Pattern = CutPattern.FollowPart;
            b.CutParameters.StepoverPercent = 50;
            b.CutParameters.DepthPerCut = depthPerCut;
            b.CutParameters.PartStock = 0.5;
            b.NonCutting.Engage = EngageType.Ramp;
            b.NonCutting.RampAngle = 3;
            b.Feeds.SpindleRpm = RpmFor(tool, 180);          // Vc 180 m/min
            b.Feeds.FeedCut = FeedFor(tool, b.Feeds.SpindleRpm, 0.08);
            return b;
        }

        // Acabamento de parede: Z-level profile, sem sobremetal, engate em arco
        public static MillingOperationBuilder WallFinish(CamSetup setup, string name, Tool tool, double depthPerCut)
        {
            MillingOperationBuilder b = setup.CreateMillingOperationBuilder(OperationType.ZLevelProfile);
            b.Name = name; b.Tool = tool; b.Method = "MILL_FINISH";
            b.CutParameters.Pattern = CutPattern.Profile;
            b.CutParameters.DepthPerCut = depthPerCut;
            b.CutParameters.PartStock = 0;
            b.NonCutting.Engage = EngageType.Arc;
            b.Feeds.SpindleRpm = RpmFor(tool, 220);
            b.Feeds.FeedCut = FeedFor(tool, b.Feeds.SpindleRpm, 0.05);
            return b;
        }

        private static double RpmFor(Tool t, double vcMetersPerMin)
        {
            return Math.Round(vcMetersPerMin * 1000 / (Math.PI * t.Diameter) / 100) * 100;
        }
        private static double FeedFor(Tool t, double rpm, double fzMm)
        {
            return Math.Round(rpm * t.Flutes * fzMm / 10) * 10;
        }
    }

    // ======================================================================
    public static class BuilderDemo
    {
        public static void Main()
        {
            CamSetup setup = new CamSetup();

            // --- ferramentas: builder configurado passo a passo ---
            ToolBuilder tb = setup.CreateToolBuilder();
            tb.Name = "ENDMILL_D16"; tb.Diameter = 16; tb.Flutes = 4; tb.FluteLength = 32;
            Tool d16 = setup.AddTool(tb);

            tb = setup.CreateToolBuilder();
            tb.Name = "BULL_D12_R1"; tb.Type = ToolType.BullNose; tb.Diameter = 12; tb.CornerRadius = 1; tb.Flutes = 4;
            Tool d12 = setup.AddTool(tb);

            // --- operacao pelo director (template) e depois ajustada ---
            MillingOperationBuilder rough = OperationTemplates.Roughing(setup, "CAVITY_MILL_D16", d16, 1.0);
            rough.CutParameters.StepoverPercent = 40;                    // ajuste fino sobre o template
            Console.WriteLine(setup.AddOperation(rough));

            MillingOperationBuilder finish = OperationTemplates.WallFinish(setup, "ZLEVEL_D12", d12, 0.3);
            Console.WriteLine(setup.AddOperation(finish));

            // --- Commit que FALHA: validacao cruzada ---
            MillingOperationBuilder bad = setup.CreateMillingOperationBuilder(OperationType.CavityMill);
            bad.Name = "BAD_OP"; bad.Tool = d12;
            bad.CutParameters.DepthPerCut = 40;          // > flute length
            bad.NonCutting.Engage = EngageType.Plunge;   // bull nose nao mergulha
            try { bad.Commit(); }
            catch (BuilderException ex) { Console.WriteLine("Rejected as expected:\n" + ex.Message + "\n"); }
            bad.Destroy();

            // --- usar depois de Destroy: erro, como no NX ---
            try { bad.Name = "X"; bad.Commit(); }
            catch (BuilderException ex) { Console.WriteLine("Rejected as expected: " + ex.Message + "\n"); }

            // --- produto e imutavel: nao ha setter publico ---
            // d16.Diameter = 20;   // nao compila

            Console.WriteLine("Setup has {0} tools and {1} operations.", setup.Tools.Count, setup.Operations.Count);
        }
    }
}
