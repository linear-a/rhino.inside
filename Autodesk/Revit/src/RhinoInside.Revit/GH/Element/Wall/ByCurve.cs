using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Grasshopper.Kernel;

using Autodesk.Revit.DB;

namespace RhinoInside.Revit.GH.Components
{
  public class WallByCurve : GH_TransactionalComponentItem
  {
    public override Guid ComponentGuid => new Guid("37A8C46F-CB5B-49FD-A483-B03D1FE14A22");
    public override GH_Exposure Exposure => GH_Exposure.primary;

    public WallByCurve() : base
    (
      "AddWall.ByCurve", "ByCurve",
      "Given its Axis, it adds a Wall element to the active Revit document",
      "Revit", "Build"
    )
    { }

    protected override void RegisterInputParams(GH_InputParamManager manager)
    {
      manager.AddCurveParameter("Axis", "A", string.Empty, GH_ParamAccess.item);
      manager[manager.AddParameter(new Parameters.ElementType(), "Type", "T", string.Empty, GH_ParamAccess.item)].Optional = true;
      manager[manager.AddParameter(new Parameters.Element(), "Level", "L", string.Empty, GH_ParamAccess.item)].Optional = true;
      manager.AddBooleanParameter("Structural", "S", string.Empty, GH_ParamAccess.item, true);
      manager[manager.AddNumberParameter("Height", "H", string.Empty, GH_ParamAccess.item)].Optional = true;

      var location = manager[manager.AddIntegerParameter("LocationLine", "LL", string.Empty, GH_ParamAccess.item)] as Grasshopper.Kernel.Parameters.Param_Integer;
      location.Optional = true;

      foreach (var e in Enum.GetValues(typeof(WallLocationLine)))
        location.AddNamedValue(Enum.GetName(typeof(WallLocationLine), e), (int) (WallLocationLine) e);

    }

    protected override void RegisterOutputParams(GH_OutputParamManager manager)
    {
      manager.AddParameter(new Parameters.Element(), "Wall", "W", "New Wall", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      Rhino.Geometry.Curve axis = null;
      DA.GetData("Axis", ref axis);

      WallType wallType = null;
      if (!DA.GetData("Type", ref wallType) && Params.Input[1].Sources.Count == 0)
        wallType = Revit.ActiveDBDocument.GetElement(Revit.ActiveDBDocument.GetDefaultElementTypeId(ElementTypeGroup.WallType)) as WallType;

      Autodesk.Revit.DB.Level level = null;
      DA.GetData("Level", ref level);
      if (level == null && axis != null)
      {
        level = Revit.ActiveDBDocument.FindLevelByElevation(axis.PointAtStart.Z / Revit.ModelUnits);
      }

      bool structural = true;
      DA.GetData("Structural", ref structural);

      double height = 0.0;
      if (!DA.GetData("Height", ref height))
        height = LiteralLengthValue(3.0);

      var locationLine = WallLocationLine.WallCenterline;
      int locationLineValue = (int) locationLine;
      if (DA.GetData("LocationLine", ref locationLineValue))
      {
        if ((int) WallLocationLine.WallCenterline > locationLineValue || locationLineValue > (int) WallLocationLine.CoreInterior)
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, string.Format("Parameter '{0}' range is [0, 5].", Params.Input[5].Name));
          return;
        }

        locationLine = (WallLocationLine) locationLineValue;
      }

      DA.DisableGapLogic();
      int Iteration = DA.Iteration;
      Revit.EnqueueAction((doc) => CommitInstance(doc, DA, Iteration, axis, wallType, level, structural, height, locationLine));
    }

    void CommitInstance
    (
      Document doc, IGH_DataAccess DA, int Iteration,
      Rhino.Geometry.Curve curve,
      Autodesk.Revit.DB.WallType wallType,
      Autodesk.Revit.DB.Level level,
      bool structural,
      double height,
      WallLocationLine locationLine
    )
    {
      var element = PreviousElement(doc, Iteration);

      if (!element?.Pinned ?? false)
      {
        ReplaceElement(doc, DA, Iteration, element);
      }
      else try
      {
        var scaleFactor = 1.0 / Revit.ModelUnits;
        if (scaleFactor != 1.0)
        {
          height *= scaleFactor;
          curve?.Scale(scaleFactor);
        }

        if
        (
          curve == null ||
          curve.IsShort(Revit.ShortCurveTolerance) ||
          !(curve.IsArc(Revit.VertexTolerance) || curve.IsLinear(Revit.VertexTolerance)) ||
          !curve.TryGetPlane(out var axisPlane, Revit.VertexTolerance) ||
          axisPlane.ZAxis.IsParallelTo(Rhino.Geometry.Vector3d.ZAxis) == 0
        )
          throw new Exception(string.Format("Parameter '{0}' must be a horizontal line or arc curve.", Params.Input[0].Name));

        if (level == null)
          throw new Exception(string.Format("Parameter '{0}' no suitable level is been found.", Params.Input[2].Name));

        if (height < Revit.VertexTolerance)
          throw new Exception(string.Format("Parameter '{0}' is too small.", Params.Input[4].Name));

        var axisList = curve.ToHost().ToList();
        Debug.Assert(axisList.Count == 1);
        var axis = axisList[0];
        double offsetDist = wallType.GetCompoundStructure().GetOffsetForLocationLine(locationLine);

        if(offsetDist != 0.0)
          axis = axis.CreateOffset(offsetDist, XYZ.BasisZ);

        if (element != null && wallType.Id != element.GetTypeId())
        {
          var newElmentId = element.ChangeTypeId(wallType.Id);
          if (newElmentId != ElementId.InvalidElementId)
          {
            element = doc.GetElement(newElmentId);
            ReplaceElement(doc, DA, Iteration, element);
          }
        }

        if (element is Wall wall && element?.Location is LocationCurve locationCurve && axisList[0].IsSameKindAs(locationCurve.Curve))
        {
          locationCurve.Curve = axis;
          wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).Set(level.Id);
          wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).Set(0.0);
          wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).Set(height);
        }
        else
        {
          element = CopyParametersFrom(Wall.Create(doc, axis, wallType.Id, level.Id, height, axisPlane.Origin.Z - level.Elevation, false, structural), element);
        }

        element?.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM).Set((int) locationLine);

        ReplaceElement(doc, DA, Iteration, element);
      }
      catch (Exception e)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
        ReplaceElement(doc, DA, Iteration, null);
      }
    }
  }
}
